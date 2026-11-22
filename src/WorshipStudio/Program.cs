using Avalonia;
using System.Runtime.InteropServices;

namespace WorshipStudio;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var dataArg = Array.IndexOf(args, "--data");
        if (dataArg >= 0 && dataArg + 1 < args.Length) Services.LibraryStore.DataDirectoryOverride = args[dataArg + 1];
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Services.AppLog.Write(e.ExceptionObject.ToString() ?? "Erro desconhecido");
        try { BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        catch (Exception ex) { Services.AppLog.Write(ex.ToString()); if (OperatingSystem.IsWindows()) MessageBox(IntPtr.Zero, ex.Message, "Worship Studio — falha ao iniciar", 0x10); }
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont()
        .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software] }).LogToTrace();
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
