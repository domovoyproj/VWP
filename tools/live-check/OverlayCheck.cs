using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using VWP;
using Drawing=System.Drawing;
using Forms=System.Windows.Forms;

static class OverlayCheck
{
    public static void Run()
    {
        Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
        string folder=Path.Combine(Environment.CurrentDirectory,"artifacts","overlay-check");Directory.CreateDirectory(folder);
        Core.Initialize();
        using var host=new DesktopHost();
        var player=(MediaPlayer)typeof(DesktopHost).GetField("player",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var vlc=(LibVLC)typeof(DesktopHost).GetField("vlc",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        using var form=new Forms.Form{ClientSize=new Drawing.Size(640,360),StartPosition=Forms.FormStartPosition.CenterScreen,TopMost=true,FormBorderStyle=Forms.FormBorderStyle.None,ShowInTaskbar=false,BackColor=Drawing.Color.Black};
        form.Shown+=async(_,_)=>
        {
            try
            {
                ShowWindow(form.Handle,5);
                SetWindowPos(form.Handle,new IntPtr(-1),0,0,0,0,0x43);
                player.Hwnd=form.Handle;
                for(int attempt=0;attempt<2;attempt++)
                {
                    using var media=new Media(vlc,Path.Combine(folder,"wallpaper-with-a-long-filename.mp4"),FromType.FromPath);
                    media.AddOption(":avcodec-hw=none");
                    if(!player.Play(media))throw new Exception("Probe video did not start");
                    for(int i=0;i<30&&!player.IsPlaying;i++)await Task.Delay(100);
                    await Task.Delay(1200);
                    for(int sample=0;sample<12;sample++)
                    {
                        if(sample==2 && !player.TakeSnapshot(0,Path.Combine(folder,"temporary-transition-snapshot.png"),0,0))throw new Exception("Transition snapshot failed");
                        await Task.Delay(80);
                        using var frame=new Drawing.Bitmap(640,360);
                        using(var graphics=Drawing.Graphics.FromImage(frame))graphics.CopyFromScreen(form.PointToScreen(Drawing.Point.Empty),Drawing.Point.Empty,frame.Size);
                        var center=frame.GetPixel(320,180);
                        if(Math.Abs(center.R-18)>20||Math.Abs(center.G-42)>20||Math.Abs(center.B-70)>20){frame.Save(Path.Combine(folder,"unexpected-frame.png"));throw new Exception("Probe video was not visible on screen: "+center);}
                        int overlay=0;
                        for(int y=0;y<360;y+=2)for(int x=0;x<640;x+=2){var pixel=frame.GetPixel(x,y);if(pixel.R>170&&pixel.G>170&&pixel.B>170)overlay++;}
                        if(overlay>5){frame.Save(Path.Combine(folder,"unexpected-overlay.png"));throw new Exception("VLC displayed title/snapshot text: "+overlay+" bright pixels");}
                    }
                    player.Stop();
                }
                File.WriteAllText(Path.Combine(folder,"status.txt"),"PASS: native video has no title, snapshot path or snapshot preview during playback and replay");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(folder,"status.txt"),e.ToString());Environment.ExitCode=1;}
            finally{player.Stop();form.Close();}
        };
        Forms.Application.Run(form);
    }
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
}
