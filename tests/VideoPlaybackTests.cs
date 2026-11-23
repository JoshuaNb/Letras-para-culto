using System.Collections.Concurrent;
using System.Diagnostics;
using WorshipStudio.Services;
using Xunit;

namespace WorshipStudio.Tests;

public sealed class VideoPlaybackTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WorshipStudio-playback-" + Guid.NewGuid().ToString("N"));
    private readonly string _ffmpeg = LocateFfmpeg();
    private static VideoPlaybackSettings Small => new(64, 36, 12, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(3));
    public VideoPlaybackTests() => Directory.CreateDirectory(_directory);
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Theory]
    [InlineData(VideoPlaybackQuality.Auto, 1280, 720, 30)]
    [InlineData(VideoPlaybackQuality.Compatible, 854, 480, 24)]
    public void QualityHasBoundedSoftwareFrameSize(VideoPlaybackQuality quality, int width, int height, int fps)
    {
        var settings = VideoPlaybackSettings.ForQuality(quality);
        settings.Validate();
        Assert.Equal((width, height, fps), (settings.Width, settings.Height, settings.FramesPerSecond));
    }

    [Fact]
    public async Task FragmentedPipeReadsProduceExactCompleteFrame()
    {
        var expected = Enumerable.Range(0, 112).Select(i => (byte)i).ToArray();
        using var source = new FragmentedStream(expected);
        var actual = new byte[expected.Length];
        await VideoPipeReader.ReadFrameAsync(source, actual, TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task TruncatedPipeNeverPublishesPartialFrame()
    {
        using var source = new MemoryStream(new byte[17]);
        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            VideoPipeReader.ReadFrameAsync(source, new byte[32], TimeSpan.FromSeconds(1), CancellationToken.None));
    }

    [Fact]
    public async Task FrameWatchdogReturnsEvenWhenPipeIgnoresCancellation()
    {
        using var source = new UnresponsiveStream();
        var clock = Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(() =>
            VideoPipeReader.ReadFrameAsync(source, new byte[32], TimeSpan.FromMilliseconds(80), CancellationToken.None));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2));
        source.EndWithFailure();
    }

    [Fact]
    public async Task UserCancellationIsNotReportedAsVideoTimeout()
    {
        using var source = new UnresponsiveStream();
        using var cancellation = new CancellationTokenSource(80);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            VideoPipeReader.ReadFrameAsync(source, new byte[32], TimeSpan.FromSeconds(10), cancellation.Token));
        source.EndWithFailure();
    }

    [Theory]
    [InlineData(VideoPlaybackQuality.Auto)]
    [InlineData(VideoPlaybackQuality.Compatible)]
    public async Task BundledDecoderActuallyDecodesBgraAndLoopsWithoutGpu(VideoPlaybackQuality quality)
    {
        var source = await CreateVideo("azul.mp4", "blue");
        using var playback = new FfmpegVideoPlayback(_ffmpeg, Small);
        var diagnostics = new ConcurrentQueue<VideoPlaybackDiagnostic>();
        playback.Diagnostic += diagnostics.Enqueue;
        playback.Play(source, quality);
        await Until(() => playback.FramesDecoded >= 13); // The input has only six frames: this crosses two EOF boundaries.
        Assert.True(playback.IsPlaying);
        using var latest = playback.TakeLatestFrame();
        Assert.NotNull(latest);
        Assert.Equal((64, 36, 64 * 4), (latest.Width, latest.Height, latest.Stride));
        AssertBlue(latest.Pixels.Span);
        Assert.Null(playback.TakeLatestFrame()); // Old frames never accumulate behind a slow UI.
        Assert.Single(diagnostics, d => d.State == VideoPlaybackState.Playing);
        Assert.DoesNotContain(diagnostics, d => d.State is VideoPlaybackState.Failed or VideoPlaybackState.Recovering);
        var completion = playback.Completion;
        playback.Stop();
        await completion.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.False(playback.IsPlaying);
        Assert.Null(playback.TakeLatestFrame());
    }

    [Fact]
    public async Task ChangingSourceDiscardsOldFramesAndStopsOldDecoder()
    {
        var blue = await CreateVideo("primeiro azul.mp4", "blue");
        var red = await CreateVideo("segundo vermelho.mp4", "red");
        using var playback = new FfmpegVideoPlayback(_ffmpeg, Small);
        playback.Play(blue);
        await Until(() => playback.IsPlaying);
        var previous = playback.Completion;
        playback.Play(red, VideoPlaybackQuality.Compatible);
        await previous.WaitAsync(TimeSpan.FromSeconds(4));
        await Until(() => playback.IsPlaying);
        using var latest = playback.TakeLatestFrame();
        Assert.NotNull(latest);
        var pixel = latest.Pixels.Span;
        Assert.True(pixel[2] > 200 && pixel[0] < 70 && pixel[1] < 70, "The replaced blue source leaked into the red decoder session.");
        var completion = playback.Completion;
        playback.Stop();
        await completion.WaitAsync(TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task InvalidVideoRetriesCompatibleOnceThenLeavesStableFailure()
    {
        var source = Path.Combine(_directory, "corrompido.mp4");
        await File.WriteAllTextAsync(source, "arquivo sem vídeo");
        using var playback = new FfmpegVideoPlayback(_ffmpeg, Small);
        var diagnostics = new ConcurrentQueue<VideoPlaybackDiagnostic>();
        playback.Diagnostic += diagnostics.Enqueue;
        playback.Play(source);
        await playback.Completion.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.Equal(new[] { VideoPlaybackState.Starting, VideoPlaybackState.Recovering, VideoPlaybackState.Starting, VideoPlaybackState.Failed }, diagnostics.Select(d => d.State));
        Assert.Equal(VideoPlaybackQuality.Compatible, diagnostics.Last().Quality);
        Assert.False(playback.IsPlaying);
        Assert.Equal(0, playback.FramesDecoded);
        Assert.Null(playback.TakeLatestFrame());
        await Task.Delay(100);
        Assert.Equal(4, diagnostics.Count);
    }

    [Fact]
    public async Task ExplicitCompatibleModeDoesNotRetryBrokenInput()
    {
        var source = Path.Combine(_directory, "inválido.mp4");
        await File.WriteAllTextAsync(source, "inválido");
        using var playback = new FfmpegVideoPlayback(_ffmpeg, Small);
        var diagnostics = new ConcurrentQueue<VideoPlaybackDiagnostic>();
        playback.Diagnostic += diagnostics.Enqueue;
        playback.Play(source, VideoPlaybackQuality.Compatible);
        await playback.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { VideoPlaybackState.Starting, VideoPlaybackState.Failed }, diagnostics.Select(d => d.State));
    }

    [Fact]
    public async Task MissingFileReportsActionableErrorWithoutStartingNativeDecoder()
    {
        using var playback = new FfmpegVideoPlayback(_ffmpeg, Small);
        var result = new TaskCompletionSource<VideoPlaybackDiagnostic>(TaskCreationOptions.RunContinuationsAsynchronously);
        playback.Diagnostic += diagnostic => result.TrySetResult(diagnostic);
        playback.Play(Path.Combine(_directory, "ausente.mp4"));
        var diagnostic = await result.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(VideoPlaybackState.Failed, diagnostic.State);
        Assert.Contains("Arquivo de vídeo não encontrado", diagnostic.Message);
        Assert.Equal(0, playback.FramesDecoded);
    }

    [Fact]
    public async Task MissingPackagedDecoderReportsFailureWithoutCrashing()
    {
        var source = Path.Combine(_directory, "existente.mp4");
        await File.WriteAllBytesAsync(source, [1, 2, 3]);
        using var playback = new FfmpegVideoPlayback(Path.Combine(_directory, "sem-decoder.exe"), Small);
        var result = new TaskCompletionSource<VideoPlaybackDiagnostic>(TaskCreationOptions.RunContinuationsAsynchronously);
        playback.Diagnostic += diagnostic => result.TrySetResult(diagnostic);
        playback.Play(source);
        var diagnostic = await result.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(VideoPlaybackState.Failed, diagnostic.State);
        Assert.Contains("Decodificador de vídeo não encontrado", diagnostic.Message);
    }

    [Fact]
    public void DisposedPlaybackCannotStartNewNativeProcess()
    {
        var playback = new FfmpegVideoPlayback(_ffmpeg, Small);
        playback.Dispose();
        playback.Stop();
        Assert.Throws<ObjectDisposedException>(() => playback.Play("qualquer.mp4"));
    }

    private async Task<string> CreateVideo(string filename, string color)
    {
        var path = Path.Combine(_directory, filename);
        var start = new ProcessStartInfo(_ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-nostdin", "-v", "error", "-y", "-f", "lavfi", "-i", $"color=c={color}:s=64x36:r=12:d=0.5", "-c:v", "mpeg4", path })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("FFmpeg não iniciou.");
        var errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(process.ExitCode == 0, await errors);
        return path;
    }

    private static void AssertBlue(ReadOnlySpan<byte> data) => Assert.True(data[0] > 200 && data[1] < 70 && data[2] < 70 && data[3] == 255, "CPU frame did not contain the decoded blue BGRA background.");

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition()) await Task.Delay(20, timeout.Token);
    }

    private static string LocateFfmpeg()
    {
        var configured = Environment.GetEnvironmentVariable("WORSHIP_FFMPEG");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var packaged = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe");
        if (File.Exists(packaged)) return packaged;
        throw new FileNotFoundException("Bundled FFmpeg is required to verify real background decoding.", packaged);
    }

    private sealed class FragmentedStream(byte[] data) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(3, buffer.Length)], cancellationToken);
    }

    private sealed class UnresponsiveStream : Stream
    {
        private readonly TaskCompletionSource<int> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void EndWithFailure() => _completion.TrySetException(new IOException("late pipe failure"));
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(_completion.Task);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
