using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using WorshipStudio.Models;
using WorshipStudio.Services;

namespace WorshipStudio.Controls;

/// <summary>Video, effects and lyrics share one Avalonia composition, without native HWND/transparent overlays.</summary>
public sealed class StageSurface : UserControl, IDisposable
{
    private readonly StageRenderer _renderer;
    private readonly Image _videoImage;
    private readonly FfmpegVideoPlayback _video;
    private WriteableBitmap? _bitmap;
    private string? _videoPath;
    private bool _videoFailed;
    private bool _readyForVideo;
    private Window? _ownerWindow;
    private bool _disposed;
    private int _framePosted;
    private long _playbackId;
    private VideoPlaybackQuality _quality;
    private StageFrame _frame = new();

    public StageRenderer Renderer => _renderer;
    public bool IsVideoPlaying => _video.IsPlaying && _videoImage.IsVisible;
    public long VideoFramesDecoded => _video.FramesDecoded;
    public VideoPlaybackDiagnostic? LastPlaybackDiagnostic { get; private set; }
    public event Action<string>? Error;
    public event Action<VideoPlaybackDiagnostic>? PlaybackDiagnostic;

    public VideoPlaybackQuality VideoQuality
    {
        get => _quality;
        set { if (_quality == value) return; _quality = value; RestartVideo(); }
    }

    public StageSurface() : this(new FfmpegVideoPlayback()) { }

    public StageSurface(FfmpegVideoPlayback video)
    {
        _video = video;
        ClipToBounds = true;
        Background = Brushes.Black;
        _renderer = new StageRenderer();
        _renderer.Error += ReportError;
        _videoImage = new Image { Stretch = Stretch.UniformToFill, IsVisible = false, IsHitTestVisible = false };
        var host = new Grid { Background = Brushes.Black, ClipToBounds = true };
        host.Children.Add(_videoImage);
        host.Children.Add(_renderer);
        Content = host;
        _video.FrameAvailable += OnFrameAvailable;
        _video.Diagnostic += OnPlaybackDiagnostic;
        AttachedToVisualTree += (_, _) =>
        {
            _ownerWindow = TopLevel.GetTopLevel(this) as Window;
            if (_ownerWindow == null) return;
            _ownerWindow.Opened += OnOwnerOpened;
            if (_ownerWindow.IsVisible) OnOwnerOpened(_ownerWindow, EventArgs.Empty);
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (_ownerWindow != null) _ownerWindow.Opened -= OnOwnerOpened;
            _ownerWindow = null;
            _readyForVideo = false;
            StopVideo();
        };
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && !_disposed) SetFrame(_frame);
        };
    }

    private void OnOwnerOpened(object? sender, EventArgs e)
    {
        _readyForVideo = true;
        Dispatcher.UIThread.Post(() => { if (!_disposed && _readyForVideo) SetFrame(_frame); }, DispatcherPriority.Loaded);
    }

    public void SetFrame(StageFrame frame)
    {
        if (_disposed) return;
        _frame = frame;
        _renderer.SetFrame(frame);
        var path = frame.Style.BackgroundPath;
        var shouldPlay = frame.Style.BackgroundMode == "Video" && !string.IsNullOrWhiteSpace(path) &&
            !frame.Blackout && _readyForVideo && IsEffectivelyVisible;
        if (!shouldPlay)
        {
            StopVideo();
            return;
        }
        if (!string.Equals(_videoPath, path, StringComparison.OrdinalIgnoreCase))
        {
            StopVideo();
            _videoPath = path;
            _videoFailed = false;
            _playbackId = _video.Play(path!, _quality);
        }
        if (_videoFailed) ShowFallback();
    }

    private void OnFrameAvailable()
    {
        if (_disposed || Interlocked.Exchange(ref _framePosted, 1) != 0) return;
        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _framePosted, 0);
            if (_disposed) return;
            using var frame = _video.TakeLatestFrame();
            if (frame == null || _frame.Blackout || _frame.Style.BackgroundMode != "Video" || !_readyForVideo || !IsEffectivelyVisible) return;
            try
            {
                if (_bitmap == null || _bitmap.PixelSize.Width != frame.Width || _bitmap.PixelSize.Height != frame.Height)
                {
                    _videoImage.Source = null;
                    _bitmap?.Dispose();
                    _bitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
                }
                using (var target = _bitmap.Lock())
                {
                    if (target.RowBytes == frame.Stride)
                        Marshal.Copy(frame.Buffer, 0, target.Address, frame.Stride * frame.Height);
                    else
                        for (var row = 0; row < frame.Height; row++)
                            Marshal.Copy(frame.Buffer, row * frame.Stride, IntPtr.Add(target.Address, row * target.RowBytes), frame.Stride);
                }
                _videoImage.Source = _bitmap;
                _videoImage.IsVisible = true;
                _renderer.VideoOverlay = true;
                _renderer.InvalidateVisual();
                _videoImage.InvalidateVisual();
            }
            catch (Exception ex)
            {
                _video.Stop();
                _videoFailed = true;
                ShowFallback();
                ReportError("Falha ao apresentar o quadro do vídeo. As letras continuam disponíveis. " + ex.Message);
            }
        }, DispatcherPriority.Render);
    }

    private void OnPlaybackDiagnostic(VideoPlaybackDiagnostic diagnostic)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || _videoPath == null || diagnostic.PlaybackId != _playbackId) return;
            LastPlaybackDiagnostic = diagnostic;
            if (diagnostic.State is VideoPlaybackState.Failed or VideoPlaybackState.Recovering)
            {
                _videoFailed = diagnostic.State == VideoPlaybackState.Failed;
                ShowFallback();
            }
            PlaybackDiagnostic?.Invoke(diagnostic);
            if (diagnostic.State == VideoPlaybackState.Failed) ReportError(diagnostic.Message);
        });
    }

    private void StopVideo()
    {
        var hadVideo = _videoPath != null;
        _video.Stop();
        _playbackId = 0;
        _videoPath = null;
        _videoFailed = false;
        ShowFallback();
        if (hadVideo && !_disposed)
        {
            LastPlaybackDiagnostic = new(VideoPlaybackState.Stopped, "Vídeo interrompido; projeção preservada.", _quality);
            PlaybackDiagnostic?.Invoke(LastPlaybackDiagnostic);
        }
    }

    private void ShowFallback()
    {
        _videoImage.IsVisible = false;
        _renderer.VideoOverlay = false;
        _renderer.InvalidateVisual();
    }

    public void RestartVideo()
    {
        if (_disposed) return;
        StopVideo();
        SetFrame(_frame);
    }

    public void ReplayTransition() => _renderer.ReplayTransition();
    private void ReportError(string message) => Error?.Invoke(message);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownerWindow != null) _ownerWindow.Opened -= OnOwnerOpened;
        _video.FrameAvailable -= OnFrameAvailable;
        _video.Diagnostic -= OnPlaybackDiagnostic;
        _video.Dispose();
        _videoImage.Source = null;
        _bitmap?.Dispose(); _bitmap = null;
        _renderer.Dispose();
    }
}
