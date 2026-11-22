using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WorshipStudio.Services;
using WorshipStudio.Themes;

namespace WorshipStudio;
public partial class App : Application
{
    public static StudioPreferences Preferences { get; private set; } = new();
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Preferences = StudioPreferences.Load(LibraryStore.DataDirectoryOverride);
        StudioThemes.Apply(Preferences.Theme);
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
