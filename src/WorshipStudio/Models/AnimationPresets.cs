namespace WorshipStudio.Models;

/// <summary>Unit-independent animation primitives. Distances, blur and channel offsets use the font size as their unit.</summary>
public sealed record AnimationProfile
{
    public double X { get; init; }
    public double Y { get; init; }
    public double ScaleX { get; init; } = 1;
    public double ScaleY { get; init; } = 1;
    public double Rotation { get; init; }
    public double Blur { get; init; }
    public double Glow { get; init; }
    public double RgbSplit { get; init; }
    public double SkewX { get; init; }
    public double OpacityStart { get; init; }
    public string Mask { get; init; } = "None";
    public string Motion { get; init; } = "Linear";
    public string? TintColor { get; init; }
    public double TintStrength { get; init; }
}

public sealed record AnimationPreset(
    string Id, string Name, string Category, double Duration, double Stagger,
    string Granularity, string Easing, AnimationProfile Profile)
{
    public string CategoryLabel => Category.ToUpperInvariant();
    public override string ToString() => Name;
}

/// <summary>The 66 choices shown in the supplied references, expressed as editable numerical animation profiles.</summary>
public static class AnimationPresets
{
    private static AnimationPreset P(string id, string name, string category, double duration, double stagger,
        string granularity, string easing, AnimationProfile profile) => new(id, name, category, duration, stagger, granularity, easing, profile);

    public static IReadOnlyList<AnimationPreset> All { get; } = Array.AsReadOnly(new[]
    {
        P("none", "Nenhuma", "Soft", 0, 0, "Text", "Linear", new() { OpacityStart = 1 }),
        P("fade-suave", "Fade Suave", "Soft", .85, .045, "Word", "EaseOut", new()),
        P("pluma-caindo", "Pluma Caindo", "Soft", 1.2, .08, "Character", "EaseInOut", new() { Y = -1.3, Rotation = -.07, Motion = "Feather" }),
        P("fade-down-elegante", "Fade Down Elegante", "Elegant", 1, .075, "Word", "EaseOut", new() { Y = -.7, Blur = .025 }),
        P("fluxo-seda", "Fluxo de Seda", "Elegant", 1.1, .05, "Character", "EaseInOut", new() { X = -.42, Y = .22, SkewX = -.12, Motion = "Silk" }),
        P("arco-gracioso", "Arco Gracioso", "Elegant", 1.05, .07, "Character", "EaseOut", new() { X = -.55, Y = .4, Rotation = -.11, Motion = "Arc" }),
        P("cascata", "Cascata", "Elegant", .75, .095, "Character", "EaseOut", new() { Y = -1.7, ScaleY = .92, Motion = "Cascade" }),
        P("blur-reveal", "Blur Reveal", "Cinematic", 1.05, .055, "Word", "EaseOut", new() { Blur = .28, ScaleX = 1.04, ScaleY = 1.04 }),
        P("cinematografico", "Cinematográfico", "Cinematic", 1.6, .11, "Line", "EaseInOut", new() { Y = .3, ScaleX = 1.14, ScaleY = 1.14, Blur = .12, Motion = "Depth" }),
        P("film-fade", "Film Fade", "Cinematic", 1.35, .04, "Text", "EaseInOut", new() { Y = .055, Blur = .018, Motion = "Film" }),
        P("typewriter-blur", "Typewriter Blur", "Cinematic", .48, .055, "Character", "EaseOut", new() { Blur = .16, Y = .08 }),
        P("typewriter-blur-rapido", "Typewriter Blur Rápido", "Cinematic", .22, .026, "Character", "EaseOut", new() { Blur = .12, Y = .045 }),
        P("typewriter-blur-forte", "Typewriter Blur Forte", "Cinematic", .68, .07, "Character", "EaseOut", new() { Blur = .42, ScaleX = 1.18, ScaleY = 1.18, Y = .12 }),
        P("maquina-escrever", "Máquina de Escrever", "Cinematic", .035, .065, "Character", "Linear", new() { Motion = "Typewriter" }),
        P("emerge-profundidade", "Emerge da Profundidade", "Cinematic", 1.25, .065, "Word", "EaseOut", new() { ScaleX = .32, ScaleY = .32, Y = .45, Blur = .22, Motion = "Depth" }),
        P("focus-pull", "Focus Pull", "Cinematic", 1.35, .085, "Word", "EaseInOut", new() { Blur = .38, ScaleX = .96, ScaleY = .96, Motion = "Focus" }),
        P("horizonte", "Horizonte", "Cinematic", 1.3, .12, "Line", "EaseOut", new() { ScaleY = .12, Blur = .07, Mask = "Up", Y = .3 }),
        P("luz-divina", "Luz Divina", "Holy", 1.5, .09, "Word", "EaseOut", new() { Glow = .85, Blur = .08, TintColor = "#FFF5CF", TintStrength = .65, Motion = "Light" }),
        P("vinda-ceu", "Vinda do Céu", "Holy", 1.45, .1, "Word", "EaseOut", new() { Y = -3.2, ScaleX = .87, ScaleY = .87, Glow = .28, Motion = "Heaven" }),
        P("gloria-flash", "Glória Flash", "Holy", .85, .055, "Word", "EaseOut", new() { ScaleX = 1.32, ScaleY = 1.32, Glow = 1.1, TintColor = "#FFFFFF", TintStrength = .8, Motion = "Flash" }),
        P("pulso-sagrado", "Pulso Sagrado", "Holy", 1.25, .08, "Word", "EaseOut", new() { ScaleX = .84, ScaleY = .84, Glow = .38, Motion = "SacredPulse" }),
        P("slide-esquerda", "Slide da Esquerda", "Modern", .65, .055, "Word", "EaseOut", new() { X = -2.2 }),
        P("slide-direita", "Slide da Direita", "Modern", .65, .055, "Word", "EaseOut", new() { X = 2.2 }),
        P("snap", "Snap", "Modern", .22, .035, "Word", "Back", new() { Y = .24, ScaleX = .94, ScaleY = .94 }),
        P("mask-up", "Mask Up", "Modern", .7, .06, "Word", "EaseOut", new() { Y = .65, Mask = "Up", OpacityStart = 1 }),
        P("mask-down", "Mask Down", "Modern", .7, .06, "Word", "EaseOut", new() { Y = -.65, Mask = "Down", OpacityStart = 1 }),
        P("zoom-power", "Zoom Power", "Energetic", .6, .045, "Word", "Back", new() { ScaleX = .18, ScaleY = .18, Blur = .045 }),
        P("pop-burst", "Pop Burst", "Energetic", .55, .05, "Character", "Back", new() { ScaleX = .25, ScaleY = .25, Rotation = -.15, Motion = "Burst" }),
        P("rotate-in", "Rotate In", "Energetic", .75, .065, "Word", "Back", new() { Rotation = -.65, ScaleX = .6, ScaleY = .6, Y = .25 }),
        P("spring", "Spring", "Youth", 1.05, .06, "Word", "Elastic", new() { ScaleX = .55, ScaleY = .55, Y = .25 }),
        P("wiggle", "Wiggle", "Youth", .95, .055, "Character", "EaseOut", new() { Rotation = -.18, Y = .2, Motion = "Wiggle" }),
        P("impacto", "Impacto", "Dramatic", .6, .075, "Word", "EaseOut", new() { ScaleX = 2.15, ScaleY = 2.15, Blur = .065, Motion = "Impact" }),
        P("subida-dramatica", "Subida Dramática", "Dramatic", 1.1, .14, "Line", "Back", new() { Y = 2.7, ScaleX = .9, ScaleY = .9, Blur = .045 }),
        P("gold-reveal", "Gold Reveal", "Premium", 1.4, .08, "Character", "EaseInOut", new() { Mask = "Left", Glow = .42, TintColor = "#FFD166", TintStrength = .95 }),
        P("luxury-fade", "Luxury Fade", "Premium", 1.8, .13, "Word", "EaseInOut", new() { Y = .18, ScaleX = 1.035, ScaleY = 1.035, Blur = .045, Glow = .1 }),
        P("real", "Real", "Premium", 1.45, .085, "Character", "EaseOut", new() { Y = -.22, ScaleX = .97, ScaleY = 1.09, TintColor = "#F5DEB3", TintStrength = .45, Motion = "Royal" }),
        P("tender-rise", "Tender Rise", "Emotional", 1.4, .095, "Word", "EaseInOut", new() { Y = .62, Blur = .025, Motion = "Tender" }),
        P("heart-pulse", "Heart Pulse", "Emotional", 1.35, .1, "Word", "EaseOut", new() { ScaleX = .88, ScaleY = .88, Motion = "Heart" }),
        P("soul-rise", "Soul Rise", "Emotional", 1.7, .12, "Line", "EaseInOut", new() { Y = 1.1, Blur = .08, Glow = .17, Motion = "Soul" }),
        P("abraco-gentil", "Abraço Gentil", "Emotional", 1.35, .07, "Word", "EaseOut", new() { X = .38, ScaleX = .91, ScaleY = .96, Motion = "Embrace" }),
        P("nevoa", "Névoa", "Ambient", 1.7, .065, "Character", "EaseInOut", new() { Blur = .34, X = -.18, Glow = .13, Motion = "Mist" }),
        P("mare", "Maré", "Ambient", 1.4, .075, "Word", "EaseInOut", new() { Y = .42, X = .15, Rotation = -.025, Motion = "Tide" }),
        P("glow-emerge", "Glow Emerge", "Ambient", 1.4, .07, "Word", "EaseOut", new() { Y = .32, Blur = .095, Glow = .62, ScaleX = .94, ScaleY = .94 }),
        P("ressurreicao", "Ressurreição", "Holy", 1.45, .11, "Word", "EaseOut", new() { Y = 2.1, Blur = .14, Glow = .54, Motion = "Resurrection" }),
        P("gloria-eterna", "Glória Eterna", "Holy", 1.75, .13, "Line", "EaseInOut", new() { ScaleX = 1.2, ScaleY = 1.2, Glow = .72, Blur = .06, Motion = "Glory" }),
        P("espirito-vivo", "Espírito Vivo", "Holy", 1.25, .065, "Character", "EaseOut", new() { X = -.35, Y = .5, Glow = .3, RgbSplit = .018, Motion = "Spirit" }),
        P("vento-fe", "Vento de Fé", "Holy", 1.1, .055, "Character", "EaseOut", new() { X = -2.6, Rotation = -.08, SkewX = -.18, Blur = .04, Motion = "Wind" }),
        P("coracao-ardente", "Coração Ardente", "Holy", 1.3, .075, "Word", "EaseOut", new() { ScaleX = .86, ScaleY = .86, Glow = .7, TintColor = "#FF914D", TintStrength = .85, Motion = "Heart" }),
        P("florescer-sagrado", "Florescer Sagrado", "Holy", 1.4, .07, "Character", "Back", new() { ScaleX = .05, ScaleY = .32, Rotation = -.09, Glow = .24, Motion = "Bloom" }),
        P("ascensao", "Ascensão", "Holy", 1.65, .1, "Word", "EaseInOut", new() { Y = 2.8, ScaleX = .76, ScaleY = .9, Glow = .32, Mask = "Up", Motion = "Ascend" }),
        P("templo", "Templo", "Holy", 1.5, .15, "Line", "EaseOut", new() { ScaleX = 1.22, ScaleY = .7, Y = .45, Glow = .19, Motion = "Temple" }),
        P("mana-ceus", "Maná dos Céus", "Holy", 1.25, .085, "Character", "Bounce", new() { Y = -2.2, Rotation = .065, Glow = .22, Motion = "Manna" }),
        P("shofar", "Shofar", "Holy", 1.15, .08, "Word", "EaseOut", new() { ScaleX = .68, ScaleY = .68, Glow = .47, Motion = "Shofar" }),
        P("luz-cruz", "Luz da Cruz", "Holy", 1.45, .08, "Word", "EaseOut", new() { Mask = "Left", ScaleX = .96, ScaleY = 1.04, Glow = .95, TintColor = "#FFF9E5", TintStrength = .75, Motion = "Cross" }),
        P("pomba-descendo", "Pomba Descendo", "Holy", 1.65, .1, "Word", "EaseInOut", new() { Y = -2.5, X = -.18, Rotation = -.08, Glow = .28, Motion = "Dove" }),
        P("jelly-pop", "Jelly Pop", "Modern", 1, .045, "Character", "Elastic", new() { ScaleX = .45, ScaleY = 1.55, Motion = "Jelly" }),
        P("carimbo", "Carimbo", "Modern", .45, .07, "Word", "Back", new() { ScaleX = 1.55, ScaleY = 1.55, Rotation = -.14, Motion = "Stamp" }),
        P("rgb-split", "RGB Split", "Modern", .85, .055, "Word", "EaseOut", new() { RgbSplit = .2, Blur = .025, X = -.15, Motion = "Rgb" }),
        P("kinetic", "Kinetic", "Modern", .7, .07, "Word", "Back", new() { X = -1.25, Y = .65, Rotation = -.13, SkewX = -.16, Motion = "Kinetic" }),
        P("ig-pop", "IG Pop", "Modern", .5, .045, "Word", "Back", new() { ScaleX = .6, ScaleY = .6, Y = .22, Motion = "Social" }),
        P("squeeze", "Squeeze", "Modern", .7, .055, "Character", "Back", new() { ScaleX = .15, ScaleY = 1.85, SkewX = .04 }),
        P("tilt", "Tilt", "Modern", .7, .055, "Word", "EaseOut", new() { Rotation = .3, SkewX = .2, Y = .25, ScaleX = .94, ScaleY = .94 }),
        P("punch", "Punch", "Modern", .5, .055, "Word", "EaseOut", new() { ScaleX = 1.65, ScaleY = 1.22, X = -.35, Motion = "Punch" }),
        P("flicker", "Flicker", "Modern", .9, .06, "Character", "Linear", new() { Glow = .36, RgbSplit = .025, Motion = "Flicker" }),
        P("mirror-flip", "Mirror Flip", "Modern", .9, .07, "Word", "EaseOut", new() { ScaleX = -1, ScaleY = 1, Rotation = .035, Blur = .025, Motion = "Flip" }),
        P("spring-bounce", "Spring Bounce", "Modern", 1.2, .07, "Character", "Bounce", new() { Y = -1.6, ScaleX = .7, ScaleY = 1.35, Motion = "SpringBounce" })
    });

    private static readonly IReadOnlyDictionary<string, AnimationPreset> Lookup =
        All.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

    public static AnimationPreset Find(string? id) => id != null && Lookup.TryGetValue(id, out var preset) ? preset : All[0];
    public static bool Contains(string? id) => id != null && Lookup.ContainsKey(id);

    /// <summary>Applies the preset's initial timing and segmentation without changing typography, color, media or background.</summary>
    public static StageStyle ApplyDefaults(StageStyle style, AnimationPreset preset) => style with
    {
        UsePresetAnimation = true,
        AnimationPreset = preset.Id,
        AnimationGranularity = preset.Granularity,
        AnimationDuration = preset.Duration,
        AnimationStagger = preset.Stagger,
        AnimationDelay = 0,
        AnimationOrder = "Forward",
        AnimationEasing = preset.Easing,
        AnimationStrength = 1,
        AnimationLoop = false,
        AnimationLoopPause = 1,
        LyricAnimation = "None"
    };
}
