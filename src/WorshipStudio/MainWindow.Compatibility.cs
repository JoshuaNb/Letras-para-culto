using Avalonia.Controls;
using Avalonia.Threading;
using WorshipStudio.Controls;
using WorshipStudio.Models;
using WorshipStudio.Services;

namespace WorshipStudio;

public partial class MainWindow
{
    private readonly bool _softwareAtStartup = Program.SoftwareRenderingRequested;

    private void ConfigureBackgroundCompatibility()
    {
        var software = C<CheckBox>("SoftwareRenderingCheck");
        software.IsChecked = WindowsCompatibility.IsLegacyRuntime || App.Preferences.ForceSoftwareRendering;
        software.IsEnabled = !WindowsCompatibility.IsLegacyRuntime;
        C<CheckBox>("CompatibleVideoCheck").IsChecked = WindowsCompatibility.IsLegacyRuntime || App.Preferences.CompatibleVideoPlayback;
        C<CheckBox>("CompatibleVideoCheck").IsEnabled = !WindowsCompatibility.IsLegacyRuntime;
        software.IsCheckedChanged += (_, _) =>
        {
            if (_loading || WindowsCompatibility.IsLegacyRuntime) return;
            App.Preferences.ForceSoftwareRendering = software.IsChecked == true;
            SavePreferences();UpdateGraphicsCompatibilityStatus();
        };
        C<CheckBox>("CompatibleVideoCheck").IsCheckedChanged += (_, _) =>
        {
            if (_loading) return;
            App.Preferences.CompatibleVideoPlayback = C<CheckBox>("CompatibleVideoCheck").IsChecked == true;
            ApplyVideoCompatibility();SavePreferences();
            Status("Qualidade do vídeo ajustada · as letras mantêm a resolução da projeção");
        };
        _preview.PlaybackDiagnostic += diagnostic => PlaybackDiagnostic("Prévia", diagnostic.Message);
        _programPreview.PlaybackDiagnostic += diagnostic => PlaybackDiagnostic("Retorno", diagnostic.Message);
        ClickAsync("RestartGraphicsButton", RestartForGraphics);
        ApplyVideoCompatibility();UpdateGraphicsCompatibilityStatus();
    }

    private void ApplyVideoCompatibility()
    {
        var quality = App.Preferences.CompatibleVideoPlayback || WindowsCompatibility.IsLegacyRuntime
            ? VideoPlaybackQuality.Compatible : VideoPlaybackQuality.Auto;
        _preview.VideoQuality = quality;
        _programPreview.VideoQuality = quality;
        if (_projection != null) _projection.Surface.VideoQuality = quality;
    }

    private void PlaybackDiagnostic(string surface, string message)
    {
        AppLog.Write(surface + ": " + message);
        Dispatcher.UIThread.Post(() =>
        {
            if (!_closing) C<TextBlock>("BackgroundPlaybackStatus").Text = surface + " · " + message;
        });
    }

    private void UpdateGraphicsCompatibilityStatus()
    {
        var pending = !WindowsCompatibility.IsLegacyRuntime && App.Preferences.ForceSoftwareRendering != _softwareAtStartup;
        C<Button>("RestartGraphicsButton").IsVisible = pending;
        C<TextBlock>("GraphicsCompatibilityStatus").Text = pending ? "Reinicie para aplicar a mudança gráfica."
            : WindowsCompatibility.IsLegacyRuntime || _softwareAtStartup ? "Compatibilidade gráfica por software ativa."
            : "GPU automática com fallback. Vídeos decodificados sem depender da GPU.";
    }

    private void UpdateBackgroundPlaybackStatus()
    {
        var style = _document.Style;
        if (style.BackgroundMode == "Video") return;
        var choice = BackgroundCatalog.Choices.First(c => c.Id == BackgroundCatalog.Normalize(style.BackgroundMode));
        C<TextBlock>("BackgroundPlaybackStatus").Text = choice.IsProcedural
            ? choice.Name + (style.MotionSpeed <= 0 ? " · movimento pausado: aumente a velocidade abaixo" : " · animação local")
            : choice.Name + " · arquivo local";
    }

    private async Task RestartForGraphics()
    {
        if (_restoring || _startupError != null) throw new InvalidOperationException("Conclua a recuperação da biblioteca antes de reiniciar.");
        if (!await Confirm("Reiniciar o programa?", "A biblioteca será salva e a projeção será encerrada para aplicar a compatibilidade gráfica.")) return;
        _store.Save(_document);App.Preferences.Save(LibraryStore.DataDirectoryOverride);
        var executable = Environment.ProcessPath ?? throw new IOException("Não foi possível localizar o executável.");
        var start = new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = true };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(App).Assembly.Location);
        if (LibraryStore.DataDirectoryOverride != null)
        {
            start.ArgumentList.Add("--data");start.ArgumentList.Add(LibraryStore.DataDirectoryOverride);
        }
        if (System.Diagnostics.Process.Start(start) == null) throw new IOException("Não foi possível reabrir o programa.");
        Close();
    }
}
