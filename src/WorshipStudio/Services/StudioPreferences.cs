using System.Text.Json;
using System.Text.Json.Serialization;
using WorshipStudio.Themes;

namespace WorshipStudio.Services;

/// <summary>Machine-local operator preferences, separate from the song library.</summary>
public sealed class StudioPreferences
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public StudioTheme Theme { get; set; } = StudioTheme.Dark;
    public bool AutoCheckUpdates { get; set; } = true;
    public bool ForceSoftwareRendering { get; set; }
    public bool CompatibleVideoPlayback { get; set; }

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorshipStudio");

    public static StudioPreferences Load(string? directory = null)
    {
        try
        {
            var path = Path.Combine(directory ?? DefaultDirectory, "preferences.json");
            if (!File.Exists(path)) return new StudioPreferences();
            var preferences = JsonSerializer.Deserialize<StudioPreferences>(File.ReadAllText(path), JsonOptions)
                ?? new StudioPreferences();
            if (!Enum.IsDefined(preferences.Theme)) preferences.Theme = StudioTheme.Dark;
            return preferences;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Damaged preferences must not prevent offline operation; preserve the file for recovery.
            return new StudioPreferences();
        }
    }

    public void Save(string? directory = null)
    {
        var folder = directory ?? DefaultDirectory;
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "preferences.json");
        var temporary = Path.Combine(folder, $"preferences.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, this, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
