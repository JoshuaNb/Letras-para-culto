using System.IO.Compression;
using System.Text.Json;
using Avalonia;
using WorshipStudio.Models;
using WorshipStudio.Services;
using Xunit;

namespace WorshipStudio.Tests;

public sealed class BackgroundCompatibilityTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WorshipStudio-backgrounds-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Theory]
    [InlineData("Túnel 3D", "Tunnel")]
    [InlineData("aurora fluida", "Aurora")]
    [InlineData("Partículas 3D", "Particles")]
    [InlineData("Vídeo local", "Video")]
    [InlineData("efeito de outra versão", "Aurora")]
    public void OlderNamesLoadWithARecognizedBackgroundWithoutChangingLyrics(string savedMode, string expectedMode)
    {
        var store = new LibraryStore(_directory);
        var song = new Song { Title = "Canção local", Lyrics = "A letra precisa continuar\nE o fundo deve aparecer" };
        var document = new StudioDocument { Songs = [song], Style = new StageStyle { BackgroundMode = savedMode } };
        File.WriteAllText(store.DocumentPath, JsonSerializer.Serialize(document, LibraryStore.JsonOptions));
        var loaded = store.Load();
        Assert.Equal(expectedMode, loaded.Style.BackgroundMode);
        Assert.Equal(song.Lyrics, loaded.Songs.Single().Lyrics);
        Assert.Equal(1, loaded.Style.MotionSpeed);
    }

    [Fact]
    public void CopiedLibraryUsesItsOwnManagedMediaEvenWhenOldComputerPathExists()
    {
        var source = new LibraryStore(Path.Combine(_directory, "old-pc"));
        var sourceFile = Path.Combine(_directory, "fixture.bin");File.WriteAllBytes(sourceFile, [1, 3, 5, 7]);
        var managed = source.ImportAsset(sourceFile);
        source.Save(new StudioDocument { Style = new StageStyle { BackgroundMode = "Video", BackgroundPath = managed } });
        var destination = new LibraryStore(Path.Combine(_directory, "new-pc"));
        Directory.CreateDirectory(Path.Combine(destination.DirectoryPath, "media"));
        var copiedMedia = Path.Combine(destination.DirectoryPath, "media", Path.GetFileName(managed));
        File.Copy(managed, copiedMedia);File.Copy(source.DocumentPath, destination.DocumentPath);
        var loaded = destination.Load();
        Assert.Equal(copiedMedia, loaded.Style.BackgroundPath);
        Assert.Equal(File.ReadAllBytes(managed), File.ReadAllBytes(loaded.Style.BackgroundPath!));
        Assert.True(File.Exists(managed));
    }

    [Fact]
    public void MissingManagedMediaKeepsItsReferenceForDiagnosticsAndRecovery()
    {
        var store = new LibraryStore(_directory);
        var missing = Path.Combine(_directory, "old-pc", "media", "missing.png");
        store.Save(new StudioDocument { Style = new StageStyle { BackgroundMode = "Image", BackgroundPath = missing } });
        var loaded = store.Load();
        Assert.Equal(missing, loaded.Style.BackgroundPath);
        Assert.Equal("Image", loaded.Style.BackgroundMode);
    }

    [Fact]
    public void RelativeMediaCannotEscapeTheCopiedLibrary()
    {
        var store = new LibraryStore(_directory);
        var json = JsonSerializer.Serialize(new StudioDocument { Style = new StageStyle { BackgroundMode = "Image", BackgroundPath = "../outside.png" } }, LibraryStore.JsonOptions);
        File.WriteAllText(store.DocumentPath, json);
        Assert.Throws<InvalidDataException>(() => store.Load());
        Assert.Equal(json, File.ReadAllText(store.DocumentPath));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CompatibilityPreferencesStayLocalAndDoNotChangeThePortableScene(bool software, bool video)
    {
        var preferences = new StudioPreferences { ForceSoftwareRendering = software, CompatibleVideoPlayback = video, AutoCheckUpdates = false };
        preferences.Save(_directory);
        var loaded = StudioPreferences.Load(_directory);
        Assert.Equal(software, loaded.ForceSoftwareRendering);Assert.Equal(video, loaded.CompatibleVideoPlayback);
        var store = new LibraryStore(_directory);var document = new StudioDocument();
        var before = JsonSerializer.Serialize(document.Style, LibraryStore.JsonOptions);
        var backup = Path.Combine(_directory, "portable.wstudio");store.Backup(backup, document);
        using var archive = ZipFile.OpenRead(backup);
        Assert.DoesNotContain(archive.Entries, entry => entry.Name == "preferences.json");
        Assert.Equal(before, JsonSerializer.Serialize(document.Style, LibraryStore.JsonOptions));
    }

    [Theory]
    [InlineData(6, 1, false, false)]
    [InlineData(10, 0, true, false)]
    [InlineData(10, 0, false, true)]
    public void CompatibilityChoiceAvoidsAngleOnOlderWindowsOrExplicitSoftware(int major, int minor, bool legacy, bool force)
    {
        Assert.Equal(new[] { Win32RenderingMode.Software }, WindowsCompatibility.SelectRenderingModes(new Version(major, minor), legacy, force));
    }
}
