using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace WorshipStudio.Services;

/// <summary>Local WASAPI capture. Events are raised on an audio/timer thread.</summary>
public sealed class AudioReactiveService : IDisposable
{
    private WasapiCapture? _capture;
    private readonly object _gate = new();
    private readonly Timer _envelopeTimer;
    private double _target, _level;
    private long _lastInput;
    public bool IsRunning => _capture != null;
    public double Level { get { lock (_gate) return _level; } }
    public double Gain { get; set; } = 4;
    public event Action<double>? LevelChanged;
    public event Action<string>? Error;

    public AudioReactiveService()
    {
        _envelopeTimer = new Timer(_ => Tick(), null, 0, 33);
    }

    /// <param name="microphone">False captures the system playback mix; true captures the default microphone.</param>
    public void Start(bool microphone = false)
    {
        Stop();
        try
        {
            _capture = microphone ? new WasapiCapture() : new WasapiLoopbackCapture();
            _capture.DataAvailable += OnData;
            _capture.RecordingStopped += OnStopped;
            _capture.StartRecording();
        }
        catch (Exception ex)
        {
            Stop();
            ReportError("Não foi possível abrir o áudio: " + ex.Message);
            throw;
        }
    }

    public void Stop()
    {
        var capture = Interlocked.Exchange(ref _capture, null);
        if (capture != null)
        {
            capture.DataAvailable -= OnData;
            capture.RecordingStopped -= OnStopped;
            try { capture.StopRecording(); } catch { /* The audio device may already have disconnected. */ }
            capture.Dispose();
        }
        lock (_gate) _target = 0;
    }

    public void Tap()
    {
        lock (_gate)
        {
            _target = 1;
            _lastInput = Environment.TickCount64;
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (sender is not WasapiCapture capture || e.BytesRecorded <= 0) return;
        var format = capture.WaveFormat;
        var floatSamples = format.Encoding == WaveFormatEncoding.IeeeFloat ||
            format is WaveFormatExtensible ext && ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
        var bytesPerSample = Math.Max(1, format.BitsPerSample / 8);
        double sum = 0;
        var samples = e.BytesRecorded / bytesPerSample;
        for (var i = 0; i < samples; i++)
        {
            int offset = i * bytesPerSample;
            double sample = floatSamples && bytesPerSample == 4 ? BitConverter.ToSingle(e.Buffer, offset) :
                bytesPerSample == 2 ? BitConverter.ToInt16(e.Buffer, offset) / 32768.0 :
                bytesPerSample == 3 ? ((e.Buffer[offset] | e.Buffer[offset + 1] << 8 | e.Buffer[offset + 2] << 16) << 8) / 2147483648.0 :
                bytesPerSample == 4 ? BitConverter.ToInt32(e.Buffer, offset) / 2147483648.0 :
                (e.Buffer[offset] - 128) / 128.0;
            if (double.IsFinite(sample)) sum += sample * sample;
        }
        lock (_gate)
        {
            _target = Math.Clamp(Math.Sqrt(sum / Math.Max(1, samples)) * Gain, 0, 1);
            _lastInput = Environment.TickCount64;
        }
    }

    private void Tick()
    {
        double value;
        lock (_gate)
        {
            // WASAPI loopback emits no packets while playback is idle.
            if (Environment.TickCount64 - _lastInput > 120) _target *= .82;
            _level += (_target - _level) * (_target > _level ? .7 : .18);
            value = _level;
        }
        foreach (var handler in LevelChanged?.GetInvocationList() ?? [])
            try { ((Action<double>)handler)(value); } catch { /* Never terminate the audio thread because a UI subscriber closed. */ }
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null) ReportError("Captura de áudio interrompida: " + e.Exception.Message);
        Stop();
    }

    private void ReportError(string message)
    {
        foreach (var handler in Error?.GetInvocationList() ?? [])
            try { ((Action<string>)handler)(message); } catch { }
    }

    public void Dispose()
    {
        Stop();
        _envelopeTimer.Dispose();
    }
}
