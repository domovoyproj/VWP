using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VWP;

static class AtmosphereCheck
{
    public static void Run()
    {
        string folder=Path.Combine(Environment.CurrentDirectory,"artifacts","atmosphere-check");Directory.CreateDirectory(folder);
        var report=new System.Collections.Generic.List<object>();
        for(int scene=0;scene<28;scene++)
        {
            byte[][] hashes=new byte[3][];int maximumOpaque=0,minimumActive=int.MaxValue;
            for(int n=0;n<3;n++)
            {
                var visual=new DrawingVisual();using(var d=visual.RenderOpen()){if(scene<18)SceneActors.Draw(d,scene,n==0?0:n==1?5:12,false);else CinematicActors.Draw(d,scene,n==0?0:n==1?5:12);}
                var frame=new RenderTargetBitmap(1920,1080,96,96,PixelFormats.Pbgra32);frame.Render(visual);
                byte[] pixels=new byte[1920*1080*4];frame.CopyPixels(pixels,1920*4,0);
                int opaque=0,active=0;for(int p=3;p<pixels.Length;p+=4){if(pixels[p]>150)opaque++;if(pixels[p]>0)active++;}
                maximumOpaque=Math.Max(maximumOpaque,opaque);minimumActive=Math.Min(minimumActive,active);hashes[n]=SHA256.HashData(pixels);
            }
            bool passed=maximumOpaque==0&&minimumActive>=100&&!hashes[0].SequenceEqual(hashes[1])&&hashes[0].SequenceEqual(hashes[2]);
            report.Add(new{scene,passed,maximumOpaque,minimumActive});
            if(!passed){File.WriteAllText(Path.Combine(folder,"result.json"),JsonSerializer.Serialize(report));throw new InvalidOperationException("Atmosphere-only overlay check failed for "+scene);}
        }
        File.WriteAllText(Path.Combine(folder,"result.json"),JsonSerializer.Serialize(report));Console.WriteLine("PASS: all 28 legacy overlays contain moving, seamless translucent atmosphere only");
    }
}
