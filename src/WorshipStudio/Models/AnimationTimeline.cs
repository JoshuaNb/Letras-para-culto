namespace WorshipStudio.Models;

public sealed record AnimationPose
{
    public double Opacity { get; init; } = 1;
    public double X { get; init; }
    public double Y { get; init; }
    public double ScaleX { get; init; } = 1;
    public double ScaleY { get; init; } = 1;
    public double Rotation { get; init; }
    public double Blur { get; init; }
    public double Glow { get; init; }
    public double RgbSplit { get; init; }
    public double SkewX { get; init; }
    public string Mask { get; init; } = "None";
    public double MaskProgress { get; init; } = 1;
    public string? TintColor { get; init; }
    public double TintStrength { get; init; }
    public static AnimationPose Identity { get; } = new();
}

/// <summary>Pure, deterministic animation timing shared by the live stage, previews and MP4 frame capture.</summary>
public static class AnimationTimeline
{
    private static double Finite(double value, double fallback = 0) => double.IsFinite(value) ? value : fallback;
    public static double TotalDuration(StageStyle style, int unitCount)
    {
        if (unitCount <= 0 || AnimationPresets.Find(style.AnimationPreset).Id == "none") return 0;
        return Math.Max(0, Finite(style.AnimationDelay)) + Math.Max(0, Finite(style.AnimationDuration)) +
               Math.Max(0, Finite(style.AnimationStagger)) * Math.Max(0, unitCount - 1);
    }

    /// <summary>Maps a visual unit to its temporal rank. Random is a fixed bijection, so every unit starts exactly once and export is reproducible.</summary>
    public static int OrderIndex(int index, int count, string? order)
    {
        if (count <= 1) return 0;
        index = Math.Clamp(index, 0, count - 1);
        switch (order)
        {
            case "Reverse": return count - 1 - index;
            case "Center":
                var center = (count - 1) / 2d;
                return (int)Math.Round(Math.Abs(index - center) * 2) - (index < center ? 1 : 0);
            case "Random":
                var multiplier = 1664525 % count;
                if (multiplier < 1) multiplier = 1;
                while (Gcd(multiplier, count) != 1) multiplier = (multiplier + 1) % count == 0 ? 1 : (multiplier + 1) % count;
                return (int)(((long)index * multiplier + 1013904223L % count) % count);
            default: return index;
        }
    }

    private static int Gcd(int a, int b) { while (b != 0) { var next = a % b; a = b; b = next; } return a; }

    /// <summary>Returns raw unit progress, including initial delay, sequential delay and optional whole-cue looping.</summary>
    public static double Progress(StageStyle style, double elapsed, int unitIndex, int unitCount)
    {
        if (unitCount <= 0 || AnimationPresets.Find(style.AnimationPreset).Id == "none") return 1;
        elapsed = Math.Max(0, Finite(elapsed));
        var total = TotalDuration(style, unitCount);
        if (style.AnimationLoop && total > 0)
            elapsed %= total + Math.Max(0, Finite(style.AnimationLoopPause));
        var start = Math.Max(0, Finite(style.AnimationDelay)) +
                    OrderIndex(unitIndex, unitCount, style.AnimationOrder) * Math.Max(0, Finite(style.AnimationStagger));
        if (elapsed < start) return 0;
        var duration = Math.Max(0, Finite(style.AnimationDuration));
        return duration <= 0 ? 1 : Math.Clamp((elapsed - start) / duration, 0, 1);
    }

    public static double Ease(double progress, string? easing)
    {
        var p = Math.Clamp(Finite(progress), 0, 1);
        if (p is 0 or 1) return p;
        return easing switch
        {
            "Linear" => p,
            "EaseIn" => p * p * p,
            "EaseInOut" => p < .5 ? 4 * p * p * p : 1 - Math.Pow(-2 * p + 2, 3) / 2,
            "Back" => 1 + 2.70158 * Math.Pow(p - 1, 3) + 1.70158 * Math.Pow(p - 1, 2),
            "Elastic" => 1 + Math.Pow(2, -10 * p) * Math.Sin((p * 10 - .75) * Math.PI * 2 / 3),
            "Bounce" => BounceOut(p),
            _ => 1 - Math.Pow(1 - p, 3)
        };
    }

    private static double BounceOut(double p)
    {
        const double n = 7.5625, d = 2.75;
        if (p < 1 / d) return n * p * p;
        if (p < 2 / d) { p -= 1.5 / d; return n * p * p + .75; }
        if (p < 2.5 / d) { p -= 2.25 / d; return n * p * p + .9375; }
        p -= 2.625 / d; return n * p * p + .984375;
    }

    /// <summary>Evaluates raw progress. Easing overshoot remains in transforms; all entries finish at the exact unmodified lyric pose.</summary>
    public static AnimationPose Evaluate(AnimationPreset preset, double progress, double strength = 1,
        int unitIndex = 0, string? easingOverride = null)
    {
        if (preset.Id == "none" || progress >= 1) return AnimationPose.Identity;
        var p = Math.Clamp(Finite(progress), 0, 1);
        var eased = Ease(p, easingOverride ?? preset.Easing);
        var remaining = 1 - eased;
        var visible = Math.Clamp(eased, 0, 1);
        var envelope = Math.Sin(Math.PI * p) * (1 - p);
        var s = Math.Clamp(Finite(strength, 1), 0, 3);
        var a = preset.Profile;
        var x = a.X * remaining * s;
        var y = a.Y * remaining * s;
        var sx = 1 + (a.ScaleX - 1) * remaining * s;
        var sy = 1 + (a.ScaleY - 1) * remaining * s;
        var rotation = a.Rotation * remaining * s;
        var opacity = a.OpacityStart + (1 - a.OpacityStart) * visible;
        var blur = a.Blur * Math.Max(0, remaining) * s;
        var glow = a.Glow * Math.Max(0, 1 - p) * (.45 + .55 * Math.Sin(Math.PI * p)) * s;
        var split = a.RgbSplit * Math.Max(0, remaining) * s;
        var phase = unitIndex * .73;
        switch (a.Motion)
        {
            case "Feather": x += Math.Sin(p * Math.PI * 2 + phase) * envelope * .4 * s; rotation += Math.Sin(p * Math.PI * 3 + phase) * envelope * .12 * s; break;
            case "Silk": y += Math.Sin(p * Math.PI * 2 + phase) * envelope * .24 * s; x += Math.Sin(p * Math.PI + phase) * envelope * .2 * s; break;
            case "Arc": y -= Math.Sin(p * Math.PI) * .65 * s; rotation += Math.Sin(p * Math.PI) * .09 * s; break;
            case "Cascade": y += Math.Sin(p * Math.PI * 2) * envelope * .14 * s; break;
            case "Depth": sx += envelope * .11 * s; sy += envelope * .11 * s; break;
            case "Film": opacity *= .95 + .05 * Math.Sin(p * Math.PI * 7); break;
            case "Typewriter": opacity = p > 0 ? 1 : 0; break;
            case "Focus": blur += Math.Sin(p * Math.PI) * .08 * s; break;
            case "Light": glow += envelope * .55 * s; break;
            case "Heaven": x += Math.Sin(p * Math.PI) * envelope * .18 * s; break;
            case "Flash": glow += Math.Exp(-Math.Pow((p - .35) * 8, 2)) * .9 * s; break;
            case "SacredPulse": sx += Math.Sin(p * Math.PI * 4) * envelope * .16 * s; sy += Math.Sin(p * Math.PI * 4) * envelope * .16 * s; break;
            case "Burst": rotation += Math.Sin(p * Math.PI) * envelope * .23 * s; break;
            case "Wiggle": rotation += Math.Sin(p * Math.PI * 9 + phase) * (1 - p) * .14 * s; break;
            case "Impact": x += Math.Sin(p * Math.PI * 17) * envelope * .065 * s; y += Math.Sin(p * Math.PI * 13) * envelope * .04 * s; break;
            case "Royal": y += Math.Sin(p * Math.PI) * envelope * .09 * s; glow += envelope * .12 * s; break;
            case "Tender": y += Math.Sin(p * Math.PI) * envelope * .12 * s; break;
            case "Heart": var heart = (Math.Exp(-Math.Pow((p - .24) * 16, 2)) + .65 * Math.Exp(-Math.Pow((p - .43) * 18, 2))) * .14 * s; sx += heart; sy += heart; break;
            case "Soul": x += Math.Sin(p * Math.PI * 2 + phase) * envelope * .12 * s; break;
            case "Embrace": x *= Math.Cos(unitIndex * Math.PI); sx += envelope * .035 * s; break;
            case "Mist": x += Math.Sin(p * Math.PI * 2 + phase) * envelope * .3 * s; blur += envelope * .1 * s; break;
            case "Tide": y += Math.Sin(p * Math.PI * 3 + phase) * envelope * .35 * s; rotation += Math.Sin(p * Math.PI * 2) * envelope * .035 * s; break;
            case "Resurrection": y -= Math.Sin(p * Math.PI) * envelope * .35 * s; glow += envelope * .28 * s; break;
            case "Glory": glow += envelope * .32 * s; sx += envelope * .045 * s; sy += envelope * .045 * s; break;
            case "Spirit": x += Math.Sin(p * Math.PI * 4 + phase) * envelope * .22 * s; y -= Math.Sin(p * Math.PI) * envelope * .12 * s; break;
            case "Wind": x += Math.Sin(p * Math.PI * 2 + phase) * envelope * .35 * s; y += Math.Cos(p * Math.PI * 2 + phase) * envelope * .11 * s; break;
            case "Bloom": sx += Math.Sin(p * Math.PI) * envelope * .16 * s; rotation += Math.Sin(p * Math.PI) * envelope * .08 * s; break;
            case "Ascend": x += Math.Sin(p * Math.PI + phase) * envelope * .13 * s; break;
            case "Temple": sx += envelope * .07 * s; sy -= envelope * .04 * s; break;
            case "Manna": x += Math.Sin(p * Math.PI * 2 + phase) * envelope * .16 * s; break;
            case "Shofar": var wave = Math.Sin(p * Math.PI * 5) * envelope * .11 * s; sx += wave; sy += wave; glow += Math.Abs(wave) * 2; break;
            case "Cross": glow += Math.Exp(-Math.Pow((p - .5) * 7, 2)) * .55 * s; break;
            case "Dove": x += Math.Sin(p * Math.PI * 3 + phase) * envelope * .35 * s; rotation += Math.Sin(p * Math.PI * 4) * envelope * .07 * s; break;
            case "Jelly": var jelly = Math.Sin(p * Math.PI * 7) * envelope * .2 * s; sx += jelly; sy -= jelly; break;
            case "Stamp": rotation += Math.Sin(p * Math.PI * 9) * envelope * .055 * s; break;
            case "Rgb": split += Math.Abs(Math.Sin(p * Math.PI * 6)) * envelope * .07 * s; break;
            case "Kinetic": y += Math.Sin(p * Math.PI * 2) * envelope * .2 * s; rotation += Math.Sin(p * Math.PI) * envelope * .1 * s; break;
            case "Social": sx += envelope * .18 * s; sy += envelope * .12 * s; break;
            case "Punch": x += Math.Sin(p * Math.PI * 9) * envelope * .09 * s; sx += envelope * .16 * s; break;
            case "Flicker": opacity *= .35 + .65 * Math.Abs(Math.Sin(p * Math.PI * 15 + .2)); glow += envelope * .2 * s; break;
            case "Flip": sx += Math.Sin(p * Math.PI) * envelope * .12 * s; break;
            case "SpringBounce": sx += Math.Sin(p * Math.PI * 5) * envelope * .18 * s; sy -= Math.Sin(p * Math.PI * 5) * envelope * .2 * s; break;
        }
        // No preset may leak previous-cue glyphs at the first sample, including oscillatory profiles.
        if (p <= 0 && a.OpacityStart <= 0) opacity = 0;
        return new AnimationPose
        {
            Opacity = Math.Clamp(opacity, 0, 1), X = x, Y = y,
            ScaleX = Math.Clamp(sx, -8, 8), ScaleY = Math.Clamp(sy, -8, 8), Rotation = rotation,
            Blur = Math.Max(0, blur), Glow = Math.Clamp(glow, 0, 3), RgbSplit = Math.Max(0, split),
            SkewX = a.SkewX * remaining * s, Mask = a.Mask, MaskProgress = visible,
            TintColor = a.TintColor, TintStrength = Math.Clamp(a.TintStrength * Math.Max(0, remaining) * s, 0, 1)
        };
    }
}
