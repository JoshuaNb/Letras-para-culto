using Avalonia;

namespace WorshipStudio.Services;

/// <summary>Rendering paths available in the legacy Windows payload.</summary>
public static class WindowsCompatibility
{
    public static bool IsLegacyRuntime
    {
        get
        {
#if NET6_0
            return true;
#else
            return false;
#endif
        }
    }

    public static bool UseSoftwareRendering(Version windowsVersion, bool legacyRuntime) =>
        legacyRuntime || windowsVersion.Major < 10;

    public static Win32RenderingMode[] SelectRenderingModes(Version windowsVersion, bool legacyRuntime, bool forceSoftware) =>
        forceSoftware || UseSoftwareRendering(windowsVersion, legacyRuntime)
            ? [Win32RenderingMode.Software]
            : [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software];

    public static Win32RenderingMode[] RenderingModes => SelectRenderingModes(Environment.OSVersion.Version, IsLegacyRuntime, false);
}
