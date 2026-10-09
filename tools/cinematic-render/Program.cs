using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VWP;
using Forms = System.Windows.Forms;

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
int width=1920,height=1080,frames=seconds*fps;
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var process=new Process{StartInfo=new ProcessStartInfo(ffmpeg){UseShellExecute=false,RedirectStandardInput=true,CreateNoWindow=true}};
foreach(string item in new[]{"-hide_banner","-loglevel","error","-y","-f","rawvideo","-pixel_format","bgra","-video_size","1920x1080","-framerate",fps.ToString(),"-i","pipe:0","-an","-vf",$"scale={outputHeight*16/9}:{outputHeight}:flags=lanczos,format=yuv420p","-r",fps.ToString(),"-c:v","libx264","-preset","veryfast","-crf","20","-movflags","+faststart","-t",seconds.ToString(),output})process.StartInfo.ArgumentList.Add(item);
process.Start();
using var audio=new AudioReaction();
using var visual=new InteractiveVisual(new SceneLayers{Background=input,MotionId=id},new MonitorPreferences{Performance="Quality",SceneAnimation=true},Forms.Screen.PrimaryScreen!,audio);
visual.SetPaused(true);
visual.Measure(new System.Windows.Size(width,height));visual.Arrange(new Rect(0,0,width,height));
byte[] pixels=new byte[width*height*4];
var timer=Stopwatch.StartNew();
try
{
    for(int frame=0;frame<frames;frame++)
    {
        visual.RenderAt((double)frame/fps);
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.CopyPixels(pixels,width*4,0);
        process.StandardInput.BaseStream.Write(pixels);
        if(frame%fps==0)Console.WriteLine($"scene {id}: {frame/fps}/{seconds}s ({timer.Elapsed:mm\\:ss})");
    }
}
finally
{
    process.StandardInput.Close();
    process.WaitForExit();
}
if(process.ExitCode!=0 || !File.Exists(output) || new FileInfo(output).Length<100_000)
    throw new InvalidOperationException($"FFmpeg failed for scene {id}: {process.ExitCode}");
Console.WriteLine($"Rendered {output}: {frames} frames, {new FileInfo(output).Length/1048576.0:F1} MiB");
}
}
