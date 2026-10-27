using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using VWP;
using Forms=System.Windows.Forms;

static class ElectricCheck
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        Core.Initialize();
        string folder=Path.Combine(Environment.CurrentDirectory,"artifacts","electric-check");Directory.CreateDirectory(folder);
        using var form=new Forms.Form{ClientSize=Forms.Screen.PrimaryScreen!.Bounds.Size,Text="VWP Electric verification",StartPosition=Forms.FormStartPosition.CenterScreen,TopMost=true,FormBorderStyle=Forms.FormBorderStyle.None};
        DesktopHost? host=null;
        form.Shown+=async(_,_)=>
        {
            try
            {
                ShowWindow(form.Handle,5);form.Activate();
                Check(DesktopHost.ClockText(new DateTime(2026,10,26,21,20,0))=="MONDAY\n26 OCTOBER, 2026\n21:20","Clock format");
                Check(DesktopHost.ClockText(new DateTime(2026,10,27,0,0,0)).StartsWith("TUESDAY\n27 OCTOBER"),"Date rollover");
                host=new DesktopHost(form.Handle);
                var dispatcher=Dispatcher.CurrentDispatcher;
                host.SurfaceReady+=()=>dispatcher.BeginInvoke(new Action(host.CompleteRecovery));
                foreach(int id in new[]{1401,1402})
                {
                    host.Play(Path.GetFullPath($"assets/electric/{id}.mp4"),Forms.Screen.PrimaryScreen!);
                    host.ShowClock(true);
                    for(int i=0;i<50&&(!host.Playing||host.Position<100);i++)await Task.Delay(100);
                    Check(host.Playing&&host.Healthy,"Video playing: "+id);
                    await Task.Delay(1300);
                    Check(host.ClockEnabled&&host.ClockCaption==DesktopHost.ClockText(DateTime.Now),"Live local clock: "+id);
                    string capture=Path.Combine(folder,id+".png");
                    Check(host.CaptureClockFrame(capture),"Video snapshot");
                    for(int i=0;i<20&&!File.Exists(capture);i++)await Task.Delay(100);
                    Check(File.Exists(capture),"Snapshot written");
                    if(Environment.GetEnvironmentVariable("VWP_ELECTRIC_PREVIEW")=="1")await Task.Delay(20000);
                    host.SetUserPaused(true);await Task.Delay(300);long frozen=host.Position;await Task.Delay(350);
                    Check(host.Paused&&Math.Abs(host.Position-frozen)<160,"Pause: "+id);
                    host.SetUserPaused(false);await Task.Delay(400);
                    host.Replay();await Task.Delay(1400);
                    Check(host.ClockEnabled&&host.ClockCaption==DesktopHost.ClockText(DateTime.Now),"Clock survives replay");
                    host.Stop();Check(!host.ClockEnabled,"Stop cleans timer");
                }
                host.Play(Path.GetFullPath("assets/electric/1401.mp4"),Forms.Screen.PrimaryScreen!);
                Check(!host.ClockEnabled,"Ordinary video has no inherited clock");
                File.WriteAllText(Path.Combine(folder,"status.txt"),"PASS: both videos play; live clock, date rollover, pause/resume, replay and cleanup");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(folder,"status.txt"),e.ToString());Environment.ExitCode=1;}
            finally{host?.Dispose();form.Close();}
        };
        Forms.Application.Run(form);
    }
    [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
}

