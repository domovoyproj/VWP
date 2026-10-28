using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VWP;
using Forms=System.Windows.Forms;

internal static class StudioVisibilityCheck
{
    public static void Run()
    {
        string folder=Path.GetFullPath("artifacts/studio-visibility-check");Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"status.txt"),"RUNNING");
        using var form=new Forms.Form{ClientSize=new System.Drawing.Size(960,540),Text="VWP · desktop visibility clock"};
        using var surface=new StudioSurface();form.Controls.Add(surface.Control);
        form.Shown+=async(_,_)=>
        {
            try
            {
                await surface.Initialize(StudioLibrary.Load(Path.GetFullPath("assets/studio-library/scenes/1906.vwpscene")),"Balance",true);
                surface.Control.Visible=false;form.Hide();
                for(int i=0;i<40&&await surface.Execute("document.visibilityState")!="\"hidden\"";i++)await Task.Delay(100);
                if(await surface.Execute("document.visibilityState")!="\"hidden\"")throw new Exception("Hidden native WebView precondition failed.");
                await Task.Delay(300);var start=await surface.ReadStatistics();await Task.Delay(2000);var running=await surface.ReadStatistics();
                if(running.Time<start.Time+1||running.Frames<start.Frames+10)throw new Exception("Hidden desktop WebView stopped animation.");
                surface.SetPaused(true);await Task.Delay(250);var frozen=await surface.ReadStatistics();await Task.Delay(650);var paused=await surface.ReadStatistics();
                if(paused.Frames!=frozen.Frames||Math.Abs(paused.Time-frozen.Time)>.001)throw new Exception("Hidden pause failed.");
                await surface.SetExportViewport(1920,1080);await surface.Seek(0);
                byte[] first=await surface.ExportFrame(1920,1080);await surface.Seek(24);byte[] last=await surface.ExportFrame(1920,1080);
                if(!first.SequenceEqual(last))throw new Exception("Hidden GPU capture lost the exact loop endpoint.");
                using(var png=new System.Drawing.Bitmap(new MemoryStream(first)))if(png.Width!=1920||png.Height!=1080)throw new Exception("Hidden capture is not native FHD.");
                await File.WriteAllBytesAsync(Path.Combine(folder,"hidden-native-frame.png"),first);await surface.Seek(frozen.Time);
                surface.SetPaused(false);await Task.Delay(900);if((await surface.ReadStatistics()).Time<frozen.Time+.3)throw new Exception("Hidden resume failed.");
                surface.Control.Visible=true;form.Show();await Task.Delay(1000);var visible=await surface.ReadStatistics();
                await Task.Delay(800);var after=await surface.ReadStatistics();if(after.Time<visible.Time+.3)throw new Exception("Visibility transition stopped animation.");
                File.WriteAllText(Path.Combine(folder,"status.txt"),"PASS: real hidden native WebView keeps animating; native pause freezes frames/time; hidden FHD GPU capture has an exact loop endpoint; hidden resume and visible transition preserve animation.");
            }
            catch(Exception error){File.WriteAllText(Path.Combine(folder,"status.txt"),error.ToString());Environment.ExitCode=1;}
            finally{form.Close();}
        };
        Forms.Application.Run(form);
    }
}
