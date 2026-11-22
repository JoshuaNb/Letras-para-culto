using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace WorshipStudio.Services;

public sealed record VideoExportRequest(
    string OutputPath,
    int SlideCount,
    double SecondsPerSlide = 5,
    int Width = 1920,
    int Height = 1080,
    int FramesPerSecond = 30,
    string? BackgroundVideoPath = null,
    string? AudioPath = null,
    bool PreferHardware = false);

/// <summary>Exports actual deterministic renderer frames, compositing native video backgrounds with FFmpeg.</summary>
public sealed class VideoExportService
{
    public string FfmpegPath { get; }
    public bool IsAvailable => File.Exists(FfmpegPath);
    public string LastEncoder { get; private set; } = "mpeg4";

    public VideoExportService(string? ffmpegPath = null)
    {
        FfmpegPath = ffmpegPath ?? new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe")
        }.FirstOrDefault(File.Exists) ?? Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe");
    }

    /// <param name="renderFramePng">Receives slide index, seconds since slide start, total seconds and cancellation token. For BackgroundVideoPath, render a transparent lyric/effect overlay.</param>
    public async Task ExportAsync(VideoExportRequest request,
        Func<int, double, double, CancellationToken, Task<byte[]>> renderFramePng,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        Validate(request);
        if (!IsAvailable) throw new FileNotFoundException("FFmpeg não foi encontrado no pacote. Reinstale o Worship Studio.", FfmpegPath);
        ct.ThrowIfCancellationRequested();
        var output = Path.GetFullPath(request.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var partial = Path.Combine(Path.GetDirectoryName(output)!, "." + Path.GetFileNameWithoutExtension(output) + "." + Guid.NewGuid().ToString("N") + ".partial.mp4");
        var frameCount = checked((int)Math.Ceiling(request.SlideCount * request.SecondsPerSlide * request.FramesPerSecond));
        var duration = frameCount / (double)request.FramesPerSecond;
        var useHardware = request.PreferHardware && await SupportsAmf(ct);
        LastEncoder = useHardware ? "h264_amf" : "mpeg4";
        var start = BaseStart();
        Add(start, "-hide_banner", "-loglevel", "warning", "-nostdin", "-y", "-f", "image2pipe", "-framerate", request.FramesPerSecond.ToString(CultureInfo.InvariantCulture), "-vcodec", "png", "-i", "pipe:0");
        var hasVideo = !string.IsNullOrWhiteSpace(request.BackgroundVideoPath);
        if (hasVideo) Add(start, "-stream_loop", "-1", "-i", Path.GetFullPath(request.BackgroundVideoPath!));
        var hasAudio = !string.IsNullOrWhiteSpace(request.AudioPath);
        if (hasAudio) Add(start, "-i", Path.GetFullPath(request.AudioPath!));
        if (hasVideo)
        {
            var filter = $"[1:v]scale={request.Width}:{request.Height}:force_original_aspect_ratio=increase,crop={request.Width}:{request.Height},setsar=1[bg];[bg][0:v]overlay=shortest=1:format=auto[v]";
            Add(start, "-filter_complex", filter, "-map", "[v]");
        }
        else Add(start, "-map", "0:v:0");
        if (useHardware) Add(start, "-c:v", "h264_amf", "-quality", "balanced", "-b:v", "12M");
        else Add(start, "-c:v", "mpeg4", "-q:v", "3");
        Add(start, "-pix_fmt", "yuv420p", "-r", request.FramesPerSecond.ToString(CultureInfo.InvariantCulture));
        if (hasAudio) Add(start, "-map", $"{(hasVideo ? 2 : 1)}:a:0", "-af", "apad", "-c:a", "aac", "-b:a", "192k");
        else Add(start, "-an");
        Add(start, "-t", duration.ToString("0.########", CultureInfo.InvariantCulture), "-frames:v", frameCount.ToString(CultureInfo.InvariantCulture), "-movflags", "+faststart", partial);

        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("Não foi possível iniciar o FFmpeg.");
            using var cancel = ct.Register(() => Kill(process));
            var errorsTask = ReadTail(process.StandardError, 24000);
            try
            {
                for (var frame = 0; frame < frameCount; frame++)
                {
                    ct.ThrowIfCancellationRequested();
                    var time = frame / (double)request.FramesPerSecond;
                    var slide = Math.Min(request.SlideCount - 1, (int)Math.Floor(time / request.SecondsPerSlide));
                    var localTime = time - slide * request.SecondsPerSlide;
                    var bytes = await renderFramePng(slide, localTime, time, ct);
                    if (bytes.Length < 8 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71)
                        throw new InvalidDataException("O renderizador não retornou um quadro PNG válido.");
                    await process.StandardInput.BaseStream.WriteAsync(bytes, ct);
                    progress?.Report((frame + 1.0) / frameCount * .98);
                }
                await process.StandardInput.BaseStream.FlushAsync(ct);
                process.StandardInput.Close();
            }
            catch (IOException) when (!ct.IsCancellationRequested)
            {
                process.StandardInput.Close();
                await process.WaitForExitAsync(ct);
                throw new InvalidOperationException("Falha ao codificar o vídeo: " + await errorsTask);
            }
            await process.WaitForExitAsync(ct);
            var errors = await errorsTask;
            ct.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new InvalidOperationException("Falha ao exportar MP4: " + errors.Trim());
            if (!File.Exists(partial) || new FileInfo(partial).Length == 0) throw new InvalidDataException("O codificador não gerou o arquivo MP4.");
            File.Move(partial, output, overwrite: true);
            progress?.Report(1);
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        finally
        {
            Kill(process);
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (InvalidOperationException) { }
            catch (TimeoutException) { }
            // The existing destination survives failed/cancelled exports.
            try { if (File.Exists(partial)) File.Delete(partial); } catch (IOException) { }
        }
    }

    private static void Validate(VideoExportRequest request)
    {
        if (request.SlideCount is < 1 or > 10000) throw new ArgumentOutOfRangeException(nameof(request.SlideCount));
        if (!double.IsFinite(request.SecondsPerSlide) || request.SecondsPerSlide is < .25 or > 3600) throw new ArgumentOutOfRangeException(nameof(request.SecondsPerSlide));
        if (request.Width < 320 || request.Width > 7680 || request.Height < 180 || request.Height > 4320 || request.Width % 2 != 0 || request.Height % 2 != 0)
            throw new ArgumentException("Use dimensões pares entre 320×180 e 7680×4320.");
        if (request.FramesPerSecond is < 1 or > 60) throw new ArgumentOutOfRangeException(nameof(request.FramesPerSecond));
        if (request.SlideCount * request.SecondsPerSlide * request.FramesPerSecond > int.MaxValue) throw new ArgumentException("O vídeo é muito longo.");
        if (string.IsNullOrWhiteSpace(request.OutputPath) || !string.Equals(Path.GetExtension(request.OutputPath), ".mp4", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Escolha um arquivo de saída .mp4.");
        foreach (var path in new[] { request.BackgroundVideoPath, request.AudioPath }.Where(p => !string.IsNullOrWhiteSpace(p)))
            if (!File.Exists(path)) throw new FileNotFoundException("Mídia da exportação não encontrada.", path);
        var target = Path.GetFullPath(request.OutputPath);
        if (new[] { request.BackgroundVideoPath, request.AudioPath }.Any(p => !string.IsNullOrWhiteSpace(p) && string.Equals(Path.GetFullPath(p), target, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("O destino não pode substituir uma mídia usada na exportação.");
    }

    private async Task<bool> SupportsAmf(CancellationToken ct)
    {
        var start = BaseStart();
        start.RedirectStandardInput = false;
        Add(start, "-hide_banner", "-loglevel", "error", "-nostdin", "-f", "lavfi", "-i", "color=size=128x128:rate=1", "-frames:v", "1", "-c:v", "h264_amf", "-f", "null", "-");
        using var process = new Process { StartInfo = start };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            process.Start();
            using var cancel = timeout.Token.Register(() => Kill(process));
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            await error;
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
        catch (InvalidOperationException) { return false; }
        finally { Kill(process); }
    }

    private ProcessStartInfo BaseStart() => new(FfmpegPath)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardInput = true,
        RedirectStandardError = true,
        RedirectStandardOutput = false,
        WorkingDirectory = Path.GetDirectoryName(FfmpegPath)!
    };

    private static void Add(ProcessStartInfo start, params string[] values)
    { foreach (var value in values) start.ArgumentList.Add(value); }

    private static async Task<string> ReadTail(StreamReader reader, int maxLength)
    {
        var text = new StringBuilder();
        var buffer = new char[2048];
        int count;
        while ((count = await reader.ReadAsync(buffer)) != 0)
        {
            text.Append(buffer, 0, count);
            if (text.Length > maxLength) text.Remove(0, text.Length - maxLength);
        }
        return text.ToString();
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
