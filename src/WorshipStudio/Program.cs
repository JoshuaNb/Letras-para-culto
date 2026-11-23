using Avalonia;
using System.Runtime.InteropServices;

namespace WorshipStudio;

internal static class Program
{
    public static bool SoftwareRenderingRequested { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        var dataArg = Array.IndexOf(args, "--data");
        if (dataArg >= 0 && dataArg + 1 < args.Length) Services.LibraryStore.DataDirectoryOverride = args[dataArg + 1];
        SoftwareRenderingRequested = args.Contains("--software-rendering", StringComparer.OrdinalIgnoreCase) ||
            Services.StudioPreferences.Load(Services.LibraryStore.DataDirectoryOverride).ForceSoftwareRendering;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Services.AppLog.Write(e.ExceptionObject.ToString() ?? "Erro desconhecido");
        try { BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        catch (Exception ex) { Services.AppLog.Write(ex.ToString()); if (OperatingSystem.IsWindows()) MessageBox(IntPtr.Zero, ex.Message, "Worship Studio — falha ao iniciar", 0x10); }
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont()
        .With(new Win32PlatformOptions { RenderingMode = Services.WindowsCompatibility.SelectRenderingModes(Environment.OSVersion.Version,
            Services.WindowsCompatibility.IsLegacyRuntime, SoftwareRenderingRequested) }).LogToTrace();
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
