using System.IO.Compression;
using WorshipStudio.Models;
using WorshipStudio.Services;
using Xunit;

namespace WorshipStudio.Tests;

public sealed class CoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WorshipStudio-tests-" + Guid.NewGuid().ToString("N"));
    public CoreTests() => Directory.CreateDirectory(_directory);
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Fact]
    public void LyricsPreservePortugueseAndSplitSectionsAndBlankLines()
    {
        var slides = LyricParser.Parse("[Verso 1]\r\nÓ Senhor, és fiel\r\nTua graça nos alcança\r\n\r\n[Refrão]\r\nAleluia!\r\n\r\n\r\nPara sempre");
        Assert.Equal(3, slides.Count);
        Assert.Equal("Verso 1", slides[0].Label);
        Assert.Equal("Ó Senhor, és fiel\nTua graça nos alcança", slides[0].Text);
        Assert.Equal("Refrão", slides[1].Label);
        Assert.Equal("Refrão", slides[2].Label);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\r\n \n[Refrão]\n\n")]
    public void EmptyLyricsDoNotProjectPhantomSlides(string? lyrics) => Assert.Empty(LyricParser.Parse(lyrics));

    [Fact]
    public void SavedLibraryRoundTripsSongsSetlistAndStyle()
    {
        var store = new LibraryStore(_directory);
        var song = new Song { Title = "Canção da criação", Artist = "Equipe local", Lyrics = "[Verso]\nPaz e luz" };
        var document = new StudioDocument
        {
            Songs = [song], Setlist = [song.Id], MonitorIndex = 2,
            Style = new StageStyle { BackgroundMode = "Tunnel", FontSize = 78, Transition = "Slide" }
        };
        store.Save(document);
        var loaded = new LibraryStore(_directory).Load();
        Assert.Single(loaded.Songs);
        Assert.Equal(song.Id, loaded.Songs[0].Id);
        Assert.Equal(song.Lyrics, loaded.Songs[0].Lyrics);
        Assert.Equal(song.Id, Assert.Single(loaded.Setlist));
        Assert.Equal("Tunnel", loaded.Style.BackgroundMode);
        Assert.Equal(78, loaded.Style.FontSize);
        Assert.Equal(2, loaded.MonitorIndex);
    }

    [Fact]
    public void BackupRestoresMediaEvenAfterOriginalManagedFileWasRemoved()
    {
        var store = new LibraryStore(Path.Combine(_directory, "original"));
        var image = Path.Combine(_directory, "background.png");
        File.WriteAllBytes(image, [137,80,78,71,13,10,26,10]);
        var managed = store.ImportAsset(image);
        var document = new StudioDocument { Style = new StageStyle { BackgroundMode = "Image", BackgroundPath = managed } };
        var archive = Path.Combine(_directory, "backup.worship");
        store.Backup(archive, document);
        File.Delete(managed);
        var restoredStore = new LibraryStore(Path.Combine(_directory, "restored"));
        var restored = restoredStore.Restore(archive);
        Assert.NotNull(restored.Style.BackgroundPath);
        Assert.True(File.Exists(restored.Style.BackgroundPath));
        Assert.Equal(File.ReadAllBytes(image), File.ReadAllBytes(restored.Style.BackgroundPath));
        Assert.StartsWith(Path.GetFullPath(restoredStore.DirectoryPath), Path.GetFullPath(restored.Style.BackgroundPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RestoreRejectsZipPathTraversal()
    {
        var archivePath = Path.Combine(_directory, "malicious.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(archive.CreateEntry("../escaped.txt").Open());
            writer.Write("must not be written");
        }
        var store = new LibraryStore(Path.Combine(_directory, "library"));
        Assert.ThrowsAny<Exception>(() => store.Restore(archivePath));
        Assert.False(File.Exists(Path.Combine(_directory, "escaped.txt")));
    }

    [Fact]
    public void FailedSavePreservesPreviouslySavedLibrary()
    {
        var store = new LibraryStore(_directory);
        var original = new StudioDocument { Songs = [new Song { Title = "Título preservado", Lyrics = "Paz" }] };
        store.Save(original);
        var invalid = new StudioDocument { Songs = [new Song { Id = Guid.Empty, Title = "Inválido" }] };
        Assert.Throws<InvalidDataException>(() => store.Save(invalid));
        Assert.Equal("Título preservado", Assert.Single(store.Load().Songs).Title);
        Assert.False(File.Exists(store.DocumentPath + ".tmp"));
    }

    [Fact]
    public void CorruptLibraryIsReportedAndNeverOverwrittenOnLoad()
    {
        var store = new LibraryStore(_directory);
        File.WriteAllText(store.DocumentPath, "{broken-json");
        Assert.ThrowsAny<Exception>(() => store.Load());
        Assert.Equal("{broken-json", File.ReadAllText(store.DocumentPath));
    }

    [Fact]
    public void SaveKeepsPreviousRevisionAndDropsMissingSetlistReferences()
    {
        var store = new LibraryStore(_directory);
        var song = new Song { Title = "Primeira revisão", Lyrics = "Luz" };
        var document = new StudioDocument { Songs = [song], Setlist = [song.Id, Guid.NewGuid()] };
        store.Save(document);
        song.Title = "Segunda revisão";
        store.Save(document);
        Assert.Equal(song.Id, Assert.Single(store.Load().Setlist));
        Assert.Contains("Primeira revis", File.ReadAllText(store.DocumentPath + ".bak"));
        Assert.Equal("Segunda revisão", Assert.Single(store.Load().Songs).Title);
    }

    [Fact]
    public void BackupRejectsMissingActiveMediaWithoutReplacingExistingBackup()
    {
        var store = new LibraryStore(_directory);
        var archive = Path.Combine(_directory,"existing.wstudio");
        File.WriteAllText(archive,"previous backup");
        var document = new StudioDocument { Style = new StageStyle { BackgroundMode="Video",BackgroundPath=Path.Combine(_directory,"missing.mp4") } };
        Assert.Throws<FileNotFoundException>(()=>store.Backup(archive,document));
        Assert.Equal("previous backup",File.ReadAllText(archive));
        Assert.False(File.Exists(archive+".partial"));
    }

    [Fact]
    public void BackupClearsMissingInactiveMediaAndCanBeRestored()
    {
        var store = new LibraryStore(_directory);
        var archive = Path.Combine(_directory,"procedural.wstudio");
        var document = new StudioDocument { Style = new StageStyle { BackgroundMode="Aurora",BackgroundPath=Path.Combine(_directory,"old-missing.jpg") } };
        store.Backup(archive,document);
        var restored = new LibraryStore(Path.Combine(_directory,"restored")).Restore(archive);
        Assert.Null(restored.Style.BackgroundPath);
        Assert.Equal("Aurora",restored.Style.BackgroundMode);
        Assert.NotNull(document.Style.BackgroundPath);
    }

    [Fact]
    public void LegacyNullArtistIsNormalizedForSearchAndEditing()
    {
        var store = new LibraryStore(_directory);
        var document = new StudioDocument { Songs=[new Song {Title="Sem autoria",Artist=null!,Lyrics="Paz"}] };
        store.Save(document);
        Assert.Equal("",Assert.Single(store.Load().Songs).Artist);
    }
}
