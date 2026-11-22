using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using WorshipStudio.Controls;
using WorshipStudio.Models;

namespace WorshipStudio;

public sealed class ProjectionWindow : Window
{
    public StageSurface Surface { get; } = new();
    public event Action? NextRequested;
    public event Action? PreviousRequested;
    public event Action? BlackoutRequested;
    public event Action? LyricsVisibilityRequested;

    public ProjectionWindow()
    {
        Title = "Worship Studio • Projeção";
        Background = Brushes.Black;
        Width = 960;
        Height = 540;
        MinWidth = 320;
        MinHeight = 180;
        Content = Surface;
        ShowActivated = false;
        KeyDown += OnProjectionKeyDown;
        Closed += (_, _) => Surface.Dispose();
    }

    public void SetFrame(StageFrame frame) => Surface.SetFrame(frame);

    /// <summary>Moves the independent output to the selected physical monitor.</summary>
    public void ShowOnScreen(Screen screen, bool fullscreen = true)
    {
        WindowState = WindowState.Normal;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = screen.Bounds.Position;
        Width = screen.Bounds.Width / screen.Scaling;
        Height = screen.Bounds.Height / screen.Scaling;
        if (!IsVisible) Show();
        if (fullscreen) WindowState = WindowState.FullScreen;
    }

    private void OnProjectionKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
            case Key.Space:
            case Key.PageDown:
                NextRequested?.Invoke();
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Up:
            case Key.PageUp:
                PreviousRequested?.Invoke();
                e.Handled = true;
                break;
            case Key.B:
                BlackoutRequested?.Invoke();
                e.Handled = true;
                break;
            case Key.L:
                LyricsVisibilityRequested?.Invoke();
                e.Handled = true;
                break;
            case Key.F11:
                WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
                e.Handled = true;
                break;
            case Key.Escape:
                WindowState = WindowState.Normal;
                e.Handled = true;
                break;
        }
    }
}
