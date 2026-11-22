using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using WorshipStudio.Models;

namespace WorshipStudio.Controls;

/// <summary>A semantic animation unit retains the bounds of the fully shaped lyric.</summary>
public sealed record AnimationUnitInfo(int Index,int TextStart,int TextLength,string Text,IReadOnlyList<Rect> Bounds);

/// <summary>
/// Shapes each lyric once, then composites independently transformed units through the
/// active Skia backend. Blur uses the native image filter, not repeated offset shadows.
/// Both the live compositor and RenderTargetBitmap execute this same drawing operation.
/// </summary>
internal sealed class GranularLetterRenderer : IDisposable
{
    private static readonly Regex Words=new(@"\S+",RegexOptions.Compiled|RegexOptions.CultureInvariant);
    private RasterAsset? _asset;
    private RasterKey? _key;
    private readonly record struct RasterKey(string Text,string Family,bool Bold,double FontSize,string Color,double TextY,int Width,int Height);

    public void Draw(DrawingContext context,Rect bounds,string text,StageStyle style,double elapsed,double audio)
    {
        var key=new RasterKey(text,style.FontFamily,style.Bold,style.FontSize,style.TextColor,style.TextY,
            Math.Max(1,(int)Math.Ceiling(bounds.Width)),Math.Max(1,(int)Math.Ceiling(bounds.Height)));
        if(_asset==null || _key!=key)
        {
            var replacement=CreateRaster(text,style,new Size(key.Width,key.Height));
            _asset?.Release();
            _asset=replacement;
            _key=key;
        }
        var units=_asset.GetUnits(style.AnimationGranularity);
        var preset=AnimationPresets.Find(style.AnimationPreset);
        var progress=units.Select(unit=>AnimationTimeline.Progress(style,elapsed,unit.Index,units.Count)).ToArray();
        // At rest, draw the complete shaped lyric once. Besides reducing GPU work,
        // this prevents AA seams where neighboring character crops meet.
        var samples=progress.All(p=>p>=1) && style.LyricAnimation!="Wave"
            ? _asset.GetUnits("Text").Select(unit=>new UnitSample(unit,AnimationPose.Identity)).ToArray()
            : units.Select(unit=>new UnitSample(unit,
                AnimationTimeline.Evaluate(preset,progress[unit.Index],style.AnimationStrength,unit.Index,style.AnimationEasing))).ToArray();
        context.Custom(new GranularDrawOperation(bounds,_asset,samples,style with{},elapsed,audio));
    }

    public static IReadOnlyList<AnimationUnitInfo> MeasureUnits(string text,StageStyle style,Size size)
    {
        if(string.IsNullOrWhiteSpace(text))return [];
        var measured=CreateLayout(text,style,size);
        using var layout=measured.Layout;
        return BuildUnits(text,layout,measured.Origin,style.AnimationGranularity);
    }

    private static RasterAsset CreateRaster(string text,StageStyle style,Size size)
    {
        var measured=CreateLayout(text,style,size);
        using var layout=measured.Layout;
        var control=new RasterTextControl(layout,measured.Origin);
        control.Measure(size);control.Arrange(new Rect(size));
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)size.Width,(int)size.Height),new Vector(96,96));
        bitmap.Render(control);
        using var memory=new MemoryStream();bitmap.Save(memory);
        var image=SKImage.FromEncodedData(memory.ToArray())??throw new InvalidOperationException("Não foi possível rasterizar a letra.");
        var unitMaps=new Dictionary<string,IReadOnlyList<AnimationUnitInfo>>(StringComparer.Ordinal);
        foreach(var granularity in new[]{"Text","Line","Word","Character"})
            unitMaps[granularity]=BuildUnits(text,layout,measured.Origin,granularity);
        return new RasterAsset(image,measured.FontSize,unitMaps);
    }

    private sealed class RasterTextControl(TextLayout layout,Point origin) : Control
    {
        public override void Render(DrawingContext context) => layout.Draw(context,origin);
    }

    private static (TextLayout Layout,double FontSize,Point Origin) CreateLayout(string text,StageStyle style,Size size)
    {
        var family=string.IsNullOrWhiteSpace(style.FontFamily)?"Segoe UI":style.FontFamily;
        var typeface=new Typeface(family,FontStyle.Normal,style.Bold?FontWeight.Bold:FontWeight.Normal);
        if(!FontManager.Current.TryGetGlyphTypeface(typeface,out _))
            typeface=new Typeface("Segoe UI",FontStyle.Normal,style.Bold?FontWeight.Bold:FontWeight.Normal);
        var width=Math.Max(1,size.Width*.84);
        var fontSize=Math.Clamp(style.FontSize,20,160)*size.Height/1080;
        var color=Color.TryParse(style.TextColor,out var parsed)?parsed:Colors.White;
        TextLayout Make(double font)=>new(text,typeface,Math.Max(1,font),new SolidColorBrush(color),
            TextAlignment.Center,TextWrapping.Wrap,TextTrimming.None,flowDirection:FlowDirection.LeftToRight,maxWidth:width);
        var layout=Make(fontSize);
        if(layout.Height>size.Height*.72 || layout.Width>width)
        {
            layout.Dispose();
            var low=1d;var high=fontSize;
            for(var pass=0;pass<11;pass++)
            {
                var candidate=(low+high)/2;
                using var sample=Make(candidate);
                if(sample.Height<=size.Height*.72 && sample.Width<=width)low=candidate;else high=candidate;
            }
            fontSize=low;layout=Make(fontSize);
        }
        var y=Math.Clamp(Math.Clamp(style.TextY,.15,.85)*size.Height-layout.Height/2,
            size.Height*.06,Math.Max(size.Height*.06,size.Height*.94-layout.Height));
        return(layout,fontSize,new Point((size.Width-width)/2,y));
    }

    private static IReadOnlyList<AnimationUnitInfo> BuildUnits(string text,TextLayout layout,Point origin,string granularity)
    {
        var units=new List<AnimationUnitInfo>();
        void Add(int start,int length)
        {
            start=Math.Clamp(start,0,text.Length);length=Math.Clamp(length,0,text.Length-start);
            if(length==0 || string.IsNullOrWhiteSpace(text.Substring(start,length)))return;
            var regions=layout.HitTestTextRange(start,length).Where(r=>r.Width>.01 && r.Height>.01)
                .Select(r=>new Rect(r.X+origin.X,r.Y+origin.Y,r.Width,r.Height)).ToArray();
            if(regions.Length==0)return;
            units.Add(new AnimationUnitInfo(units.Count,start,length,text.Substring(start,length),regions));
        }
        switch(granularity)
        {
            case "Text":Add(0,text.Length);break;
            case "Line":
                foreach(var line in layout.TextLines)Add(line.FirstTextSourceIndex,line.Length);
                break;
            case "Character":
                var indices=StringInfo.ParseCombiningCharacters(text);
                for(var i=0;i<indices.Length;i++)Add(indices[i],(i+1<indices.Length?indices[i+1]:text.Length)-indices[i]);
                break;
            default:
                foreach(Match word in Words.Matches(text))Add(word.Index,word.Length);
                break;
        }
        return units;
    }

    private sealed class RasterAsset(SKImage image,double fontSize,Dictionary<string,IReadOnlyList<AnimationUnitInfo>> units)
    {
        private int _references=1;
        public SKImage Image {get;}=image;
        public double FontSize {get;}=fontSize;
        public IReadOnlyList<AnimationUnitInfo> GetUnits(string granularity)=>units.GetValueOrDefault(granularity)??units["Word"];
        public void AddReference()=>Interlocked.Increment(ref _references);
        public void Release(){if(Interlocked.Decrement(ref _references)==0)Image.Dispose();}
    }

    private sealed record UnitSample(AnimationUnitInfo Unit,AnimationPose Pose);

    private sealed class GranularDrawOperation : ICustomDrawOperation
    {
        private readonly RasterAsset _asset;
        private readonly UnitSample[] _samples;
        private readonly StageStyle _style;
        private readonly double _elapsed,_audio;
        private bool _disposed;
        public Rect Bounds {get;}
        public GranularDrawOperation(Rect bounds,RasterAsset asset,UnitSample[] samples,StageStyle style,double elapsed,double audio)
        {
            Bounds=bounds;_asset=asset;_asset.AddReference();_samples=samples;_style=style;_elapsed=elapsed;_audio=audio;
        }
        public bool HitTest(Point p)=>false;
        public bool Equals(ICustomDrawOperation? other)=>false;
        public void Dispose(){if(_disposed)return;_disposed=true;_asset.Release();}
        public void Render(ImmediateDrawingContext context)
        {
            if(_disposed)return;
            var feature=context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
            if(feature==null)return;
            using var lease=feature.Lease();
            var canvas=lease.SkCanvas;
            canvas.Save();
            try
            {
                canvas.ClipRect(ToSk(Bounds));
                ApplyContinuousBlock(canvas);
                foreach(var sample in _samples)DrawUnit(canvas,sample);
            }
            finally{canvas.Restore();}
        }

        private void ApplyContinuousBlock(SKCanvas canvas)
        {
            var intensity=Math.Clamp(_style.LyricAnimationIntensity,0,1);
            var phase=_elapsed*Math.Clamp(_style.LyricAnimationSpeed,.1,4)*Math.PI;
            var scale=1+Math.Clamp(_audio,0,1)*.015;
            var center=Bounds.Center;
            if(_style.LyricAnimation=="Float")canvas.Translate(0,(float)(Math.Sin(phase*.7)*Bounds.Height*.022*intensity));
            if(_style.LyricAnimation=="Bounce")canvas.Translate(0,(float)(-Math.Abs(Math.Sin(phase*1.1))*Bounds.Height*.045*intensity));
            if(_style.LyricAnimation=="Shake")canvas.Translate((float)((Math.Sin(phase*13)+Math.Sin(phase*21.7))*.5*Bounds.Width*.004*intensity),(float)(Math.Sin(phase*17.2)*Bounds.Height*.006*intensity));
            if(_style.LyricAnimation=="Pulse")scale*=1+Math.Sin(phase*1.5)*.045*intensity;
            if(_style.LyricAnimation=="Breathe")scale*=1+Math.Sin(phase*.55)*.018*intensity;
            canvas.Translate((float)center.X,(float)center.Y);
            if(_style.LyricAnimation=="Sway")canvas.RotateRadians((float)(Math.Sin(phase*.65)*.035*intensity));
            canvas.Scale((float)scale);
            canvas.Translate((float)-center.X,(float)-center.Y);
        }

        private void DrawUnit(SKCanvas canvas,UnitSample sample)
        {
            var pose=sample.Pose;
            if(pose.Opacity<=.001)return;
            var union=sample.Unit.Bounds.Aggregate((a,b)=>a.Union(b));
            var center=union.Center;
            var font=_asset.FontSize;
            var opacity=Math.Clamp(pose.Opacity,0,1);
            var phase=_elapsed*Math.Clamp(_style.LyricAnimationSpeed,.1,4)*Math.PI;
            var intensity=Math.Clamp(_style.LyricAnimationIntensity,0,1);
            if(_style.LyricAnimation=="Breathe")opacity*=1-(.2-.2*Math.Sin(phase*.55))*intensity;
            canvas.Save();
            try
            {
                canvas.Translate((float)(center.X+pose.X*font),(float)(center.Y+pose.Y*font));
                if(_style.LyricAnimation=="Wave")canvas.Translate(0,(float)(Math.Sin(phase+sample.Unit.Index*.95)*font*.24*intensity));
                canvas.RotateRadians((float)pose.Rotation);
                canvas.Skew((float)pose.SkewX,0);
                canvas.Scale((float)pose.ScaleX,(float)pose.ScaleY);
                canvas.Translate((float)-center.X,(float)-center.Y);
                ApplyMask(canvas,union,pose);
                foreach(var fragment in sample.Unit.Bounds)
                {
                    var source=new Rect(Math.Max(0,fragment.X-.5),Math.Max(0,fragment.Y-1),fragment.Width+1,fragment.Height+2).Intersect(Bounds);
                    if(source.Width<=0||source.Height<=0)continue;
                    var rectangle=ToSk(source);
                    using var paint=new SKPaint {IsAntialias=true,FilterQuality=SKFilterQuality.High,Color=SKColors.White.WithAlpha((byte)(opacity*255))};
                    using var blur=pose.Blur>.001?SKImageFilter.CreateBlur((float)(pose.Blur*font),(float)(pose.Blur*font)):null;
                    paint.ImageFilter=blur;
                    if(pose.TintStrength>.001 && SKColor.TryParse(pose.TintColor,out var tint))
                        paint.ColorFilter=SKColorFilter.CreateBlendMode(tint.WithAlpha((byte)(Math.Clamp(pose.TintStrength,0,1)*255)),SKBlendMode.SrcATop);
                    var glow=pose.Glow;
                    if(_style.LyricAnimation=="Glow")glow+=(.16+.16*Math.Sin(phase*.8))*intensity;
                    if(glow>.001)
                    {
                        using var glowPaint=new SKPaint {IsAntialias=true,FilterQuality=SKFilterQuality.High,Color=SKColors.White.WithAlpha((byte)(Math.Clamp(glow*opacity,0,.8)*255))};
                        using var glowBlur=SKImageFilter.CreateBlur((float)Math.Max(1,font*.12),(float)Math.Max(1,font*.12));
                        glowPaint.ImageFilter=glowBlur;
                        canvas.DrawImage(_asset.Image,rectangle,rectangle,glowPaint);
                    }
                    if(pose.RgbSplit>.001)
                    {
                        var split=(float)(pose.RgbSplit*font);
                        DrawSplit(-split,0,new float[]{1,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0, 0,0,0,1,0});
                        DrawSplit(split,0,new float[]{0,0,0,0,0, 0,1,0,0,0, 0,0,1,0,0, 0,0,0,1,0});
                        void DrawSplit(float x,float y,float[] matrix)
                        {
                            using var splitPaint=new SKPaint {IsAntialias=true,FilterQuality=SKFilterQuality.High,Color=SKColors.White.WithAlpha((byte)(opacity*.7*255))};
                            using var filter=SKColorFilter.CreateColorMatrix(matrix);splitPaint.ColorFilter=filter;
                            var target=rectangle;target.Offset(x,y);canvas.DrawImage(_asset.Image,rectangle,target,splitPaint);
                        }
                    }
                    // A small native shadow preserves readability on bright video backgrounds.
                    using(var shadowPaint=new SKPaint {IsAntialias=true,Color=SKColors.White.WithAlpha((byte)(opacity*.55*255))})
                    using(var shadowFilter=SKColorFilter.CreateBlendMode(SKColors.Black,SKBlendMode.SrcIn))
                    {
                        shadowPaint.ColorFilter=shadowFilter;
                        var shadow=rectangle;shadow.Offset((float)Math.Max(1,font*.035),(float)Math.Max(1,font*.035));
                        canvas.DrawImage(_asset.Image,rectangle,shadow,shadowPaint);
                    }
                    canvas.DrawImage(_asset.Image,rectangle,rectangle,paint);
                    paint.ColorFilter?.Dispose();
                }
            }
            finally{canvas.Restore();}
        }

        private static void ApplyMask(SKCanvas canvas,Rect rect,AnimationPose pose)
        {
            var p=Math.Clamp(pose.MaskProgress,0,1);
            var clip=pose.Mask switch
            {
                "Up"=>new Rect(rect.X,rect.Bottom-rect.Height*p,rect.Width,rect.Height*p),
                "Down"=>new Rect(rect.X,rect.Y,rect.Width,rect.Height*p),
                "Left"=>new Rect(rect.Right-rect.Width*p,rect.Y,rect.Width*p,rect.Height),
                "Right"=>new Rect(rect.X,rect.Y,rect.Width*p,rect.Height),
                _=>rect
            };
            if(pose.Mask!="None")canvas.ClipRect(ToSk(clip));
        }
        private static SKRect ToSk(Rect rect)=>new((float)rect.Left,(float)rect.Top,(float)rect.Right,(float)rect.Bottom);
    }

    public void Dispose(){_asset?.Release();_asset=null;_key=null;}
}
