using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VWP;
using Forms=System.Windows.Forms;

internal static class StudioLibraryCheck
{
    internal static void Run(string[] args)
    {
        string root=Environment.CurrentDirectory,assets=Path.Combine(root,"assets"),output=Path.Combine(root,"artifacts","studio-library-check");Directory.CreateDirectory(output);
        string progress=Path.Combine(output,"progress.txt");File.WriteAllText(progress,"");File.WriteAllText(Path.Combine(output,"status.txt"),"RUNNING: native library rendering and face protection checks.");
        using var form=new Forms.Form{Text="VWP · проверка всей библиотеки",ClientSize=new Size(1280,720),StartPosition=Forms.FormStartPosition.CenterScreen};
        form.Shown+=async(_,_)=>
        {
            try
            {
                bool complete=args.Length==1;
                var entries=StudioLibrary.Read(assets);
                if(complete&&entries.Count!=132)throw new Exception("Complete release catalogue must contain 132 scenes; found "+entries.Count);
                int[] ids=complete?entries.Select(e=>e.Id).ToArray():args[1].Split(',').Select(int.Parse).ToArray();
                var results=new List<object>();
                using var directions=JsonDocument.Parse(File.ReadAllText(Path.Combine(assets,"studio-library","directions.json")));
                foreach(int id in ids)
                {
                    string file=Path.Combine(assets,"studio-library","scenes",$"{id}.vwpscene");
                    var project=complete?StudioLibrary.Load(file):StudioProject.LoadBundled(file,assets);
                    project.Settings.Clock=false;
                    string sceneOutput=Path.Combine(output,id.ToString());Directory.CreateDirectory(sceneOutput);
                    using var surface=new StudioSurface();form.Controls.Add(surface.Control);
                    await surface.Initialize(project,"Quality",true,manual:true);await surface.SetExportViewport(1920,1080);
                    foreach(double second in new[]{0d,4,project.Duration})
                    {
                        await surface.Seek(second);await Task.Delay(160);
                        await surface.CaptureExport(Path.Combine(sceneOutput,$"frame-{second}.png"),1920,1080);
                    }
                    string first=Path.Combine(sceneOutput,"frame-0.png"),moving=Path.Combine(sceneOutput,"frame-4.png"),last=Path.Combine(sceneOutput,$"frame-{project.Duration}.png");
                    using var image=new Bitmap(first);if(image.Width!=1920||image.Height!=1080)throw new Exception(id+": incorrect native dimensions");
                    var seam=Difference(first,last);if(seam.changed!=0)throw new Exception(id+": loop seam differs in "+seam.changed+" pixels");
                    var motion=Difference(first,moving);if(motion.changed<60)throw new Exception(id+": insufficient visible motion ("+motion.changed+")");
                    var face=directions.RootElement.GetProperty("scenes").EnumerateArray().First(s=>s.GetProperty("id").GetInt32()==id).GetProperty("face");
                    double faceDelta=face.ValueKind==JsonValueKind.Null?0:RegionDifference(first,moving,(int)Math.Round(face[0].GetDouble()*1920),(int)Math.Round(face[1].GetDouble()*1080));
                    if(faceDelta>.75)throw new Exception(id+": animation leaks onto protected facial features; mean delta "+faceDelta);
                    await surface.Seek(4);await surface.Execute("window.studio.live()");await Task.Delay(1800);
                    var stats=await surface.ReadStatistics();surface.SetPaused(true);await Task.Delay(200);var paused=await surface.ReadStatistics();await Task.Delay(200);var frozen=await surface.ReadStatistics();
                    if(paused.Frames!=frozen.Frames||Math.Abs(paused.Time-frozen.Time)>.001)throw new Exception(id+": paused scene keeps rendering");
                    if(stats.TextureMb>240||stats.TargetMb>140)throw new Exception(id+": GPU resource budget exceeded");
                    if(!complete)File.WriteAllText(Path.Combine(sceneOutput,"development.txt"),"DEVELOPMENT ONLY: does not certify catalogue completeness.");
                    var result=new{id,project.Name,motionPixels=motion.changed,motionMean=motion.mean,loopSeamPixels=seam.changed,faceDeltaMean=faceDelta,stats};results.Add(result);
                    File.WriteAllText(Path.Combine(sceneOutput,"result.json"),JsonSerializer.Serialize(result,StudioProject.Json));
                    File.AppendAllText(progress,$"{id}: PASS; motion {motion.changed:N0} px; loop exact; {stats.Fps:0.0} FPS; {stats.TextureMb:0.0} MB textures\n");
                    form.Controls.Remove(surface.Control);
                }
                File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{complete,count=results.Count,results},StudioProject.Json));
                File.WriteAllText(Path.Combine(output,"status.txt"),$"PASS: {results.Count} native FHD scene checks. Complete catalogue: {complete}.");
            }
            catch(Exception error){File.WriteAllText(Path.Combine(output,"status.txt"),error.ToString());Environment.ExitCode=1;}
            finally{form.Close();}
        };
        Forms.Application.Run(form);
    }
    static (int changed,double mean) Difference(string a,string b)
    {
        using var first=new Bitmap(a);using var second=new Bitmap(b);int changed=0;long total=0;
        var rect=new Rectangle(0,0,first.Width,first.Height);
        var x=first.LockBits(rect,System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var y=second.LockBits(rect,System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            int size=Math.Abs(x.Stride)*first.Height;var one=new byte[size];var two=new byte[size];System.Runtime.InteropServices.Marshal.Copy(x.Scan0,one,0,size);System.Runtime.InteropServices.Marshal.Copy(y.Scan0,two,0,size);
            for(int i=0;i<size;i+=4){int delta=Math.Abs(one[i]-two[i])+Math.Abs(one[i+1]-two[i+1])+Math.Abs(one[i+2]-two[i+2]);total+=delta;if(delta>6)changed++;}
            return(changed,total/(double)(first.Width*first.Height*3));
        }
        finally{first.UnlockBits(x);second.UnlockBits(y);}
    }
    static double RegionDifference(string a,string b,int x,int y)
    {
        using var first=new Bitmap(a);using var second=new Bitmap(b);long delta=0;int count=0;
        for(int py=Math.Max(0,y-8);py<=Math.Min(first.Height-1,y+8);py++)for(int px=Math.Max(0,x-8);px<=Math.Min(first.Width-1,x+8);px++)
        {
            var one=first.GetPixel(px,py);var two=second.GetPixel(px,py);delta+=Math.Abs(one.R-two.R)+Math.Abs(one.G-two.G)+Math.Abs(one.B-two.B);count+=3;
        }
        return delta/(double)count;
    }
}
