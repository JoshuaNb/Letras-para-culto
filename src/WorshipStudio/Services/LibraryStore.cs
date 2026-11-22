using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using WorshipStudio.Models;

namespace WorshipStudio.Services;

public sealed class LibraryStore
{
    public static string? DataDirectoryOverride { get; set; }
    public static JsonSerializerOptions JsonOptions { get; } = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string DirectoryPath { get; }
    public string DocumentPath => Path.Combine(DirectoryPath, "library.json");
    public LibraryStore(string? directory = null)
    {
        DirectoryPath = Path.GetFullPath(directory ?? DataDirectoryOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorshipStudio"));
        Directory.CreateDirectory(DirectoryPath);
    }
    public StudioDocument Load()
    {
        if (!File.Exists(DocumentPath)) return CreateInitial();
        var document = JsonSerializer.Deserialize<StudioDocument>(File.ReadAllText(DocumentPath), JsonOptions) ?? throw new InvalidDataException("Biblioteca vazia ou inválida.");
        Validate(document);
        ResolveMedia(document);
        return document;
    }
    public void Save(StudioDocument document)
    {
        Validate(document);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        var pending = DocumentPath + ".tmp";
        using (var file = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        using (var writer = new StreamWriter(file)) { writer.Write(json); writer.Flush(); file.Flush(true); }
        if (File.Exists(DocumentPath)) File.Copy(DocumentPath, DocumentPath + ".bak", true);
        File.Move(pending, DocumentPath, true);
    }
    public string ImportAsset(string source)
    {
        var info = new FileInfo(source);
        if (!info.Exists) throw new FileNotFoundException("Mídia não encontrada.", source);
        if (info.Length > 4L * 1024 * 1024 * 1024) throw new InvalidDataException("A mídia excede o limite de 4 GB.");
        using var stream = File.OpenRead(source);
        var hash = Convert.ToHexString(SHA256.HashData(stream))[..20];
        var destination = Path.Combine(DirectoryPath, "media", hash + Path.GetExtension(source).ToLowerInvariant());
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (!File.Exists(destination)) File.Copy(source, destination);
        return destination;
    }
    public void Backup(string destination, StudioDocument document)
    {
        var portable = JsonSerializer.Deserialize<StudioDocument>(JsonSerializer.Serialize(document, JsonOptions), JsonOptions)!;
        if (!string.IsNullOrEmpty(portable.Style.BackgroundPath) && !File.Exists(portable.Style.BackgroundPath))
        {
            if (portable.Style.BackgroundMode is "Image" or "Video") throw new FileNotFoundException("A mídia da cena está ausente. Selecione novamente o arquivo antes de salvar o backup.", portable.Style.BackgroundPath);
            portable.Style.BackgroundPath = null;
        }
        var temporary = destination + ".partial";
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                if (File.Exists(portable.Style.BackgroundPath))
                {
                    var fileName = "media/" + Path.GetFileName(portable.Style.BackgroundPath);
                    archive.CreateEntryFromFile(portable.Style.BackgroundPath, fileName, CompressionLevel.Fastest);
                    portable.Style.BackgroundPath = fileName;
                }
                var entry = archive.CreateEntry("library.json");
                using var writer = new StreamWriter(entry.Open());
                writer.Write(JsonSerializer.Serialize(portable, JsonOptions));
            }
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public StudioDocument Restore(string source)
    {
        using var archive = ZipFile.OpenRead(source);
        if (archive.Entries.Count > 5000 || archive.Entries.Sum(e => e.Length) > 8L * 1024 * 1024 * 1024) throw new InvalidDataException("Backup excede os limites permitidos.");
        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('\\', '/');
            if (normalized.StartsWith('/') || normalized.Contains(':') || normalized.Split('/').Contains("..")) throw new InvalidDataException("Backup contém um caminho inválido.");
            if (normalized != "library.json" && (!normalized.StartsWith("media/") || normalized.Count(c=>c=='/') != 1)) throw new InvalidDataException("Arquivo não reconhecido no backup.");
        }
        var library = archive.GetEntry("library.json") ?? throw new InvalidDataException("Backup sem biblioteca.");
        StudioDocument document;
        using (var reader = new StreamReader(library.Open())) document = JsonSerializer.Deserialize<StudioDocument>(reader.ReadToEnd(), JsonOptions) ?? throw new InvalidDataException("Biblioteca inválida.");
        Validate(document);
        var background = document.Style.BackgroundPath;
        if (!string.IsNullOrEmpty(background))
        {
            background = background.Replace('\\', '/');
            if (!background.StartsWith("media/") || background.Split('/').Length != 2 || background.Contains("..") || background.Contains(':')) throw new InvalidDataException("Referência de mídia inválida no backup.");
            if (archive.GetEntry(background) == null) throw new InvalidDataException("A mídia referenciada não está no backup.");
        }
        if (File.Exists(DocumentPath)) File.Copy(DocumentPath, DocumentPath + ".before-restore.bak", true);
        foreach (var entry in archive.Entries.Where(e=>e.FullName.StartsWith("media/") && e.Name.Length>0))
        {
            var path = Path.Combine(DirectoryPath, "media", entry.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path, true);
        }
        ResolveMedia(document);
        Save(document);
        return document;
    }
    private void ResolveMedia(StudioDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.Style.BackgroundPath) && !Path.IsPathRooted(document.Style.BackgroundPath))
        {
            var resolved = Path.GetFullPath(Path.Combine(DirectoryPath, document.Style.BackgroundPath));
            if (!resolved.StartsWith(DirectoryPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Caminho da mídia fora da biblioteca.");
            document.Style.BackgroundPath = resolved;
        }
    }
    public static void Validate(StudioDocument document)
    {
        if (document.Version != 1) throw new InvalidDataException("Esta versão da biblioteca não é suportada.");
        if (document.Songs == null || document.Setlist == null || document.Style == null) throw new InvalidDataException("Biblioteca incompleta.");
        if (document.Songs.Count > 50000 || document.Songs.Any(s=>s==null || s.Id==Guid.Empty || string.IsNullOrWhiteSpace(s.Title) || s.Lyrics == null || s.Lyrics.Length>2_000_000)) throw new InvalidDataException("A biblioteca contém louvores inválidos.");
        if (document.Songs.Select(s=>s.Id).Distinct().Count()!=document.Songs.Count) throw new InvalidDataException("Louvores com identificadores duplicados.");
        foreach (var song in document.Songs) song.Artist ??= "";
        document.Setlist.RemoveAll(id=>document.Songs.All(s=>s.Id!=id));
        var style = document.Style;
        style.FontSize = double.IsFinite(style.FontSize) ? Math.Clamp(style.FontSize, 24, 140) : 64;
        style.TransitionSeconds = double.IsFinite(style.TransitionSeconds) ? Math.Clamp(style.TransitionSeconds, 0, 3) : .6;
        style.MotionSpeed = double.IsFinite(style.MotionSpeed) ? Math.Clamp(style.MotionSpeed, 0, 4) : 1;
        style.Dim = double.IsFinite(style.Dim) ? Math.Clamp(style.Dim, 0, 1) : .35;
        style.TextY = double.IsFinite(style.TextY) ? Math.Clamp(style.TextY, .1, .9) : .5;
        style.LyricAnimationSpeed = double.IsFinite(style.LyricAnimationSpeed) ? Math.Clamp(style.LyricAnimationSpeed, .2, 3) : 1;
        style.LyricAnimationIntensity = double.IsFinite(style.LyricAnimationIntensity) ? Math.Clamp(style.LyricAnimationIntensity, 0, 1) : .5;
        style.AnimationDuration=double.IsFinite(style.AnimationDuration)?Math.Clamp(style.AnimationDuration,0,5):.7;
        style.AnimationStagger=double.IsFinite(style.AnimationStagger)?Math.Clamp(style.AnimationStagger,0,.5):.06;
        style.AnimationDelay=double.IsFinite(style.AnimationDelay)?Math.Clamp(style.AnimationDelay,0,3):0;
        style.AnimationStrength=double.IsFinite(style.AnimationStrength)?Math.Clamp(style.AnimationStrength,0,2):1;
        style.AnimationLoopPause=double.IsFinite(style.AnimationLoopPause)?Math.Clamp(style.AnimationLoopPause,0,8):1;
        if(!AnimationPresets.Contains(style.AnimationPreset))style.AnimationPreset="none";
        if(style.AnimationGranularity is not("Character" or "Word" or "Line" or "Text"))style.AnimationGranularity="Word";
        if(style.AnimationOrder is not("Forward" or "Reverse" or "Center" or "Random"))style.AnimationOrder="Forward";
        if(style.AnimationEasing is not("Linear" or "EaseOut" or "EaseInOut" or "Back" or "Elastic" or "Bounce"))style.AnimationEasing="EaseOut";
        if (string.IsNullOrWhiteSpace(style.FontFamily)) style.FontFamily = "Segoe UI";
        if (style.LyricAnimation is not ("None" or "Float" or "Wave" or "Pulse" or "Breathe" or "Sway" or "Bounce" or "Glow" or "Shake")) style.LyricAnimation = "None";
        if (style.Transition is not ("Fade" or "Slide" or "SlideDown" or "SlideLeft" or "SlideRight" or "Zoom" or "Pop" or "Rotate" or "Wipe" or "Typewriter" or "Words" or "Lines" or "Cut")) style.Transition = "Fade";
        if (!double.IsFinite(document.SecondsPerSlide)) document.SecondsPerSlide=8;
        document.SecondsPerSlide = Math.Clamp(document.SecondsPerSlide, 1, 120);
    }
    public static StudioDocument CreateInitial()
    {
        var song = new Song { Title = "Bem-vindo à adoração", Artist = "Texto original · Worship Studio", Lyrics = "[Abertura]\nReunidos em Tua presença\nCom gratidão vamos cantar\n\n[Adoração]\nNossa voz se une em louvor\nNosso coração é Teu altar\n\n[Final]\nQue a paz habite entre nós\nE o amor guie o caminhar" };
        return new StudioDocument { Songs = [song], Setlist = [song.Id] };
    }
}

public static class AppLog
{
    public static void Write(string message)
    {
        try
        {
            var directory = LibraryStore.DataDirectoryOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorshipStudio");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory,"studio.log"), $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch { }
    }
}
