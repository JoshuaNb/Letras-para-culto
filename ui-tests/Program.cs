using System.Security.Cryptography;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WorshipStudio;
using WorshipStudio.Controls;
using WorshipStudio.Models;
using WorshipStudio.Services;
using WorshipStudio.Themes;

// Exercise the shipped App/MainWindow, bindings, routed input, windows and renderer surfaces.
// The headless backend replaces OS windows only. No user library, LAN service or updater request is opened.
var outputArg = Array.IndexOf(args, "--output");
var output = Path.GetFullPath(outputArg >= 0 && outputArg + 1 < args.Length
    ? args[outputArg + 1] : Path.Combine(AppContext.BaseDirectory, "evidence"));
Directory.CreateDirectory(output);
var isolatedData = Path.Combine(Path.GetTempPath(), "WorshipStudio-ui-tests-" + Guid.NewGuid().ToString("N"));
LibraryStore.DataDirectoryOverride = isolatedData;
new StudioPreferences { Theme = StudioTheme.Dark, AutoCheckUpdates = false }.Save(isolatedData);
var fixtureSong = new Song
{
    Title = "Adoração de teste", Artist = "Teste isolado",
    Lyrics = "[Abertura]\nPrimeira estrofe\nGraça e paz\n\n[Refrão]\nSegunda estrofe\nLuz no coração\n\n[Final]\nTerceira estrofe\nAmém"
};
var secondSong = new Song { Title = "Outro louvor", Artist = "Biblioteca isolada", Lyrics = "Outro texto\nSomente na prévia" };
var fixture = new StudioDocument
{
    Songs = [fixtureSong, secondSong], Setlist = [fixtureSong.Id, secondSong.Id],
    Style = new StageStyle
    {
        BackgroundMode = "Solid", BackgroundColor = "#102339", TextColor = "#FFF1B2",
        AccentColor = "#EF8DA1", Bold = false, FontSize = 66, Transition = "Cut", MotionSpeed = 0,
        LyricAnimation = "None", Dim = .15
    }
};
new LibraryStore().Save(fixture);
var results = new List<CheckResult>();
var imageHashes = new Dictionary<string, string>();
MainWindow? main = null;

try
{
    AppBuilder.Configure<App>().UseSkia().WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .SetupWithoutStarting();
    App.Preferences.AutoCheckUpdates = false;
    Check("App real e MainWindow carregam XAML sem acesso à biblioteca do operador", () =>
    {
        Require(Application.Current is App, "O teste precisa carregar App.axaml de produção.");
        Require(Application.Current!.ApplicationLifetime == null, "O teste não deve iniciar desktop lifetime nativo.");
        main = new MainWindow { Width = 1560, Height = 980 };
        main.Show(); Pump();
        Require(main.IsVisible, "A janela real não abriu no backend headless.");
        Require(C<TextBlock>("SelectedTitle").Text == fixtureSong.Title, "A biblioteca isolada não foi selecionada.");
        Require(C<CheckBox>("AutoUpdateCheck").IsChecked == false, "Consulta automática precisa ficar desligada no teste.");
    });
    if (main == null) throw new InvalidOperationException("MainWindow não foi construída; demais testes dependem dela.");

    var preview = (StageSurface)C<ContentControl>("PreviewHost").Content!;
    var live = (StageSurface)C<ContentControl>("LivePreviewHost").Content!;
    var originalStyle = preview.Renderer.Frame.Style with { };
    var initialText = preview.Renderer.Frame.Text;

    Check("Prévia e retorno usam duas superfícies reais e independentes", () =>
    {
        Require(!ReferenceEquals(preview, live) && !ReferenceEquals(preview.Renderer, live.Renderer), "Prévia e retorno compartilham o mesmo renderizador.");
        Require(initialText.Contains("Primeira estrofe"), "A prévia não recebeu a letra da biblioteca.");
        Require(live.Renderer.Frame.Blackout && live.Renderer.Frame.Text.Length == 0, "O retorno deve iniciar preto sem projeção.");
        Require(preview.Bounds.Width > 150 && live.Bounds.Width > 150, "Os dois monitores de operação não receberam espaço no layout.");
        Require(Math.Abs(preview.Bounds.Width / preview.Bounds.Height - 16d / 9) < .03, "A prévia precisa conservar 16:9.");
    });

    Check("Biblioteca, sequência e três abas são compostas na janela", () =>
    {
        Require(C<ListBox>("LibraryList").ItemCount == 2, "A biblioteca precisa listar os dois louvores isolados.");
        Require(C<WrapPanel>("SlidesPanel").Children.Count == 3, "A sequência de estrofes não foi criada.");
        var tabs = C<TabControl>("InspectorTabs");
        Require(tabs.Items.OfType<TabItem>().Select(t => t.Header?.ToString()).SequenceEqual(new[] { "Cena", "Conexões", "App" }), "Abas de inspeção ausentes.");
        Require(C<ComboBox>("ThemeCombo").ItemCount == 4, "O seletor precisa oferecer quatro temas.");
    });

    foreach (var choice in StudioThemes.Choices)
    {
        Check($"Tema {choice.Name} atualiza a interface, persiste e preserva a projeção", () =>
        {
            C<ComboBox>("ThemeCombo").SelectedItem = choice;
            Pump();
            Require(StudioThemes.Current == choice.Id && App.Preferences.Theme == choice.Id, "A seleção não atualizou o tema ativo.");
            Require(StudioPreferences.Load(isolatedData).Theme == choice.Id, "O tema não foi salvo para a próxima abertura.");
            Require(main.Background is ISolidColorBrush background && background.Color == Color.Parse(StudioThemes.Palette(choice.Id)["WsBackground"]), "A janela não atualizou o recurso dinâmico de fundo.");
            Require(preview.Renderer.Frame.Style == originalStyle && preview.Renderer.Frame.Text == initialText, "O tema da interface alterou a cena projetada.");
            Require(C<ColorField>("TextColorField").Value == originalStyle.TextColor, "O tema alterou a cor da letra.");
            Capture(main, "theme-" + choice.Id.ToString().ToLowerInvariant());
        });
    }
    Check("Os quatro temas geram imagens distintas da interface real", () =>
        Require(imageHashes.Count == 4 && imageHashes.Values.Distinct().Count() == 4, "Temas não resultaram em quatro capturas diferentes."));

    Check("Atalhos de espaço de trabalho abrem Cena e Conexões", () =>
    {
        Click(C<Button>("ConnectionsWorkspaceButton"));
        Require(C<TabControl>("InspectorTabs").SelectedIndex == 1, "Conexões não abriu pelo botão superior.");
        Require(C<Button>("RemoteButton").IsEffectivelyVisible && C<TextBox>("HolyricsUrl").IsEffectivelyVisible, "Controles de conexão não foram compostos.");
        Click(C<Button>("OperationWorkspaceButton"));
        Require(C<TabControl>("InspectorTabs").SelectedIndex == 0, "Operação não retornou à cena.");
    });

    Check("Conexões oferecem pareamento Android e download do APK sem iniciar servidor", () =>
    {
        C<TabControl>("InspectorTabs").SelectedIndex=1;Pump();
        Require(C<TextBlock>("PairingCodeLabel").Text=="— — — — — —","Um código de pareamento apareceu com servidor desligado.");
        Require(string.IsNullOrEmpty(C<TextBox>("RemoteHostAddress").Text),"O endereço deveria ficar vazio com controle desligado.");
        Require(C<Button>("DownloadApkButton").Content as string=="Baixar APK para Android…","Atalho para salvar o APK não foi composto.");
        C<TabControl>("InspectorTabs").SelectedIndex=0;Pump();
    });

    Check("Baixar letras abre a janela real do Holyrics e cancelar preserva a biblioteca", () =>
    {
        C<TabControl>("InspectorTabs").SelectedIndex=1;Pump();
        var open=C<Button>("HolyricsImportButton");open.BringIntoView();Pump();Click(open);
        var dialog=main.OwnedWindows.OfType<HolyricsImportWindow>().Single(w=>w.IsVisible);Pump();
        T H<T>(string name) where T:Control=>dialog.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
        Require(H<TextBox>("HolyricsDownloadUrl").Text==C<TextBox>("HolyricsUrl").Text,"O endereço do servidor não foi transferido à janela.");
        Require(H<Button>("HolyricsCatalogConnect").IsEnabled,"O catálogo não está pronto para consulta explícita.");
        Require(!H<Button>("HolyricsCatalogDownload").IsEnabled,"Download foi habilitado sem seleção.");
        Capture(dialog,"holyrics-download");
        Click(H<Button>("HolyricsCatalogCancel"));Pump();
        Require(!dialog.IsVisible&&open.IsEnabled,"Cancelar não reativou a operação.");
        Require(new LibraryStore().Load().Songs.Count==2,"Cancelar a importação alterou a biblioteca.");
        C<TabControl>("InspectorTabs").SelectedIndex=0;Pump();
    });

    Check("Preferências exibem versão e opção de consulta automática desativada", () =>
    {
        C<TabControl>("InspectorTabs").SelectedIndex = 2; Pump();
        Require(C<TextBlock>("VersionLabel").Text!.Contains(UpdateService.CurrentVersion.ToString()), "Versão do assembly ausente nas preferências.");
        Require(C<CheckBox>("AutoUpdateCheck").IsChecked == false && !StudioPreferences.Load(isolatedData).AutoCheckUpdates, "A preferência offline do teste não foi preservada.");
        Require(C<TextBlock>("UpdateDetails").Text == "Nenhuma consulta realizada nesta sessão.", "O teste disparou uma consulta de rede indevida.");
        C<TabControl>("InspectorTabs").SelectedIndex = 0; Pump();
    });

    Check("Busca real filtra a biblioteca e recupera a seleção", () =>
    {
        C<TextBox>("SearchBox").Text = "Outro louvor"; Pump();
        Require(C<ListBox>("LibraryList").ItemCount == 1 && C<TextBlock>("SelectedTitle").Text == secondSong.Title, "A busca não atualizou lista e prévia.");
        Require(preview.Renderer.Frame.Text.Contains("Outro texto"), "A prévia não acompanhou o resultado da busca.");
        C<TextBox>("SearchBox").Text = ""; Pump();
        C<ListBox>("LibraryList").SelectedItem = C<ListBox>("LibraryList").Items.OfType<Song>().Single(s => s.Id == fixtureSong.Id); Pump();
        Require(C<WrapPanel>("SlidesPanel").Children.Count == 3 && preview.Renderer.Frame.Text == initialText, "Limpar a busca não recuperou a biblioteca e suas estrofes.");
    });

    Check("Controle de busca automática pode ser desligado e persiste sem consultar a rede", () =>
    {
        C<TabControl>("InspectorTabs").SelectedIndex = 2; Pump();
        var automatic = C<CheckBox>("AutoUpdateCheck");
        Click(automatic);
        Require(automatic.IsChecked == true && StudioPreferences.Load(isolatedData).AutoCheckUpdates, "Ativar consulta automática não persistiu a escolha.");
        Click(automatic);
        Require(automatic.IsChecked == false && !StudioPreferences.Load(isolatedData).AutoCheckUpdates, "Desativar consulta automática não persistiu a escolha.");
        Require(C<TextBlock>("UpdateDetails").Text == "Nenhuma consulta realizada nesta sessão.", "A mudança de preferência disparou consulta indevida durante o teste.");
        C<TabControl>("InspectorTabs").SelectedIndex = 0; Pump();
    });

    ProjectionWindow? projection = null;
    Check("Projetar estrofe abre a janela de saída independente com a letra correta", () =>
    {
        Click(C<Button>("SendButton"));
        projection = FindProjection();
        Require(projection.IsVisible && !ReferenceEquals(projection, main), "A projeção não abriu como janela independente.");
        Require(!ReferenceEquals(projection.Surface, preview) && !ReferenceEquals(projection.Surface, live), "A saída reutilizou uma superfície do operador.");
        Require(projection.Surface.Renderer.Frame.Text == initialText && live.Renderer.Frame.Text == initialText, "Saída e retorno não receberam a letra enviada.");
        Require(!live.Renderer.Frame.Blackout && C<TextBlock>("LiveLabel").Text == "AO VIVO", "O retorno não saiu da tela preta.");
    });

    Check("Selecionar outra estrofe prepara a prévia sem alterar a saída", () =>
    {
        Click((Button)C<WrapPanel>("SlidesPanel").Children[1]);
        Require(preview.Renderer.Frame.Text.Contains("Segunda estrofe"), "O cartão não preparou a segunda estrofe.");
        Require(live.Renderer.Frame.Text == initialText && projection!.Surface.Renderer.Frame.Text == initialText, "Selecionar a prévia avançou a projeção indevidamente.");
    });

    Check("Próxima e anterior navegam na apresentação ao vivo", () =>
    {
        Click(C<Button>("NextButton"));
        Require(live.Renderer.Frame.Text.Contains("Segunda estrofe") && projection!.Surface.Renderer.Frame.Text == live.Renderer.Frame.Text, "Próxima não atualizou saída e retorno juntos.");
        Click(C<Button>("PrevButton"));
        Require(live.Renderer.Frame.Text == initialText, "Anterior não restaurou a estrofe inicial.");
    });

    Check("Tela preta atua apenas no retorno e na saída, conservando a prévia", () =>
    {
        Click(C<Button>("BlackButton"));
        Require(live.Renderer.Frame.Blackout && projection!.Surface.Renderer.Frame.Blackout, "Tela preta não cobriu a saída real e o retorno.");
        Require(!preview.Renderer.Frame.Blackout && preview.Renderer.Frame.Text.Contains("Segunda estrofe"), "Tela preta apagou a prévia do operador.");
        Require(C<TextBlock>("LiveLabel").Text == "TELA PRETA", "Indicador de saída não acompanhou o blackout.");
        Click(C<Button>("BlackButton"));
        Require(!live.Renderer.Frame.Blackout && !projection!.Surface.Renderer.Frame.Blackout, "A restauração da tela não recuperou a saída.");
    });

    Check("Ocultar letra mantém o fundo e restaura a apresentação", () =>
    {
        Click(C<Button>("HideButton"));
        Require(live.Renderer.Frame.HideLyrics && projection!.Surface.Renderer.Frame.HideLyrics && !live.Renderer.Frame.Blackout, "Ocultar letra foi confundido com tela preta.");
        Require(!preview.Renderer.Frame.HideLyrics, "Ocultar letra escondeu a prévia.");
        Click(C<Button>("HideButton"));
        Require(!live.Renderer.Frame.HideLyrics && !projection!.Surface.Renderer.Frame.HideLyrics, "A letra não reapareceu.");
    });

    // The injected transport serves inert bytes only. No network or operating-system installer is invoked.
    var installerBytes = Encoding.UTF8.GetBytes("INERT UI TEST FIXTURE - NEVER AN EXECUTABLE\n");
    var installerHash = Convert.ToHexString(SHA256.HashData(installerBytes)).ToLowerInvariant();
    const string updateTag = "v1.4.0";
    const string installerName = "WorshipStudio-Setup-1.4.0.exe";
    const string manifestName = "SHA256SUMS-v1.4.0.txt";
    var manifestBytes = Encoding.UTF8.GetBytes($"{installerHash}  {installerName}\n");
    var transport = new FixtureUpdateHandler(installerBytes, manifestBytes);
    using var updateClient = new HttpClient(transport);
    using var updateService = new UpdateService(updateClient, Path.Combine(isolatedData, "ui-update-downloads"));
    var releaseFixture = new UpdateRelease(new Version(1, 4, 0), updateTag, "Versão de teste 1.4.0", "Notas de teste sem rede.", null,
        new Uri($"{UpdateService.RepositoryUrl}/releases/tag/{updateTag}"),
        new Uri($"{UpdateService.RepositoryUrl}/releases/download/{updateTag}/{installerName}"), installerName,
        installerBytes.LongLength, installerHash,
        new Uri($"{UpdateService.RepositoryUrl}/releases/download/{updateTag}/{manifestName}"), manifestName,
        manifestBytes.LongLength, Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant());
    UpdateWindow? updateWindow = null;
    string? downloadedPath = null, downloadedHash = null;
    var installCallbacks = 0;
    Task<bool> DeclineInstallation(string path) { installCallbacks++; return Task.FromResult(false); }

    Check("Janela de atualização modeless mantém a operação e navegação habilitadas", () =>
    {
        updateWindow = new UpdateWindow(updateService, releaseFixture, DeclineInstallation);
        updateWindow.DownloadVerified += (path, hash) => { downloadedPath = path; downloadedHash = hash; };
        updateWindow.Show(main); Pump();
        Require(main.IsEnabled && main.IsEffectivelyEnabled && C<Button>("NextButton").IsEffectivelyEnabled, "A atualização bloqueou a janela de operação.");
        Require(updateWindow.Owner == main && updateWindow.IsVisible, "A janela de atualização não está associada ao operador.");
        Click(C<Button>("NextButton"));
        Require(live.Renderer.Frame.Text.Contains("Segunda estrofe"), "Não foi possível avançar a letra com a atualização aberta.");
        Require(transport.Requests == 0 && installCallbacks == 0, "Abrir a janela iniciou um download ou instalação sem ação do operador.");
    });

    Check("Botão de download usa serviço real, confere hash e oferece instalar depois", () =>
    {
        Click(FindButton(updateWindow!, "Baixar atualização"));
        WaitUntil(() => downloadedPath != null && updateWindow!.GetVisualDescendants().OfType<Button>().Any(b => b.Content as string == "Instalar e fechar o app"));
        Require(File.Exists(downloadedPath) && File.ReadAllBytes(downloadedPath!).SequenceEqual(installerBytes), "O download real não retornou os bytes conferidos.");
        Require(downloadedHash!.Equals(installerHash, StringComparison.OrdinalIgnoreCase), "O evento de download não informou o SHA-256 correto.");
        Require(transport.Requests == 2 && installCallbacks == 0, "Download consultou recursos inesperados ou iniciou instalação.");
        Require(FindButton(updateWindow!, "Instalar depois").IsEnabled && main.IsEffectivelyEnabled, "Download finalizado bloqueou a operação.");
        Click(FindButton(updateWindow!, "Instalar depois"));
        Require(!updateWindow!.IsVisible && main.IsVisible, "Adiar instalação não fechou apenas a janela de atualização.");
    });

    Check("Download conferido é reutilizado na sessão e instalação pode ser adiada", () =>
    {
        updateWindow = new UpdateWindow(updateService, releaseFixture, DeclineInstallation, downloadedPath, downloadedHash);
        updateWindow.Show(main); Pump();
        Click(FindButton(updateWindow, "Instalar e fechar o app"));
        WaitUntil(() => installCallbacks == 1);
        WaitUntil(() => FindButton(updateWindow, "Instalar e fechar o app").IsEnabled);
        Require(updateWindow.IsVisible && main.IsVisible && main.IsEffectivelyEnabled, "Recusar a instalação encerrou ou bloqueou a operação.");
        Require(transport.Requests == 2, "Reabrir download já conferido iniciou outro download.");
    });

    Check("Arquivo de atualização alterado após download é recusado antes da instalação", () =>
    {
        File.AppendAllText(downloadedPath!, "TAMPERED");
        Click(FindButton(updateWindow!, "Instalar e fechar o app"));
        WaitUntil(() => updateWindow!.GetVisualDescendants().OfType<Button>().Any(b => b.Content as string == "Tentar novamente"));
        Require(installCallbacks == 1 && transport.Requests == 2, "Arquivo alterado chegou ao callback de instalação ou gerou tráfego inesperado.");
        Require(updateWindow!.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("arquivo foi alterado", StringComparison.OrdinalIgnoreCase) == true), "A falha de integridade não foi explicada ao operador.");
        updateWindow!.Close(); Pump();
    });

    Check("Galeria real abre pelo espaço Animações e cancelar preserva a cena", () =>
    {
        var before = preview.Renderer.Frame.Style with { };
        Click(C<Button>("AnimationsWorkspaceButton"));
        var editor = main.OwnedWindows.OfType<AnimationEditorWindow>().Single();
        Require(editor.IsVisible && editor.GetVisualDescendants().OfType<StageRenderer>().Count() >= 67, "A galeria não compôs os 66 cartões e a prévia.");
        var cancel = editor.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Cancelar");
        Click(cancel);
        Require(!editor.IsVisible && preview.Renderer.Frame.Style == before, "Cancelar alterou a cena ou manteve a galeria aberta.");
    });

    Check("Fechar projeção apaga o retorno e conserva a janela de operação", () =>
    {
        Click(C<Button>("OpenProjectionButton"));
        Require(main.IsVisible && !projection!.IsVisible, "Fechar projeção encerrou a operação ou deixou a saída aberta.");
        WaitUntil(() => live.Renderer.Frame.Blackout);
        Require(C<TextBlock>("LiveLabel").Text == "SAÍDA FECHADA", "Indicador não informa saída fechada.");
    });

    Check("Encerrar operação salva a biblioteca sem incluir preferências na cena", () =>
    {
        main.Close(); Pump();
        var restored = new LibraryStore().Load();
        Require(restored.Songs.Count == 2 && restored.Songs.Single(s => s.Id == fixtureSong.Id).Lyrics == fixtureSong.Lyrics, "A biblioteca de teste não foi preservada ao fechar.");
        Require(restored.Style == originalStyle, "As mudanças de tema alteraram o estilo salvo da projeção.");
        Require(!File.ReadAllText(new LibraryStore().DocumentPath).Contains("AutoCheckUpdates"), "Preferências foram misturadas com a biblioteca.");
    });
}
catch (Exception ex)
{
    results.Add(new CheckResult("Execução completa da interface", false, ex.ToString()));
    Console.WriteLine("FAIL Execução completa da interface: " + ex);
}
finally
{
    try { main?.Close(); Dispatcher.UIThread.RunJobs(); } catch { }
    var report = new
    {
        suite = "Interface real Avalonia Headless + Skia", application = typeof(App).Assembly.GetName().Version?.ToString(),
        mode = "isolated, automatic update checks disabled, no native OS UI", passed = results.Count(r => r.Passed),
        failed = results.Count(r => !r.Passed), checks = results, imageHashes
    };
    File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    LibraryStore.DataDirectoryOverride = null;
    try { if (Directory.Exists(isolatedData)) Directory.Delete(isolatedData, recursive: true); } catch { }
}
Console.WriteLine($"Interface: {results.Count(r => r.Passed)} aprovados; {results.Count(r => !r.Passed)} falhas.");
Environment.ExitCode = results.Any(r => !r.Passed) ? 1 : 0;

void Check(string name, Action test)
{
    try { test(); results.Add(new CheckResult(name, true, null)); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { results.Add(new CheckResult(name, false, ex.ToString())); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
}
void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
T C<T>(string name) where T : Control => main!.FindControl<T>(name) ?? throw new InvalidOperationException("Controle ausente: " + name);
void Pump()
{
    Dispatcher.UIThread.RunJobs();
    if (main?.IsVisible == true) main.UpdateLayout();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    Dispatcher.UIThread.RunJobs();
}
void Click(Control button)
{
    Require(button.IsEnabled && button.IsEffectivelyVisible, "Botão indisponível para operação: " + button.Name);
    var owner = TopLevel.GetTopLevel(button) ?? throw new InvalidOperationException("Botão fora da janela.");
    var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), owner)
        ?? throw new InvalidOperationException("Botão sem posição no layout.");
    owner.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
    owner.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
    Pump();
}
ProjectionWindow FindProjection()
{
    // The output is intentionally unowned and no public accessor exists. Inspect the created instance only;
    // every operation above is still performed through the real controls and routed input.
    var field = typeof(MainWindow).GetField("_projection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    return field.GetValue(main) as ProjectionWindow ?? throw new InvalidOperationException("A janela de projeção não foi criada.");
}
Button FindButton(Window window, string text) => window.GetVisualDescendants().OfType<Button>()
    .Single(b => b.Content as string == text);
void WaitUntil(Func<bool> condition)
{
    var watch = Stopwatch.StartNew();
    while (!condition())
    {
        if (watch.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("A interface não concluiu a ação no prazo do teste.");
        Pump();
        Thread.Sleep(10);
    }
    Pump();
}
void Capture(Window window, string name)
{
    using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("A janela não produziu pixels no backend Skia.");
    var path = Path.Combine(output, name + ".png");
    bitmap.Save(path);
    imageHashes[name] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
sealed record CheckResult(string Name, bool Passed, string? Error);
sealed class FixtureUpdateHandler(byte[] installer, byte[] manifest) : HttpMessageHandler
{
    public int Requests { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests++;
        var content = request.RequestUri!.AbsoluteUri.EndsWith(".txt", StringComparison.Ordinal) ? manifest : installer;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request, Content = new ByteArrayContent(content)
        });
    }
}
