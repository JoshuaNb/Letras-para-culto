using Avalonia;
using Avalonia.Media;
using WorshipStudio.Models;

namespace WorshipStudio.Controls;

/// <summary>Bounded, deterministic drawing primitives; works on Skia software without shaders or files.</summary>
internal static class ProceduralBackgroundRenderer
{
    public static void Draw(DrawingContext context,Rect rect,StageStyle style,string mode,double time,double audio)
    {
        var accent=Color.TryParse(style.AccentColor,out var parsed)?parsed:Color.Parse("#48DCC4");
        switch(mode)
        {
            case "GraceWaves": Waves(context,rect,accent,time,audio);break;
            case "LightRays": Rays(context,rect,accent,time,audio);break;
            case "Galaxy": Galaxy(context,rect,accent,time,audio);break;
            case "Bokeh": Bokeh(context,rect,accent,time,audio);break;
            case "WireLandscape": Landscape(context,rect,accent,time,audio);break;
            case "Ocean": Ocean(context,rect,accent,time,audio);break;
            case "Ribbons": Ribbons(context,rect,accent,time,audio);break;
            case "Prism": Prisms(context,rect,accent,time,audio);break;
        }
    }

    private static void Waves(DrawingContext c,Rect r,Color accent,double t,double audio)
    {
        Glow(c,new Point(r.Width*.7,r.Height*.2),r.Width*.58,r.Height*.7,accent,45+audio*22);
        for(var layer=0;layer<6;layer++)
        {
            var points=new List<Point>(68){new(-r.Width*.03,r.Height*1.04)};
            for(var step=0;step<=64;step++)
            {
                var x=(step/64d*1.06-.03)*r.Width;
                var y=r.Height*(.36+layer*.105+Math.Sin(step/64d*5.4+t*.19+layer*.7)*(.09+audio*.025)+Math.Sin(step*.05-t*.12)*.035);
                points.Add(new Point(x,y));
            }
            points.Add(new Point(r.Width*1.03,r.Height*1.04));
            var tint=Mix(accent,Colors.White,.06+layer*.035);
            Polygon(c,points,Vertical(tint,30+layer*6+audio*16,8+layer*3),null);
        }
    }

    private static void Rays(DrawingContext c,Rect r,Color accent,double t,double audio)
    {
        var origin=new Point(r.Width*(.5+Math.Sin(t*.08)*.18),-r.Height*.13);
        Glow(c,new Point(origin.X,0),r.Width*.42,r.Height*.5,Mix(accent,Colors.White,.4),65+audio*45);
        for(var ray=0;ray<8;ray++)
        {
            var center=r.Width*(ray/7d*1.6-.3+Math.Sin(t*.11+ray)*.11);
            var spread=r.Width*(.035+Hash(ray+90)*.045+audio*.012);
            Polygon(c,new[]{origin,new Point(center-spread,r.Height*1.08),new Point(center+spread,r.Height*1.08)},
                Vertical(Mix(accent,Colors.White,.18),70+audio*40,0),null);
        }
        for(var i=0;i<36;i++)
        {
            var p=new Point(r.Width*Hash(i*5+8),r.Height*Fraction(Hash(i*5+9)+t*(.004+Hash(i+5)*.005)));
            var radius=r.Height*(.001+Hash(i+22)*.0018);
            c.DrawEllipse(Tint(accent,25+audio*50),null,p,radius,radius);
        }
    }

    private static void Galaxy(DrawingContext c,Rect r,Color accent,double t,double audio)
    {
        var center=new Point(r.Width*.52,r.Height*.48);
        Glow(c,center,r.Width*.43,r.Height*.5,accent,50+audio*25);
        Glow(c,center,r.Width*.1,r.Height*.12,Mix(accent,Colors.White,.7),95+audio*55);
        for(var arm=0;arm<4;arm++)
        {
            var points=new List<Point>(49);
            for(var step=0;step<=48;step++)
            {
                var radius=step/48d;
                var angle=arm*Math.PI/2+radius*5.3+t*.065;
                points.Add(new Point(center.X+Math.Cos(angle)*radius*r.Width*.5,center.Y+Math.Sin(angle)*radius*r.Height*.42));
            }
            Line(c,points,new Pen(Tint(accent,36+audio*28),Math.Max(1,r.Width*.0025)));
        }
        for(var i=0;i<128;i++)
        {
            var depth=.2+Hash(i*4+3)*.8;
            var radius=Math.Sqrt(Hash(i*4+1));
            var angle=i%4*Math.PI/2+radius*5.3+t*(.025+depth*.08)+(Hash(i*4+2)-.5)*.55;
            var p=new Point(center.X+Math.Cos(angle)*radius*r.Width*.52,center.Y+Math.Sin(angle)*radius*r.Height*.43);
            var size=Math.Max(.5,r.Height*(.0008+depth*.002)*(1+audio*.5));
            var color=Mix(accent,Colors.White,.3+depth*.6);
            c.DrawEllipse(Tint(color,70+depth*160),null,p,size,size);
        }
    }

    private static void Bokeh(DrawingContext c,Rect r,Color accent,double t,double audio)
    {
        for(var i=0;i<18;i++)
        {
            var depth=.3+Hash(i*6+1)*.7;
            var x=r.Width*(Hash(i*6+2)+Math.Sin(t*.07+i)*.07);
            var y=r.Height*(Fraction(Hash(i*6+3)-t*(.008+depth*.008))*1.3-.15);
            var radius=r.Width*(.016+depth*.055)*(1+Math.Sin(t*.16+i)*.08+audio*.16);
            var color=Mix(accent,Colors.White,Hash(i*6+4)*.35);
            Glow(c,new Point(x,y),radius*1.5,radius*1.5,color,38+depth*50+audio*30);
            c.DrawEllipse(null,new Pen(Tint(color,18+depth*24),Math.Max(.7,r.Width*.00065)),new Point(x,y),radius,radius);
        }
    }

    private static void Landscape(DrawingContext c,Rect r,Color accent,double t,double audio)
    {
        var horizon=r.Height*.34;
        Glow(c,new Point(r.Width*.5,horizon),r.Width*.6,r.Height*.36,accent,55+audio*35);
        Point Project(double x,double z)
        {
            var elevation=1.35+Math.Sin(x*.67+z*.31+t*.28)*(.19+audio*.12);
            return new Point(r.Width*.5+x*r.Width*.7/z,horizon+elevation*r.Height*.82/z);
        }
        for(var row=17;row>=0;row--)
        {
            var z=1.15+Fraction((row+t*.52)/18)*24;
            var points=new List<Point>(23);
            for(var x=-11;x<=11;x++)points.Add(Project(x*.65,z));
            Line(c,points,new Pen(Tint(accent,30+(1-z/27)*100+audio*28),Math.Max(.65,r.Width*.0008)));
        }
        for(var column=-10;column<=10;column++)
        {
            var points=new List<Point>(23);
            for(var row=22;row>=0;row--)points.Add(Project(column*.65,1.15+row*1.05));
            Line(c,points,new Pen(Tint(accent,60+audio*45),Math.Max(.6,r.Width*.00065)));
        }
    }

    private static void Ocean(DrawingContext c,Rect r,Color accent,double t,double audio)
    {
        var horizon=r.Height*.39;
        var light=Mix(accent,Colors.White,.65);
        Glow(c,new Point(r.Width*.67,horizon-r.Height*.06),r.Width*.33,r.Height*.38,accent,60+audio*25);
        Glow(c,new Point(r.Width*.67,horizon-r.Height*.055),r.Height*.055,r.Height*.055,light,160);
        c.DrawLine(new Pen(Tint(accent,50),Math.Max(.7,r.Height*.001)),new Point(0,horizon),new Point(r.Width,horizon));
        for(var band=0;band<34;band++)
        {
            var depth=(band+1)/34d;
            var y=horizon+depth*depth*r.Height*.65;
            var points=new List<Point>(37);
            for(var x=0;x<=36;x++)
                points.Add(new Point(x/36d*r.Width,y+Math.Sin(x*.71+t*(.24+depth*.3)+band*.8)*r.Height*(.001+depth*.006)*(1+audio*.8)));
            Line(c,points,new Pen(Tint(accent,13+depth*38+audio*25),Math.Max(.6,r.Height*.0016*depth)));
            var center=r.Width*(.67+Math.Sin(t*.29+band*.77)*depth*.04);
            var half=r.Width*(.009+depth*.16)*(1+Math.Sin(band*1.9+t*.31)*.3);
            c.DrawLine(new Pen(Tint(light,20+depth*24+audio*20),Math.Max(.7,r.Height*.002*depth)),new Point(center-half,y),new Point(center+half,y));
        }
    }

    private static void Ribbons(DrawingContext c,Rect r,Color accent,double t,double audio)
    {
        for(var ribbon=0;ribbon<5;ribbon++)
        {
            var points=new List<Point>(114);
            var thickness=r.Height*(.024+ribbon*.005+audio*.012);
            Point Position(int step,bool lower)
            {
                var unit=step/56d;
                var wave=Math.Sin(unit*5.8-t*.19+ribbon*.65)*(.16+audio*.025)+Math.Sin(unit*2.1+t*.1)*.08;
                return new Point((unit*1.2-.1)*r.Width,r.Height*(.18+ribbon*.15+wave)+(lower?thickness:0));
            }
            for(var step=0;step<=56;step++)points.Add(Position(step,false));
            for(var step=56;step>=0;step--)points.Add(Position(step,true));
            var color=Mix(accent,Colors.White,ribbon*.09);
            Polygon(c,points,Vertical(color,35+ribbon*9+audio*25,12+ribbon*4),new Pen(Tint(color,18+audio*15),Math.Max(.5,r.Height*.001)));
        }
    }

    private static void Prisms(DrawingContext c,Rect r,Color accent,double t,double audio)
    {
        var vertices=new[]{(-1d,-1d,-1d),(1d,1d,-1d),(-1d,1d,1d),(1d,-1d,1d)};
        var faces=new[]{new[]{0,1,2},new[]{0,1,3},new[]{0,2,3},new[]{1,2,3}};
        Glow(c,new Point(r.Width*.45,r.Height*.5),r.Width*.7,r.Height*.7,accent,18+audio*12);
        for(var prism=0;prism<8;prism++)
        {
            var center=new Point(r.Width*(Hash(prism*7+1)*1.1-.05),r.Height*(.1+Hash(prism*7+2)*.8+Math.Sin(t*.12+prism)*.07));
            var size=r.Height*(.045+Hash(prism*7+3)*.1)*(1+audio*.12);
            var yaw=t*(.1+Hash(prism+20)*.08)+prism*.91;
            var pitch=t*.065+prism*.71;
            var points=new Point[4];var depths=new double[4];
            for(var vertex=0;vertex<4;vertex++)
            {
                var (x,y,z)=vertices[vertex];
                var rx=x*Math.Cos(yaw)-z*Math.Sin(yaw);var rz=x*Math.Sin(yaw)+z*Math.Cos(yaw);
                var ry=y*Math.Cos(pitch)-rz*Math.Sin(pitch);var dz=y*Math.Sin(pitch)+rz*Math.Cos(pitch);
                depths[vertex]=dz;var perspective=4/(4+dz);
                points[vertex]=new Point(center.X+rx*size*perspective,center.Y+ry*size*perspective);
            }
            foreach(var face in faces.OrderByDescending(face=>face.Average(index=>depths[index])))
            {
                var shade=(face.Average(index=>depths[index])+1.8)/3.6;
                var color=Mix(accent,Colors.White,Math.Clamp(shade*.3,0,.3));
                Polygon(c,face.Select(index=>points[index]),Tint(color,22+shade*35+audio*22),new Pen(Tint(color,65+audio*35),Math.Max(.7,r.Width*.0007)));
            }
        }
    }

    private static void Polygon(DrawingContext c,IEnumerable<Point> points,IBrush fill,IPen? pen)
    {
        var geometry=Path(points,true);c.DrawGeometry(fill,pen,geometry);
    }
    private static void Line(DrawingContext c,IEnumerable<Point> points,IPen pen)=>c.DrawGeometry(null,pen,Path(points,false));
    private static StreamGeometry Path(IEnumerable<Point> points,bool filled)
    {
        var path=new StreamGeometry();using var open=path.Open();using var iterator=points.GetEnumerator();
        if(iterator.MoveNext()){open.BeginFigure(iterator.Current,filled);while(iterator.MoveNext())open.LineTo(iterator.Current);open.EndFigure(filled);}
        return path;
    }
    private static void Glow(DrawingContext c,Point center,double rx,double ry,Color color,double alpha)
    {
        var brush=new RadialGradientBrush
        {
            Center=new RelativePoint(.5,.5,RelativeUnit.Relative),GradientOrigin=new RelativePoint(.5,.5,RelativeUnit.Relative),
            RadiusX=new RelativeScalar(.5,RelativeUnit.Relative),RadiusY=new RelativeScalar(.5,RelativeUnit.Relative),
            GradientStops=new GradientStops{new(WithAlpha(color,alpha),0),new(WithAlpha(color,alpha*.28),.45),new(WithAlpha(color,0),1)}
        };
        c.DrawEllipse(brush,null,center,rx,ry);
    }
    private static LinearGradientBrush Vertical(Color color,double top,double bottom)=>new()
    {
        StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(0,1,RelativeUnit.Relative),
        GradientStops=new GradientStops{new(WithAlpha(color,top),0),new(WithAlpha(color,bottom),1)}
    };
    private static IBrush Tint(Color color,double alpha)=>new SolidColorBrush(WithAlpha(color,alpha));
    private static Color WithAlpha(Color c,double alpha)=>Color.FromArgb((byte)Math.Clamp(alpha,0,255),c.R,c.G,c.B);
    private static Color Mix(Color first,Color second,double amount)=>Color.FromRgb((byte)(first.R+(second.R-first.R)*amount),(byte)(first.G+(second.G-first.G)*amount),(byte)(first.B+(second.B-first.B)*amount));
    private static double Fraction(double value)=>value-Math.Floor(value);
    private static double Hash(int seed)
    {
        var n=unchecked((uint)(seed*374761393+668265263));n=(n^(n>>13))*1274126177;
        return (n^(n>>16))/(double)uint.MaxValue;
    }
}
