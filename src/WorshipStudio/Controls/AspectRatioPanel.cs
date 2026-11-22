using Avalonia;
using Avalonia.Controls;

namespace WorshipStudio.Controls;

/// <summary>Fits a native-video surface without applying transforms to Win32 child windows.</summary>
public sealed class AspectRatioPanel : Panel
{
    private static Size Fit(Size available)
    {
        var width=double.IsFinite(available.Width)?available.Width:960;
        var height=double.IsFinite(available.Height)?available.Height:540;
        var scale=Math.Max(0,Math.Min(width/16,height/9));
        return new Size(16*scale,9*scale);
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var fit=Fit(availableSize);
        foreach(var child in Children)child.Measure(fit);
        return fit;
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var fit=Fit(finalSize);
        var bounds=new Rect((finalSize.Width-fit.Width)/2,(finalSize.Height-fit.Height)/2,fit.Width,fit.Height);
        foreach(var child in Children)child.Arrange(bounds);
        return finalSize;
    }
}
