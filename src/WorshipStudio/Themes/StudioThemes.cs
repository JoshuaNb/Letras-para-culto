using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace WorshipStudio.Themes;

public enum StudioTheme { Dark, Light, Gray, PastelYellow }

public sealed record ThemeChoice(StudioTheme Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Operator-only palettes. Projection colors always come from StageStyle.</summary>
public static class StudioThemes
{
    public static IReadOnlyList<ThemeChoice> Choices { get; } = new[]
    {
        new ThemeChoice(StudioTheme.Light, "Branco"),
        new ThemeChoice(StudioTheme.Dark, "Escuro"),
        new ThemeChoice(StudioTheme.Gray, "Cinza"),
        new ThemeChoice(StudioTheme.PastelYellow, "Amarelo pastel")
    };

    public static StudioTheme Current { get; private set; } = StudioTheme.Dark;

    public static void Apply(StudioTheme theme)
    {
        var app = Application.Current ?? throw new InvalidOperationException("A interface ainda não foi inicializada.");
        if (!Enum.IsDefined(theme)) theme = StudioTheme.Dark;
        foreach (var entry in Palette(theme))
            app.Resources[entry.Key] = new SolidColorBrush(Color.Parse(entry.Value));

        app.RequestedThemeVariant = theme is StudioTheme.Light or StudioTheme.PastelYellow
            ? ThemeVariant.Light : ThemeVariant.Dark;
        Current = theme;
    }

    public static IReadOnlyDictionary<string, string> Palette(StudioTheme theme) => theme switch
    {
        StudioTheme.Light => Colors(
            "#E8EBEF", "#FFFFFF", "#F4F6F8", "#FFFFFF", "#CBD2DA", "#1D2731", "#53616F", "#647383",
            "#176F64", "#FFFFFF", "#DCEEE9", "#E6EAF0", "#F8E3E6", "#932D40", "#F2F4F7", "#B83C50", "#FFFFFF", "#246B55",
            clip:"#D7E6F4",clipText:"#273C52",clipAccent:"#356F9E",trackHeader:"#E8EDF2",timelineBg:"#F2F5F8",setlistClip:"#E8DDBF",setlistText:"#5B4723"),
        StudioTheme.Gray => Colors(
            "#33373D", "#3F444B", "#484E56", "#30353B", "#69717C", "#F8F9FB", "#D0D5DD", "#BBC4D0",
            "#B9D8D0", "#182B27", "#536B65", "#555C65", "#643C46", "#FFE2E7", "#373C43", "#FF9CAD", "#241C21", "#B4E9C5", "#D4EEE6",
            clip:"#4C6376",clipText:"#F1F5FA",clipAccent:"#AFD8F3",trackHeader:"#393E45",timelineBg:"#30353B",setlistClip:"#6E6450",setlistText:"#FFF0CD"),
        StudioTheme.PastelYellow => Colors(
            "#ECE6CF", "#FCF8E7", "#F5EFD8", "#FFFCEE", "#C5BA92", "#302E25", "#68604A", "#7A6E4E",
            "#D8BC65", "#332B16", "#EAD694", "#F0E4B8", "#F1DAD2", "#8E3541", "#E7DFBF", "#A53E4A", "#FFFFFF", "#416443", "#69551A",
            clip:"#D5E2DF",clipText:"#24423F",clipAccent:"#476B66",trackHeader:"#E7DFBF",timelineBg:"#F3EEDC",setlistClip:"#E5D193",setlistText:"#5A471E"),
        _ => Colors(
            "#1D1E22", "#23252A", "#292B32", "#17181C", "#40424C", "#EFF1F4", "#B2B8C2", "#939DAA",
            "#EC7968", "#231412", "#443039", "#343A42", "#462A33", "#FFB4C1", "#18191D", "#FF8298", "#27191D", "#A1DEB6", "#FFA18F",
            clip:"#344B60",clipText:"#E0EBF5",clipAccent:"#78B2D8",trackHeader:"#22242A",timelineBg:"#181A1F",setlistClip:"#5C5441",setlistText:"#FFF1CD")
    };

    private static IReadOnlyDictionary<string, string> Colors(
        string background, string surface, string panel, string input, string border, string text, string muted,
        string subtle, string accent, string accentText, string selection, string hover, string danger,
        string dangerText, string header, string live, string liveText, string success, string? accentLabel = null,
        string? clip = null,string? clipText = null,string? clipAccent = null,string? trackHeader = null,string? timelineBg = null,
        string? setlistClip = null,string? setlistText = null) => new Dictionary<string, string>
    {
        ["WsBackground"] = background, ["WsSurface"] = surface, ["WsPanel"] = panel,
        ["WsInput"] = input, ["WsBorder"] = border, ["WsText"] = text,
        ["WsMuted"] = muted, ["WsSubtle"] = subtle, ["WsAccent"] = accent,
        ["WsAccentText"] = accentText, ["WsAccentLabel"] = accentLabel ?? accent, ["WsSelection"] = selection, ["WsHover"] = hover,
        ["WsDanger"] = danger, ["WsDangerText"] = dangerText, ["WsHeader"] = header,
        ["WsLive"] = live, ["WsLiveText"] = liveText, ["WsSuccess"] = success, ["WsShadow"] = "#30000000",
        ["WsClip"] = clip ?? selection,["WsClipText"] = clipText ?? text,["WsClipAccent"] = clipAccent ?? accent,
        ["WsTrackHeader"] = trackHeader ?? surface,["WsTimelineBg"] = timelineBg ?? background,
        ["WsSetlistClip"] = setlistClip ?? selection,["WsSetlistText"] = setlistText ?? text
    };
}
