using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace VWP;
public sealed class VideoRecipe
{
    public double Start {get;set;}
    public double End {get;set;}
    public double Speed {get;set;}=1;
    public double Brightness {get;set;}
    public double Contrast {get;set;}=1;
    public double Saturation {get;set;}=1;
    public double Seam {get;set;}=.5;
}
public static class VideoRender
{
    public static string Encoder=>Path.Combine(AppContext.BaseDirectory,"tools","ffmpeg.exe");
    public static async Task Run(string input,string output,VideoRecipe recipe,PerformanceProfile profile,IProgress<double>? progress=null,CancellationToken cancel=default)
    {
        if(!File.Exists(Encoder))throw new FileNotFoundException("FFmpeg не включён в сборку.");
        double duration=recipe.End-recipe.Start;
        if(duration<.5||recipe.Start<0||recipe.Speed<.25||recipe.Speed>3||recipe.Seam<0||recipe.Seam>=duration/recipe.Speed/2)throw new ArgumentException("Проверьте границы фрагмента, скорость и длительность стыка.");
        string F(double value)=>value.ToString("0.######",CultureInfo.InvariantCulture);
        string filter=$"setpts=(PTS-STARTPTS)/{F(recipe.Speed)},fps={profile.Fps},scale=-2:'trunc(min(ih,{profile.MaxHeight})/2)*2',setsar=1,eq=brightness={F(recipe.Brightness)}:contrast={F(recipe.Contrast)}:saturation={F(recipe.Saturation)},format=yuv420p";
        double length=duration/recipe.Speed;
        string complex=recipe.Seam>0?$"[0:v]{filter},split=3[main][head][tail];[main]trim=start={F(recipe.Seam)}:end={F(length-recipe.Seam)},setpts=PTS-STARTPTS[mid];[tail]trim=start={F(length-recipe.Seam)}:end={F(length)},setpts=PTS-STARTPTS[end];[head]trim=end={F(recipe.Seam)},setpts=PTS-STARTPTS[start];[end][start]blend=all_expr='A*(1-T/{F(recipe.Seam)})+B*T/{F(recipe.Seam)}'[join];[mid][join]concat=n=2:v=1:a=0[out]":$"[0:v]{filter}[out]";
        var start=new ProcessStartInfo(Encoder){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
        string tempo=recipe.Speed<.5?$"atempo=0.5,atempo={F(recipe.Speed*2)}":recipe.Speed>2?$"atempo=2,atempo={F(recipe.Speed/2)}":$"atempo={F(recipe.Speed)}";
        string audio=tempo+$",atrim=start={F(recipe.Seam)}:end={F(length)},asetpts=PTS-STARTPTS"+(recipe.Seam>0?$",afade=t=in:d={F(recipe.Seam)},afade=t=out:st={F(length-recipe.Seam*2)}:d={F(recipe.Seam)}":"");
        foreach(string arg in new[]{"-y","-hide_banner","-loglevel","error","-ss",F(recipe.Start),"-t",F(duration),"-i",input,"-filter_complex",complex,"-map","[out]","-map","0:a?","-af",audio,"-c:a","aac","-c:v","libx264","-threads","2","-crf",profile.Key=="Quality"?"18":"22","-preset","fast","-movflags","+faststart","-progress","pipe:1",output})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)??throw new InvalidOperationException("Не удалось запустить редактор.");
        using var registration=cancel.Register(()=>{try{process.Kill(true);}catch{}});
        var errors=process.StandardError.ReadToEndAsync();
        while(await process.StandardOutput.ReadLineAsync() is string line)
            if(line.StartsWith("out_time_us=")&&long.TryParse(line[12..],out long time))progress?.Report(Math.Clamp(time/1000000.0/(length-recipe.Seam)*100,0,100));
        await process.WaitForExitAsync();cancel.ThrowIfCancellationRequested();
        if(process.ExitCode!=0)throw new InvalidOperationException(await errors);
    }
    public static async Task Thumbnail(string input,string output)
    {
        var start=new ProcessStartInfo(Encoder){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
        foreach(string arg in new[]{"-y","-loglevel","error","-i",input,"-frames:v","1","-vf","scale=640:-2",output})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;var error=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();if(process.ExitCode!=0)throw new InvalidOperationException(await error);
    }
    public static async Task<string> Optimized(string input,PerformanceProfile profile)
    {
        if(profile.Key=="Quality")return input;
        string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","optimized");Directory.CreateDirectory(folder);
        var info=new FileInfo(input);string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input+info.Length+info.LastWriteTimeUtc.Ticks+profile.Key)));
        string output=Path.Combine(folder,key+".mp4");if(File.Exists(output))return output;
        string temp=output+".tmp.mp4";
        var start=new ProcessStartInfo(Encoder){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
        foreach(string arg in new[]{"-y","-loglevel","error","-i",input,"-vf",$"fps={profile.Fps},scale=-2:'trunc(min(ih,{profile.MaxHeight})/2)*2'","-c:v","libx264","-threads","2","-preset","fast","-crf","23","-c:a","aac","-movflags","+faststart",temp})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;var errors=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();if(process.ExitCode!=0){if(File.Exists(temp))File.Delete(temp);throw new InvalidOperationException(await errors);}File.Move(temp,output,true);return output;
    }
}
