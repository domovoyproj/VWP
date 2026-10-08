using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using VWP;
class Program
{
    [STAThread] static int Main(string[] args)
    {
        try
        {
            string output=args.Length>0?Path.GetFullPath(args[0]):Path.GetFullPath("assets/spatial");Directory.CreateDirectory(output);
            RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
            for(int id=18;id<SpatialScene.SceneCount;id++)
            {
                using var scene=new SpatialScene(id);scene.Measure(new Size(960,540));scene.Arrange(new Rect(0,0,960,540));scene.UpdateLayout();
                byte[]? first=null;
                foreach(double time in new[]{0.0,3.0,7.0})
                {
                    scene.Update(time);scene.UpdateLayout();var bitmap=new RenderTargetBitmap(960,540,96,96,PixelFormats.Pbgra32);bitmap.Render(scene);
                    byte[] bytes=new byte[960*540*4];bitmap.CopyPixels(bytes,960*4,0);
                    if(first is null)first=bytes;else if(first.SequenceEqual(bytes))throw new Exception("Frozen scene "+id);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream=File.Create(Path.Combine(output,time==3?$"{id}.png":$"{id}-check-{time:0}.png"));encoder.Save(stream);
                }
                if(scene.MovingGroups<1)throw new Exception("No actors "+id);
                Console.WriteLine($"PASS {id}: {scene.MovingGroups} animated groups, different rendered pixels at 0/3/7 seconds");
            }
            var plain=new Wallpaper("video","0.mp4",null,"",PresetId:0);var spatial=plain with {IsSpatial=true};
            if(PlaybackRules.Key(plain)==PlaybackRules.Key(spatial))throw new Exception("Colliding keys");
            Console.WriteLine("PASS: 10 new 3D scenes; video/3D identities remain independent");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
