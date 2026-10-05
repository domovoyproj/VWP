using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace VWP;
public sealed class InteractiveVisual : FrameworkElement,IDisposable
{
    readonly DispatcherTimer timer=new();
    readonly Forms.Screen screen;
    readonly AudioReaction audio;
    readonly SceneLayers layers;
    readonly BitmapSource background;
    readonly BitmapSource? foreground;
    readonly Color accent;
    MonitorPreferences config;
    double x,y,phase,beat;
    bool paused;
    public int RenderedFrames {get;private set;}
    public double CursorOffsetX=>x;
    public InteractiveVisual(SceneLayers layers,MonitorPreferences config,Forms.Screen screen,AudioReaction audio)
    {
        this.layers=layers;this.config=config;this.screen=screen;this.audio=audio;
        background=Load(layers.Background!,PerformanceProfile.Resolve(config.Performance).MaxHeight);
        if(layers.Foreground is not null && File.Exists(layers.Foreground))foreground=Load(layers.Foreground,PerformanceProfile.Resolve(config.Performance).MaxHeight);
        accent=(Color)ColorConverter.ConvertFromString(layers.Accent);
        timer.Tick+=(_,_)=>{phase+=timer.Interval.TotalSeconds;GetCursorPos(out var point);x+=(Math.Clamp((point.X-screen.Bounds.Left)/(double)screen.Bounds.Width-.5,-.5,.5)-x)*.08;y+=(Math.Clamp((point.Y-screen.Bounds.Top)/(double)screen.Bounds.Height-.5,-.5,.5)-y)*.08;beat+=(audio.Level-beat)*.25;InvalidateVisual();};
        Configure(config);timer.Start();
    }
    public static BitmapSource Load(string path,int height)
    {
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelHeight=height;image.UriSource=new Uri(Path.GetFullPath(path));image.EndInit();image.Freeze();return image;
    }
    public void Configure(MonitorPreferences settings){config=settings;timer.Interval=TimeSpan.FromSeconds(1.0/PerformanceProfile.Resolve(settings.Performance).Fps);InvalidateVisual();}
    public void SetPaused(bool value){paused=value;if(value)timer.Stop();else timer.Start();}
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);if(ActualWidth<=0||ActualHeight<=0)return;RenderedFrames++;
        drawing.PushClip(new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight)));
        DrawPlane(drawing,background,.025);
        if(foreground is not null)DrawPlane(drawing,foreground,.06);
        double music=config.MusicReactive?beat:0;
        var glow=new RadialGradientBrush(Color.FromArgb((byte)(18+music*55),accent.R,accent.G,accent.B),Colors.Transparent){Center=new Point(.72,.4),GradientOrigin=new Point(.72,.4)};
        drawing.DrawRectangle(glow,null,new Rect(0,0,ActualWidth,ActualHeight));
        var brush=new SolidColorBrush(Color.FromArgb((byte)(70+music*150),accent.R,accent.G,accent.B));brush.Freeze();
        int count=PerformanceProfile.Resolve(config.Performance).Particles;
        for(int i=0;i<count;i++)
        {
            double px=Fraction(i*.6180339)*ActualWidth+Math.Sin(phase*.35+i)*15+x*25;
            double py=Fraction(i*.381966+phase*(layers.Effect=="Rain"?.14:.018)*(layers.Effect=="Embers"?-1:1))*ActualHeight;
            double size=1+(i%3)+music*3;
            if(layers.Effect=="Rain")drawing.DrawLine(new Pen(brush,1),new Point(px,py),new Point(px-3,py+14));
            else drawing.DrawEllipse(brush,null,new Point(px,py),size,layers.Effect=="Petals"?size*.5:size);
        }
        drawing.Pop();
    }
    void DrawPlane(DrawingContext drawing,BitmapSource image,double depth)
    {
        double scale=Math.Max(ActualWidth/image.PixelWidth,ActualHeight/image.PixelHeight)*1.08;
        double width=image.PixelWidth*scale,height=image.PixelHeight*scale;
        double dx=(ActualWidth-width)*config.FocusX+x*ActualWidth*depth*config.Depth;
        double dy=(ActualHeight-height)*config.FocusY+y*ActualHeight*depth*config.Depth;
        drawing.DrawImage(image,new Rect(dx,dy,width,height));
    }
    static double Fraction(double value)=>value-Math.Floor(value);
    public void Dispose()=>timer.Stop();
    [StructLayout(LayoutKind.Sequential)] struct CursorPoint {public int X,Y;}
    [DllImport("user32.dll")] static extern bool GetCursorPos(out CursorPoint cursor);
}
