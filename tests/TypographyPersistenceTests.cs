using System.Text.Json;
using WorshipStudio.Models;
using WorshipStudio.Services;
using Xunit;

namespace WorshipStudio.Tests;

public sealed class TypographyPersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WorshipStudio-typography-" + Guid.NewGuid().ToString("N"));
    public TypographyPersistenceTests() => Directory.CreateDirectory(_directory);
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Fact]
    public void TypographyAndAnimationSurviveLibraryAndSceneRoundTrip()
    {
        var style = new StageStyle
        {
            FontFamily = "Georgia", FontSize = 94, Bold = false, TextColor = "#F4C847",
            Transition = "Words", TransitionSeconds = 1.7,
            LyricAnimation = "Wave", LyricAnimationSpeed = 1.8, LyricAnimationIntensity = .72
        };
        var store = new LibraryStore(_directory);
        store.Save(new StudioDocument { Style = style });
        var loaded = new LibraryStore(_directory).Load().Style;
        Assert.Equal(style, loaded);
        var scene = JsonSerializer.Deserialize<StageStyle>(JsonSerializer.Serialize(style, LibraryStore.JsonOptions), LibraryStore.JsonOptions);
        Assert.Equal(style, scene);
    }

    [Fact]
    public void LegacyScenesKeepFontAndColorAndReceiveSafeAnimationDefaults()
    {
        var scene = JsonSerializer.Deserialize<StageStyle>("""{"FontFamily":"Consolas","FontSize":82,"TextColor":"#E5AAFF","Transition":"Typewriter"}""", LibraryStore.JsonOptions)!;
        Assert.Equal("Consolas", scene.FontFamily);
        Assert.Equal("#E5AAFF", scene.TextColor);
        Assert.True(scene.Bold);
        Assert.Equal("None", scene.LyricAnimation);
        Assert.Equal(1, scene.LyricAnimationSpeed);
        Assert.Equal(.5, scene.LyricAnimationIntensity);
        Assert.Equal("Typewriter", scene.Transition);
    }

    [Fact]
    public void BackupRestoresTypographyAndAnimationExactly()
    {
        var style = new StageStyle { FontFamily = "Verdana", Bold = true, TextColor = "#7EC8FF", Transition = "Wipe", LyricAnimation = "Breathe", LyricAnimationSpeed = .8, LyricAnimationIntensity = .35 };
        var store = new LibraryStore(Path.Combine(_directory, "before"));
        var path = Path.Combine(_directory, "typography.wstudio");
        store.Backup(path, new StudioDocument { Style = style });
        var restored = new LibraryStore(Path.Combine(_directory, "restored")).Restore(path);
        Assert.Equal(style, restored.Style);
    }
}
