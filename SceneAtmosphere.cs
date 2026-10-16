using System;
using System.Windows;
using System.Windows.Media;

namespace VWP;

// Built-in illustrated scenes may add weather and light, never opaque actors.
internal static class SceneAtmosphere
{
    static double Fraction(double value)=>value-Math.Floor(value);
    public static string Description(int id)=>id switch
    {
        0 or 1 or 6 or 9=>"Дождь и мягкое мерцание отражений",
        2 or 8 or 13=>"Листва и движение воздуха",
        3 or 4 or 10 or 11 or 24=>"Звёзды и мягкое свечение",
        12 or 21=>"Снежинки и зимний воздух",
        7 or 17 or 26=>"Блики и движение воды",
        14 or 20=>"Пар над чашкой и тёплый свет",
        _=>"Атмосфера, свет и движение воздуха"
    };
    public static void Draw(DrawingContext drawing,int id,double seconds,bool economy=false)
    {
        double cycle=((seconds%12)+12)%12/12,phase=cycle*Math.PI*2;
        bool rain=id is 0 or 1 or 6 or 9,snow=id is 12 or 21,water=id is 7 or 17 or 26;
        int count=economy?40:rain?96:72;
        var color=rain?Color.FromRgb(155,185,207):snow?Color.FromRgb(231,239,251):water?Color.FromRgb(139,194,211):Color.FromRgb(231,205,164);
        drawing.PushClip(new RectangleGeometry(new Rect(0,0,1920,1080)));
        for(int i=0;i<count;i++)
        {
            double x=Fraction(i*.6180339)*1920+Math.Sin(phase+i)*18;
            double y=Fraction(i*.381966+cycle*(rain?3:1)*(water?-1:1))*1080;
            double edge=Math.Clamp(Math.Min(y,1080-y)/45,0,1);
            byte alpha=(byte)((rain?72:105)*edge*(.60+.25*Math.Sin(phase*2+i)));
            var brush=new SolidColorBrush(Color.FromArgb(alpha,color.R,color.G,color.B));brush.Freeze();
            if(rain)drawing.DrawLine(new Pen(brush,.8),new(x,y),new(x-4,y+19));
            else drawing.DrawEllipse(brush,null,new(x,y),1.2+i%3,snow?1.2+i%3:.9+i%2);
        }
        if(id is 14 or 20)
        {
            double cupX=id==14?1500:555,cupY=id==14?705:515;
            for(int i=0;i<10;i++)
            {
                double p=Fraction(cycle+i*.1);var brush=new SolidColorBrush(Color.FromArgb((byte)(28*Math.Sin(p*Math.PI)),231,219,204));brush.Freeze();
                drawing.DrawEllipse(brush,null,new(cupX+Math.Sin(phase+p*5)*8,cupY-p*85),3+p*7,6+p*9);
            }
        }
        drawing.Pop();
    }
}
