using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace VWP;
public sealed partial class InteractiveVisual
{
    void DrawEnvironment(DrawingContext drawing,double seconds)
    {
        double t=seconds*layers.Speed,w=ActualWidth,h=ActualHeight;
        double music=WantsAudio&&config.Performance!="Eco"?beat:0;
        int count=(int)(PerformanceProfile.Resolve(config.Performance).Particles*layers.Density);
        double top=h*layers.RegionTop,bottom=h*layers.RegionBottom,height=bottom-top;
        drawing.PushClip(new RectangleGeometry(new Rect(0,top,w,height)));
        if(layers.Effect=="Water")
        {
            double amount=layers.Parallax;
            double scale=(config.Fit=="Fit"?Math.Min(w/background.PixelWidth,h/background.PixelHeight):Math.Max(w/background.PixelWidth,h/background.PixelHeight))*(1+.08*amount);
            double iw=background.PixelWidth*scale,ih=background.PixelHeight*scale;
            double dx=(w-iw)*config.FocusX+x*w*.025*amount,dy=(h-ih)*config.FocusY+y*h*.025*amount;
            for(int row=0;row<36;row++)
            {
                double stripe=height/36,yy=top+row*stripe;
                double strength=Math.Sin((row+.5)/36*Math.PI)*layers.Density;
                drawing.PushClip(new RectangleGeometry(new Rect(0,yy,w,stripe+1)));
                drawing.PushOpacity(.7);drawing.DrawImage(background,new Rect(dx+Math.Sin(t*1.4+row*.65)*strength*2,dy+Math.Cos(t+row*.45)*strength*.5,iw,ih));drawing.Pop();drawing.Pop();
            }
        }
        if(layers.Effect=="Lightning")
        {
            for(int bolt=0;bolt<6;bolt++)
            {
                double opacity=Math.Pow(.5+.5*Math.Sin(t*(2+bolt%2)+bolt*1.7),8)*layers.Density;
                double bx=w*(bolt<3?.08+bolt*.08:.76+(bolt-3)*.08),by=top+height*(.15+Fraction(bolt*.381)*.6);
                var shape=new StreamGeometry();using(var ctx=shape.Open())
                {
                    ctx.BeginFigure(new Point(bx,by),false,false);
                    for(int j=1;j<15;j++)ctx.LineTo(new Point(bx+Math.Sin(j*2.3+t*3+bolt)*6,by+j*height*.018),true,false);
                }
                shape.Freeze();drawing.PushOpacity(Math.Clamp(opacity,0,1));
                drawing.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb(55,accent.R,accent.G,accent.B)),8*layers.ParticleSize),shape);
                drawing.DrawGeometry(null,new Pen(new SolidColorBrush(accent),2*layers.ParticleSize),shape);drawing.DrawGeometry(null,new Pen(Brushes.White,.7*layers.ParticleSize),shape);drawing.Pop();
            }
        }
        for(int i=0;i<count && layers.Effect is not ("None" or "Water" or "Lightning");i++)
        {
            double seed=i*2.39996,depth=.35+Fraction(i*.754877)*.65;
            double px=Fraction(i*.6180339+t*layers.Wind*.025)*w+Math.Sin(t*.6+seed)*10;
            double py=top+Fraction(i*.381966+t*(layers.Effect=="Rain"?.35:layers.Effect=="Embers"?-.07:.035)*depth)*(height+40)-20;
            double radius=(.7+depth*2)*layers.ParticleSize*(1+music*.8);
            byte alpha=(byte)Math.Clamp(65+depth*100+music*70,0,255);
            Color color=layers.Effect=="Snow"?Color.FromArgb(alpha,238,246,255):Color.FromArgb(alpha,accent.R,accent.G,accent.B);
            var brush=new SolidColorBrush(color);brush.Freeze();
            switch(layers.Effect)
            {
                case "Rain": drawing.DrawLine(new Pen(brush,Math.Max(.5,radius*.3)),new Point(px,py),new Point(px+layers.Wind*16,py+12+depth*18));break;
                case "Petals": case "Leaves":
                    drawing.PushTransform(new RotateTransform(Math.Sin(t+seed)*65,px,py));
                    drawing.DrawEllipse(brush,null,new Point(px,py),radius*1.8,radius*.7);drawing.Pop();break;
                case "Stars": case "Fireflies":
                    drawing.PushOpacity(.25+.75*Math.Pow(.5+.5*Math.Sin(t+seed),2));
                    drawing.DrawEllipse(brush,null,new Point(Fraction(i*.6180339)*w+Math.Sin(t*.3+seed)*12,top+Fraction(i*.381966)*height+Math.Cos(t*.4+seed)*9),radius,radius);drawing.Pop();break;
                case "Mist":
                    if(i>8)break;
                    var fog=new RadialGradientBrush(Color.FromArgb(18,accent.R,accent.G,accent.B),Colors.Transparent);
                    drawing.DrawEllipse(fog,null,new Point(Fraction(i*.6180339)*w+Math.Sin(t*.15+seed)*w*.1,top+height*.6+Math.Cos(t*.2+seed)*20),w*.3,height*.18);break;
                default: drawing.DrawEllipse(brush,null,new Point(px,py),radius,radius);break;
            }
        }
        drawing.Pop();
        if(music>.01)
        {
            var glow=new RadialGradientBrush(Color.FromArgb((byte)(music*45),accent.R,accent.G,accent.B),Colors.Transparent);
            drawing.DrawRectangle(glow,null,new Rect(0,0,w,h));
        }
        if(layers.Clock)
        {
            var now=DateTime.Now;
            double size=Math.Clamp(h*.065,16,72);
            var text=new FormattedText(now.ToString("HH:mm")+"\n"+now.ToString("dddd, d MMMM",CultureInfo.CurrentCulture),CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip){TextAlignment=TextAlignment.Center};
            drawing.DrawText(text,new Point(w/2,h*.06));
        }
    }
}
