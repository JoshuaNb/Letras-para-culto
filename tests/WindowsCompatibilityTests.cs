using Avalonia;
using WorshipStudio.Services;
using Xunit;

namespace WorshipStudio.Tests;

public class WindowsCompatibilityTests
{
    [Theory]
    [InlineData(6, 1)]
    [InlineData(6, 2)]
    [InlineData(6, 3)]
    public void OlderWindowsNeverAttemptsToLoadTheModernAngleBinary(int major, int minor)
    {
        Assert.True(WindowsCompatibility.UseSoftwareRendering(new Version(major, minor), false));
    }

    [Fact]
    public void LegacyPayloadAlwaysSelectsSoftwareEvenWhenTestedOnCurrentWindows()
    {
        Assert.True(WindowsCompatibility.UseSoftwareRendering(new Version(10, 0, 26100), true));
    }

    [Fact]
    public void ModernPayloadKeepsGpuWithSoftwareFallback()
    {
        Assert.False(WindowsCompatibility.UseSoftwareRendering(new Version(10, 0, 19045), false));
        var expected = WindowsCompatibility.IsLegacyRuntime
            ? new[] { Win32RenderingMode.Software }
            : new[] { Win32RenderingMode.AngleEgl, Win32RenderingMode.Software };
        Assert.Equal(expected, WindowsCompatibility.RenderingModes);
    }
}
