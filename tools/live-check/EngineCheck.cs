using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using VWP;
using Forms=System.Windows.Forms;
internal static class EngineCheck
{
    static void Check(bool value,string label){if(!value)throw new Exception(label);}
    static byte[] Pixels(FrameworkElement visual)
    {
        visual.Measure(new Size(640,360));visual.Arrange(new Rect(0,0,640,360));visual.UpdateLayout();
        var bitmap=new RenderTargetBitmap(640,360,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var data=new byte[640*360*4];bitmap.CopyPixels(data,640*4,0);return data;
    }
    internal static void Run()
    {
        string output=Path.Combine(Environment.CurrentDirectory,"artifacts","engine-check");Directory.CreateDirectory(output);
        try
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/VWP;component/StudioResources.xaml")});ThemeService.Apply("Dark","#9AB7F9");
            string source=Path.GetFullPath("assets/village-winter/1701.png");
            using var audio=new AudioReaction();
            foreach(string effect in SceneDesigner.Effects.Keys)
            {
                var layers=new SceneLayers{Background=source,Effect=effect,Realtime=true,Parallax=0,Density=2};
                using var visual=new InteractiveVisual(layers,new(){Performance="Balance"},Forms.Screen.PrimaryScreen!,audio);visual.SetPaused(true);
                visual.RenderAt(0);var before=Pixels(visual);visual.RenderAt(2);var after=Pixels(visual);
                int changed=Enumerable.Range(0,before.Length/4).Count(i=>Math.Abs(before[i*4]-after[i*4])+Math.Abs(before[i*4+1]-after[i*4+1])+Math.Abs(before[i*4+2]-after[i*4+2])>5);
                Check(effect=="None"?changed==0:changed>10,"Effect render: "+effect+" changed="+changed);
            }
            var custom=new SceneLayers{Background=source,Realtime=true,Effect="Snow",Speed=1.7,Density=1.3,Wind=-.4,RegionTop=.2,RegionBottom=.8,Clock=true,AudioReactive=true,Parallax=.8};
            var wall=new Wallpaper("Winter live",source,source,"Live scene");string bundle=Path.Combine(output,"scene.vwpbundle");if(File.Exists(bundle))File.Delete(bundle);
            BundleService.Export(bundle,"Test","VWP",new[]{wall},new(),_=>custom).GetAwaiter().GetResult();
            var imported=BundleService.Import(bundle,Path.Combine(output,"imported")).GetAwaiter().GetResult().Scenes.Single();
            Check(imported.Layers?.Speed==1.7&&imported.Layers.Realtime&&imported.Layers.Clock&&imported.Layers.AudioReactive&&imported.Layers.RegionTop==.2,"Image scene bundle properties round trip");
            Check(File.Exists(imported.Video)&&File.Exists(imported.Layers!.Background),"Image scene assets round trip");
            var editor=new SceneDesigner("Зимний вечер",new(){Background=source,Realtime=true,Effect="Snow",Accent="#C8E8FF"});
            editor.Show();Pump(200);editor.UpdateLayout();
            var screenshot=new RenderTargetBitmap((int)editor.ActualWidth,(int)editor.ActualHeight,96,96,PixelFormats.Pbgra32);screenshot.Render(editor);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(screenshot));using(var file=File.Create(Path.Combine(output,"designer.png")))png.Save(file);
            editor.Close();
            Core.Initialize();
            using(var surface=new Forms.Form{Text="VWP scene playback check",ClientSize=new System.Drawing.Size(640,360),ShowInTaskbar=false})
            using(var host=new DesktopHost(surface.Handle))
            {
                surface.Show();host.PlayInteractive(custom,Forms.Screen.PrimaryScreen!,new(){Performance="Balance"},audio,null);Pump(500);
                Check(host.Active&&host.Healthy&&host.Interactive?.RenderedFrames>0,"Realtime scene plays in native host");
                host.SetUserPaused(true);double frozen=host.Interactive!.AnimationSeconds;Pump(200);Check(host.Paused&&Math.Abs(host.Interactive.AnimationSeconds-frozen)<.03,"Realtime pause freezes animation clock");
                host.SetAutomaticPause("test application");host.SetUserPaused(false);Check(host.Paused,"Application policy retains automatic pause");
                host.SetAutomaticPause(null);Pump(150);Check(!host.Paused&&host.Interactive.AnimationSeconds>frozen,"Realtime resumes after application rule clears");
                host.Stop();Check(!host.Active&&host.Interactive is null,"Native realtime cleanup");surface.Close();
            }
            Check(PausePolicy.ApplicationNames("CS2.exe, blender.exe;cs2.exe").Length==2,"Application rule normalization");
            Check(PausePolicy.MatchingApplication(new[]{"cs2"},new[]{"CS2","explorer"})=="cs2","Case insensitive application rules");
            Check(LibrarySearch.Matches(new("Берёзы",source,null,"1080p","Деревня","Зимний снег"),"ДЕРЕВНЯ снег"),"Search across metadata");
            custom.Speed=double.NaN;Check(!custom.ValidProperties(),"Reject malformed scene properties");
            File.WriteAllText(Path.Combine(output,"status.txt"),"PASS: eleven effect modes; quiet no-effect scene; image bundle round trip; native realtime host playback/pause/resume/cleanup; property validation; application rules; metadata search; scene designer rendered.");
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"status.txt"),error.ToString());Environment.ExitCode=1;}
    }
    static void Pump(int milliseconds)
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};
        timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
}
