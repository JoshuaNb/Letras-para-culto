using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using SkiaSharp;
using WorshipStudio.Controls;
using WorshipStudio.Models;

// Run on one UI thread using real Skia pixels. No native media player or user library is opened.
AppBuilder.Configure<Application>().UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();
Application.Current!.Styles.Add(new FluentTheme());

var argumentOutput = Array.IndexOf(args, "--output");
var output = Path.GetFullPath(argumentOutput >= 0 && argumentOutput + 1 < args.Length ? args[argumentOutput + 1] : Path.Combine(AppContext.BaseDirectory, "evidence"));
Directory.CreateDirectory(output);
var results = new List<CheckResult>();
var baselineOnly = args.Contains("--baseline-font");
var baseStyle = new StageStyle
{
    FontFamily = "Segoe UI", FontSize = 110, Bold = true, BackgroundMode = "Solid",
    BackgroundColor = "#000000", TextColor = "#FFFFFF", Dim = 0, MotionSpeed = 0,
    Transition = "Cut", LyricAnimation = "None", LyricAnimationIntensity = .65
};
const string sampleText = "Grande é a Tua fidelidade\nCoração, bênçãos e paz";

void Check(string name, Action action)
{
    try { action(); results.Add(new(name, true, null)); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { results.Add(new(name, false, ex.Message)); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
}
void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
Rendered Render(StageRenderer renderer, StageStyle style, double time = 0, double progress = 1, string text = sampleText, string? artifact = null)
{
    renderer.SetFrame(new StageFrame { Text = text, Style = style });
    using var bitmap = renderer.Capture(960, 540, time, progress);
    using var memory = new MemoryStream(); bitmap.Save(memory);
    var png = memory.ToArray();
    using var decoded = SKBitmap.Decode(png) ?? throw new InvalidDataException("PNG inválido.");
    if (artifact != null) File.WriteAllBytes(Path.Combine(output, artifact + ".png"), png);
    return new Rendered(decoded.Pixels, decoded.Width, decoded.Height);
}
Rendered CurrentPixels(Control control, int width, int height)
{
    control.Measure(new Size(width, height));
    control.Arrange(new Rect(0, 0, width, height));
    using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
    bitmap.Render(control);
    using var memory = new MemoryStream(); bitmap.Save(memory);
    using var decoded = SKBitmap.Decode(memory.ToArray());
    return new Rendered(decoded.Pixels, decoded.Width, decoded.Height);
}

using (var sameRenderer = new StageRenderer())
{
    Check("Fontes mudam na mesma estrofe sem limpar a tela", () =>
    {
        var segoe = Render(sameRenderer, baseStyle, artifact: "font-segoe");
        var georgia = Render(sameRenderer, baseStyle with { FontFamily = "Georgia" }, artifact: "font-georgia");
        var consolas = Render(sameRenderer, baseStyle with { FontFamily = "Consolas" }, artifact: "font-consolas");
        Require(segoe.Ink > 1000 && georgia.Ink > 1000 && consolas.Ink > 1000, "As três fontes precisam produzir texto visível.");
        Require(segoe.Hash != georgia.Hash && georgia.Hash != consolas.Hash && segoe.Hash != consolas.Hash,
            "Trocar a família mantendo o mesmo texto reutilizou os pixels da fonte anterior.");
        var returned = Render(sameRenderer, baseStyle);
        Require(segoe.Hash == returned.Hash, "Voltar à fonte inicial precisa reproduzir sua forma original.");
    });
    if (!baselineOnly)
    {
        Check("Negrito altera a espessura e pode ser desativado", () =>
        {
            var normal = Render(sameRenderer, baseStyle with { Bold = false }, artifact: "normal");
            var bold = Render(sameRenderer, baseStyle with { Bold = true }, artifact: "bold");
            var returned = Render(sameRenderer, baseStyle with { Bold = false });
            Require(normal.Hash != bold.Hash, "Negrito e normal resultaram nos mesmos pixels.");
            Require(bold.Ink > normal.Ink * 1.05, "O peso negrito precisa ocupar mais área que o peso normal.");
            Require(normal.Hash == returned.Hash, "Desativar negrito não restaurou o peso normal.");
        });
        Check("Cores da letra são aplicadas imediatamente", () =>
        {
            var red = Render(sameRenderer, baseStyle with { TextColor = "#FF0000" }, artifact: "color-red");
            var blue = Render(sameRenderer, baseStyle with { TextColor = "#0080FF" }, artifact: "color-blue");
            Require(red.Pixels.Count(c => c.Red > 200 && c.Green < 20 && c.Blue < 20) > 800, "A letra vermelha não apareceu no quadro.");
            Require(blue.Pixels.Count(c => c.Blue > 200 && c.Green > 80 && c.Red < 20) > 800, "A letra azul não apareceu no quadro.");
            Require(red.Hash != blue.Hash, "A troca de cor não alterou a projeção.");
        });
    }
}

if (!baselineOnly)
{
    Check("Seletor visual reflete a cor e avisa uma vez por mudança", () =>
    {
        var color = new ColorField { Width = 180, Height = 40 };
        var changes = 0;
        color.ColorChanged += () => changes++;
        var window = new Window { Width = 200, Height = 70, Content = color };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            color.Value = "#FF0000";
            var red = CurrentPixels(color, 180, 40);
            color.Value = "#0080FF";
            var blue = CurrentPixels(color, 180, 40);
            color.Value = "#0080FF";
            Require(changes == 2, "A alteração de cor deve ser publicada uma vez, sem recursão ou evento duplicado.");
            Require(red.Pixels.Count(c => c.Red > 200 && c.Green < 20 && c.Blue < 20) > 50, "A amostra vermelha do seletor não foi atualizada.");
            Require(blue.Pixels.Count(c => c.Blue > 200 && c.Green > 80 && c.Red < 20) > 50, "A amostra azul do seletor não foi atualizada.");
        }
        finally { window.Close(); }
    });
    Check("Duração zero mostra a entrada completa no primeiro quadro ao vivo", () =>
    {
        using var renderer = new StageRenderer();
        var complete = Render(renderer, baseStyle);
        foreach (var transition in new[] { "Fade", "Slide", "Zoom", "Pop", "Rotate", "Wipe", "Typewriter", "Words", "Lines" })
        {
            renderer.SetFrame(new StageFrame { Text = sampleText, Style = baseStyle with { Transition = transition, TransitionSeconds = 0 } });
            renderer.SetRenderTime(0);
            var live = CurrentPixels(renderer, 960, 540);
            Require(live.Hash == complete.Hash, "A entrada " + transition + " com duração zero não foi imediata.");
        }
        var explicitStart = Render(renderer, baseStyle with { Transition = "Fade", TransitionSeconds = 0 }, progress: 0);
        Require(explicitStart.Ink < complete.Ink * .1, "A amostragem explícita do Capture deve continuar respeitando o progresso informado.");
    });
    foreach (var transition in new[] { "Fade", "Slide", "SlideDown", "SlideLeft", "SlideRight", "Zoom", "Pop", "Rotate", "Wipe", "Typewriter", "Words", "Lines" })
    {
        var mode = transition;
        Check("Entrada " + mode + " evolui e termina completa", () =>
        {
            using var renderer = new StageRenderer();
            var style = baseStyle with { Transition = mode, TransitionSeconds = 1 };
            var beginning = Render(renderer, style, progress: 0);
            var middle = Render(renderer, style, progress: .35, artifact: "entrance-" + mode);
            var complete = Render(renderer, style, progress: 1);
            var cut = Render(renderer, baseStyle, progress: 1);
            Require(beginning.Ink < complete.Ink * .1, "O início da entrada deveria ocultar a nova estrofe.");
            Require(middle.Hash != beginning.Hash && middle.Hash != complete.Hash, "O meio da entrada precisa representar um quadro intermediário.");
            Require(complete.Ink > 1000 && complete.Hash == cut.Hash, "A entrada deve terminar com toda a estrofe na posição final.");
        });
    }
    foreach (var effect in new[] { "Float", "Wave", "Pulse", "Breathe", "Sway", "Bounce", "Glow", "Shake" })
    {
        var mode = effect;
        Check("Animação contínua " + mode + " funciona com fundo parado", () =>
        {
            using var renderer = new StageRenderer();
            var style = baseStyle with { LyricAnimation = mode, LyricAnimationSpeed = 1.3, MotionSpeed = 0 };
            var first = Render(renderer, style, .17);
            var later = Render(renderer, style, .83, artifact: "animation-" + mode);
            Require(first.Ink > 500 && later.Ink > 500, "O efeito precisa preservar a legibilidade do texto.");
            Require(first.Hash != later.Hash, "O efeito não mudou entre os tempos amostrados.");
            var repeated = Render(renderer, style, .83);
            Require(later.Hash == repeated.Hash, "O tempo determinístico deve gerar os mesmos pixels na exportação.");
        });
    }
    Check("Nenhuma animação mantém letras estáveis", () =>
    {
        using var renderer = new StageRenderer();
        Require(Render(renderer, baseStyle, .17).Hash == Render(renderer, baseStyle, .83).Hash, "O modo sem animação não pode movimentar as letras.");
    });
    Check("Velocidade e intensidade alteram o movimento escolhido", () =>
    {
        using var renderer = new StageRenderer();
        var moving = baseStyle with { LyricAnimation = "Float", LyricAnimationSpeed = .5, LyricAnimationIntensity = .3 };
        var first = Render(renderer, moving, .61);
        var faster = Render(renderer, moving with { LyricAnimationSpeed = 2.1 }, .61);
        var stronger = Render(renderer, moving with { LyricAnimationIntensity = .9 }, .61);
        Require(first.Hash != faster.Hash, "A velocidade não alterou o movimento.");
        Require(first.Hash != stronger.Hash, "A intensidade não alterou o movimento.");
    });
    Check("Unicode e estrofe extensa permanecem dentro do quadro", () =>
    {
        using var renderer = new StageRenderer();
        var text = "Água, ação, fé e coração 👨‍👩‍👧‍👦\n" + string.Join('\n', Enumerable.Repeat("A esperança permanece em cada novo amanhecer e em todo caminhar", 10));
        foreach (var mode in new[] { "Wave", "Float", "Sway", "Bounce" })
        {
            var shot = Render(renderer, baseStyle with { LyricAnimation = mode, FontSize = 140 }, .42, text: text);
            Require(shot.Ink > 1000, "A estrofe extensa desapareceu.");
            Require(shot.EdgeInk(4) == 0, "A animação cortou texto na borda do quadro.");
        }
    });
    Check("Tela preta e letra oculta prevalecem sobre animações", () =>
    {
        using var renderer = new StageRenderer();
        foreach (var black in new[] { false, true })
        {
            renderer.SetFrame(new StageFrame { Text = sampleText, Style = baseStyle with { LyricAnimation = "Glow" }, Blackout = black, HideLyrics = !black });
            using var bitmap = renderer.Capture(960, 540, .42);
            using var memory = new MemoryStream(); bitmap.Save(memory);
            using var decoded = SKBitmap.Decode(memory.ToArray());
            Require(decoded.Pixels.All(c => c.Red == 0 && c.Green == 0 && c.Blue == 0 && c.Alpha == 255), "Animação vazou na tela preta/letra oculta.");
        }
    });
}

if(!baselineOnly)
{
    GranularityChecks.RunAndThrow(Path.Combine(output,"granularity-1.2"));
    BackgroundChecks.RunAndThrow(Path.Combine(output,"backgrounds-1.8"));
}
var summary = new { Passed = results.Count(r => r.Passed), Failed = results.Count(r => !r.Passed), Checks = results };
File.WriteAllText(Path.Combine(output, "typography-render-tests.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{summary.Passed} aprovados; {summary.Failed} falhas. Evidências: {output}");
return summary.Failed == 0 ? 0 : 1;

internal sealed record CheckResult(string Name, bool Passed, string? Error);
internal sealed record Rendered(SKColor[] Pixels, int Width, int Height)
{
    public string Hash => Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(Pixels.AsSpan())));
    public int Ink => Pixels.Count(c => c.Red > 25 || c.Green > 25 || c.Blue > 25);
    public int EdgeInk(int margin) => Pixels.Where((c, i) => (i % Width < margin || i % Width >= Width - margin || i / Width < margin || i / Width >= Height - margin) && (c.Red > 25 || c.Green > 25 || c.Blue > 25)).Count();
}
