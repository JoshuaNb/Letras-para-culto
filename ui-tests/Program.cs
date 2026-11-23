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

    Check("Menus reais abrem preferências e conexões sem iniciar serviços", () =>
    {
        MenuCommand("MenuPreferences");
        Require(C<TabControl>("InspectorTabs").SelectedIndex == 2, "O menu não abriu Preferências.");
        MenuCommand("MenuConnections");
        Require(C<TabControl>("InspectorTabs").SelectedIndex == 1, "O menu não abriu Conexões.");
        Require(C<TextBlock>("PairingCodeLabel").Text == "— — — — — —", "Abrir o menu iniciou a rede.");
        Click(C<Button>("OperationWorkspaceButton"));
    });

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

    Check("Recebidos abre sem iniciar a rede ou alterar a apresentação", () =>
    {
        C<TabControl>("InspectorTabs").SelectedIndex=1;Pump();
        var before=preview.Renderer.Frame;
        var open=C<Button>("ReceivedFilesButton");open.BringIntoView();Pump();Click(open);
        var inbox=main.OwnedWindows.OfType<ReceivedFilesWindow>().Single(w=>w.IsVisible);inbox.UpdateLayout();Pump();
        T R<T>(string name) where T:Control=>inbox.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
        WaitUntil(()=>R<Button>("ReceivedRefresh").IsEnabled);
        Require(R<ListBox>("ReceivedFilesList").ItemCount==0,"A caixa de entrada isolada deveria estar vazia.");
        Require(!R<Button>("ReceivedUseBackground").IsEnabled&&!R<Button>("ReceivedImportLyrics").IsEnabled&&!R<Button>("ReceivedSaveCopy").IsEnabled,"Ações foram habilitadas sem arquivo selecionado.");
        Require(C<TextBlock>("PairingCodeLabel").Text=="— — — — — —","Abrir a caixa iniciou o servidor.");
        Require(preview.Renderer.Frame.Text==before.Text&&preview.Renderer.Frame.Style==before.Style,"O recebimento alterou a cena sem escolha do operador.");
        Capture(inbox,"received-empty");Click(R<Button>("ReceivedClose"));Pump();
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

    Check("Preferência de vídeo econômico aplica nas superfícies e preserva letras", () =>
    {
        C<TabControl>("InspectorTabs").SelectedIndex=2;Pump();
        var economy=C<CheckBox>("CompatibleVideoCheck");economy.BringIntoView();Pump();
        if(WindowsCompatibility.IsLegacyRuntime)
        {
            Require(economy.IsChecked==true&&!economy.IsEnabled&&preview.VideoQuality==VideoPlaybackQuality.Compatible,"Edição legado não fixou a qualidade econômica.");
        }
        else
        {
            var original=preview.Renderer.Frame.Style with{};
            Click(economy);
            Require(StudioPreferences.Load(isolatedData).CompatibleVideoPlayback&&preview.VideoQuality==VideoPlaybackQuality.Compatible&&live.VideoQuality==VideoPlaybackQuality.Compatible,"A preferência não chegou às superfícies.");
            Require(preview.Renderer.Frame.Style==original&&preview.Renderer.Frame.Text==initialText,"A qualidade do vídeo mudou a cena ou as letras.");
            Click(economy);
            Require(!StudioPreferences.Load(isolatedData).CompatibleVideoPlayback&&preview.VideoQuality==VideoPlaybackQuality.Auto,"A opção econômica não foi revertida.");
        }
        C<TabControl>("InspectorTabs").SelectedIndex=0;Pump();
    });

    Check("Compatibilidade gráfica persiste e pede reinício sem fechar a operação", () =>
    {
        C<TabControl>("InspectorTabs").SelectedIndex=2;Pump();
        var software=C<CheckBox>("SoftwareRenderingCheck");software.BringIntoView();Pump();
        if(WindowsCompatibility.IsLegacyRuntime)
            Require(software.IsChecked==true&&!software.IsEnabled,"A edição legado não informou o modo por software.");
        else
        {
            Click(software);
            Require(StudioPreferences.Load(isolatedData).ForceSoftwareRendering&&C<Button>("RestartGraphicsButton").IsVisible&&main.IsVisible,"A escolha gráfica não foi salva ou fechou o operador sem ação.");
            Click(software);
            Require(!StudioPreferences.Load(isolatedData).ForceSoftwareRendering&&!C<Button>("RestartGraphicsButton").IsVisible,"Voltar ao modo automático não removeu o reinício pendente.");
        }
        C<TabControl>("InspectorTabs").SelectedIndex=0;Pump();
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

    Check("Oito novos fundos chegam à prévia, retorno e janela independente com as letras", () =>
    {
        var combo=C<ComboBox>("BackgroundCombo");var oldIndex=combo.SelectedIndex;
        var oldSpeed=C<Slider>("SpeedSlider").Value;
        var choices=BackgroundCatalog.Choices;
        Require(combo.ItemCount==14&&choices.Count(c=>c.IsProcedural)==11,"O catálogo de fundos não foi composto.");
        Require(projection!.Surface.VideoQuality==preview.VideoQuality,"A projeção não herdou a qualidade de vídeo.");
        var added=choices.Where(c=>c.IsProcedural&&c.Id is not("Aurora" or "Tunnel" or "Particles")).ToArray();
        Require(added.Length==8,"Os novos fundos não chegaram ao catálogo.");
        C<Slider>("SpeedSlider").Value=1;
        foreach(var choice in added)
        {
            combo.SelectedIndex=choices.ToList().IndexOf(choice);Pump();
            Require(preview.Renderer.Frame.Style.BackgroundMode==choice.Id&&live.Renderer.Frame.Style.BackgroundMode==choice.Id&&projection.Surface.Renderer.Frame.Style.BackgroundMode==choice.Id,"Um fundo não alcançou as três superfícies: "+choice.Name);
            Require(live.Renderer.Frame.Text==initialText&&projection.Surface.Renderer.Frame.Text==initialText,"Mudar o fundo interrompeu as letras.");
            if(choice.Id=="Galaxy")Capture(main,"background-galaxy-live");
        }
        combo.SelectedIndex=oldIndex;C<Slider>("SpeedSlider").Value=oldSpeed;Pump();
    });

    Check("Trilha do roteiro prepara outro louvor sem interromper a projeção", () =>
    {
        var timeline = C<StackPanel>("SetlistTimelinePanel");
        Require(timeline.Children.OfType<Button>().Select(b => (Guid)b.Tag!).SequenceEqual(new[] { fixtureSong.Id, secondSong.Id }), "O roteiro não representa a ordem da biblioteca.");
        Click((Button)timeline.Children[1]);
        Require(preview.Renderer.Frame.Text.Contains("Outro texto"), "O clipe do roteiro não preparou o louvor.");
        Require(live.Renderer.Frame.Text == initialText && projection!.Surface.Renderer.Frame.Text == initialText, "Preparar no roteiro alterou o programa ao vivo.");
        Click((Button)timeline.Children[0]);
        Require(preview.Renderer.Frame.Text == initialText && C<TextBlock>("TimelinePosition").Text!.Contains("01 / 03"), "O roteiro não recuperou a prévia e sua posição.");
    });

    Check("Reordenar o roteiro atualiza os clipes e preserva a saída", () =>
    {
        var timeline = C<StackPanel>("SetlistTimelinePanel");
        Click((Button)timeline.Children[1]);Click(C<Button>("SetUpButton"));
        Require((Guid)((Button)timeline.Children[0]).Tag! == secondSong.Id, "Mover para cima não atualizou a trilha.");
        Click(C<Button>("SetDownButton"));
        Require((Guid)((Button)timeline.Children[0]).Tag! == fixtureSong.Id, "Mover para baixo não restaurou a ordem.");
        Click((Button)timeline.Children[0]);
        Require(live.Renderer.Frame.Text == initialText, "Reordenar o roteiro alterou a projeção.");
    });

    Check("Menu de projeção controla tela preta e letra na saída real", () =>
    {
        MenuCommand("MenuBlackout");
        Require(live.Renderer.Frame.Blackout && projection!.Surface.Renderer.Frame.Blackout, "Tela preta do menu não alcançou a saída.");
        MenuCommand("MenuBlackout");MenuCommand("MenuHideLyrics");
        Require(!live.Renderer.Frame.Blackout && live.Renderer.Frame.HideLyrics && projection!.Surface.Renderer.Frame.HideLyrics, "Ocultar letra do menu não preservou o fundo.");
        MenuCommand("MenuHideLyrics");
        Require(!live.Renderer.Frame.HideLyrics && !preview.Renderer.Frame.HideLyrics, "O menu não restaurou a letra.");
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

    Check("Abertura carrega letras reais e permite dispensar a rede sem uma segunda consulta", () =>
    {
        var directory = Path.Combine(isolatedData, "startup-isolated");
        var store = new LibraryStore(directory);
        store.Save(new StudioDocument { Songs = [fixtureSong], Setlist = [fixtureSong.Id] });
        var pending = new TaskCompletionSource<UpdateRelease?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        using var session = new StartupCoordinator(directory, checkUpdates: _ => { Interlocked.Increment(ref requests); return pending.Task; }).Start(true);
        MainWindow? opened = null;
        StartupUpdateResult? updateResult = null;
        var startup = new StartupWindow(session, (library, update) =>
        {
            updateResult = update;
            opened = new MainWindow(library, update);opened.Show();
        });
        try
        {
            startup.Show();Pump();
            WaitUntil(() => session.Library.IsCompleted && Volatile.Read(ref requests) == 1);
            WaitUntil(() => startup.FindControl<TextBlock>("StartupLibraryStatus")!.Text!.Contains("1 louvor"));
            Capture(startup, "startup-library-ready");
            Click(startup.FindControl<Button>("ContinueStartupButton")!);
            WaitUntil(() => opened != null && opened.IsVisible && !startup.IsVisible);
            Require(updateResult?.State == StartupUpdateState.Cancelled, "Dispensar a espera não cancelou a consulta.");
            Require(opened!.FindControl<TextBlock>("SelectedTitle")!.Text == fixtureSong.Title, "A abertura não entregou a biblioteca local à operação.");
            Require(Volatile.Read(ref requests) == 1, "A janela de operação consultou novamente na mesma abertura.");
            Require(!((StageSurface)opened.FindControl<ContentControl>("LivePreviewHost")!.Content!).Renderer.Frame.Text.Any(), "Abertura colocou uma letra ao vivo sem comando.");
            Capture(opened, "startup-opened-operation");
        }
        finally { pending.TrySetResult(null);opened?.Close();startup.Close();Pump(); }
    });

    Check("Fechar durante a abertura cancela a leitura e impede janela tardia", () =>
    {
        using var entered = new ManualResetEventSlim();
        using var gate = new ManualResetEventSlim();
        using var session = new StartupCoordinator(loadLibrary: token =>
        {
            entered.Set();gate.Wait(token);
            return new StartupLibraryResult(fixture, null, null);
        }).Start(false);
        var callbacks = 0;
        var startup = new StartupWindow(session, (_, _) => callbacks++);
        startup.Show();Pump();WaitUntil(() => entered.IsSet);
        startup.Close();gate.Set();WaitUntil(() => session.Library.IsCompleted);
        Require(!startup.IsVisible && callbacks == 0, "Uma operação tardia abriu após cancelar a inicialização.");
    });

    Check("Layout mínimo mantém monitores, menu e clipes acessíveis", () =>
    {
        main.Width = 1180;main.Height = 760;Pump();
        Require(preview.Bounds.Width > 150 && live.Bounds.Width > 150, "Os monitores desapareceram no tamanho mínimo.");
        Require(C<Menu>("StudioMenu").Bounds.Height > 0 && C<Button>("SendButton").IsEffectivelyVisible, "Menu ou projeção ficaram inacessíveis.");
        var clip = C<WrapPanel>("SlidesPanel").Children.OfType<Button>().First();
        Require(clip.Bounds.Height >= 100, "O texto da estrofe foi cortado pela trilha.");
        Capture(main, "minimum-layout");
        main.Width = 1560;main.Height = 980;Pump();
        Capture(main, "operation-final");
    });

    Check("Arquivos recebidos persistem e só entram na biblioteca ou cena por ação explícita", () =>
    {
        var data=Path.Combine(isolatedData,"inbox-operation");
        var inboxStore=new ReceivedFileStore(Path.Combine(data,"received"));
        var words=Encoding.UTF8.GetBytes("[Refrão]\nGraça e paz\nAleluia");
        var songFile=inboxStore.ReceiveAsync("Louvor transferido.txt",words.Length,async(stream,ct)=>await stream.WriteAsync(words,ct)).GetAwaiter().GetResult();
        byte[] png;
        using(var bitmap=new SkiaSharp.SKBitmap(16,16))
        {
            bitmap.Erase(SkiaSharp.SKColors.Teal);
            using var image=SkiaSharp.SKImage.FromBitmap(bitmap);using var encoded=image.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);png=encoded.ToArray();
        }
        var imageFile=inboxStore.ReceiveAsync("Fundo recebido.png",png.Length,async(stream,ct)=>await stream.WriteAsync(png,ct)).GetAwaiter().GetResult();
        var doc=LibraryStore.CreateInitial();doc.Style.BackgroundMode="Solid";new LibraryStore(data).Save(doc);
        MainWindow? operation=null;
        var oldOverride=LibraryStore.DataDirectoryOverride;
        try
        {
            LibraryStore.DataDirectoryOverride=data;operation=new MainWindow();operation.Show();operation.UpdateLayout();Pump();
            T O<T>(string name) where T:Control=>operation.FindControl<T>(name)!;
            O<TabControl>("InspectorTabs").SelectedIndex=1;operation.UpdateLayout();var button=O<Button>("ReceivedFilesButton");button.BringIntoView();Pump();Click(button);
            var inbox=operation.OwnedWindows.OfType<ReceivedFilesWindow>().Single(w=>w.IsVisible);inbox.UpdateLayout();Pump();
            T R<T>(string name) where T:Control=>inbox.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
            var list=R<ListBox>("ReceivedFilesList");WaitUntil(()=>list.ItemCount==2);
            Require(new LibraryStore(data).Load().Songs.Count==1&&new LibraryStore(data).Load().Style.BackgroundMode=="Solid","Receber arquivos aplicou conteúdo automaticamente.");
            list.SelectedItem=list.Items.OfType<ReceivedFile>().Single(f=>f.Id==songFile.Id);inbox.UpdateLayout();Pump();
            Require(R<Button>("ReceivedImportLyrics").IsEnabled&&!R<Button>("ReceivedUseBackground").IsEnabled,"As ações não acompanham o tipo TXT.");
            Click(R<Button>("ReceivedImportLyrics"));
            WaitUntil(()=>O<ListBox>("LibraryList").Items.OfType<Song>().Any(s=>s.Title=="Louvor transferido"));
            list.SelectedItem=list.Items.OfType<ReceivedFile>().Single(f=>f.Id==imageFile.Id);inbox.UpdateLayout();Pump();
            Require(R<Button>("ReceivedUseBackground").IsEnabled&&!R<Button>("ReceivedImportLyrics").IsEnabled,"As ações não acompanham a imagem.");
            Click(R<Button>("ReceivedUseBackground"));var surface=(StageSurface)O<ContentControl>("PreviewHost").Content!;WaitUntil(()=>surface.Renderer.Frame.Style.BackgroundMode=="Image");
            Capture(inbox,"received-with-files");Click(R<Button>("ReceivedClose"));Pump();operation.Close();Pump();
            var saved=new LibraryStore(data).Load();Require(saved.Songs.Any(s=>s.Title=="Louvor transferido"),"A letra recebida não foi salva ao fechar.");Require(File.Exists(saved.Style.BackgroundPath),"A imagem não foi copiada à mídia da biblioteca.");
            Require(File.ReadAllBytes(saved.Style.BackgroundPath!).SequenceEqual(png),"A cópia de mídia difere do arquivo recebido.");
            Require(inboxStore.List().Count==2&&File.Exists(inboxStore.GetFilePath(songFile.Id)),"Usar arquivos removeu o original recebido.");
        }
        finally{operation?.Close();LibraryStore.DataDirectoryOverride=oldOverride;Pump();}
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
        mode = "isolated, official update checks disabled; injected startup/update transport; no native OS UI", passed = results.Count(r => r.Passed),
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
void MenuCommand(string name)
{
    var menu = C<Menu>("StudioMenu").Items.OfType<MenuItem>().Single(parent => parent.Items.OfType<MenuItem>().Any(item => item.Name == name));
    Click(menu);Pump();
    Click(C<MenuItem>(name));Pump();
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
