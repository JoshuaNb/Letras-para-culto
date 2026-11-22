using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using WorshipStudio.Models;

namespace WorshipStudio.Controls;

/// <summary>
/// The real output surface. Native Direct3D video is combined with a transparent
/// Avalonia overlay because native video windows have their own Win32 airspace.
/// </summary>
public sealed class StageSurface : UserControl, IDisposable
{
    private readonly Grid _host;
    private readonly StageRenderer _renderer;
    private readonly StageRenderer _videoOverlay;
    private readonly DispatcherTimer _videoStartTimer;
    private VideoView? _videoView;
    private LibVLC? _vlc;
    private MediaPlayer? _player;
    private Media? _media;
    private string? _videoPath;
    private string? _attemptedVideoPath;
    private bool _videoFailed;
    private bool _initializationFailed;
    private bool _readyForVideo;
    private Window? _ownerWindow;
    private string? _pendingVideoPath;
    private long _pendingVideoStarted;
    private bool _disposed;
    private StageFrame _frame = new();

    public StageRenderer Renderer => _renderer;
    public bool IsVideoPlaying => _player?.IsPlaying == true;
    public event Action<string>? Error;

    public StageSurface()
    {
        ClipToBounds = true;
        Background = Brushes.Black;
        _renderer = new StageRenderer();
        _videoOverlay = new StageRenderer { VideoOverlay = true };
        _videoStartTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(25), DispatcherPriority.Loaded, (_, _) => StartPendingVideo());
        _videoStartTimer.Stop();
        _renderer.Error += ReportError;
        _videoOverlay.Error += ReportError;
        _host = new Grid { Background = Brushes.Black, ClipToBounds = true };
        _host.Children.Add(_renderer);
        Content = _host;
        SizeChanged += (_, _) => UpdateVideoCrop();
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
            ClearPendingVideo();
            ShowFallback();
            if (!_disposed && _player?.IsPlaying == true) _player.SetPause(true);
        };
    }

    private void OnOwnerOpened(object? sender, EventArgs e)
    {
        // Persisted video settings may be applied while MainWindow is still being
        // constructed. Native media requires the real, visible Win32 host first.
        _readyForVideo = true;
        Dispatcher.UIThread.Post(() => { if (!_disposed && _readyForVideo) SetFrame(_frame); }, DispatcherPriority.Loaded);
    }

    public void SetFrame(StageFrame frame)
    {
        if (_disposed) return;
        _frame = frame;
        _renderer.SetFrame(frame);
        _videoOverlay.SetFrame(frame);
        var video = frame.Style.BackgroundMode == "Video" && !string.IsNullOrWhiteSpace(frame.Style.BackgroundPath);
        if (video && !frame.Blackout)
        {
            var path = frame.Style.BackgroundPath!;
            if (!string.Equals(_attemptedVideoPath, path, StringComparison.OrdinalIgnoreCase))
            {
                _attemptedVideoPath = path;
                _videoFailed = false;
                _initializationFailed = false;
            }
            if (!_videoFailed && !File.Exists(path)) FailVideo("Arquivo de vídeo não encontrado. Escolha novamente o fundo ou restaure a mídia local.");
            if (!_videoFailed && (!_readyForVideo || !IsEffectivelyVisible))
            {
                ShowFallback();
                return;
            }
            if (!_videoFailed && EnsureVideo())
            {
                // NativeControlHost creates/attaches its HWND after becoming visible.
                // Calling libvlc_media_player_play before that creates a separate VLC window.
                _videoView!.IsVisible = true;
                if (!string.Equals(_videoPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    if (HasVideoHost()) PlayVideo(path);
                    else QueueVideoStart(path);
                }
                if (!_videoFailed)
                {
                    _renderer.IsVisible = false;
                    if (_player?.State == VLCState.Paused) _player.SetPause(false);
                }
            }
            if (_videoFailed) ShowFallback();
        }
        else
        {
            ClearPendingVideo();
            ShowFallback();
            // Blackout never depends on the native transparent-window overlay:
            // hide the native video HWND and show the opaque black canvas.
            if (!video)
            {
                if (_player?.IsPlaying == true) _player.SetPause(true);
                _videoPath = null;
                _attemptedVideoPath = null;
                _videoFailed = false;
                _initializationFailed = false;
            }
        }
    }

    private bool EnsureVideo()
    {
        if (_videoView != null) return true;
        if (_initializationFailed) return false;
        try
        {
            Core.Initialize();
            _vlc = new LibVLC("--quiet", "--no-video-title-show", "--avcodec-hw=any");
            _player = new MediaPlayer(_vlc) { Mute = true, EnableMouseInput = false, EnableKeyInput = false };
            _player.Playing += (_, _) => Dispatcher.UIThread.Post(UpdateVideoCrop);
            _player.EncounteredError += (_, _) => Dispatcher.UIThread.Post(() => FailVideo("O vídeo não pôde ser reproduzido. Verifique o arquivo e o formato."));
            _player.EndReached += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                if (!_disposed && !_videoFailed && _frame.Style.BackgroundMode == "Video" && _media != null)
                    _player?.Play(_media);
            });
            _videoView = new VideoView
            {
                MediaPlayer = _player,
                Content = _videoOverlay,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsVisible = false
            };
            // Native video is the bottom layer. Its Content owns the transparent top layer.
            _host.Children.Insert(0, _videoView);
            return true;
        }
        catch (Exception ex)
        {
            _initializationFailed = true;
            if (_videoView != null)
            {
                _videoView.MediaPlayer = null;
                _videoView.Content = null;
                _host.Children.Remove(_videoView);
                _videoView = null;
            }
            _player?.Dispose();
            _player = null;
            _vlc?.Dispose();
            _vlc = null;
            FailVideo("Falha ao iniciar vídeo: " + ex.Message);
            return false;
        }
    }

    private void PlayVideo(string path)
    {
        if (_vlc == null || _player == null) return;
        if (!HasVideoHost()) { QueueVideoStart(path); return; }
        ClearPendingVideo();
        try
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Arquivo de vídeo não encontrado.", path);
            _player.Stop();
            _media?.Dispose();
            _media = new Media(_vlc, new Uri(Path.GetFullPath(path)));
            _media.AddOption(":input-repeat=65535");
            _media.AddOption(":no-audio");
            _videoPath = path;
            if (!_player.Play(_media)) throw new InvalidOperationException("O mecanismo de vídeo recusou a reprodução.");
        }
        catch (Exception ex)
        {
            _videoPath = null;
            FailVideo("Não foi possível abrir o vídeo: " + ex.Message);
        }
    }

    public void RestartVideo()
    {
        if (_disposed || _frame.Style.BackgroundMode != "Video") return;
        _attemptedVideoPath = null;
        _videoPath = null;
        _videoFailed = false;
        _initializationFailed = false;
        SetFrame(_frame);
    }

    private bool HasVideoHost() => _player != null && (!OperatingSystem.IsWindows() || _player.Hwnd != IntPtr.Zero);

    private void QueueVideoStart(string path)
    {
        if (!string.Equals(path, _pendingVideoPath, StringComparison.OrdinalIgnoreCase))
        {
            _pendingVideoPath = path;
            _pendingVideoStarted = Environment.TickCount64;
        }
        _videoStartTimer.Start();
    }

    private void StartPendingVideo()
    {
        if (_disposed || !_readyForVideo || !IsEffectivelyVisible || _frame.Blackout ||
            _frame.Style.BackgroundMode != "Video" || _videoFailed || _pendingVideoPath == null)
        {
            ClearPendingVideo();
            return;
        }
        if (HasVideoHost())
        {
            var path = _pendingVideoPath;
            PlayVideo(path);
        }
        else if (Environment.TickCount64 - _pendingVideoStarted > 5000)
        {
            ClearPendingVideo();
            FailVideo("Não foi possível preparar a janela de vídeo. Reabra a projeção ou escolha novamente o fundo.");
        }
    }

    private void ClearPendingVideo()
    {
        _videoStartTimer.Stop();
        _pendingVideoPath = null;
    }

    private void ShowFallback()
    {
        if (_videoView != null) _videoView.IsVisible = false;
        _renderer.IsVisible = true;
    }

    private void FailVideo(string message)
    {
        if (_disposed || _videoFailed) return;
        _videoFailed = true;
        ClearPendingVideo();
        ShowFallback();
        ReportError(message);
    }

    public void ReplayTransition()
    {
        _renderer.ReplayTransition();
        _videoOverlay.ReplayTransition();
    }

    private void UpdateVideoCrop()
    {
        if (_player == null || _disposed || Bounds.Width < 1 || Bounds.Height < 1) return;
        // Match the cover/crop behavior used by image backgrounds and MP4 export.
        _player.CropGeometry = $"{(int)Math.Round(Bounds.Width)}:{(int)Math.Round(Bounds.Height)}";
        _player.Scale = 0;
    }

    private void ReportError(string message) => Error?.Invoke(message);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearPendingVideo();
        if (_videoView != null)
        {
            _videoView.IsVisible = false;
            _videoView.MediaPlayer = null;
            _videoView.Content = null;
            _host.Children.Remove(_videoView);
        }
        _player?.Stop();
        _media?.Dispose();
        _player?.Dispose();
        _vlc?.Dispose();
        _renderer.Dispose();
        _videoOverlay.Dispose();
    }
}
