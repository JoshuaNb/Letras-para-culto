using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NAudio.Wave;
using WorshipStudio.Controls;
using WorshipStudio.Models;
using WorshipStudio.Services;

namespace WorshipStudio;

/// <summary>Exports the actual animated stage, including native-video background compositing.</summary>
public sealed class ExportDialog : Window
{
    private readonly IReadOnlyList<Slide> _slides;
    private readonly StageStyle _style;
    private readonly TextBox _destination = new() { Watermark = "Escolha o arquivo MP4 de destino…", IsReadOnly = true };
    private readonly TextBox _audio = new() { Watermark = "Sem trilha de áudio", IsReadOnly = true };
    private readonly ComboBox _resolution = new() { ItemsSource = new[] { "1280 × 720 (HD)", "1920 × 1080 (Full HD)" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _fps = new() { ItemsSource = new[] { "24 quadros/s", "30 quadros/s" }, SelectedIndex = 1, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly NumericUpDown _seconds = new() { Minimum = .5m, Maximum = 120m, Increment = .5m, FormatString = "0.0 's'", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _hardware = new() { Content = "Tentar codificação H.264 pela GPU AMD (AMF)", IsChecked = false };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 100, Height = 8, Value = 0 };
    private readonly TextBlock _duration = new() { Foreground = Brush.Parse("#8D9BB0"), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { Text = "Pronto para exportar. O arquivo é gerado neste computador, sem conexão.", TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#A9B7CA") };
    private readonly Button _start = new() { Content = "Exportar MP4", Classes = { "primary" } };
    private readonly Button _cancel = new() { Content = "Fechar" };
    private readonly StackPanel _settings = new() { Spacing = 15 };
    private CancellationTokenSource? _cts;
    private bool _busy;
    private bool _closeAfterCancel;

    public ExportDialog(IReadOnlyList<Slide> slides, StageStyle style, double initialSeconds = 8)
    {
        _slides = slides.ToArray();
        _style = style with { };
        Title = "Worship Studio • Exportar vídeo";
        Width = 640;
        Height = 625;
        MinWidth = 520;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _seconds.Value = (decimal)Math.Clamp(double.IsFinite(initialSeconds) ? initialSeconds : 8, .5, 120);

        var chooseDestination = new Button { Content = "Escolher…" };
        chooseDestination.Click += async (_, _) => await ChooseDestination();
        var chooseAudio = new Button { Content = "Adicionar…" };
        chooseAudio.Click += async (_, _) => await ChooseAudio();
        var removeAudio = new Button { Content = "Remover" };
        removeAudio.Click += (_, _) => _audio.Text = "";

        _settings.Children.Add(Field("ARQUIVO DE DESTINO", Row(_destination, chooseDestination)));
        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*") };
        var res = Field("RESOLUÇÃO", _resolution);
        var fps = Field("TAXA DE QUADROS", _fps);
        Grid.SetColumn(fps, 2);
        columns.Children.Add(res);
        columns.Children.Add(fps);
        _settings.Children.Add(columns);
        _settings.Children.Add(Field("TEMPO POR ESTROFE", _seconds));
        _settings.Children.Add(_duration);
        _settings.Children.Add(Field("TRILHA LOCAL (OPCIONAL)", Row(_audio, chooseAudio, removeAudio)));
        _settings.Children.Add(new TextBlock
        {
            Text = "A intensidade do áudio escolhido também anima os efeitos do vídeo. A trilha é incluída no MP4; silêncio completa o tempo restante.",
            FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#8D9BB0")
        });
        _settings.Children.Add(_hardware);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(_cancel);
        actions.Children.Add(_start);
        var body = new StackPanel { Margin = new Thickness(26), Spacing = 18 };
        body.Children.Add(new TextBlock { Text = "Exportar a projeção", FontSize = 23, FontWeight = FontWeight.SemiBold });
        body.Children.Add(new TextBlock { Text = "Letras animadas, fundos e transições em um arquivo MP4 offline.", TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#8D9BB0") });
        body.Children.Add(_settings);
        body.Children.Add(_progress);
        body.Children.Add(_status);
        body.Children.Add(actions);
        Content = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

        _seconds.ValueChanged += (_, _) => UpdateDuration();
        _start.Click += async (_, _) => await StartExport();
        _cancel.Click += (_, _) =>
        {
            if (_busy)
            {
                _cts?.Cancel();
                _cancel.IsEnabled = false;
                _status.Text = "Cancelando e removendo o arquivo temporário…";
            }
            else Close();
        };
        Closing += (_, e) =>
        {
            if (!_busy) return;
            e.Cancel = true;
            _closeAfterCancel = true;
            _cts?.Cancel();
            _cancel.IsEnabled = false;
            _status.Text = "Cancelando antes de fechar…";
        };
        UpdateDuration();
    }

    private static Control Field(string label, Control input)
    {
        var stack = new StackPanel { Spacing = 7 };
        stack.Children.Add(new TextBlock { Text = label, Classes = { "caption" } });
        stack.Children.Add(input);
        return stack;
    }

    private static Grid Row(Control grow, params Control[] buttons)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*" + string.Concat(buttons.Select(_ => ",8,Auto"))) };
        grid.Children.Add(grow);
        for (var i = 0; i < buttons.Length; i++)
        {
            Grid.SetColumn(buttons[i], i * 2 + 2);
            grid.Children.Add(buttons[i]);
        }
        return grid;
    }

    private void UpdateDuration()
    {
        var seconds = (double)(_seconds.Value ?? 8);
        var duration = TimeSpan.FromSeconds(_slides.Count * seconds);
        _duration.Text = $"{_slides.Count} estrofe(s) • duração total {duration:hh\\:mm\\:ss}. Cada estrofe recebe o mesmo tempo.";
    }

    private async Task ChooseDestination()
    {
        var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar vídeo MP4",
            SuggestedFileName = "Worship Studio.mp4",
            DefaultExtension = "mp4",
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType("Vídeo MP4") { Patterns = ["*.mp4"] }]
        });
        if (target?.TryGetLocalPath() is { } path) _destination.Text = path;
    }

    private async Task ChooseAudio()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Escolher trilha de áudio local",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Áudio") { Patterns = ["*.wav", "*.mp3", "*.m4a", "*.aac", "*.flac", "*.wma"] }, FilePickerFileTypes.All]
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) _audio.Text = path;
    }

    private async Task StartExport()
    {
        if (_busy) return;
        if (_slides.Count == 0) { _status.Text = "Não há estrofes para exportar."; return; }
        if (string.IsNullOrWhiteSpace(_destination.Text))
        {
            await ChooseDestination();
            if (string.IsNullOrWhiteSpace(_destination.Text)) return;
        }
        var output = _destination.Text!.Trim();
        if (!string.Equals(Path.GetExtension(output), ".mp4", StringComparison.OrdinalIgnoreCase)) output += ".mp4";
        _destination.Text = output;
        var width = _resolution.SelectedIndex == 1 ? 1920 : 1280;
        var height = _resolution.SelectedIndex == 1 ? 1080 : 720;
        var fps = _fps.SelectedIndex == 0 ? 24 : 30;
        var seconds = (double)(_seconds.Value ?? 8);
        var audioPath = string.IsNullOrWhiteSpace(_audio.Text) ? null : _audio.Text!.Trim();
        var videoPath = _style.BackgroundMode == "Video" ? _style.BackgroundPath : null;
        var request = new VideoExportRequest(output, _slides.Count, seconds, width, height, fps, videoPath, audioPath, _hardware.IsChecked == true);
        var service = new VideoExportService();
        if (!service.IsAvailable) { _status.Text = "FFmpeg não foi encontrado. Reinstale o pacote completo do Worship Studio."; return; }

        _busy = true;
        _settings.IsEnabled = false;
        _start.IsEnabled = false;
        _cancel.Content = "Cancelar exportação";
        _progress.Value = 0;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        using var renderer = new StageRenderer { VideoOverlay = !string.IsNullOrWhiteSpace(videoPath) };
        try
        {
            float[]? envelope = null;
            if (audioPath != null)
            {
                _status.Text = "Analisando o áudio local para sincronizar os efeitos…";
                envelope = await Task.Run(() => ReadAudioEnvelope(audioPath, _slides.Count * seconds, ct), ct);
            }
            _status.Text = "Renderizando quadros e codificando o MP4…";
            var progress = new Progress<double>(value =>
            {
                if (!_busy || ct.IsCancellationRequested) return;
                _progress.Value = value * 100;
                _status.Text = $"Exportando {value:P0} • codificador {service.LastEncoder}";
            });
            await service.ExportAsync(request, async (index, localTime, totalTime, token) =>
            {
                token.ThrowIfCancellationRequested();
                return await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    token.ThrowIfCancellationRequested();
                    var level = envelope != null && (int)(totalTime * 60) < envelope.Length ? envelope[(int)(totalTime * 60)] : 0;
                    renderer.SetFrame(new StageFrame
                    {
                        Text = _slides[index].Text,
                        PreviousText = index > 0 ? _slides[index - 1].Text : "",
                        Style = _style,
                        AudioLevel = level,
                        Revision = index
                    });
                    using var bitmap = renderer.Capture(width, height, totalTime,
                        _style.Transition == "Cut" || _style.TransitionSeconds <= 0 ? 1 : Math.Clamp(localTime / _style.TransitionSeconds, 0, 1),cueElapsed:localTime);
                    using var memory = new MemoryStream();
                    bitmap.Save(memory);
                    return memory.ToArray();
                });
            }, progress, ct);
            _progress.Value = 100;
            _status.Text = $"MP4 salvo em {output}\nCodificador usado: {service.LastEncoder}.";
        }
        catch (OperationCanceledException) { _progress.Value = 0; _status.Text = "Exportação cancelada. O destino anterior foi preservado."; }
        catch (Exception ex) { _status.Text = "Não foi possível exportar: " + ex.Message; AppLog.Write(ex.ToString()); }
        finally
        {
            _busy = false;
            _cts.Dispose();
            _cts = null;
            _settings.IsEnabled = true;
            _start.IsEnabled = true;
            _cancel.IsEnabled = true;
            _cancel.Content = "Fechar";
            if (_closeAfterCancel) Close();
        }
    }

    /// <summary>60 Hz RMS envelope of the supplied audio; no network or live capture.</summary>
    private static float[] ReadAudioEnvelope(string path, double duration, CancellationToken ct)
    {
        using var reader = new AudioFileReader(path);
        var bins = checked((int)Math.Ceiling(duration * 60) + 1);
        var result = new float[bins];
        var buffer = new float[32768];
        var samplesPerSecond = (long)reader.WaveFormat.SampleRate * reader.WaveFormat.Channels;
        long position = 0;
        var currentBin = 0;
        double sum = 0;
        var count = 0;
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            for (var i = 0; i < read; i++, position++)
            {
                var bin = (int)(position * 60 / samplesPerSecond);
                if (bin >= bins) goto Completed;
                if (bin != currentBin)
                {
                    result[currentBin] = (float)Math.Clamp(Math.Sqrt(sum / Math.Max(1, count)) * 4, 0, 1);
                    currentBin = bin;
                    sum = 0;
                    count = 0;
                }
                if (float.IsFinite(buffer[i])) { sum += buffer[i] * buffer[i]; count++; }
            }
        }
        Completed:
        if (currentBin < bins) result[currentBin] = (float)Math.Clamp(Math.Sqrt(sum / Math.Max(1, count)) * 4, 0, 1);
        var level = 0f;
        for (var i = 0; i < result.Length; i++)
        {
            level += (result[i] - level) * (result[i] > level ? .7f : .18f);
            result[i] = level;
        }
        return result;
    }
}
