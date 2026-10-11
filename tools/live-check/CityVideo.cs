using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using VWP;
using Forms=System.Windows.Forms;

static class CityVideo
{
    public static void Run(string[] args)
    {
        int height=args.Length>1?int.Parse(args[1]):720,fps=args.Length>2?int.Parse(args[2]):30;
        if(height is <360 or >2160||fps is <1 or >60)throw new ArgumentException("city-video [height] [fps]");
        Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
        int width=height*16/9;const int seconds=20;
        bool stills=Array.IndexOf(args,"--stills")>=0;
        bool bake=Array.IndexOf(args,"--bake")>=0;
        string folder=Path.Combine(Environment.CurrentDirectory,"artifacts","city-video-v3");Directory.CreateDirectory(folder);
        string output=Path.Combine(folder,"Afterlight-3D-v3.mp4"),status=Path.Combine(folder,"status.txt");
        using var form=new Forms.Form{ClientSize=new Size(Math.Min(width,1280),Math.Min(height,720)),AutoScaleMode=Forms.AutoScaleMode.None,FormBorderStyle=Forms.FormBorderStyle.None,StartPosition=Forms.FormStartPosition.Manual,Location=new Point(0,0),ShowInTaskbar=false};
        using var surface=new CitySurface();form.Controls.Add(surface.Control);
        form.Shown+=async(_,_)=>
        {
            Process? encoder=null;
            try
            {
                File.WriteAllText(status,"Loading native 3D scene");await surface.InitializeCityRender(bake);await surface.SetExportViewport(width,height);
                if(bake){
                    string motions=await surface.Control.CoreWebView2.ExecuteScriptAsync("window.city.exportMotions()");
                    using var verify=JsonDocument.Parse(motions);if(verify.RootElement.GetProperty("0").GetArrayLength()!=4||verify.RootElement.GetProperty("1").GetArrayLength()!=4)throw new InvalidOperationException("Baked motion library is incomplete");
                    File.WriteAllText(Path.Combine(Environment.CurrentDirectory,"assets","city","models","street-motion.json"),motions);
                    File.WriteAllText(status,"PASS: eight retargeted street animation clips baked");return;
                }
                int sequence=0;TaskCompletionSource? pending=null;
                surface.Message+=json=>{using var m=JsonDocument.Parse(json);if(m.RootElement.TryGetProperty("type",out var type)&&type.GetString()=="frame"&&m.RootElement.GetProperty("id").GetInt32()==sequence)pending?.TrySetResult();};
                async Task Draw(double time,object? customPose=null)
                {
                    double u=Math.Clamp((time-7)/13,0,1),e=u*u*(3-2*u);
                    object pose=customPose??new{x=Math.Sin(time*.16)*.28,y=3.2+e*17,z=26-time*3.4,yaw=Math.Sin(time*.15)*.06,pitch=-.035-e*.14,palette=0,flying=true};
                    sequence++;pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
                    surface.Control.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="render",time=time+24,pose,id=sequence}));
                    await pending.Task.WaitAsync(TimeSpan.FromSeconds(25));
                }
                foreach(int time in new[]{0,5,12,14,16,19}){await Draw(time);await surface.CaptureExport(Path.Combine(folder,$"{time:D2}.png"),width,height);}
                await Draw(0,new{x=-5.3,y=1.8,z=-34,yaw=0,pitch=0,palette=0,flying=true});await surface.CaptureExport(Path.Combine(folder,"person.png"),width,height);
                await Draw(.4,new{x=-5.3,y=1.8,z=-34,yaw=0,pitch=0,palette=0,flying=true});await surface.CaptureExport(Path.Combine(folder,"person-walk.png"),width,height);
                File.WriteAllText(Path.Combine(folder,"metrics.json"),await surface.Control.CoreWebView2.ExecuteScriptAsync("window.city.metrics()"));
                if(stills){File.WriteAllText(status,"PASS: native 3D review frames captured");return;}
                string ffmpeg=Path.Combine(Environment.CurrentDirectory,"tools","ffmpeg.exe");
                var start=new ProcessStartInfo(ffmpeg){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true};
                foreach(string a in new[]{"-y","-v","error","-f","rawvideo","-pixel_format","bgra","-video_size",$"{width}x{height}","-framerate",fps.ToString(),"-i","pipe:0","-an","-c:v","libx264","-preset","veryfast","-crf","18","-pix_fmt","yuv420p","-movflags","+faststart",output})start.ArgumentList.Add(a);
                encoder=Process.Start(start)!;byte[] pixels=new byte[width*height*4];
                string capture=Path.Combine(folder,"frame.png");
                for(int i=0;i<fps*seconds;i++)
                {
                    await Draw((double)i/fps);await surface.CaptureExport(capture,width,height);
                    using var source=new Bitmap(capture);if(source.Width!=width||source.Height!=height)throw new InvalidOperationException("Export viewport has incorrect dimensions");using var image=new Bitmap(width,height,PixelFormat.Format32bppArgb);
                    using(var graphics=Graphics.FromImage(image)){graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;graphics.DrawImage(source,0,0,width,height);}
                    var data=image.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                    try{Marshal.Copy(data.Scan0,pixels,0,pixels.Length);}finally{image.UnlockBits(data);}
                    await encoder.StandardInput.BaseStream.WriteAsync(pixels);
                    if(i%fps==0)File.WriteAllText(status,$"Rendering 3D video {i/fps}/{seconds} s · {width}x{height} · {fps} FPS");
                }
                encoder.StandardInput.Close();await encoder.WaitForExitAsync();if(encoder.ExitCode!=0)throw new InvalidOperationException("Video encoder failed");
                File.WriteAllText(status,$"PASS: {seconds}-second video from the actual 3D renderer; {output}");
            }
            catch(Exception e){File.WriteAllText(status,e.ToString());Environment.ExitCode=1;}
            finally{if(encoder is not null){if(!encoder.HasExited){encoder.StandardInput.Close();encoder.Kill();}encoder.Dispose();}form.Close();}
        };
        Forms.Application.Run(form);
    }
}
