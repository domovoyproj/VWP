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

static class LivingRender
{
    public static void Run(string[] args)
    {
        int scene=int.Parse(args[1]),height=args.Length>2?int.Parse(args[2]):720,fps=args.Length>3?int.Parse(args[3]):30;
        bool anime=scene is >=1101 and <=1115;
        bool vibe=scene is >=1201 and <=1220;
        bool season=scene is >=1301 and <=1306;
        if((!anime&&!vibe&&!season&&!(scene is >=1001 and <=1003))||height is <360 or >2160||fps is <1 or >60)throw new ArgumentException("living-render <1001..1003|1101..1115|1201..1220|1301..1306> [height] [fps]");
        Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
        int width=height*16/9;string folder=Path.Combine(Environment.CurrentDirectory,"artifacts","living-check");Directory.CreateDirectory(folder);
        string output=Path.Combine(folder,$"{scene}-{height}p.mp4"),status=Path.Combine(folder,$"{scene}-status.txt");
        bool stills=Array.IndexOf(args,"--stills")>=0;
        using var form=new Forms.Form{ClientSize=new Size(Math.Min(width,1280),Math.Min(height,720)),AutoScaleMode=Forms.AutoScaleMode.None,FormBorderStyle=Forms.FormBorderStyle.None,StartPosition=Forms.FormStartPosition.Manual,Location=new Point(0,0),ShowInTaskbar=false};
        using var surface=new CitySurface();form.Controls.Add(surface.Control);
        form.Shown+=async(_,_)=>
        {
            Process? encoder=null;
            try
            {
                File.WriteAllText(status,"Loading textures");form.Show();form.Activate();if(season)await surface.InitializeSeason(scene);else if(vibe)await surface.InitializeVibe(scene);else if(anime)await surface.InitializeAnime(scene);else await surface.InitializeLiving(scene);await surface.SetExportViewport(width,height);
                int sequence=0;TaskCompletionSource? pending=null;
                surface.Message+=json=>{using var m=JsonDocument.Parse(json);if(m.RootElement.TryGetProperty("type",out var type)&&type.GetString()=="frame"&&m.RootElement.GetProperty("id").GetInt32()==sequence)pending?.TrySetResult();};
                async Task Draw(double seconds)
                {
                    sequence++;pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
                    surface.Control.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="render",time=seconds,id=sequence}));
                    await pending.Task.WaitAsync(TimeSpan.FromSeconds(15));
                }
                // Device-metrics resize and the first GPU upload can outlive the
                // ready message. Warm the compositor before accepting any frame.
                string capture=Path.Combine(folder,$"{scene}-frame.png");await Draw(0);await Task.Delay(250);await Draw(0);await surface.CaptureExport(Path.Combine(folder,$"{scene}-start.png"),width,height);
                await Draw(5);await surface.CaptureExport(Path.Combine(folder,$"{scene}-motion.png"),width,height);
                await Draw(12);await surface.CaptureExport(Path.Combine(folder,$"{scene}-loop.png"),width,height);
                using(var first=new Bitmap(Path.Combine(folder,$"{scene}-start.png")))using(var moved=new Bitmap(Path.Combine(folder,$"{scene}-motion.png")))using(var loop=new Bitmap(Path.Combine(folder,$"{scene}-loop.png")))
                {
                    if(first.Width!=width||first.Height!=height)throw new InvalidOperationException($"Export viewport was {first.Width}x{first.Height}, expected {width}x{height}");
                    int changed=0,edgeChanges=0;for(int y=0;y<first.Height;y+=3)for(int x=0;x<first.Width;x+=3){Color a=first.GetPixel(x,y),b=moved.GetPixel(x,y),c=loop.GetPixel(x,y);if(Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B)>18)changed++;if(Math.Abs(a.R-c.R)+Math.Abs(a.G-c.G)+Math.Abs(a.B-c.B)>3)edgeChanges++;}
                    if(changed<700||edgeChanges>10)throw new InvalidOperationException($"Animation verification: {changed} changed samples, {edgeChanges} loop seam samples");
                    File.WriteAllText(Path.Combine(folder,$"{scene}-metrics.json"),JsonSerializer.Serialize(new{changed,edgeChanges,captureWidth=first.Width,captureHeight=first.Height}));
                }
                if(vibe)
                {
                    using var metrics=JsonDocument.Parse(await surface.Control.CoreWebView2.ExecuteScriptAsync("window.living.metrics()"));
                    if(metrics.RootElement.GetProperty("character").GetBoolean())
                    {
                        await surface.Control.CoreWebView2.ExecuteScriptAsync("window.living.characterOnly(true)");
                        await Draw(0);await surface.CaptureExport(Path.Combine(folder,$"{scene}-character-start.png"),width,height);
                        await Draw(5);await surface.CaptureExport(Path.Combine(folder,$"{scene}-character-motion.png"),width,height);
                        await Draw(12);await surface.CaptureExport(Path.Combine(folder,$"{scene}-character-loop.png"),width,height);
                        using var first=new Bitmap(Path.Combine(folder,$"{scene}-character-start.png"));using var moved=new Bitmap(Path.Combine(folder,$"{scene}-character-motion.png"));using var loop=new Bitmap(Path.Combine(folder,$"{scene}-character-loop.png"));
                        int changed=0,seam=0;for(int y=0;y<height;y+=3)for(int x=0;x<width;x+=3){Color a=first.GetPixel(x,y),b=moved.GetPixel(x,y),c=loop.GetPixel(x,y);if(Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B)>18)changed++;if(Math.Abs(a.R-c.R)+Math.Abs(a.G-c.G)+Math.Abs(a.B-c.B)>3)seam++;}
                        if(changed<80||seam>10)throw new InvalidOperationException($"Character layer: {changed} changing samples, {seam} seam samples");
                        File.WriteAllText(Path.Combine(folder,$"{scene}-character-metrics.json"),JsonSerializer.Serialize(new{changed,seam}));
                        await surface.Control.CoreWebView2.ExecuteScriptAsync("window.living.characterOnly(false)");
                    }
                    await Draw(5);await surface.CaptureExport(Path.Combine(Environment.CurrentDirectory,"assets","vibes",scene+".poster.png"),width,height);
                }
                if(season){await Draw(5);await surface.CaptureExport(Path.Combine(Environment.CurrentDirectory,"assets","seasons",scene+".poster.png"),width,height);}
                if(stills){File.WriteAllText(status,$"PASS: native {width}x{height} export viewport, changing pixels and seamless loop");return;}
                string ffmpeg=Path.Combine(Environment.CurrentDirectory,"tools","ffmpeg.exe");
                var start=new ProcessStartInfo(ffmpeg){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true};
                foreach(string a in new[]{"-y","-v","error","-f","rawvideo","-pixel_format","bgra","-video_size",$"{width}x{height}","-framerate",fps.ToString(),"-i","pipe:0","-an","-c:v","libx264","-preset",season?"medium":"veryfast","-crf",height>=2160?"18":"19","-pix_fmt","yuv420p","-movflags","+faststart",output})start.ArgumentList.Add(a);
                encoder=Process.Start(start)!;byte[] pixels=new byte[width*height*4];
                for(int frame=0;frame<fps*12;frame++)
                {
                    await Draw((double)frame/fps);await surface.CaptureExport(capture,width,height);
                    using var source=new Bitmap(capture);using var image=new Bitmap(width,height,PixelFormat.Format32bppArgb);
                    using(var graphics=Graphics.FromImage(image)){graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;graphics.DrawImage(source,0,0,width,height);}
                    var data=image.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                    try{Marshal.Copy(data.Scan0,pixels,0,pixels.Length);}finally{image.UnlockBits(data);}
                    await encoder.StandardInput.BaseStream.WriteAsync(pixels);
                    if(frame%fps==0)File.WriteAllText(status,$"Rendering {frame/fps}/12 s · {width}x{height} · {fps} FPS");
                }
                encoder.StandardInput.Close();await encoder.WaitForExitAsync();if(encoder.ExitCode!=0)throw new InvalidOperationException("Video encoder failed");
                if(height==2160&&fps==60){string media=Path.Combine(Environment.CurrentDirectory,"assets",season?"seasons":vibe?"vibes":anime?"anime":"living");File.Copy(output,Path.Combine(media,scene+".mp4"),true);if(!anime&&!vibe&&!season){await Draw(5);await surface.CaptureExport(Path.Combine(media,scene+".png"),width,height);}}
                File.WriteAllText(status,$"PASS: moving scene, seamless 12-second loop; {output}");
            }
            catch(Exception e){File.WriteAllText(status,e.ToString());Environment.ExitCode=1;}
            finally{if(encoder is not null){if(!encoder.HasExited){encoder.StandardInput.Close();encoder.Kill();}encoder.Dispose();}form.Close();}
        };
        Forms.Application.Run(form);
    }
}
