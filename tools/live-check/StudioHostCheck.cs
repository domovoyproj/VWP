using System;
using System.IO;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using VWP;
using Forms=System.Windows.Forms;

internal static class StudioHostCheck
{
    public static void Run()
    {
        Core.Initialize();string folder=Path.GetFullPath("artifacts/studio-host-check");Directory.CreateDirectory(folder);
        using var form=new Forms.Form{ClientSize=new System.Drawing.Size(1280,720),Text="VWP · wallpaper host verification"};
        form.Shown+=async(_,_)=>
        {
            using var host=new DesktopHost(form.Handle);using var audio=new AudioReaction();
            try
            {
                foreach(string file in Directory.GetFiles("assets/studio-originals","*.vwpscene",SearchOption.AllDirectories))
                {
                    var project=StudioProject.Load(Path.GetFullPath(file));await host.PlayStudio(project,Forms.Screen.PrimaryScreen!,"Balance",audio);await Task.Delay(600);
                    if(!host.Active||!host.Healthy)throw new Exception("Native wallpaper host failed: "+project.Id);
                    string? snapshot=await host.StudioSnapshot();if(snapshot is null)throw new Exception("Wallpaper produced no frame.");File.Copy(snapshot,Path.Combine(folder,project.Id+".png"),true);
                    host.SetUserPaused(true);await Task.Delay(2300);double time=host.StudioStats!.Time;await Task.Delay(2300);if(Math.Abs(host.StudioStats.Time-time)>.03)throw new Exception("Desktop host pause failed.");
                    host.SetAutomaticPause("Test");host.SetUserPaused(false);if(!host.Paused)throw new Exception("User pause released automatic pause.");host.SetAutomaticPause(null);await Task.Delay(2300);if(host.Paused||host.StudioStats.Time<=time+.1)throw new Exception("Desktop host resume failed.");
                }
                host.Stop();if(host.Active)throw new Exception("Native wallpaper host did not stop.");
                File.WriteAllText(Path.Combine(folder,"status.txt"),"PASS: three projects attach to native wallpaper HWND without star gate; scene switching, GPU capture, user/automatic pause precedence, resume and disposal.");
            }
            catch(Exception error){File.WriteAllText(Path.Combine(folder,"status.txt"),error.ToString());Environment.ExitCode=1;}
            finally{host.Stop();form.Close();}
        };
        Forms.Application.Run(form);
    }
}
