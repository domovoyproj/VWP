using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VWP;

// Moving foreground objects for the detailed backgrounds of the expansion scenes.
// Coordinates use the same 1920 x 1080 composition as SceneActors.
public static class CinematicActors
{
    static readonly Dictionary<string,Brush> brushes=new();
    static readonly Dictionary<(Brush,double),Pen> pens=new();
    static readonly Brush ink=Brush("#192B43");
    static readonly Brush pale=Brush("#EBD8BD");
    static readonly Brush light=Brush("#FFDCA8");
    static readonly Brush glass=Brush("#83B4C9");
    static readonly Pen softLine=new(Brush("#7AD1E9"),2);
    static BitmapSource? train;
    static Brush Brush(string hex){if(!brushes.TryGetValue(hex,out var b)){b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));b.Freeze();brushes[hex]=b;}return b;}
    static double Cycle(double t,double duration){double p=t/duration;return p-Math.Floor(p);}
    static void Line(DrawingContext d,Brush brush,double width,double x,double y,double x2,double y2)
    {
        if(!pens.TryGetValue((brush,width),out var pen)){pen=new Pen(brush,width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};pen.Freeze();pens[(brush,width)]=pen;}
        d.DrawLine(pen,new(x,y),new(x2,y2));
    }
    static void Ellipse(DrawingContext d,Brush brush,double x,double y,double rx,double ry)=>d.DrawEllipse(brush,null,new(x,y),rx,ry);
    static void Polygon(DrawingContext d,Brush brush,params Point[] points)
    {
        var shape=new StreamGeometry();using(var c=shape.Open()){c.BeginFigure(points[0],true,true);for(int i=1;i<points.Length;i++)c.LineTo(points[i],true,false);}shape.Freeze();d.DrawGeometry(brush,null,shape);
    }
    static void Person(DrawingContext d,double x,double y,double scale,double time,Brush coat)
    {
        d.PushTransform(new TranslateTransform(x,y));d.PushTransform(new ScaleTransform(scale,scale));
        double step=Math.Sin(time*4);
        Ellipse(d,Brush("#55000000"),0,2,20,4);
        Line(d,ink,8,-5,-41,-10+step*11,-3);
        Line(d,ink,8,5,-41,10-step*11,-3);
        Polygon(d,coat,new(-18,-91),new(14,-91),new(18,-42),new(-16,-42));
        Ellipse(d,pale,0,-103,13,14);
        Ellipse(d,ink,0,-115,15,7);
        Line(d,coat,8,-12,-80,-25+step*9,-50);
        Line(d,coat,8,12,-80,24-step*9,-50);
        d.Pop();d.Pop();
    }
    static void Particles(DrawingContext d,double t,int count,Brush brush,double drift)
    {
        for(int i=0;i<count;i++)
        {
            double x=(i*317.731+t*drift*(1+i%3))%1950-15;
            double y=(i*197.231+t*(8+i%5)*7)%1100-10;
            d.PushOpacity(.18+(i%4)*.09);
            Ellipse(d,brush,x,y,1.3+i%3,1.3+i%3);
            d.Pop();
        }
    }
    public static void Draw(DrawingContext d,int id,double t)
    {
        switch(id)
        {
            case 18: Greenhouse(d,t);break;
            case 19: Harbor(d,t);break;
            case 20: Cafe(d,t);break;
            case 21: Alpine(d,t);break;
            case 22: Festival(d,t);break;
            case 23: Metro(d,t);break;
            case 24: Observatory(d,t);break;
            case 25: Workshop(d,t);break;
            case 26: Underwater(d,t);break;
            case 27: Court(d,t);break;
        }
    }
    static void Greenhouse(DrawingContext d,double t)
    {
        double x=890+Math.Sin(t*.26)*145;
        Person(d,x,685,.72,t,Brush("#456D61"));
        d.PushTransform(new TranslateTransform(x+22,615));d.PushTransform(new RotateTransform(Math.Sin(t*2)*12));
        d.DrawRoundedRectangle(Brush("#A7ADB4"),null,new Rect(0,0,36,26),6,6);
        Line(d,Brush("#B5BDC4"),5,30,6,55,-7);
        for(int i=0;i<5;i++)Ellipse(d,Brush("#78CBE5"),60+i*12,20+i*7+Math.Sin(t*3+i)*5,2.5,4);
        d.Pop();d.Pop();
        Particles(d,t,18,Brush("#E2FFEE"),3);
    }
    static void Harbor(DrawingContext d,double t)
    {
        double x=-280+Cycle(t,34)*2400,y=575+Math.Sin(t*1.7)*3;
        d.PushTransform(new TranslateTransform(x,y));
        Polygon(d,ink,new(0,0),new(300,0),new(272,58),new(37,58));
        d.DrawRoundedRectangle(Brush("#E8DED1"),null,new Rect(40,-72,222,73),7,7);
        d.DrawRoundedRectangle(Brush("#6688A0"),null,new Rect(65,-52,170,27),3,3);
        for(int i=0;i<5;i++)d.DrawRectangle(light,null,new Rect(76+i*32,-45,19,13));
        Line(d,ink,4,160,-72,160,-113);
        Polygon(d,Brush("#D6B47F"),new(162,-108),new(210,-95),new(162,-83));
        d.Pop();
        for(int i=0;i<4;i++)
        {
            double rx=x+35+i*68,ry=y+71+i%2*8;
            Line(d,Brush("#88B9D5"),1.5,rx,ry,rx+48,ry);
        }
    }
    static void Cafe(DrawingContext d,double t)
    {
        d.PushClip(new RectangleGeometry(new Rect(0,0,1920,613)));
        Person(d,755+Math.Sin(t*.2)*45,593,.7,t,Brush("#533D39"));
        d.Pop();
        for(int i=0;i<5;i++)
        {
            double x=555+i*9+Math.Sin(t*.7+i)*8,y=515-Cycle(t+i*1.7,8)*120;
            d.PushOpacity(.23*(1-Cycle(t+i*1.7,8)));
            Ellipse(d,Brush("#F3E3C9"),x,y,7+i%3*2,13+i%3*4);
            d.Pop();
        }
        Particles(d,t,23,Brush("#F1CB9A"),1);
    }
    static BitmapSource Train()
    {
        if(train is not null)return train;
        string path=Path.Combine(AppContext.BaseDirectory,"assets","cinematic","train.png");
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri(path);image.EndInit();image.Freeze();train=image;return image;
    }
    static void Alpine(DrawingContext d,double t)
    {
        double p=Cycle(t,12),x=-900+p*3700,width=870;
        double rail=556+(x+width*.52)*.125;
        double top=rail-185;
        d.DrawImage(Train(),new Rect(x,top,width,width*724/2172));
        Particles(d,t,70,Brush("#F1F6FF"),-18);
    }
    static void Festival(DrawingContext d,double t)
    {
        for(int boat=0;boat<2;boat++)
        {
            double p=Cycle(t+boat*18,42),x=120+p*1450,y=790+boat*80+Math.Sin(t*.8+boat)*4;
            Polygon(d,ink,new(x,y),new(x+125,y),new(x+105,y+29),new(x+23,y+29));
            Ellipse(d,light,x+59,y-18,13,19);
            Line(d,Brush("#D7B891"),2,x+59,y-41,x+59,y-14);
            Line(d,Brush("#8BC3DE"),1,x+20,y+36,x+105,y+36);
        }
        for(int i=0;i<12;i++)
        {
            double p=Cycle(t+i*2.2,28),x=160+i*139+Math.Sin(t*.2+i)*18,y=890-p*500;
            d.PushOpacity(Math.Sin(Math.PI*p)*.65);
            d.DrawRoundedRectangle(light,null,new Rect(x,y,11,15),3,3);
            d.Pop();
        }
    }
    static void Metro(DrawingContext d,double t)
    {
        double p=Cycle(t,28),scale=.18+p*.85,x=480+p*280,y=493+p*213;
        d.PushTransform(new TranslateTransform(x,y));d.PushTransform(new ScaleTransform(scale,scale));
        Polygon(d,Brush("#D6D7D5"),new(-275,-220),new(180,-215),new(250,-150),new(250,0),new(-300,0));
        d.DrawRoundedRectangle(Brush("#29415B"),null,new Rect(-227,-180,355,93),14,14);
        for(int i=0;i<7;i++)d.DrawRectangle(light,null,new Rect(-210+i*49,-167,35,55));
        Ellipse(d,Brush("#FFF2D5"),189,-52,19,15);
        d.Pop();d.Pop();
        Person(d,1230+Math.Sin(t*.24)*30,840,.52,t,Brush("#495B79"));
    }
    static void Observatory(DrawingContext d,double t)
    {
        for(int i=0;i<24;i++)
        {
            double x=95+i*73,y=70+(i*107)%425;
            d.PushOpacity(.25+.5*(.5+.5*Math.Sin(t*1.3+i*2.7)));
            Ellipse(d,Brush("#DAECFF"),x,y,1.2+i%3,1.2+i%3);
            d.Pop();
        }
        Person(d,1280+Math.Sin(t*.27)*60,840,.72,t,Brush("#3E445B"));
        Line(d,Brush("#94BFEE"),1.5,1090,540,1125+Math.Sin(t*.3)*80,150);
    }
    static void Workshop(DrawingContext d,double t)
    {
        double swing=Math.Sin(t*.65)*34;
        Line(d,Brush("#4F6879"),23,1070,230,1010+swing,405);
        Line(d,Brush("#91A5AB"),18,1010+swing,405,950+swing*1.6,510);
        d.DrawRoundedRectangle(Brush("#AABAC1"),null,new Rect(931+swing*1.6,492,42,58),7,7);
        for(int i=0;i<5;i++)
        {
            double x=950+swing*1.6+i*17,y=552+Cycle(t+i*.35,1.5)*85;
            d.PushOpacity(1-Cycle(t+i*.35,1.5));Ellipse(d,light,x,y,2.5,4);d.Pop();
        }
    }
    static void Underwater(DrawingContext d,double t)
    {
        double x=-240+Cycle(t,35)*2350,y=440+Math.Sin(t*.5)*30;
        d.PushTransform(new TranslateTransform(x,y));
        Ellipse(d,Brush("#B9C9C5"),0,0,118,42);
        Ellipse(d,ink,12,-8,45,22);
        Ellipse(d,glass,12,-8,32,14);
        Polygon(d,Brush("#8DA7A9"),new(-95,-7),new(-150,-37),new(-143,15));
        Line(d,Brush("#F4E7AD"),5,110,-4,225,-37);
        d.Pop();
        for(int i=0;i<24;i++)
        {
            double p=Cycle(t+i*.7,9),bx=120+i*79+Math.Sin(t*.3+i)*10,by=1050-p*950;
            d.PushOpacity(.3+.3*Math.Sin(Math.PI*p));d.DrawEllipse(null,softLine,new(bx,by),3+i%5,3+i%5);d.Pop();
        }
    }
    static void Court(DrawingContext d,double t)
    {
        double p=Cycle(t,5),x=650+p*540,y=725-240*Math.Sin(Math.PI*p);
        Person(d,640+Math.Sin(t*.7)*90,895,.84,t,Brush("#353E53"));
        Person(d,1260+Math.Sin(t*.5)*65,870,.8,t+1.8,Brush("#6A3E42"));
        Ellipse(d,Brush("#D99146"),x,y,15,15);
        Line(d,Brush("#6F3B29"),1.2,x-14,y,x+14,y);
        Particles(d,t,13,Brush("#F7D5A7"),2);
    }
}
