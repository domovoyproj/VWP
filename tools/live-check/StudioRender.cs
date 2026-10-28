using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VWP;
using Forms=System.Windows.Forms;

internal static class StudioRender
{
    public static void Run(string[] args)
    {
        var project=StudioLibrary.Load(Path.GetFullPath(args[1]));project.Settings.Clock=false;int height=args.Length>2?int.Parse(args[2]):720,fps=30,width=height*16/9;
        if(height is <360 or >2160)throw new ArgumentException("studio-render project [height]");
        string folder=Path.GetFullPath(Path.Combine("artifacts","studio-video",project.Id));Directory.CreateDirectory(folder);
        using var form=new Forms.Form{ClientSize=new System.Drawing.Size(960,540),ShowInTaskbar=false,StartPosition=Forms.FormStartPosition.CenterScreen};using var surface=new StudioSurface();form.Controls.Add(surface.Control);
        form.Shown+=async(_,_)=>
        {
            Process? encoder=null;
            try
            {
                await surface.Initialize(project,"Quality",true,manual:true);await surface.SetExportViewport(width,height);
                var start=new ProcessStartInfo(Path.GetFullPath("tools/ffmpeg.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardError=true};
                foreach(string argument in new[]{"-y","-v","error","-f","image2pipe","-vcodec","png","-framerate",fps.ToString(),"-i","pipe:0","-an","-c:v","libx264","-preset","fast","-crf","18","-pix_fmt","yuv420p","-movflags","+faststart",Path.Combine(folder,"preview.mp4")})start.ArgumentList.Add(argument);
                encoder=Process.Start(start)!;var errors=encoder.StandardError.ReadToEndAsync();int count=(int)Math.Round(project.Duration*fps);
                for(int frame=0;frame<count;frame++)
                {
                    await surface.Seek((double)frame/fps);
                    string screenshot=await surface.Control.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.captureScreenshot",JsonSerializer.Serialize(new{format="png",fromSurface=true,captureBeyondViewport=true,clip=new{x=0,y=0,width,height,scale=1}}));
                    using var capture=JsonDocument.Parse(screenshot);await encoder.StandardInput.BaseStream.WriteAsync(Convert.FromBase64String(capture.RootElement.GetProperty("data").GetString()!));
                    if(frame%fps==0)File.WriteAllText(Path.Combine(folder,"status.txt"),$"Rendering {frame/fps}/{project.Duration:0} seconds at {width}x{height}, {fps} FPS.");
                }
                encoder.StandardInput.Close();await encoder.WaitForExitAsync();if(encoder.ExitCode!=0)throw new Exception(await errors);
                File.WriteAllText(Path.Combine(folder,"status.txt"),"PASS: complete loop captured directly from the native GPU scene.");
            }
            catch(Exception error){File.WriteAllText(Path.Combine(folder,"status.txt"),error.ToString());Environment.ExitCode=1;}
            finally{if(encoder is not null){if(!encoder.HasExited)encoder.Kill();encoder.Dispose();}form.Close();}
        };
        Forms.Application.Run(form);
    }
}
