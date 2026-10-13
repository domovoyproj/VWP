using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VWP;

static class BasketballCheck
{
    public static void Run()
    {
        string folder=Path.Combine(Environment.CurrentDirectory,"artifacts","basketball-check");Directory.CreateDirectory(folder);
        var hashes=new System.Collections.Generic.List<byte[]>();
        int maximumOpaque=0,minimumActive=int.MaxValue;
        foreach(int time in new[]{2,5,9})
        {
            var visual=new DrawingVisual();using(var drawing=visual.RenderOpen())CinematicActors.Draw(drawing,27,time);
            var frame=new RenderTargetBitmap(1920,1080,96,96,PixelFormats.Pbgra32);frame.Render(visual);
            byte[] pixels=new byte[1920*1080*4];frame.CopyPixels(pixels,1920*4,0);
            int opaque=0,active=0;for(int n=3;n<pixels.Length;n+=4){if(pixels[n]>150)opaque++;if(pixels[n]>0)active++;}
            maximumOpaque=Math.Max(maximumOpaque,opaque);minimumActive=Math.Min(minimumActive,active);hashes.Add(SHA256.HashData(pixels));
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(frame));using var file=File.Create(Path.Combine(folder,$"overlay-{time}.png"));png.Save(file);
        }
        bool passed=maximumOpaque==0&&minimumActive>=100&&!hashes[0].SequenceEqual(hashes[1])&&!hashes[1].SequenceEqual(hashes[2]);
        File.WriteAllText(Path.Combine(folder,"result.json"),JsonSerializer.Serialize(new{passed,maximumOpaque,minimumActive,description="Only moving translucent atmosphere remains; no opaque character or ball overlay"}));
        if(!passed)throw new InvalidOperationException("Basketball character removal verification failed");
        Console.WriteLine("PASS: basketball has moving ambient particles and no opaque character or ball overlay");
    }
}
