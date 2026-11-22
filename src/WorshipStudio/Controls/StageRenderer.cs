using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
    private readonly Dictionary<string, FormattedText> _textCache = new();

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
        if (!string.Equals(_text, frame.Text, StringComparison.Ordinal))
        {
            _previousText = _text;
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

        var progress = _fixedTransition ?? Math.Clamp((_clock.Elapsed.TotalSeconds - _changedAt) /
            Math.Max(.05, style.TransitionSeconds), 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);
        var transition = style.Transition;
        if (progress < 1 && transition != "Cut" && transition != "Typewriter" && _previousText.Length > 0)
            DrawLyrics(context, rect, _previousText, style, 1 - eased,
                transition == "Slide" ? -eased * rect.Height * .08 : 0, 1, audio);

        if (transition == "Typewriter" && progress < 1)
        {
            var indices = StringInfo.ParseCombiningCharacters(_text);
            var count = Math.Clamp((int)Math.Ceiling(indices.Length * progress), 0, indices.Length);
            var length = count == indices.Length ? _text.Length : indices[count];
            DrawLyrics(context, rect, _text[..length], style, 1, 0, 1, audio);
        }
        else
        {
            var opacity = transition == "Cut" ? 1 : eased;
            var offset = transition == "Slide" ? (1 - eased) * rect.Height * .08 : 0;
            var scale = transition == "Zoom" ? .88 + .12 * eased : 1;
            DrawLyrics(context, rect, _text, style, opacity, offset, scale, audio);
        }
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
        double opacity, double offset, double scale, double audio)
    {
        if (string.IsNullOrWhiteSpace(value) || opacity <= .001) return;
        var maxWidth = rect.Width * .84;
        var fontSize = Math.Clamp(style.FontSize, 20, 160) * rect.Height / 1080;
        var typeface = new Typeface(new FontFamily(string.IsNullOrWhiteSpace(style.FontFamily) ? "Segoe UI" : style.FontFamily), FontStyle.Normal, FontWeight.SemiBold);
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
        var x = (rect.Width - maxWidth) / 2;
        var y = Math.Clamp(Math.Clamp(style.TextY, .15, .85) * rect.Height - text.Height / 2,
            rect.Height * .06, Math.Max(rect.Height * .06, rect.Height * .94 - text.Height)) + offset;
        using var opacityState = context.PushOpacity(Math.Clamp(opacity, 0, 1));
        var animatedScale = scale * (1 + audio * .015);
        using var transform = context.PushTransform(Matrix.CreateTranslation(-rect.Width / 2, -rect.Height / 2) *
            Matrix.CreateScale(animatedScale, animatedScale) * Matrix.CreateTranslation(rect.Width / 2, rect.Height / 2));
        var shadowOffset = Math.Max(1, rect.Height / 360);
        context.DrawText(shadow, new Point(x + shadowOffset, y + shadowOffset));
        context.DrawText(text, new Point(x, y));
    }

    private FormattedText GetText(string value, Typeface typeface, double size, double width, string color)
    {
        var key = value + '\u001e' + typeface + '\u001e' + size.ToString("F3", CultureInfo.InvariantCulture) + '\u001e' + width.ToString("F2", CultureInfo.InvariantCulture) + '\u001e' + color;
        if (_textCache.TryGetValue(key, out var formatted)) return formatted;
        if (_textCache.Count > 180) _textCache.Clear();
        formatted = new FormattedText(value, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
            typeface, Math.Max(1, size), Brush(color, "#FFFFFF"))
        {
            MaxTextWidth = Math.Max(1, width),
            TextAlignment = TextAlignment.Center,
            Trimming = TextTrimming.None
        };
        _textCache[key] = formatted;
        return formatted;
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
        _textCache.Clear();
    }
}
