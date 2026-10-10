using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using VWP;
using Forms=System.Windows.Forms;

static class TransitionCheck
{
    static void Check(bool value,string name){if(!value)throw new Exception(name);}
    public static void Run()
    {
        Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);Core.Initialize();
        string folder=Path.Combine(Environment.CurrentDirectory,"artifacts","transition-check");Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"stage.txt"),"starting\n");
        File.WriteAllText(Path.Combine(folder,"colors.csv"),"");
        foreach(var video in new[]{("blue","0x122A46"),("warm","0xD67240")})
        {
            string path=Path.Combine(folder,video.Item1+".mp4");if(File.Exists(path))continue;
            string bundled=Path.Combine(Environment.CurrentDirectory,"tools","ffmpeg.exe");
            var start=new ProcessStartInfo(File.Exists(bundled)?bundled:"ffmpeg"){UseShellExecute=false,CreateNoWindow=true};
            foreach(string argument in new[]{"-y","-v","error","-f","lavfi","-i",$"color=c={video.Item2}:s=640x360:r=30:d=8","-c:v","libx264","-pix_fmt","yuv420p",path})start.ArgumentList.Add(argument);
            using var process=Process.Start(start)!;
            if(!process.WaitForExit(20000)){process.Kill();throw new Exception("Control video generation timed out");}
            Check(process.ExitCode==0,"Control video generation failed");
        }
        void Stage(string value)=>File.AppendAllText(Path.Combine(folder,"stage.txt"),value+"\n");
        using var form=new Forms.Form{ClientSize=new Size(640,360),StartPosition=Forms.FormStartPosition.CenterScreen,FormBorderStyle=Forms.FormBorderStyle.None,TopMost=true,ShowInTaskbar=false};
        DesktopHost? host=null;
        form.Shown+=async(_,_)=>
        {
            try
            {
                ShowWindow(form.Handle,5);SetWindowPos(form.Handle,new IntPtr(-1),0,0,0,0,0x43);
                host=new DesktopHost(form.Handle);
                var dispatcher=Dispatcher.CurrentDispatcher;
                host.SurfaceReady+=()=>dispatcher.BeginInvoke(new Action(host.CompleteRecovery));
                string Video(string id)=>Path.Combine(folder,id+".mp4");string Cover(string id)=>Path.Combine(folder,id+".png");
                using(var bitmap=new Bitmap(640,360))using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.FromArgb(18,42,70));bitmap.Save(Cover("blue"));g.Clear(Color.FromArgb(214,114,64));bitmap.Save(Cover("warm"));}
                var desktop=new FakeDesktop();
                using(var backdrop=new StaticBackdrop(desktop,Path.Combine(folder,"backdrop")))
                {
                    backdrop.Apply(Cover("blue"),Forms.Screen.PrimaryScreen!);backdrop.Apply(Cover("warm"),Forms.Screen.PrimaryScreen!);backdrop.Restore();
                    Check(desktop.Changes.SequenceEqual(new[]{Cover("blue"),Cover("warm"),"original"}),"Backdrop replaces scenes directly and restores the original only on stop");
                    backdrop.Apply(Cover("blue"),Forms.Screen.PrimaryScreen!);desktop.Current="user-selected";backdrop.Apply(Cover("warm"),Forms.Screen.PrimaryScreen!);backdrop.Restore();
                    Check(desktop.Current=="user-selected","A separately selected Windows wallpaper remains the restore target");
                    backdrop.Apply(Cover("blue"),Forms.Screen.PrimaryScreen!);desktop.FailNext=true;
                    try{backdrop.Apply(Cover("warm"),Forms.Screen.PrimaryScreen!);throw new Exception("Backdrop failure was swallowed");}catch(InvalidOperationException){}
                    backdrop.Restore();Check(desktop.Current=="user-selected","Failed replacement keeps the previous restore journal");
                }
                host.Play(Video("blue"),Forms.Screen.PrimaryScreen!,Cover("blue"));
                ShowWindow(form.Handle,5);SetWindowPos(form.Handle,new IntPtr(-1),0,0,0,0,0x43);form.Activate();
                await Task.Delay(1400);
                File.WriteAllText(Path.Combine(folder,"window.txt"),$"Visible={form.Visible} native={IsWindowVisible(form.Handle)} bounds={form.Bounds} capture={form.PointToScreen(new Point(320,180))} handle={form.Handle} healthy={host.Healthy} playing={host.Playing}");
                for(int i=0;i<30&&Pixel(form).R>=40;i++)await Task.Delay(100);
                using(var capture=new Bitmap(640,360)){using(var g=Graphics.FromImage(capture))g.CopyFromScreen(form.PointToScreen(Point.Empty),Point.Empty,capture.Size);capture.Save(Path.Combine(folder,"initial.png"));}
                Check(Pixel(form).R<40,"Blue video must be visible: "+Pixel(form));
                Stage("first transition");host.Play(Video("warm"),Forms.Screen.PrimaryScreen!,Cover("warm"),true);Stage("sampling first");
                var colors=await Sample(form,folder,2400);
                Check(colors.Distinct().Count()>8,"There must be visible intermediate blend colors");
                Check(colors.All(c=>c.R>8&&c.G>25&&c.B>40),"Transition must never show black");
                Check(colors.Last().R>180,"New video must replace the old image");
                Check(colors.Zip(colors.Skip(1),(a,b)=>Math.Abs(a.R-b.R)).Max()<65,"No abrupt scene cut");
                Stage("rapid transition");host.Play(Video("blue"),Forms.Screen.PrimaryScreen!,Cover("blue"),true);await Task.Delay(650);
                Color before=Pixel(form);host.Play(Video("warm"),Forms.Screen.PrimaryScreen!,Cover("warm"),true);Color after=Pixel(form);
                Check(Math.Abs(before.R-after.R)<40,"Rapid switching preserves the current blend");
                await Sample(form,folder,2100);
                Stage("paused transition");host.Play(Video("blue"),Forms.Screen.PrimaryScreen!,Cover("blue"),true);Stage("pause request");host.SetUserPaused(true);await Sample(form,folder,3000);Stage("pause result");
                Check(host.Paused&&Pixel(form).R<40,"Paused scene transitions to its frame: "+Pixel(form)+" position="+host.Position);
                long frozen=host.Position;await Task.Delay(500);
                Check(!host.Playing&&Math.Abs(host.Position-frozen)<150,"Requested pause freezes playback after the transition");
                host.Stop();await Task.Delay(100);
                Check(FindWindowEx(form.Handle,IntPtr.Zero,"STATIC","VWP Transition")==IntPtr.Zero,"Stop removes transition surfaces");
                File.WriteAllText(Path.Combine(folder,"status.txt"),"PASS: visible 1s dissolve, no black/cut, rapid replacement, paused transition, cleanup and direct backdrop replacement/restore");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(folder,"status.txt"),e.ToString());Environment.ExitCode=1;}
            finally{host?.Dispose();form.Close();}
        };
        Forms.Application.Run(form);
    }
    static Color Pixel(Forms.Form form){using var image=new Bitmap(1,1);using var g=Graphics.FromImage(image);var point=form.PointToScreen(new Point(320,180));g.CopyFromScreen(point,Point.Empty,new Size(1,1));return image.GetPixel(0,0);}
    static async Task<Color[]> Sample(Forms.Form form,string folder,int duration)
    {
        var colors=new System.Collections.Generic.List<Color>();var clock=Stopwatch.StartNew();
        while(clock.ElapsedMilliseconds<duration){colors.Add(Pixel(form));await Task.Delay(40);}
        File.AppendAllText(Path.Combine(folder,"colors.csv"),string.Join("\n",colors.Select(c=>$"{c.R},{c.G},{c.B}"))+"\n");return colors.ToArray();
    }
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
    sealed class FakeDesktop:StaticBackdrop.IDesktopWallpaper
    {
        public string Current="original";
        public bool FailNext;
        public readonly System.Collections.Generic.List<string> Changes=new();
        public void SetWallpaper(string monitor,string wallpaper){if(FailNext){FailNext=false;throw new InvalidOperationException("Simulated Windows failure");}Current=wallpaper;Changes.Add(wallpaper);}
        public void GetWallpaper(string monitor,out string wallpaper)=>wallpaper=Current;
        public void GetMonitorDevicePathAt(uint index,out string monitor)=>monitor="primary";
        public void GetMonitorDevicePathCount(out uint count)=>count=1;
        public void GetMonitorRECT(string monitor,out StaticBackdrop.Rect rect){var bounds=Forms.Screen.PrimaryScreen!.Bounds;rect=new(){Left=bounds.Left,Top=bounds.Top,Right=bounds.Right,Bottom=bounds.Bottom};}
    }
}
