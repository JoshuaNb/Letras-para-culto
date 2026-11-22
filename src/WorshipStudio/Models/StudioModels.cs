namespace WorshipStudio.Models;

public sealed record Song
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Novo louvor";
    public string Artist { get; set; } = "";
    public string Lyrics { get; set; } = "";
    public override string ToString() => Title;
}

public sealed record Slide(string Label, string Text);

public sealed record StageStyle
{
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 64;
    public string TextColor { get; set; } = "#FFFFFF";
    public string AccentColor { get; set; } = "#48DCC4";
    public string BackgroundColor { get; set; } = "#08121F";
    public string BackgroundMode { get; set; } = "Aurora";
    public string? BackgroundPath { get; set; }
    public string Transition { get; set; } = "Fade";
    public double TransitionSeconds { get; set; } = .6;
    public double MotionSpeed { get; set; } = 1;
    public double Dim { get; set; } = .35;
    public double TextY { get; set; } = .5;
}

public sealed record StageFrame
{
    public string Text { get; init; } = "";
    public string PreviousText { get; init; } = "";
    public StageStyle Style { get; init; } = new();
    public bool Blackout { get; init; }
    public bool HideLyrics { get; init; }
    public double AudioLevel { get; init; }
    public long Revision { get; init; }
}

public sealed class StudioDocument
{
    public int Version { get; set; } = 1;
    public List<Song> Songs { get; set; } = [];
    public List<Guid> Setlist { get; set; } = [];
    public StageStyle Style { get; set; } = new();
    public int MonitorIndex { get; set; } = 1;
    public double SecondsPerSlide { get; set; } = 8;
    public int RemotePort { get; set; } = 8765;
}

public static class LyricParser
{
    public static List<Slide> Parse(string? lyrics)
    {
        var slides = new List<Slide>();
        var lines = new List<string>();
        var label = "Estrofe";
        void Flush()
        {
            var text = string.Join('\n', lines).Trim();
            if (text.Length > 0) slides.Add(new Slide(label, text));
            lines.Clear();
        }
        foreach (var raw in (lyrics ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Trim().StartsWith('[') && line.Trim().EndsWith(']'))
            {
                Flush();
                label = line.Trim()[1..^1];
            }
            else if (string.IsNullOrWhiteSpace(line)) Flush();
            else lines.Add(line);
        }
        Flush();
        return slides;
    }
}
