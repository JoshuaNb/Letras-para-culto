using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace WorshipStudio.Controls;

/// <summary>A local, keyboard-accessible color field with a live visual picker.</summary>
public sealed class ColorField : UserControl
{
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<ColorField, string>(nameof(Value), "#FFFFFF");

    private readonly Border _swatch;
    private readonly TextBlock _hex;
    private readonly Button _button;
    private bool _pickerOpen;
    private string _caption = "Escolher cor";

    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Caption
    {
        get => _caption;
        set
        {
            _caption = value;
            if (_button != null)
            {
                AutomationProperties.SetName(_button, "Escolher " + value.ToLowerInvariant());
                ToolTip.SetTip(_button, "Escolher " + value.ToLowerInvariant());
            }
        }
    }
    public event Action? ColorChanged;

    public ColorField()
    {
        _swatch = new Border
        {
            Width = 21, Height = 21, CornerRadius = new CornerRadius(4),
            BorderBrush = Brush.Parse("#607086"), BorderThickness = new Thickness(1)
        };
        _hex = new TextBlock { FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("21,7,*,Auto") };
        row.Children.Add(_swatch);
        Grid.SetColumn(_hex, 2);
        row.Children.Add(_hex);
        var more = new TextBlock { Text = "▾", Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
        Grid.SetColumn(more, 3);
        row.Children.Add(more);
        _button = new Button
        {
            Content = row, HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(7), MinHeight = 36
        };
        Content = _button;
        Caption = _caption;
        _button.Click += async (_, _) => await OpenPicker();
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ValueProperty) return;
        Refresh();
        ColorChanged?.Invoke();
    }

    private void Refresh()
    {
        if (_swatch == null || _hex == null) return;
        var color = Color.TryParse(Value, out var parsed) ? parsed : Colors.White;
        _swatch.Background = new SolidColorBrush(color);
        _hex.Text = Format(color);
    }

    private async Task OpenPicker()
    {
        if (_pickerOpen || TopLevel.GetTopLevel(this) is not Window owner) return;
        _pickerOpen = true;
        var original = Value;
        var current = Color.TryParse(Value, out var parsed) ? parsed : Colors.White;
        var accepted = false;
        var updating = false;
        var ending = false;
        var dialog = new Window
        {
            Title = Caption, Width = 440, Height = 680, MinWidth = 400, MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = true
        };
        var spectrum = new ColorSpectrum { Height = 165, HorizontalAlignment = HorizontalAlignment.Stretch };
        var hue = new Slider { Minimum = 0, Maximum = 359.999, Value = 0, SmallChange = 1, LargeChange = 15 };
        AutomationProperties.SetName(hue, "Matiz da cor");
        var hueBar = new Border
        {
            Height = 8, CornerRadius = new CornerRadius(4),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, .5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, .5, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new(Colors.Red, 0), new(Colors.Yellow, 1d / 6), new(Colors.Lime, 2d / 6),
                    new(Colors.Cyan, .5), new(Colors.Blue, 4d / 6), new(Colors.Magenta, 5d / 6), new(Colors.Red, 1)
                }
            }
        };
        var preview = new Border { Height = 38, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), BorderBrush = Brush.Parse("#68778D") };
        var hex = new TextBox { Watermark = "#FFFFFF", MaxLength = 9, FontFamily = new FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(hex, "Código hexadecimal da cor");
        var error = new TextBlock { Foreground = Brush.Parse("#FF9BAA"), FontSize = 10, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        var red = ChannelSlider("Vermelho");
        var green = ChannelSlider("Verde");
        var blue = ChannelSlider("Azul");
        var redValue = new TextBlock();
        var greenValue = new TextBlock();
        var blueValue = new TextBlock();

        void SetCurrent(Color color, bool updateHsv = true, bool publish = true)
        {
            if (ending) return;
            updating = true;
            current = color;
            preview.Background = new SolidColorBrush(color);
            hex.Text = Format(color);
            red.Value = color.R; green.Value = color.G; blue.Value = color.B;
            redValue.Text = color.R.ToString(); greenValue.Text = color.G.ToString(); blueValue.Text = color.B.ToString();
            if (updateHsv)
            {
                var hsv = ToHsv(color);
                // Grays have no unique hue: retain the last hue so white/black are easy to recolor.
                if (hsv.Saturation > .00001) hue.Value = hsv.Hue;
                spectrum.Set(hue.Value, hsv.Saturation, hsv.Value);
            }
            error.IsVisible = false;
            updating = false;
            if (publish) Value = Format(color);
        }

        bool ApplyHex()
        {
            if (updating || ending) return true;
            var input = hex.Text?.Trim();
            if (Color.TryParse(input, out var color))
            {
                SetCurrent(color);
                return true;
            }
            updating = true;
            hex.Text = Format(current);
            updating = false;
            error.Text = "Código inválido. Use #RRGGBB, por exemplo #FFD166. A cor anterior foi mantida.";
            error.IsVisible = true;
            return false;
        }

        spectrum.ColorSelected += () =>
        {
            if (!updating) SetCurrent(FromHsv(hue.Value, spectrum.Saturation, spectrum.Brightness, current.A), false);
        };
        hue.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty || updating) return;
            spectrum.Set(hue.Value, spectrum.Saturation, spectrum.Brightness);
            SetCurrent(FromHsv(hue.Value, spectrum.Saturation, spectrum.Brightness, current.A), false);
        };
        foreach (var channel in new[] { red, green, blue }) channel.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty && !updating)
                SetCurrent(Color.FromArgb(current.A, (byte)Math.Round(red.Value), (byte)Math.Round(green.Value), (byte)Math.Round(blue.Value)));
        };
        hex.LostFocus += (_, _) => ApplyHex();
        hex.KeyDown += (_, e) => { if (e.Key == Key.Enter) { ApplyHex(); e.Handled = true; } };

        var palette = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var code in new[] { "#FFFFFF", "#FFF4D6", "#FFEA00", "#FFD166", "#FF8A33", "#FF4D6D", "#E754C7", "#BE8CFF", "#7C5CFC", "#5A94FF", "#4AD9FF", "#48DCC4", "#5BE28D", "#B7EF67", "#8B9BB2", "#000000" })
        {
            var color = Color.Parse(code);
            var chip = new Button
            {
                Width = 39, Height = 29, MinHeight = 29, Padding = new Thickness(0), Margin = new Thickness(2),
                Background = new SolidColorBrush(color), BorderBrush = Brush.Parse("#617086"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5)
            };
            ToolTip.SetTip(chip, code);
            AutomationProperties.SetName(chip, "Escolher " + code);
            chip.Click += (_, _) => SetCurrent(color);
            palette.Children.Add(chip);
        }
        var apply = new Button { Content = "Aplicar cor", Classes = { "primary" } };
        var cancel = new Button { Content = "Cancelar" };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        buttons.Children.Add(cancel); buttons.Children.Add(apply);
        var previewRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
        previewRow.Children.Add(preview); Grid.SetColumn(hex, 2); previewRow.Children.Add(hex);
        var body = new StackPanel { Margin = new Thickness(23), Spacing = 9 };
        body.Children.Add(new TextBlock { Text = Caption, FontSize = 23, FontWeight = FontWeight.SemiBold });
        body.Children.Add(new TextBlock { Text = "A prévia e a projeção acompanham os ajustes em tempo real.", FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#91A2B9"), Margin = new Thickness(0, 0, 0, 4) });
        body.Children.Add(spectrum);
        body.Children.Add(new TextBlock { Text = "MATIZ", Classes = { "caption" } });
        body.Children.Add(hueBar); body.Children.Add(hue);
        body.Children.Add(previewRow); body.Children.Add(error);
        body.Children.Add(ChannelRow("Vermelho", red, redValue));
        body.Children.Add(ChannelRow("Verde", green, greenValue));
        body.Children.Add(ChannelRow("Azul", blue, blueValue));
        body.Children.Add(new TextBlock { Text = "PALETA", Classes = { "caption" }, Margin = new Thickness(0, 4, 0, 0) });
        body.Children.Add(palette);
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        layout.Children.Add(new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        buttons.Margin = new Thickness(23, 10, 23, 18);
        Grid.SetRow(buttons, 1);
        layout.Children.Add(buttons);
        dialog.Content = layout;
        cancel.Click += (_, _) => dialog.Close();
        apply.Click += (_, _) => { if (!ApplyHex()) { hex.Focus(); return; } accepted = true; dialog.Close(); };
        dialog.KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; dialog.Close(); } };
        dialog.Closing += (_, _) => { ending = true; if (!accepted) Value = original; };
        try
        {
            // Initializing the picker must not change a saved color until an actual adjustment occurs.
            SetCurrent(current, publish: false);
            await dialog.ShowDialog(owner);
        }
        finally { _pickerOpen = false; }
    }

    private static Slider ChannelSlider(string name)
    {
        var slider = new Slider { Minimum = 0, Maximum = 255, SmallChange = 1, LargeChange = 10 };
        AutomationProperties.SetName(slider, name);
        return slider;
    }

    private static Control ChannelRow(string label, Slider slider, TextBlock value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("66,*,30"), MinHeight = 25 };
        row.Children.Add(new TextBlock { Text = label, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(slider, 1); row.Children.Add(slider);
        value.FontSize = 11; value.Foreground = Brush.Parse("#A8B6CA"); value.HorizontalAlignment = HorizontalAlignment.Right; value.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(value, 2); row.Children.Add(value);
        return row;
    }

    private static string Format(Color color) => color.A == 255
        ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
        : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private static (double Hue, double Saturation, double Value) ToHsv(Color color)
    {
        var r = color.R / 255d; var g = color.G / 255d; var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b)); var delta = max - min;
        var hue = delta < .000001 ? 0 : max == r ? 60 * (((g - b) / delta) % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        if (hue < 0) hue += 360;
        return (hue, max < .000001 ? 0 : delta / max, max);
    }

    private static Color FromHsv(double hue, double saturation, double brightness, byte alpha = 255)
    {
        hue = ((hue % 360) + 360) % 360;
        var c = brightness * saturation; var x = c * (1 - Math.Abs(hue / 60 % 2 - 1)); var m = brightness - c;
        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x),
            < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x)
        };
        return Color.FromArgb(alpha, (byte)Math.Clamp(Math.Round((r + m) * 255), 0, 255),
            (byte)Math.Clamp(Math.Round((g + m) * 255), 0, 255), (byte)Math.Clamp(Math.Round((b + m) * 255), 0, 255));
    }

    private sealed class ColorSpectrum : Control
    {
        private double _hue;
        public double Saturation { get; private set; }
        public double Brightness { get; private set; } = 1;
        public event Action? ColorSelected;

        public ColorSpectrum()
        {
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Cross);
            AutomationProperties.SetName(this, "Saturação e luminosidade da cor. Use as setas do teclado para ajustar.");
        }
        public void Set(double hue, double saturation, double brightness)
        {
            _hue = hue; Saturation = Math.Clamp(saturation, 0, 1); Brightness = Math.Clamp(brightness, 0, 1);
            InvalidateVisual();
        }
        public override void Render(DrawingContext context)
        {
            var rect = new Rect(Bounds.Size);
            context.FillRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = new GradientStops { new(Colors.White, 0), new(FromHsv(_hue, 1, 1), 1) }
            }, rect);
            context.FillRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops { new(Colors.Transparent, 0), new(Colors.Black, 1) }
            }, rect);
            var point = new Point(Saturation * rect.Width, (1 - Brightness) * rect.Height);
            context.DrawEllipse(null, new Pen(Brushes.Black, 3), point, 6, 6);
            context.DrawEllipse(null, new Pen(Brushes.White, 1.5), point, 6, 6);
            if (IsKeyboardFocusWithin) context.DrawRectangle(null, new Pen(Brush.Parse("#48DCC4"), 2), rect.Deflate(1));
        }
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            Focus(); e.Pointer.Capture(this); SelectAt(e.GetPosition(this)); e.Handled = true;
        }
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (e.Pointer.Captured == this) { SelectAt(e.GetPosition(this)); e.Handled = true; }
        }
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (e.Pointer.Captured == this) { e.Pointer.Capture(null); e.Handled = true; }
        }
        private void SelectAt(Point point)
        {
            Saturation = Math.Clamp(point.X / Math.Max(1, Bounds.Width), 0, 1);
            Brightness = 1 - Math.Clamp(point.Y / Math.Max(1, Bounds.Height), 0, 1);
            InvalidateVisual(); ColorSelected?.Invoke();
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? .1 : .01;
            switch (e.Key)
            {
                case Key.Left: Saturation = Math.Clamp(Saturation - step, 0, 1); break;
                case Key.Right: Saturation = Math.Clamp(Saturation + step, 0, 1); break;
                case Key.Up: Brightness = Math.Clamp(Brightness + step, 0, 1); break;
                case Key.Down: Brightness = Math.Clamp(Brightness - step, 0, 1); break;
                default: return;
            }
            e.Handled = true; InvalidateVisual(); ColorSelected?.Invoke();
        }
    }
}
