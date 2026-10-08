using System;
using System.IO;
using System.Diagnostics;
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
    bool disposed;
    readonly Stopwatch clock=new();
    PlanetMotionEffect? planets;
    MediaPlayer? motionVideo;
    bool videoReady;
    public SpatialScene? Spatial {get;private set;}
    protected override int VisualChildrenCount=>Spatial is null?0:1;
    protected override Visual GetVisualChild(int index)=>index==0 && Spatial is not null?Spatial:throw new ArgumentOutOfRangeException(nameof(index));
    protected override Size MeasureOverride(Size available){Spatial?.Measure(available);return base.MeasureOverride(available);}
    protected override Size ArrangeOverride(Size size){Spatial?.Arrange(new Rect(size));return size;}
    public string? VideoError {get;private set;}
    public bool HasMovingBackground=>videoReady;
    public double VideoSeconds=>motionVideo?.Position.TotalSeconds??0;
    public double AnimationSeconds=>clock.Elapsed.TotalSeconds;
    public bool ObjectMotionActive=>layers.MotionId is >=0 and <SpatialScene.SceneCount && config.SceneAnimation;
    public int RenderedFrames {get;private set;}
    public double CursorOffsetX=>x;
    public InteractiveVisual(SceneLayers layers,MonitorPreferences config,Forms.Screen screen,AudioReaction audio)
    {
        this.layers=layers;this.config=config;this.screen=screen;this.audio=audio;
        background=Load(layers.Background!,PerformanceProfile.Resolve(config.Performance).MaxHeight);
        if(layers.Foreground is not null && File.Exists(layers.Foreground))foreground=Load(layers.Foreground,PerformanceProfile.Resolve(config.Performance).MaxHeight);
        accent=(Color)ColorConverter.ConvertFromString(layers.Accent);
        timer.Tick+=(_,_)=>{phase=clock.Elapsed.TotalSeconds;Spatial?.Update(phase);GetCursorPos(out var point);x+=(Math.Clamp((point.X-screen.Bounds.Left)/(double)screen.Bounds.Width-.5,-.5,.5)-x)*.08;y+=(Math.Clamp((point.Y-screen.Bounds.Top)/(double)screen.Bounds.Height-.5,-.5,.5)-y)*.08;beat+=(audio.Level-beat)*.25;if(planets is not null)planets.Time=phase;InvalidateVisual();};
        Configure(config);
        if(ObjectMotionActive && Spatial is null && layers.Video is string video && File.Exists(video))
        {
            motionVideo=new MediaPlayer{Volume=0};
            motionVideo.MediaOpened+=(_,_)=>{if(disposed)return;videoReady=true;if(paused)motionVideo.Pause();InvalidateVisual();};
            motionVideo.MediaEnded+=(_,_)=>{if(disposed)return;motionVideo.Position=TimeSpan.Zero;if(!paused)motionVideo.Play();};
            motionVideo.MediaFailed+=(_,e)=>{videoReady=false;VideoError=e.ErrorException.Message;InvalidateVisual();};
            motionVideo.Open(new Uri(Path.GetFullPath(video)));motionVideo.Play();
        }
        clock.Start();timer.Start();
    }
    public static BitmapSource Load(string path,int height)
    {
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelHeight=height;image.UriSource=new Uri(Path.GetFullPath(path));image.EndInit();image.Freeze();return image;
    }
    public void Configure(MonitorPreferences settings)
    {
        config=settings;timer.Interval=TimeSpan.FromSeconds(1.0/PerformanceProfile.Resolve(settings.Performance).Fps);
        if(ObjectMotionActive && Spatial is null){Spatial=new SpatialScene(layers.MotionId!.Value);AddVisualChild(Spatial);AddLogicalChild(Spatial);InvalidateMeasure();}
        else if(!ObjectMotionActive && Spatial is not null){RemoveVisualChild(Spatial);RemoveLogicalChild(Spatial);Spatial.Dispose();Spatial=null;InvalidateMeasure();}
        if(ObjectMotionActive && Spatial is null && layers.MotionId is int id && settings.Performance!="Eco" && PlanetMotionEffect.Available(id))
        {planets??=new PlanetMotionEffect(id);Effect=planets;}
        else{Effect=null;planets=null;}
        InvalidateVisual();
    }
    public void SetPaused(bool value){if(disposed)return;paused=value;if(value){timer.Stop();clock.Stop();motionVideo?.Pause();}else{clock.Start();timer.Start();motionVideo?.Play();}}
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);if(ActualWidth<=0||ActualHeight<=0)return;RenderedFrames++;
        if(Spatial is not null)return;
        drawing.PushClip(new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight)));
        if(ObjectMotionActive)
        {
            DrawScene(drawing,phase);
            drawing.Pop();return;
        }
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
    void DrawScene(DrawingContext drawing,double time)
    {
        double scale=config.Fit=="Fit"?Math.Min(ActualWidth/1920,ActualHeight/1080):Math.Max(ActualWidth/1920,ActualHeight/1080);
        double width=1920*scale,height=1080*scale;
        double dx=(ActualWidth-width)*config.FocusX,dy=(ActualHeight-height)*config.FocusY;
        drawing.DrawRectangle(Brushes.Black,null,new Rect(0,0,ActualWidth,ActualHeight));
        if(planets is not null)planets.Frame=new(dx/ActualWidth,dy/ActualHeight,width/ActualWidth,height/ActualHeight);
        drawing.PushTransform(new TranslateTransform(dx,dy));drawing.PushTransform(new ScaleTransform(scale,scale));
        drawing.PushClip(new RectangleGeometry(new Rect(0,0,1920,1080)));
        drawing.DrawImage(background,new Rect(0,0,1920,1080));
        if(videoReady && motionVideo is not null)drawing.DrawVideo(motionVideo,new Rect(0,0,1920,1080));
        SceneActors.Draw(drawing,layers.MotionId!.Value,time,config.Performance=="Eco");
        if(foreground is not null)drawing.DrawImage(foreground,new Rect(0,0,1920,1080));
        drawing.Pop();drawing.Pop();drawing.Pop();
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
    public void Dispose(){disposed=true;timer.Stop();clock.Stop();motionVideo?.Close();motionVideo=null;Spatial?.Dispose();Effect=null;}
    [StructLayout(LayoutKind.Sequential)] struct CursorPoint {public int X,Y;}
    [DllImport("user32.dll")] static extern bool GetCursorPos(out CursorPoint cursor);
}
