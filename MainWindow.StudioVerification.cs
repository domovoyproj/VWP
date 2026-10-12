using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace VWP;
public partial class MainWindow
{
    async Task VerifyStudio()
    {
        string folder=Path.Combine(AppContext.BaseDirectory,"verification");Directory.CreateDirectory(folder);var results=new List<object>();var saved=preferences;featureTimer.Stop();preferences=new(){Theme="Dark",CheckUpdates=false,PauseBattery=false,PauseFullscreen=false};
        void Check(string name,bool passed){results.Add(new{name,passed});if(!passed)throw new InvalidOperationException(name);}
        try
        {
            await Task.Delay(400);UpdateTheme();await Task.Delay(100);RenderVerification("dark-launcher");
            Check("dark palette",((SolidColorBrush)Application.Current.Resources["CanvasBrush"]).Color.R<40);
            preferences.Theme="Light";UpdateTheme();Check("light palette",((SolidColorBrush)Application.Current.Resources["CanvasBrush"]).Color.R>200);preferences.Theme="Dark";UpdateTheme();
            var screen=SelectedScreen;var config=Config(screen);config.MusicReactive=false;config.Performance="Quality";ApplyScene(items[0],screen);
            for(int i=0;i<100&&host?.Position is not >100;i++)await Task.Delay(100);
            Check("video desktop surface plays",host is not null && host.Active && host.Healthy && host.Playing && host.Position>100);
            PauseClick(this,new());await Task.Delay(500);long frozen=host!.Position;await Task.Delay(500);Check("video pause stops playback",config.UserPaused&&host.Paused&&!host.Playing&&Math.Abs(host.Position-frozen)<150);PauseClick(this,new());
            var resumeClock=Stopwatch.StartNew();for(int i=0;i<100&&(!host.Playing||host.Position==frozen);i++)await Task.Delay(100);
            Check("video resumes",!config.UserPaused&&host.Playing&&host.Position!=frozen);results.Add(new{name="resume timing",milliseconds=resumeClock.ElapsedMilliseconds});
            byte[] samples=new byte[400];for(int i=0;i<100;i++)BitConverter.GetBytes(.25f).CopyTo(samples,i*4);Check("system audio amplitude",Math.Abs(AudioReaction.Measure(samples,samples.Length)-.75)<.01);
            audio.SetEnabled(true);await Task.Delay(250);results.Add(new{name="WASAPI device",available=audio.Error is null,error=audio.Error});
            audio.SetEnabled(false);
            ShowMini();await Task.Delay(100);Check("mini player displays scene",mini?.TitleLabel.Text==items[0].Name && mini.IsVisible);mini!.Hide();
            string invalidVideo=Path.Combine(folder,"invalid.mp4");File.WriteAllText(invalidVideo,"not a video");config.Performance="Eco";ApplyScene(new("Invalid",invalidVideo,null,"TEST"),screen);
            for(int i=0;i<100&&pendingScenes.ContainsKey(screen.DeviceName);i++)await Task.Delay(100);
            Check("failed preparation preserves current wallpaper",host.Active&&host.Playing&&config.Scene==PlaybackRules.Key(items[0])&&Status.Text.StartsWith("Не удалось применить:"));File.Delete(invalidVideo);config.Performance="Quality";
            SettingsClick(this,new());await Task.Delay(200);RenderVerification("studio-settings");SettingsOverlay.Visibility=Visibility.Collapsed;
            Editor.Open(items[0]);EditorOverlay.Visibility=Visibility.Visible;await Task.Delay(300);RenderVerification("editor");Editor.Close();EditorOverlay.Visibility=Visibility.Collapsed;
            Check("editor keeps artwork while loading",Editor.Poster.Source is not null);
            int beforeEdit=items.Count;Editor.Open(items[0]);var cancelledEdit=Editor.Render(true);Editor.Close();await cancelledEdit;
            Check("closing editor cancels export",items.Count==beforeEdit && Editor.ExportButton.IsEnabled && Editor.Video.Source is null);
            string edited=Path.Combine(folder,"edited.mp4");await VideoRender.Run(items[0].Path,edited,new(){Start=.5,End=4.5,Speed=1.25,Seam=.4,Brightness=.05,Saturation=.8},PerformanceProfile.Resolve("Eco"));Check("video editor renders",new FileInfo(edited).Length>1000);
            string thumbnail=Path.Combine(folder,"edited.jpg");await VideoRender.Thumbnail(edited,thumbnail);Check("editor thumbnail",File.Exists(thumbnail));
            async Task<int> Encode(params string[] args){var start=new ProcessStartInfo(VideoRender.Encoder){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};foreach(var arg in args)start.ArgumentList.Add(arg);using var process=Process.Start(start)!;var error=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();if(process.ExitCode!=0)results.Add(new{name="ffmpeg diagnostics",error=await error});return process.ExitCode;}
            string tone=Path.Combine(folder,"tone.mp4");Check("editor audio fixture",await Encode("-y","-loglevel","error","-f","lavfi","-i","testsrc2=size=640x360:rate=24","-f","lavfi","-i","sine=frequency=440","-t","2","-c:v","libx264","-c:a","aac",tone)==0);
            string toneEdit=Path.Combine(folder,"tone-edit.mp4");await VideoRender.Run(tone,toneEdit,new(){End=2,Speed=1.1,Seam=.3},PerformanceProfile.Resolve("Eco"));Check("edited audio preserved",await Encode("-v","error","-i",toneEdit,"-map","0:a","-f","null","-")==0);
            string cancelInput=Path.Combine(folder,"cancel-"+Guid.NewGuid().ToString("N")+".mp4");File.Copy(tone,cancelInput);
            using(var cancel=new CancellationTokenSource())
            {
                var preparing=VideoRender.Optimized(cancelInput,PerformanceProfile.Resolve("Eco"),cancel.Token);
                using var waiterCancel=new CancellationTokenSource();var waiting=VideoRender.Optimized(cancelInput,PerformanceProfile.Resolve("Eco"),waiterCancel.Token);waiterCancel.Cancel();
                bool stopped=false;try{await waiting;}catch(OperationCanceledException){stopped=true;}Check("proxy waiting can be cancelled",stopped);
                cancel.Cancel();stopped=false;try{await preparing;}catch(OperationCanceledException){stopped=true;}Check("running proxy can be cancelled",stopped);
            }
            string optimizedFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","optimized");Check("cancelled proxy leaves no temporary file",!Directory.EnumerateFiles(optimizedFolder,"*.tmp.mp4").Any());
            var pair=await Task.WhenAll(VideoRender.Optimized(cancelInput,PerformanceProfile.Resolve("Eco")),VideoRender.Optimized(cancelInput,PerformanceProfile.Resolve("Eco")));
            Check("concurrent proxy preparation is playable",pair[0]==pair[1]&&await Encode("-v","error","-i",pair[0],"-f","null","-")==0);File.Delete(pair[0]);File.Delete(cancelInput);
            string pendingInput=Path.Combine(folder,"pending-"+Guid.NewGuid().ToString("N")+".mp4");File.Copy(tone,pendingInput);config.Performance="Eco";
            ApplyScene(new("Pending",pendingInput,null,"TEST"),screen);StopClick(this,new());await Task.Delay(250);
            Check("stop cancels scene preparation",host?.Active!=true && pendingScenes.Count==0 && applyCancellations.Count==0);File.Delete(pendingInput);
            var layered=new Wallpaper("Layers test",tone,items[0].Thumbnail,"TEST");preferences.Layers[tone]=LayersFor(items[0])!;config.Interactive=true;ApplyScene(layered,screen);await Task.Delay(400);
            var surface=host?.Interactive;Check("custom layers create interactive surface",surface is not null && surface.RenderedFrames>0);
            host!.SetUserPaused(true);await Task.Delay(150);int frames=surface!.RenderedFrames;await Task.Delay(250);Check("custom layers pause",surface.RenderedFrames==frames);host.SetUserPaused(false);await Task.Delay(250);Check("custom layers resume",surface.RenderedFrames>frames);host.Stop();config.Interactive=false;
            var custom=new Wallpaper("Edited test",edited,thumbnail,"TEST");string bundle=Path.Combine(folder,"test.vwpbundle");config.Playlist=new(){"preset:0",PlaybackRules.Key(custom)};config.Scene="preset:0";
            await BundleService.Export(bundle,"Test collection","VWP",new[]{items[0],custom},config,LayersFor);var imported=await BundleService.Import(bundle,Path.Combine(folder,"imported"));
            Check("bundle scene and settings roundtrip",imported.Scenes.Count==2 && imported.Settings.Playlist.Count==2 && imported.Scenes[0].Layers?.Foreground is string fg && File.Exists(fg));
            string corrupt=Path.Combine(folder,"corrupt.vwpbundle");File.Copy(bundle,corrupt,true);using(var archive=ZipFile.Open(corrupt,ZipArchiveMode.Update)){using var input=archive.GetEntry("manifest.json")!.Open();var manifest=JsonSerializer.Deserialize<BundleManifest>(input)!;input.Close();var video=archive.GetEntry(manifest.Scenes[0].Video)!;string path=video.FullName;video.Delete();using var file=archive.CreateEntry(path).Open();file.WriteByte(7);}
            bool rejected=false;try{await BundleService.Import(corrupt,Path.Combine(folder,"corrupt-import"));}catch(InvalidDataException){rejected=true;}Check("corrupt bundle rejected",rejected);
            string traversal=Path.Combine(folder,"traversal.vwpbundle");File.Delete(traversal);using(var archive=ZipFile.Open(traversal,ZipArchiveMode.Create)){using var file=archive.CreateEntry("manifest.json").Open();JsonSerializer.Serialize(file,new BundleManifest{Scenes=new(){new(){Video="../escape.mp4"}},Hashes=new(){{"../escape.mp4",new string('a',64)}}});}
            rejected=false;try{await BundleService.Import(traversal,Path.Combine(folder,"unsafe-import"));}catch(InvalidDataException){rejected=true;}Check("bundle path traversal rejected",rejected);
            catalog=new(){new("Sakura depth","domovoyproj","Layered anime scene","Аниме","https://github.com/domovoyproj/VWP/releases/download/v0.3.0/Sakura-Depth.vwpbundle",new string('a',64),"https://github.com/domovoyproj")};GalleryOverlay.Visibility=Visibility.Visible;FilterGallery();await Task.Delay(100);RenderVerification("gallery");Check("gallery author search",Gallery.Entries.Items.Count==1);Gallery.Search.Text="nobody";Check("gallery search filters",Gallery.Entries.Items.Count==0);GalleryOverlay.Visibility=Visibility.Collapsed;
        }
        catch(Exception e){results.Add(new{name="error",passed=false,error=e.ToString()});Environment.ExitCode=1;}
        finally{preferences=saved;foreach(var player in hosts.Values)player.Stop();File.WriteAllText(Path.Combine(folder,"studio-result.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));QuitClick(this,new());}
    }
}
