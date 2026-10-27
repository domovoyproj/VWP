using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VWP;
using Forms=System.Windows.Forms;

internal static class StudioCheck
{
    internal static void Run(string[] args)
    {
        string output=Path.Combine(Environment.CurrentDirectory,"artifacts","studio-check");Directory.CreateDirectory(output);
        using var form=new Forms.Form{Text="VWP Quality Studio",ClientSize=new System.Drawing.Size(1280,720),StartPosition=Forms.FormStartPosition.CenterScreen};
        form.Shown+=async(_,_)=>
        {
            try
            {
                string[] paths=args[0]=="--studio-check-all"?Directory.GetFiles("assets/studio-originals","*.vwpscene",SearchOption.AllDirectories):new[]{args.Length>1?args[1]:"assets/studio-originals/silver-shore/scene.vwpscene"};
                foreach(string path in paths)
                {
                var project=StudioProject.Load(Path.GetFullPath(path));string sceneOutput=Path.Combine(output,project.Id);Directory.CreateDirectory(sceneOutput);
                using var surface=new StudioSurface();form.Controls.Add(surface.Control);
                await surface.Initialize(project,"Quality",true,manual:true);await surface.SetExportViewport(1920,1080);
                foreach(double second in new[]{0d,1,3,4,12,project.Duration}){await surface.Seek(second);await Task.Delay(180);await surface.CaptureExport(Path.Combine(sceneOutput,$"frame-{second}.png"),1920,1080);}
                if(!File.ReadAllBytes(Path.Combine(sceneOutput,"frame-0.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(sceneOutput,$"frame-{project.Duration}.png"))))throw new Exception("Loop seam mismatch: "+project.Id);
                if(File.ReadAllBytes(Path.Combine(sceneOutput,"frame-0.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(sceneOutput,"frame-4.png"))))throw new Exception("Scene has no motion: "+project.Id);
                await surface.Execute("window.studio.quality({maxHeight:1080,fps:60,particles:1,bloom:true});window.studio.live();");
                await Task.Delay(10000);var stats=await surface.ReadStatistics();File.WriteAllText(Path.Combine(sceneOutput,"stats.json"),JsonSerializer.Serialize(stats,StudioProject.Json));
                surface.SetPaused(true);await Task.Delay(200);var frozen=await surface.ReadStatistics();await Task.Delay(500);var later=await surface.ReadStatistics();if(Math.Abs(frozen.Time-later.Time)>.001||frozen.Frames!=later.Frames)throw new Exception("Pause did not freeze time.");
                surface.Configure("Eco",project.Settings);await Task.Delay(200);var eco=await surface.ReadStatistics();if(eco.Height!=720||eco.TextureMb>=stats.TextureMb)throw new Exception("Eco must reduce render resolution and texture memory.");
                surface.Configure("Balance",project.Settings);await Task.Delay(200);
                string state=await surface.State();await surface.Execute("window.studio.seek(9)");await surface.Restore(state);if(Math.Abs((await surface.ReadStatistics()).Time-frozen.Time)>.02)throw new Exception("State restore lost scene time.");
                await surface.SetExportViewport(1600,1200);surface.SetFraming(new(){Fit="Fit"});await Task.Delay(200);var fit=await surface.ReadStatistics();if(Math.Abs(fit.Frame[1]-.125)>.002)throw new Exception("Fit failed to preserve 16:9 composition on a 4:3 display.");
                File.WriteAllText(Path.Combine(sceneOutput,"status.txt"),"PASS: native GPU at 1920x1080; frame0 equals loop endpoint; animation changes; pause freezes time; Eco reduces textures and resolution; state restores.");
                if(Array.Exists(args,a=>a=="--preview"))await Task.Delay(60000);
                form.Controls.Remove(surface.Control);
                }
                File.WriteAllText(Path.Combine(output,"status.txt"),"PASS: all requested Studio scenes passed native rendering and lifecycle checks.");
            }
            catch(Exception error){File.WriteAllText(Path.Combine(output,"status.txt"),error.ToString());Environment.ExitCode=1;}
            finally{form.Close();}
        };
        Forms.Application.Run(form);
    }
}
