using WorshipStudio.Services;
using WorshipStudio.Themes;
using Xunit;

namespace WorshipStudio.Tests;

public sealed class StudioPreferencesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "WorshipStudio-preferences-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FreshInstallationUsesDarkAndAutomaticUpdates()
    {
        var settings = StudioPreferences.Load(_folder);
        Assert.Equal(StudioTheme.Dark, settings.Theme);
        Assert.True(settings.AutoCheckUpdates);
        Assert.False(Directory.Exists(_folder));
    }

    [Theory]
    [InlineData(StudioTheme.Light)]
    [InlineData(StudioTheme.Dark)]
    [InlineData(StudioTheme.Gray)]
    [InlineData(StudioTheme.PastelYellow)]
    public void ChoiceAndUpdatePreferenceSurviveRestart(StudioTheme theme)
    {
        new StudioPreferences { Theme = theme, AutoCheckUpdates = false }.Save(_folder);
        var settings = StudioPreferences.Load(_folder);
        Assert.Equal(theme, settings.Theme);
        Assert.False(settings.AutoCheckUpdates);
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
        Assert.Contains(theme.ToString(), File.ReadAllText(Path.Combine(_folder, "preferences.json")));
        settings.AutoCheckUpdates = true;
        settings.Save(_folder);
        Assert.True(StudioPreferences.Load(_folder).AutoCheckUpdates);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"Theme\":\"MissingTheme\"}")]
    [InlineData("null")]
    public void DamagedSettingsRecoverWithoutRemovingOriginal(string content)
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "preferences.json");
        File.WriteAllText(path, content);
        var settings = StudioPreferences.Load(_folder);
        Assert.Equal(StudioTheme.Dark, settings.Theme);
        Assert.True(settings.AutoCheckUpdates);
        Assert.Equal(content, File.ReadAllText(path));
    }

    [Fact]
    public void UnknownNumericThemeFallsBackWithoutChangingOtherPreferences()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "preferences.json"), "{\"Theme\":999,\"AutoCheckUpdates\":false}");
        var settings = StudioPreferences.Load(_folder);
        Assert.Equal(StudioTheme.Dark, settings.Theme);
        Assert.False(settings.AutoCheckUpdates);
    }

    [Theory]
    [InlineData(StudioTheme.Light)]
    [InlineData(StudioTheme.Dark)]
    [InlineData(StudioTheme.Gray)]
    [InlineData(StudioTheme.PastelYellow)]
    public void EachPaletteKeepsControlsAndSecondaryTextReadable(StudioTheme theme)
    {
        var palette = StudioThemes.Palette(theme);
        foreach (var background in new[] { "WsBackground", "WsSurface", "WsPanel", "WsInput" })
        {
            Assert.True(Contrast(palette["WsText"], palette[background]) >= 4.5, $"{theme} text/{background}");
            Assert.True(Contrast(palette["WsMuted"], palette[background]) >= 4.5, $"{theme} muted/{background}");
        }
        Assert.True(Contrast(palette["WsAccentText"], palette["WsAccent"]) >= 4.5);
        Assert.True(Contrast(palette["WsDangerText"], palette["WsDanger"]) >= 4.5);
        Assert.True(Contrast(palette["WsText"], palette["WsSelection"]) >= 4.5);
        foreach (var background in new[] { "WsPanel", "WsSurface", "WsSelection" })
            Assert.True(Contrast(palette["WsAccentLabel"], palette[background]) >= 4.5, $"{theme} accent label/{background}");
    }

    [Theory]
    [InlineData(StudioTheme.Light)]
    [InlineData(StudioTheme.Dark)]
    [InlineData(StudioTheme.Gray)]
    [InlineData(StudioTheme.PastelYellow)]
    public void TimelineClipsKeepLyricsAndSetlistTextReadable(StudioTheme theme)
    {
        var palette = StudioThemes.Palette(theme);
        Assert.True(Contrast(palette["WsClipText"], palette["WsClip"]) >= 4.5, $"{theme} lyric clips");
        Assert.True(Contrast(palette["WsSetlistText"], palette["WsSetlistClip"]) >= 4.5, $"{theme} setlist clips");
        Assert.True(Contrast(palette["WsSetlistText"], palette["WsSelection"]) >= 4.5, $"{theme} selected setlist clip");
    }

    private static double Contrast(string first, string second)
    {
        static double Luminance(string hex)
        {
            static double Channel(int value)
            {
                var fraction = value / 255d;
                return fraction <= 0.04045 ? fraction / 12.92 : Math.Pow((fraction + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Channel(Convert.ToInt32(hex.Substring(1, 2), 16))
                 + 0.7152 * Channel(Convert.ToInt32(hex.Substring(3, 2), 16))
                 + 0.0722 * Channel(Convert.ToInt32(hex.Substring(5, 2), 16));
        }
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}
