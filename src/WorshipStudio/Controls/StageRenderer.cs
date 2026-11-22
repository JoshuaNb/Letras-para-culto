using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using WorshipStudio.Models;

namespace WorshipStudio.Controls;

/// <summary>
/// One drawing path for the operator preview, the independent output and offline rendering.
/// Avalonia composes these primitives with its configured Skia GPU backend.
/// </summary>
public sealed class StageRenderer : Control, IDisposable
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer;
    private StageFrame _frame = new();
    private string _previousText = "";
    private string _text = "";
    private double _changedAt;
    private string? _imagePath;
    private Bitmap? _image;
    private double? _fixedTime;
    private double? _fixedTransition;
    private bool _disposed;
    private readonly Dictionary<TextCacheKey, TextLayout> _textCache = new();
    private readonly Dictionary<(string Family, bool Bold), Typeface> _typefaces = new();
    private static readonly Regex WordPattern = new(@"\S+\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly record struct TextCacheKey(string Text, string Family, object? AssetKey,
        FontStyle Style, FontWeight Weight, FontStretch Stretch, double Size, double Width, Color Color);

    /// <summary>True only for the transparent native video overlay.</summary>
    public bool VideoOverlay { get; set; }
    public StageFrame Frame => _frame;
    public string? MediaError { get; private set; }
    public event Action<string>? Error;

    public StageRenderer()
    {
        ClipToBounds = true;
        Focusable = false;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16.666), DispatcherPriority.Render,
            (_, _) => { if (IsVisible && !_disposed) InvalidateVisual(); });
        AttachedToVisualTree += (_, _) => _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    public void Update(StageFrame frame)
    {
        if (_disposed) return;
        if (_frame.Style.FontFamily != frame.Style.FontFamily || _frame.Style.Bold != frame.Style.Bold ||
            _frame.Style.FontSize != frame.Style.FontSize || _frame.Style.TextColor != frame.Style.TextColor)
            ClearTextCache();
        if (!string.Equals(_text, frame.Text, StringComparison.Ordinal) || frame.Revision != _frame.Revision)
        {
            _previousText = string.IsNullOrEmpty(frame.PreviousText) ? _text : frame.PreviousText;
            _text = frame.Text ?? "";
            _changedAt = _clock.Elapsed.TotalSeconds;
        }
        _frame = frame;
        LoadImage(frame.Style.BackgroundMode == "Image" ? frame.Style.BackgroundPath : null);
        InvalidateVisual();
    }

    public void SetFrame(StageFrame frame) => Update(frame);
    public void RenderAt(double seconds) => SetRenderTime(seconds);
    public void ReplayTransition()
    {
        _previousText = "";
        _changedAt = _clock.Elapsed.TotalSeconds;
        InvalidateVisual();
    }

    private void LoadImage(string? path)
    {
        if (_imagePath == path) return;
        _imagePath = path;
        _image?.Dispose();
        _image = null;
        MediaError = null;
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            // Decode from the closed stream so removable media are not kept locked.
            using var stream = File.OpenRead(path);
            _image = Bitmap.DecodeToWidth(stream, 3840);
        }
        catch (Exception ex)
        {
            MediaError = "Não foi possível abrir a imagem: " + ex.Message;
            Error?.Invoke(MediaError);
        }
    }

    /// <summary>Deterministic sampling for actual MP4 frames; call on the UI thread.</summary>
    public void SetRenderTime(double? seconds, double? transitionProgress = null)
    {
        _fixedTime = seconds;
        _fixedTransition = transitionProgress;
        InvalidateVisual();
    }

    public RenderTargetBitmap Capture(int width, int height, double seconds = 0, double transitionProgress = 1)
    {
        var oldSize = Bounds.Size;
        var oldTime = _fixedTime;
        var oldTransition = _fixedTransition;
        SetRenderTime(seconds, transitionProgress);
        Measure(new Size(width, height));
        Arrange(new Rect(0, 0, width, height));
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(this);
        _fixedTime = oldTime;
        _fixedTransition = oldTransition;
        if (oldSize.Width > 0 && oldSize.Height > 0) Arrange(new Rect(oldSize));
        return bitmap;
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 1 || Bounds.Height <= 1) return;
        // Dispose between frames, never while a drawing operation still uses a cached layout.
        if (_textCache.Count > 220) ClearTextCache();
        var rect = new Rect(Bounds.Size);
        using var clip = context.PushClip(rect);
        if (_frame.Blackout)
        {
            context.FillRectangle(Brushes.Black, rect);
            return;
        }

        var style = _frame.Style;
        var time = (_fixedTime ?? _clock.Elapsed.TotalSeconds) * Math.Clamp(style.MotionSpeed, 0, 5);
        var audio = Math.Clamp(_frame.AudioLevel, 0, 1);
        if (!VideoOverlay)
        {
            context.FillRectangle(Brush(style.BackgroundColor, "#071320"), rect);
            switch (style.BackgroundMode)
            {
                case "Image":
                    DrawImage(context, rect);
                    break;
                case "Tunnel":
                    DrawTunnel(context, rect, style, time, audio);
                    break;
                case "Particles":
                    DrawParticles(context, rect, style, time, audio);
                    break;
                case "Aurora":
                    DrawAurora(context, rect, style, time, audio);
                    break;
                case "Video":
                    // Native GPU video is rendered underneath by ProjectionWindow.
                    DrawAurora(context, rect, style, time, audio);
                    break;
            }
        }
        if (style.Dim > 0)
            context.FillRectangle(new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(style.Dim, 0, .95) * 255), 0, 0, 0)), rect);
        if (_frame.HideLyrics || string.IsNullOrWhiteSpace(_text)) return;

        var progress = _fixedTransition ?? (style.TransitionSeconds <= 0 ? 1 :
            Math.Clamp((_clock.Elapsed.TotalSeconds - _changedAt) / style.TransitionSeconds, 0, 1));
        progress = Math.Clamp(progress, 0, 1);
        var eased = EaseOut(progress);
        var transition = style.Transition;
        var lyricTime = (_fixedTime ?? _clock.Elapsed.TotalSeconds) * Math.Clamp(style.LyricAnimationSpeed, .1, 4);
        if (progress < 1 && transition is not ("Cut" or "Typewriter" or "Words" or "Lines") && _previousText.Length > 0)
            DrawLyrics(context, rect, _previousText, style, 1 - eased, 0,
                transition == "Slide" ? -eased * rect.Height * .08 : 0, 1, 0, audio, lyricTime);

        var x = transition switch { "SlideLeft" => -(1-eased)*rect.Width*.11, "SlideRight" => (1-eased)*rect.Width*.11, _ => 0 };
        var y = transition switch { "Slide" => (1-eased)*rect.Height*.08, "SlideDown" => -(1-eased)*rect.Height*.08, _ => 0 };
        var scale = transition switch
        {
            "Zoom" => .8 + .2 * eased,
            "Pop" => .5 + .5 * EaseOutBack(progress),
            "Rotate" => .85 + .15 * eased,
            _ => 1
        };
        var rotation = transition == "Rotate" ? -(1-eased)*.16 : 0;
        var opacity = transition is "Cut" or "Typewriter" or "Words" or "Lines" or "Wipe" ? 1 : eased;
        if (transition == "Wipe" && progress < 1)
        {
            using var reveal = context.PushClip(new Rect(0, 0, rect.Width * eased, rect.Height));
            DrawLyrics(context, rect, _text, style, opacity, x, y, scale, rotation, audio, lyricTime);
        }
        else
        {
            DrawLyrics(context, rect, _text, style, opacity, x, y, scale, rotation, audio, lyricTime,
                transition is "Typewriter" or "Words" ? RevealLength(_text, progress, transition == "Words") : null,
                transition == "Lines" ? progress : 1);
        }
    }

    private static double EaseOut(double value) => 1 - Math.Pow(1-value, 3);
    private static double EaseOutBack(double value)
    {
        const double c1=1.70158;
        var shifted=value-1;
        return 1+(c1+1)*shifted*shifted*shifted+c1*shifted*shifted;
    }

    private static int RevealLength(string text, double progress, bool words)
    {
        if (progress <= 0) return 0;
        if (progress >= 1) return text.Length;
        if (words)
        {
            var matches=WordPattern.Matches(text);
            var count=Math.Clamp((int)Math.Ceiling(matches.Count*progress),0,matches.Count);
            return count==0?0:matches[count-1].Index+matches[count-1].Length;
        }
        var elements=StringInfo.ParseCombiningCharacters(text);
        var revealed=Math.Clamp((int)Math.Ceiling(elements.Length*progress),0,elements.Length);
        return revealed==elements.Length?text.Length:elements[revealed];
    }

    private void DrawImage(DrawingContext context, Rect rect)
    {
        if (_image == null) return;
        var scale = Math.Max(rect.Width / _image.Size.Width, rect.Height / _image.Size.Height);
        var w = _image.Size.Width * scale;
        var h = _image.Size.Height * scale;
        context.DrawImage(_image, new Rect(_image.Size), new Rect((rect.Width - w) / 2, (rect.Height - h) / 2, w, h));
    }

    private static void DrawAurora(DrawingContext context, Rect rect, StageStyle style, double time, double audio)
    {
        var accent = ColorOr(style.AccentColor, "#46DEC1");
        var purple = Color.Parse("#6C4BBD");
        for (var i = 0; i < 5; i++)
        {
            var phase = i * 1.73;
            var center = new Point(rect.Width * (.12 + i * .22 + Math.Sin(time * .11 + phase) * .16),
                rect.Height * (.5 + Math.Cos(time * .13 + phase) * .33));
            var color = i % 2 == 0 ? accent : purple;
            var brush = new RadialGradientBrush
            {
                Center = new RelativePoint(.5, .5, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(.5, .5, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(.5, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(.5, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new(Color.FromArgb((byte)(70 + audio * 65), color.R, color.G, color.B), 0),
                    new(Color.FromArgb(0, color.R, color.G, color.B), 1)
                }
            };
            context.DrawEllipse(brush, null, center, rect.Width * (.42 + audio * .06), rect.Height * .75);
        }
        // Undulating curtains, drawn with GPU-composited primitives.
        for (var strip = 0; strip < 22; strip++)
        {
            var x = rect.Width * strip / 21;
            var top = rect.Height * (.16 + .12 * Math.Sin(strip * .25 + time * .15));
            var bottom = rect.Height * (.85 + .09 * Math.Cos(strip * .17 + time * .12));
            var c = Color.FromArgb((byte)(9 + audio * 12), accent.R, accent.G, accent.B);
            context.DrawLine(new Pen(new SolidColorBrush(c), Math.Max(1, rect.Width / 65)), new Point(x, top), new Point(x + rect.Width * .12, bottom));
        }
    }

    /// <summary>True perspective projection of moving 3D points and rings.</summary>
    private static void DrawTunnel(DrawingContext context, Rect rect, StageStyle style, double time, double audio)
    {
        var accent = ColorOr(style.AccentColor, "#46DEC1");
        var center = new Point(rect.Width / 2, rect.Height / 2);
        var focal = Math.Max(rect.Width, rect.Height) * .5;
        var shift = time * (.65 + audio * 1.4);
        const int ringCount = 22;
        const int sides = 8;
        for (var ring = ringCount - 1; ring >= 0; ring--)
        {
            var z = 1.2 + ((ring + shift) % ringCount);
            var rotation = time * .055 + z * .045;
            var radius = 2.8 + audio * .22;
            var alpha = (byte)Math.Clamp(210 * (1 - z / (ringCount + 1)), 12, 210);
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, accent.R, accent.G, accent.B)),
                Math.Max(.6, rect.Width / 1920 * 1.8));
            for (var edge = 0; edge < sides; edge++)
            {
                var angle1 = edge * Math.PI * 2 / sides + rotation;
                var angle2 = (edge + 1) * Math.PI * 2 / sides + rotation;
                var p1 = new Point(center.X + Math.Cos(angle1) * radius * focal / z,
                    center.Y + Math.Sin(angle1) * radius * focal / z);
                var p2 = new Point(center.X + Math.Cos(angle2) * radius * focal / z,
                    center.Y + Math.Sin(angle2) * radius * focal / z);
                context.DrawLine(pen, p1, p2);
                if (ring % 2 == 0)
                {
                    var nextZ = z + 1;
                    var p3 = new Point(center.X + Math.Cos(angle1 + .045) * radius * focal / nextZ,
                        center.Y + Math.Sin(angle1 + .045) * radius * focal / nextZ);
                    context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb((byte)(alpha / 3), accent.R, accent.G, accent.B)), .65), p1, p3);
                }
            }
        }
    }

    private static void DrawParticles(DrawingContext context, Rect rect, StageStyle style, double time, double audio)
    {
        var accent = ColorOr(style.AccentColor, "#46DEC1");
        var focal = Math.Max(rect.Width, rect.Height) * .5;
        var yaw = time * .035;
        for (var i = 0; i < 160; i++)
        {
            // Fixed hash seeds keep each frame deterministic without allocating Random objects.
            var seedX = Hash(i * 3 + 1) * 12 - 6;
            var seedY = Hash(i * 3 + 2) * 8 - 4;
            var z = .7 + ((Hash(i * 3 + 3) * 22 - time * (.5 + audio) % 22 + 22) % 22);
            var x = seedX * Math.Cos(yaw) - seedY * Math.Sin(yaw);
            var y = seedX * Math.Sin(yaw) + seedY * Math.Cos(yaw);
            var px = rect.Width / 2 + x * focal / z;
            var py = rect.Height / 2 + y * focal / z;
            if (px < -20 || py < -20 || px > rect.Width + 20 || py > rect.Height + 20) continue;
            var r = Math.Clamp((1.5 + audio * 2.5) * focal / z / 120, .6, 14);
            var alpha = (byte)Math.Clamp(245 * (1 - z / 25), 10, 245);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(alpha / 6), accent.R, accent.G, accent.B)), null,
                new Point(px, py), r * 3.5, r * 3.5);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(alpha, accent.R, accent.G, accent.B)), null,
                new Point(px, py), r, r);
        }
    }

    private static double Hash(int seed)
    {
        var n = unchecked((uint)(seed * 374761393 + 668265263));
        n = (n ^ (n >> 13)) * 1274126177;
        return (n ^ (n >> 16)) / (double)uint.MaxValue;
    }

    private void DrawLyrics(DrawingContext context, Rect rect, string value, StageStyle style,
        double opacity, double offsetX, double offsetY, double scale, double rotation, double audio,
        double seconds, int? revealLength = null, double lineProgress = 1)
    {
        if (string.IsNullOrWhiteSpace(value) || opacity <= .001 || revealLength == 0 || lineProgress <= 0) return;
        var maxWidth = rect.Width * .84;
        var fontSize = Math.Clamp(style.FontSize, 20, 160) * rect.Height / 1080;
        var typeface = ResolveTypeface(style);
        var text = GetText(value, typeface, fontSize, maxWidth, style.TextColor);
        // Keep complete stanzas inside the safe area rather than clipping long lyrics.
        if (text.Height > rect.Height * .72 || text.Width > maxWidth)
        {
            var low = 1d;
            var high = fontSize;
            for (var pass = 0; pass < 11; pass++)
            {
                var candidate = (low + high) / 2;
                var measured = GetText(value, typeface, candidate, maxWidth, style.TextColor);
                if (measured.Height <= rect.Height * .72 && measured.Width <= maxWidth) low = candidate;
                else high = candidate;
            }
            fontSize = low;
            text = GetText(value, typeface, fontSize, maxWidth, style.TextColor);
        }
        var shadow = GetText(value, typeface, fontSize, maxWidth, "#B0000000");
        var x = (rect.Width - maxWidth) / 2 + offsetX;
        var y = Math.Clamp(Math.Clamp(style.TextY, .15, .85) * rect.Height - text.Height / 2,
            rect.Height * .06, Math.Max(rect.Height * .06, rect.Height * .94 - text.Height)) + offsetY;
        var intensity = Math.Clamp(style.LyricAnimationIntensity, 0, 1);
        var phase=seconds*Math.PI;
        switch(style.LyricAnimation)
        {
            case "Float": y+=Math.Sin(phase*.7)*rect.Height*.022*intensity; break;
            case "Pulse": scale*=1+Math.Sin(phase*1.5)*.045*intensity; break;
            case "Breathe":
                scale*=1+Math.Sin(phase*.55)*.018*intensity;
                opacity*=1-(.2-.2*Math.Sin(phase*.55))*intensity;
                break;
            case "Sway": rotation+=Math.Sin(phase*.65)*.035*intensity; break;
            case "Bounce": y-=Math.Abs(Math.Sin(phase*1.1))*rect.Height*.045*intensity; break;
            case "Shake":
                x+=(Math.Sin(phase*13)+Math.Sin(phase*21.7))*.5*rect.Width*.004*intensity;
                y+=Math.Sin(phase*17.2)*rect.Height*.006*intensity;
                break;
        }
        using var opacityState = context.PushOpacity(Math.Clamp(opacity, 0, 1));
        var animatedScale = scale * (1 + audio * .015);
        var anchor=new Point(rect.Width/2, y+text.Height/2);
        using var transform = context.PushTransform(Matrix.CreateTranslation(-anchor.X, -anchor.Y) *
            Matrix.CreateScale(animatedScale, animatedScale) * Matrix.CreateRotation(rotation) * Matrix.CreateTranslation(anchor.X, anchor.Y));
        var shadowOffset = Math.Max(1, rect.Height / 360);
        var revealRects = revealLength is { } length && length<value.Length ? text.HitTestTextRange(0,length).ToArray() : null;
        var top=0d;
        for(var index=0;index<text.TextLines.Count;index++)
        {
            var line=text.TextLines[index];
            var lineY=y+top;
            if(style.LyricAnimation=="Wave")lineY+=Math.Sin(phase+index*.95)*fontSize*.24*intensity;
            var stagger=lineProgress>=1?1:Math.Clamp((lineProgress-index*.6/Math.Max(1,text.TextLines.Count))/.4,0,1);
            var lineOpacity=EaseOut(stagger);
            var entranceOffset=(1-lineOpacity)*fontSize*.75;
            if(lineOpacity>.001)
            {
                using var lineFade=context.PushOpacity(lineOpacity);
                if(revealRects==null)DrawLine();
                else foreach(var region in revealRects)
                {
                    if(region.Bottom <= top+.01 || region.Top >= top+line.Height-.01)continue;
                    using var revealClip=context.PushClip(new Rect(x+region.X-1,lineY+entranceOffset-1,region.Width+shadowOffset+2,line.Height+shadowOffset+2));
                    DrawLine();
                }
            }
            top+=line.Height;

            void DrawLine()
            {
                var origin=new Point(x,lineY+entranceOffset);
                if(style.LyricAnimation=="Glow" && intensity>0)
                {
                    var glowOpacity=(.08+.08*(1+Math.Sin(phase*.8)))*intensity;
                    var radius=Math.Max(1,fontSize*.06)*(1+Math.Sin(phase*.8)*.25);
                    using var glow=context.PushOpacity(glowOpacity);
                    for(var ray=0;ray<8;ray++)
                    {
                        var angle=ray*Math.PI/4;
                        line.Draw(context,origin+new Vector(Math.Cos(angle)*radius,Math.Sin(angle)*radius));
                    }
                }
                shadow.TextLines[index].Draw(context,origin+new Vector(shadowOffset,shadowOffset));
                line.Draw(context,origin);
            }
        }
    }

    private Typeface ResolveTypeface(StageStyle style)
    {
        var family=string.IsNullOrWhiteSpace(style.FontFamily)?"Segoe UI":style.FontFamily.Trim();
        if(_typefaces.TryGetValue((family,style.Bold),out var cached))return cached;
        Typeface typeface;
        try
        {
            typeface=new Typeface(new FontFamily(family),FontStyle.Normal,style.Bold?FontWeight.Bold:FontWeight.Normal);
            if(!FontManager.Current.TryGetGlyphTypeface(typeface,out _))
                typeface=new Typeface("Segoe UI",FontStyle.Normal,style.Bold?FontWeight.Bold:FontWeight.Normal);
        }
        catch(ArgumentException){typeface=new Typeface("Segoe UI",FontStyle.Normal,style.Bold?FontWeight.Bold:FontWeight.Normal);}
        _typefaces[(family,style.Bold)]=typeface;
        return typeface;
    }

    private TextLayout GetText(string value, Typeface typeface, double size, double width, string color)
    {
        // Typeface has ValueType.ToString(): it returns the same text for every family.
        // Key each actual typography component so font/weight changes never reuse stale glyphs.
        var key = new TextCacheKey(value, typeface.FontFamily.Name, typeface.FontFamily.Key,
            typeface.Style, typeface.Weight, typeface.Stretch, Math.Round(size,3), Math.Round(width,2), ColorOr(color,"#FFFFFF"));
        if (_textCache.TryGetValue(key, out var formatted)) return formatted;
        formatted = new TextLayout(value,typeface,Math.Max(1,size),Brush(color,"#FFFFFF"),
            TextAlignment.Center,TextWrapping.Wrap,TextTrimming.None,flowDirection:FlowDirection.LeftToRight,maxWidth:Math.Max(1,width));
        _textCache[key] = formatted;
        return formatted;
    }

    private void ClearTextCache()
    {
        foreach(var layout in _textCache.Values)layout.Dispose();
        _textCache.Clear();
    }

    private static Color ColorOr(string? value, string fallback)
        => Color.TryParse(value, out var color) ? color : Color.Parse(fallback);
    private static SolidColorBrush Brush(string? value, string fallback) => new(ColorOr(value, fallback));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _image?.Dispose();
        _image = null;
        ClearTextCache();
        _typefaces.Clear();
    }
}
