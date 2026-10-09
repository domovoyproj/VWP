using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VWP;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if(args.Length<1 || !int.TryParse(args[0],out int id) || id is <18 or >27)
            throw new ArgumentException("Usage: cinematic-render <18..27> [seconds] [fps] [output-height]");
        int seconds=args.Length>1?int.Parse(args[1]):12;
        int fps=args.Length>2?int.Parse(args[2]):60;
        int outputHeight=args.Length>3?int.Parse(args[3]):2160;
        if(seconds is <1 or >60 || fps is <1 or >60 || outputHeight is <540 or >2160)
            throw new ArgumentOutOfRangeException(nameof(args));

        string root=Environment.CurrentDirectory;
        string input=Path.Combine(root,"assets","cinematic",$"{id}.png");
        string output=Path.Combine(root,"assets","cinematic",$"{id}.mp4");
        string ffmpeg=Path.Combine(root,"tools","ffmpeg.exe");
        if(!File.Exists(ffmpeg))ffmpeg="ffmpeg";
        if(!File.Exists(input))throw new FileNotFoundException("Cinematic background missing",input);
        int width=960,height=540,frames=seconds*fps;
        int targetWidth=outputHeight*16/9;
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        var process=new Process{StartInfo=new ProcessStartInfo(ffmpeg){UseShellExecute=false,RedirectStandardInput=true,CreateNoWindow=true}};
        string filter=$"[0:v]scale={targetWidth}:{outputHeight}:flags=lanczos,format=rgba[bg];"+
                      $"[1:v]scale={targetWidth}:{outputHeight}:flags=bicubic,format=rgba[actors];"+
                      "[bg][actors]overlay=0:0:shortest=1,format=yuv420p[v]";
        foreach(string item in new[]{"-hide_banner","-loglevel","error","-y","-loop","1","-framerate",fps.ToString(),"-i",input,
                "-f","rawvideo","-pixel_format","bgra","-video_size","960x540","-framerate",fps.ToString(),"-i","pipe:0",
                "-filter_complex",filter,"-map","[v]","-an","-frames:v",frames.ToString(),"-r",fps.ToString(),
                "-c:v","libx264","-preset","veryfast","-crf","20","-movflags","+faststart",output})
            process.StartInfo.ArgumentList.Add(item);
        process.Start();
        byte[] pixels=new byte[width*height*4];
        byte[]? earlyHash=null,laterHash=null;
        int activePixels=0;
        var timer=Stopwatch.StartNew();
        try
        {
            for(int frame=0;frame<frames;frame++)
            {
                double time=(double)frame/fps;
                var visual=new DrawingVisual();
                using(var drawing=visual.RenderOpen())
                {
                    drawing.PushTransform(new ScaleTransform(.5,.5));
                    double loopTime=time%12;
                    drawing.PushOpacity(Math.Clamp(Math.Min(loopTime,12-loopTime)*2,0,1));
                    CinematicActors.Draw(drawing,id,time);
                    drawing.Pop();drawing.Pop();
                }
                var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
                bitmap.Render(visual);
                bitmap.CopyPixels(pixels,width*4,0);
                if(frame==frames/4)earlyHash=SHA256.HashData(pixels);
                if(frame==frames/2)
                {
                    laterHash=SHA256.HashData(pixels);
                    for(int pixel=3;pixel<pixels.Length;pixel+=4)if(pixels[pixel]!=0)activePixels++;
                }
                process.StandardInput.BaseStream.Write(pixels);
                if(frame%fps==0)Console.WriteLine($"scene {id}: {frame/fps}/{seconds}s ({timer.Elapsed:mm\\:ss})");
            }
        }
        finally
        {
            process.StandardInput.Close();
            process.WaitForExit();
        }
        if(process.ExitCode!=0 || activePixels<100 || earlyHash is null || laterHash is null || earlyHash.SequenceEqual(laterHash) ||
           !File.Exists(output) || new FileInfo(output).Length<100_000)
            throw new InvalidOperationException($"Cinematic scene {id} failed: encoder={process.ExitCode}, moving pixels={activePixels}");
        Console.WriteLine($"Rendered {output}: {frames} frames, {activePixels} foreground pixels, {new FileInfo(output).Length/1048576.0:F1} MiB");
    }
}
