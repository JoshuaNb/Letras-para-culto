using System.Diagnostics;
using System.Text.Json;
using SkiaSharp;
using WorshipStudio.Services;
using Xunit;

namespace WorshipStudio.Tests;

public sealed class VideoExportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WorshipStudio-export-" + Guid.NewGuid().ToString("N"));
    private readonly string _ffmpeg = LocateFfmpeg();
    public VideoExportTests() => Directory.CreateDirectory(_directory);
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Fact]
    public async Task ExportProducesPlayableMp4WithExpectedDimensionsAndFrameCount()
    {
        var output = Path.Combine(_directory, "vídeo de louvor.mp4");
        var service = new VideoExportService(_ffmpeg);
        var calls = new List<int>();
        await service.ExportAsync(new VideoExportRequest(output, 2, .5, 320, 180, 8), (slide, local, total, ct) =>
        {
            calls.Add(slide);
            return Task.FromResult(Frame(slide == 0 ? SKColors.Teal : SKColors.Purple));
        });
        Assert.Equal(8, calls.Count);
        Assert.Equal(new[] { 0,0,0,0,1,1,1,1 }, calls);
        var probe = await Run(Path.Combine(Path.GetDirectoryName(_ffmpeg)!, "ffprobe.exe"), "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=codec_name,width,height,nb_frames", "-of", "json", output);
        using var doc = JsonDocument.Parse(probe);
        var stream = doc.RootElement.GetProperty("streams")[0];
        Assert.Equal("mpeg4", stream.GetProperty("codec_name").GetString());
        Assert.Equal(320, stream.GetProperty("width").GetInt32());
        Assert.Equal(180, stream.GetProperty("height").GetInt32());
        Assert.Equal("8", stream.GetProperty("nb_frames").GetString());
    }

    [Fact]
    public async Task VideoBackgroundIsActuallyCompositedBehindTransparentOverlay()
    {
        var source = Path.Combine(_directory, "fundo azul.mp4");
        await Run(_ffmpeg, "-v", "error", "-y", "-f", "lavfi", "-i", "color=c=blue:s=320x180:r=8:d=0.5", "-c:v", "mpeg4", source);
        var output = Path.Combine(_directory, "composite.mp4");
        var service = new VideoExportService(_ffmpeg);
        await service.ExportAsync(new VideoExportRequest(output, 1, .5, 320, 180, 8, source), (_,_,_,_) =>
        {
            using var bitmap = new SKBitmap(new SKImageInfo(320,180,SKColorType.Bgra8888,SKAlphaType.Premul));
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0,0,80,80,paint);
            using var image = SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png,100);
            return Task.FromResult(png.ToArray());
        });
        var frame = Path.Combine(_directory,"frame.png");
        await Run(_ffmpeg,"-v","error","-y","-i",output,"-frames:v","1",frame);
        using var decoded = SKBitmap.Decode(frame);
        var foreground = decoded.GetPixel(30,30);
        var background = decoded.GetPixel(270,130);
        Assert.True(foreground.Red > 200 && foreground.Blue < 70, $"Overlay missing: {foreground}");
        Assert.True(background.Blue > 200 && background.Red < 70, $"Video background missing: {background}");
    }

    [Fact]
    public async Task CancelledExportKeepsExistingFileAndRemovesPartialOutput()
    {
        var output = Path.Combine(_directory,"existing.mp4");
        var previous = new byte[] {1,2,3,4};
        File.WriteAllBytes(output,previous);
        using var cts = new CancellationTokenSource();
        var service = new VideoExportService(_ffmpeg);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExportAsync(new VideoExportRequest(output,1,2,320,180,8), (_,_,_,_) =>
        {
            cts.Cancel();
            return Task.FromResult(Frame(SKColors.Black));
        },ct:cts.Token));
        Assert.Equal(previous,File.ReadAllBytes(output));
        Assert.Single(Directory.GetFiles(_directory));
    }

    private static byte[] Frame(SKColor color)
    {
        using var bitmap = new SKBitmap(320,180);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png,100);
        return data.ToArray();
    }

    private static string LocateFfmpeg()
    {
        var configured = Environment.GetEnvironmentVariable("WORSHIP_FFMPEG");
        if(!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var packaged = Path.Combine(AppContext.BaseDirectory,"tools","ffmpeg","ffmpeg.exe");
        if(File.Exists(packaged)) return packaged;
        var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","tools","ffmpeg","ffmpeg.exe"));
        if(File.Exists(source)) return source;
        throw new FileNotFoundException("Bundled FFmpeg required for actual MP4 verification",packaged);
    }

    private static async Task<string> Run(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true };
        foreach(var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start media tool");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if(!process.HasExited)process.Kill(true);throw; }
        Assert.True(process.ExitCode==0,await errors);
        return await output;
    }
}
