using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using VWP;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;
class Program
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 [STAThread] static void Main(string[] args)
 {
  if(args.Length==1 && args[0]=="--cinematic-check")
  {
   string cinematicOutput=Path.Combine(Environment.CurrentDirectory,"artifacts","cinematic-check");Directory.CreateDirectory(cinematicOutput);
   var checkApp=new Application();
   var checkWindow=new Window{Width=960,Height=540,Left=-2200,Top=-2200,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,Background=Brushes.Black};
   checkWindow.Loaded+=async(_,_)=>{
    try {
     using var audio=new AudioReaction();
     for(int id=0;id<28;id++)
     {
      string background=id<18?Path.Combine(AppContext.BaseDirectory,"assets",$"scene-{id}.png"):Path.Combine(AppContext.BaseDirectory,"assets","cinematic",$"{id}.png");
      using var visual=new InteractiveVisual(new(){Background=background,MotionId=id},new(){Performance="Quality"},Forms.Screen.PrimaryScreen!,audio);
      checkWindow.Content=visual;await Task.Delay(200);
      Check(visual.CinematicActive && visual.Spatial is null,"Cinematic mode "+id);
      var a=Pixels(visual);await Task.Delay(900);var b=Pixels(visual);
      int changed=0;for(int i=0;i<a.Length;i+=4)if(Math.Abs(a[i]-b[i])+Math.Abs(a[i+1]-b[i+1])+Math.Abs(a[i+2]-b[i+2])>12)changed++;
      Check(changed>80,"Static cinematic scene "+id+"; changed pixels "+changed);
      if(id==21){Capture(visual,Path.Combine(cinematicOutput,"alpine.png"));}
      File.AppendAllText(Path.Combine(cinematicOutput,"progress.txt"),$"{id}: {changed} changed pixels\n");
      checkWindow.Content=null;
     }
     File.WriteAllText(Path.Combine(cinematicOutput,"status.txt"),"PASS: all 28 cinematic scenes changed visible pixels");
    } catch(Exception e){File.WriteAllText(Path.Combine(cinematicOutput,"status.txt"),e.ToString());Environment.ExitCode=1;}
    finally{checkWindow.Close();checkApp.Shutdown(Environment.ExitCode);}
   };
   checkApp.Run(checkWindow);return;
  }
  if(args.Length==2 && (args[0]=="--desktop-probe" || args[0]=="--video-probe") && int.TryParse(args[1],out int sceneId))
  {
   string status=Path.Combine(Environment.CurrentDirectory,"artifacts","desktop-probe-status.txt");
   Directory.CreateDirectory(Path.GetDirectoryName(status)!);
   File.WriteAllText(status,"starting");
   var probeApp=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
   AudioReaction? probeAudio=null;
   DesktopHost? probeDesktop=null;
   probeApp.Startup+=(_,_)=>{
    try {
    File.AppendAllText(status,"\nstartup");
    probeAudio=new AudioReaction();
    File.AppendAllText(status,"\naudio");
    probeDesktop=new DesktopHost();
    File.AppendAllText(status,"\nhost");
    if(args[0]=="--video-probe")
    {
     probeDesktop.LoopRequired+=()=>probeApp.Dispatcher.BeginInvoke(new Action(()=>probeDesktop?.Replay()));
     probeDesktop.Play(Path.Combine(Environment.CurrentDirectory,"artifacts","alpine-probe.mp4"),Forms.Screen.PrimaryScreen!);
    }
    else
    {
     string cover=Path.Combine(AppContext.BaseDirectory,"assets","cinematic",$"{sceneId}.png");
     probeDesktop.PlayInteractive(new(){Background=cover,MotionId=sceneId},Forms.Screen.PrimaryScreen!,new(){Performance="Quality"},probeAudio,null);
    }
    File.AppendAllText(status,"\nplaying");
    int ticks=0;
    var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
    timer.Tick+=(_,_)=>{
     ticks++;
     var visual=probeDesktop.Interactive;
     File.AppendAllText(status,$"\n{ticks}: root={visual?.ActualWidth:0}x{visual?.ActualHeight:0} spatial={visual?.Spatial?.ActualWidth:0}x{visual?.Spatial?.ActualHeight:0} viewport={visual?.Spatial?.Viewport.ActualWidth:0}x{visual?.Spatial?.Viewport.ActualHeight:0} frames={visual?.RenderedFrames} time={visual?.Spatial?.Time:0.00}");
     if(ticks>=20){timer.Stop();probeApp.Shutdown();}
    };
    timer.Start();
    } catch(Exception e){File.AppendAllText(status,"\n"+e);probeApp.Shutdown(1);}
   };
   probeApp.Exit+=(_,_)=>{probeDesktop?.Dispose();probeAudio?.Dispose();};
   probeApp.Run();
   return;
  }
  string output=Path.Combine(Environment.CurrentDirectory,"artifacts","spatial-check");Directory.CreateDirectory(output);
  string thumbs=Path.Combine(Environment.CurrentDirectory,"assets","spatial");Directory.CreateDirectory(thumbs);
  var app=new Application();
  var window=new Window{Title="VWP · 18 scenes verification",Width=960,Height=540,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Left=80,Top=70,Topmost=true,ShowInTaskbar=false,Background=Brushes.Black};
  window.Loaded+=async(_,_)=>{
   try {
    Check(JsonSerializer.Deserialize<MonitorPreferences>("{}")!.SceneAnimation,"Migration");
    Check(!JsonSerializer.Deserialize<MonitorPreferences>("{\"SceneAnimation\":false}")!.SceneAnimation,"Opt-out");
    using var audio=new AudioReaction();
    for(int id=0;id<18;id++) {
     var config=new MonitorPreferences{Performance="Balance"};
     using var visual=new InteractiveVisual(new(){Background=Path.Combine(AppContext.BaseDirectory,"assets",$"scene-{id}.png"),MotionId=id},config,Forms.Screen.PrimaryScreen!,audio);
     window.Content=visual;await Task.Delay(450);
     Check(visual.Spatial is not null,"Missing 3D "+id);var scene=visual.Spatial!;
     Check(scene.MovingGroups>0,"Missing motion "+id);Check(scene.Time>0,"Clock "+id);
     visual.SetPaused(true);double stopped=scene.Time;await Task.Delay(120);Check(scene.Time==stopped,"Pause "+id);
     double first=0;
     foreach(double time in new[]{0.0,3.0,9.0,17.0}) {
      scene.Update(time);visual.InvalidateVisual();await Task.Delay(180);
      double stamp=Fingerprint(((ModelVisual3D)scene.Viewport.Children[0]).Content);
      if(time==0)first=stamp;else Check(Math.Abs(stamp-first)>.01,"Frozen transforms "+id);
      Capture(visual,Path.Combine(output,$"{id}-{time:0}.png"));
      if(time==3){var bitmap=new RenderTargetBitmap(960,540,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(thumbs,$"{id}.png"));encoder.Save(stream);}
     }
     visual.SetPaused(false);await Task.Delay(100);Check(scene.Time!=17,"Resume "+id);
     config.SceneAnimation=false;visual.Configure(config);Check(visual.Spatial is null,"Disable "+id);
     config.SceneAnimation=true;visual.Configure(config);Check(visual.Spatial is not null,"Enable "+id);
     visual.Dispose();double disposed=visual.AnimationSeconds;visual.SetPaused(false);await Task.Delay(30);Check(visual.AnimationSeconds==disposed,"Dispose "+id);
     window.Content=null;File.AppendAllText(Path.Combine(output,"progress.txt"),$"{id}: PASS\n");
    }
    LibVLCSharp.Shared.Core.Initialize();
    using(var desktop=new DesktopHost()) {
     for(int id=0;id<18;id++) {
      desktop.PlayInteractive(new(){Background=Path.Combine(AppContext.BaseDirectory,"assets",$"scene-{id}.png"),MotionId=id},Forms.Screen.PrimaryScreen!,new(){Performance="Balance"},audio,null);
      await Task.Delay(450);
      Check(desktop.Active && desktop.Healthy,"Desktop host "+id);
      var child=desktop.Interactive!;Check(child.ActualWidth>100 && child.ActualHeight>100,"Desktop layout "+id);
      Check(child.RenderedFrames>1 && child.Spatial!.Time>0,"Desktop animation "+id);
      desktop.SetUserPaused(true);double paused=child.Spatial!.Time;await Task.Delay(80);Check(child.Spatial.Time==paused,"Desktop pause "+id);
      desktop.SetUserPaused(false);await Task.Delay(80);Check(child.Spatial.Time>paused,"Desktop resume "+id);
      desktop.Stop();Check(!desktop.Active,"Desktop stop "+id);
     }
    }
    File.WriteAllText(Path.Combine(output,"status.txt"),"PASS: 18 3D scenes; changing transforms at 0/3/9/17 sec; pause/resume; disable/enable; disposal; migration; all 18 desktop hosts, layout, animation, pause/resume/stop. GPU tier="+(RenderCapability.Tier>>16));
   } catch(Exception e){File.WriteAllText(Path.Combine(output,"status.txt"),e.ToString());Environment.ExitCode=1;}
   finally{window.Close();app.Shutdown(Environment.ExitCode);}
  };
  app.Run(window);
 }
 static double Fingerprint(Model3D model){var m=model.Transform.Value;double v=m.OffsetX+3*m.OffsetY+7*m.OffsetZ+11*m.M11+13*m.M22+17*m.M33+19*m.M12;if(model is Model3DGroup group)foreach(var child in group.Children)v+=Fingerprint(child);return v;}
 static void Capture(FrameworkElement visual,string path){var bitmap=new RenderTargetBitmap(960,540,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);}
 static byte[] Pixels(FrameworkElement visual){var bitmap=new RenderTargetBitmap(960,540,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);byte[] pixels=new byte[960*540*4];bitmap.CopyPixels(pixels,960*4,0);return pixels;}
}
