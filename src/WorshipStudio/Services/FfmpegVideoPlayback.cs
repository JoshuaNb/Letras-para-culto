using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace WorshipStudio.Services;

public enum VideoPlaybackQuality { Auto, Compatible }
public enum VideoPlaybackState { Starting, Playing, Recovering, Failed, Stopped }
public sealed record VideoPlaybackDiagnostic(VideoPlaybackState State, string Message, VideoPlaybackQuality Quality, long PlaybackId = 0);

public sealed record VideoPlaybackSettings(int Width, int Height, int FramesPerSecond, TimeSpan StartupTimeout, TimeSpan FrameTimeout)
{
    public static VideoPlaybackSettings ForQuality(VideoPlaybackQuality quality) => quality == VideoPlaybackQuality.Compatible
        ? new(854, 480, 24, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5))
        : new(1280, 720, 30, TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(4));

    public void Validate()
    {
        if (Width is < 2 or > 1920 || Height is < 2 or > 1080 || FramesPerSecond is < 1 or > 60 ||
            StartupTimeout <= TimeSpan.Zero || FrameTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Width), "Configuração de vídeo fora dos limites seguros.");
    }
}

/// <summary>One bounded, pooled BGRA frame. Ownership moves to the consumer until Dispose.</summary>
public sealed class DecodedVideoFrame : IDisposable
{
    private byte[]? _buffer;
    internal DecodedVideoFrame(byte[] buffer, int width, int height) { _buffer = buffer; Width = width; Height = height; }
    public int Width { get; }
    public int Height { get; }
    public int Stride => Width * 4;
    public ReadOnlyMemory<byte> Pixels => (_buffer ?? throw new ObjectDisposedException(nameof(DecodedVideoFrame))).AsMemory(0, Stride * Height);
    internal byte[] Buffer => _buffer ?? throw new ObjectDisposedException(nameof(DecodedVideoFrame));
    // A timed-out pipe read may still own this memory. Do not return it to the shared pool.
    internal void DiscardIncompleteBuffer() => Interlocked.Exchange(ref _buffer, null);
    public void Dispose() { var buffer = Interlocked.Exchange(ref _buffer, null); if (buffer != null) ArrayPool<byte>.Shared.Return(buffer); }
}

/// <summary>Read a complete frame with an absolute deadline, including streams that ignore cancellation.</summary>
public static class VideoPipeReader
{
    public static async Task ReadFrameAsync(Stream source, Memory<byte> destination, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var offset = 0;
        try
        {
            while (offset < destination.Length)
            {
                var read = source.ReadAsync(destination[offset..], deadline.Token).AsTask();
                int count;
                try { count = await read.WaitAsync(deadline.Token).ConfigureAwait(false); }
                catch
                {
                    // Observe eventual failures when an OS pipe ignores cancellation. Its owner kills the process.
                    _ = read.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    throw;
                }
                if (count == 0) throw new EndOfStreamException(offset == 0 ? "O decodificador encerrou o vídeo." : "O decodificador entregou um quadro incompleto.");
                offset += count;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("O vídeo deixou de entregar quadros dentro do prazo."); }
    }
}

/// <summary>
/// Software decoding in an isolated bundled FFmpeg process. No native video HWND, transparent
/// window, in-process decoder or graphics-driver API is needed. Only the newest frame is retained.
/// </summary>
public sealed class FfmpegVideoPlayback : IDisposable
{
    private readonly object _gate = new();
    private readonly VideoPlaybackSettings? _settingsOverride;
    private Session? _session;
    private DecodedVideoFrame? _latest;
    private bool _disposed;
    private long _framesDecoded;
    private int _playing;
    private long _sequence;
    private sealed class Session(string path, VideoPlaybackQuality quality, long id)
    {
        public string Path { get; } = path;
        public VideoPlaybackQuality Quality { get; } = quality;
        public long Id { get; } = id;
        public bool Finished { get; set; }
        public CancellationTokenSource Cancellation { get; } = new();
        public Task Completion { get; set; } = Task.CompletedTask;
    }

    public string FfmpegPath { get; }
    public bool IsPlaying => Volatile.Read(ref _playing) != 0;
    public long FramesDecoded => Interlocked.Read(ref _framesDecoded);
    public Task Completion { get { lock (_gate) return _session?.Completion ?? Task.CompletedTask; } }
    public event Action? FrameAvailable;
    public event Action<VideoPlaybackDiagnostic>? Diagnostic;

    public FfmpegVideoPlayback(string? ffmpegPath = null, VideoPlaybackSettings? settings = null)
    {
        settings?.Validate();
        _settingsOverride = settings;
        var configured = Environment.GetEnvironmentVariable("WORSHIP_FFMPEG");
        FfmpegPath = ffmpegPath ?? (!string.IsNullOrWhiteSpace(configured) ? configured : null) ?? new[] {
            Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe")
        }.FirstOrDefault(File.Exists) ?? Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe");
    }

    public long Play(string path, VideoPlaybackQuality quality = VideoPlaybackQuality.Auto)
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FfmpegVideoPlayback));
            CancelCurrent();
            var session = new Session(path, quality, ++_sequence);
            _session = session;
            session.Completion = Task.Run(() => RunAsync(session));
            return session.Id;
        }
    }

    public DecodedVideoFrame? TakeLatestFrame()
    {
        lock (_gate) { var result = _latest; _latest = null; return result; }
    }

    public void Stop()
    {
        lock (_gate) { CancelCurrent(); }
    }

    private void CancelCurrent()
    {
        if (_session != null)
        {
            _session.Cancellation.Cancel();
            if (_session.Finished) _session.Cancellation.Dispose();
        }
        _session = null;
        Volatile.Write(ref _playing, 0);
        _latest?.Dispose(); _latest = null;
    }

    private bool IsCurrent(Session session) { lock (_gate) return !_disposed && ReferenceEquals(_session, session); }

    private async Task RunAsync(Session session)
    {
        var quality = session.Quality;
        try
        {
            if (!File.Exists(session.Path)) throw new FileNotFoundException("Arquivo de vídeo não encontrado. Escolha novamente o fundo ou restaure a mídia local.");
            if (!File.Exists(FfmpegPath)) throw new FileNotFoundException("Decodificador de vídeo não encontrado no pacote. Reinstale o Worship Studio.");
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    Report(session, VideoPlaybackState.Starting, "Preparando vídeo local com decodificação por CPU…", quality);
                    await DecodeAsync(session, quality).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException) when (session.Cancellation.IsCancellationRequested) { return; }
                catch (Exception ex) when (attempt == 0 && quality == VideoPlaybackQuality.Auto && IsCurrent(session))
                {
                    quality = VideoPlaybackQuality.Compatible;
                    ClearFailedFrames(session);
                    Report(session, VideoPlaybackState.Recovering,
                        "Vídeo interrompido; tentando modo compatível por CPU. " + ex.Message, quality);
                }
            }
        }
        catch (OperationCanceledException) when (session.Cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            ClearFailedFrames(session);
            Report(session, VideoPlaybackState.Failed,
                "Não foi possível reproduzir o vídeo. O fundo animado de segurança e as letras continuam disponíveis. " + ex.Message, quality);
        }
        finally
        {
            lock (_gate)
            {
                // A completed session stays identifiable until Stop/Play, but its token must remain cancellable.
                session.Finished = true;
                if (!ReferenceEquals(_session, session)) session.Cancellation.Dispose();
            }
        }
    }

    private async Task DecodeAsync(Session session, VideoPlaybackQuality quality)
    {
        var settings = _settingsOverride ?? VideoPlaybackSettings.ForQuality(quality);
        settings.Validate();
        var start = new ProcessStartInfo(FfmpegPath) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        var filter = FormattableString.Invariant($"scale={settings.Width}:{settings.Height}:force_original_aspect_ratio=increase,crop={settings.Width}:{settings.Height},setsar=1,fps={settings.FramesPerSecond}");
        foreach (var arg in new[] { "-nostdin", "-hide_banner", "-loglevel", "error", "-threads", "2", "-hwaccel", "none",
            "-protocol_whitelist", "file,pipe", "-stream_loop", "-1", "-re", "-i", Path.GetFullPath(session.Path),
            "-map", "0:v:0", "-an", "-sn", "-dn", "-vf", filter, "-threads", "1", "-pix_fmt", "bgra", "-c:v", "rawvideo",
            "-f", "image2", "-update", "1", "pipe:1" }) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        var errors = new StringBuilder();
        Task? drain = null;
        try
        {
            session.Cancellation.Token.ThrowIfCancellationRequested();
            if (!process.Start()) throw new InvalidOperationException("O decodificador não iniciou.");
            drain = DrainErrorsAsync(process.StandardError, errors);
            var first = true;
            while (true)
            {
                session.Cancellation.Token.ThrowIfCancellationRequested();
                var buffer = ArrayPool<byte>.Shared.Rent(checked(settings.Width * settings.Height * 4));
                DecodedVideoFrame? frame = new(buffer, settings.Width, settings.Height);
                try
                {
                    try
                    {
                        await VideoPipeReader.ReadFrameAsync(process.StandardOutput.BaseStream,
                            buffer.AsMemory(0, frame.Stride * frame.Height), first ? settings.StartupTimeout : settings.FrameTimeout,
                            session.Cancellation.Token).ConfigureAwait(false);
                    }
                    catch { frame.DiscardIncompleteBuffer(); throw; }
                    lock (_gate)
                    {
                        if (!ReferenceEquals(_session, session) || _disposed) return;
                        _latest?.Dispose(); _latest = frame; frame = null;
                        Interlocked.Increment(ref _framesDecoded);
                        Volatile.Write(ref _playing, 1);
                    }
                    if (first)
                    {
                        Report(session, VideoPlaybackState.Playing,
                            $"Vídeo ativo por CPU • {settings.Width} × {settings.Height} • {settings.FramesPerSecond} fps" +
                            (quality == VideoPlaybackQuality.Compatible ? " • modo compatível" : ""), quality);
                        first = false;
                    }
                    try { FrameAvailable?.Invoke(); } catch { /* A UI subscriber cannot terminate decoding. */ }
                }
                finally { frame?.Dispose(); }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            lock (errors)
                throw new IOException(ex.Message + (errors.Length == 0 ? "" : " " + errors.ToString().Trim()), ex);
        }
        finally
        {
            try { if (process.Id > 0 && !process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            try { if (process.Id > 0) await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); } catch { }
            if (drain != null)
            {
                try { await drain.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); }
                catch
                {
                    _ = drain.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
            }
        }
    }

    private void ClearFailedFrames(Session session)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_session, session)) return;
            Volatile.Write(ref _playing, 0);
            _latest?.Dispose(); _latest = null;
        }
    }

    private static async Task DrainErrorsAsync(StreamReader reader, StringBuilder errors)
    {
        var chars = new char[512];
        while (true)
        {
            var count = await reader.ReadAsync(chars, 0, chars.Length).ConfigureAwait(false);
            if (count == 0) return;
            lock (errors) { if (errors.Length < 4096) errors.Append(chars, 0, Math.Min(count, 4096 - errors.Length)); }
        }
    }

    private void Report(Session session, VideoPlaybackState state, string message, VideoPlaybackQuality quality)
    {
        if (!IsCurrent(session)) return;
        try { Diagnostic?.Invoke(new(state, message, quality, session.Id)); } catch { }
    }

    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; CancelCurrent(); }
    }
}
