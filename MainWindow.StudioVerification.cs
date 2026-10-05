using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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
            var screen=SelectedScreen;var config=Config(screen);config.Interactive=true;config.MusicReactive=false;config.Performance="Eco";ApplyScene(items[0],screen);await Task.Delay(1400);
            var interactive=host!.Interactive;Check("interactive native surface",interactive is not null && host.Active && host.Healthy && interactive.ActualWidth>0 && interactive.RenderedFrames>0);
            int frames=interactive!.RenderedFrames;host.SetUserPaused(true);await Task.Delay(400);int pausedFrames=interactive.RenderedFrames;await Task.Delay(500);Check("interactive pause stops rendering",pausedFrames==interactive.RenderedFrames);host.SetUserPaused(false);await Task.Delay(300);Check("interactive resumes",interactive.RenderedFrames>pausedFrames);
            var shot=new RenderTargetBitmap((int)interactive.ActualWidth,(int)interactive.ActualHeight,96,96,PixelFormats.Pbgra32);shot.Render(interactive);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(shot));using(var output=File.Create(Path.Combine(folder,"interactive.png")))png.Save(output);
            byte[] samples=new byte[400];for(int i=0;i<100;i++)BitConverter.GetBytes(.25f).CopyTo(samples,i*4);Check("system audio amplitude",Math.Abs(AudioReaction.Measure(samples,samples.Length)-.75)<.01);
            audio.SetEnabled(true);await Task.Delay(250);results.Add(new{name="WASAPI device",available=audio.Error is null,error=audio.Error});
            if(audio.Error is null){using var sound=new WaveOutEvent();sound.Init(new SignalGenerator{Gain=.025,Frequency=440,Type=SignalGeneratorType.Sin});sound.Play();await Task.Delay(650);Check("system output drives audio reaction",audio.Level>.01);sound.Stop();}audio.SetEnabled(false);
            ShowMini();await Task.Delay(100);Check("mini player displays scene",mini?.TitleLabel.Text==items[0].Name && mini.IsVisible);mini!.Hide();
            SettingsClick(this,new());await Task.Delay(200);RenderVerification("studio-settings");SettingsOverlay.Visibility=Visibility.Collapsed;
            Editor.Open(items[0]);EditorOverlay.Visibility=Visibility.Visible;await Task.Delay(300);RenderVerification("editor");Editor.Close();EditorOverlay.Visibility=Visibility.Collapsed;
            string edited=Path.Combine(folder,"edited.mp4");await VideoRender.Run(items[0].Path,edited,new(){Start=.5,End=4.5,Speed=1.25,Seam=.4,Brightness=.05,Saturation=.8},PerformanceProfile.Resolve("Eco"));Check("video editor renders",new FileInfo(edited).Length>1000);
            string thumbnail=Path.Combine(folder,"edited.jpg");await VideoRender.Thumbnail(edited,thumbnail);Check("editor thumbnail",File.Exists(thumbnail));
            async Task<int> Encode(params string[] args){var start=new ProcessStartInfo(VideoRender.Encoder){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};foreach(var arg in args)start.ArgumentList.Add(arg);using var process=Process.Start(start)!;var error=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();if(process.ExitCode!=0)results.Add(new{name="ffmpeg diagnostics",error=await error});return process.ExitCode;}
            string tone=Path.Combine(folder,"tone.mp4");Check("editor audio fixture",await Encode("-y","-loglevel","error","-f","lavfi","-i","testsrc2=size=640x360:rate=24","-f","lavfi","-i","sine=frequency=440","-t","2","-c:v","libx264","-c:a","aac",tone)==0);
            string toneEdit=Path.Combine(folder,"tone-edit.mp4");await VideoRender.Run(tone,toneEdit,new(){End=2,Speed=1.1,Seam=.3},PerformanceProfile.Resolve("Eco"));Check("edited audio preserved",await Encode("-v","error","-i",toneEdit,"-map","0:a","-f","null","-")==0);
            string proxy=await VideoRender.Optimized(items[0].Path,PerformanceProfile.Resolve("Eco"));Check("eco profile creates playable proxy",File.Exists(proxy)&&await Encode("-v","error","-i",proxy,"-f","null","-")==0);
            var custom=new Wallpaper("Edited test",edited,thumbnail,"TEST");string bundle=Path.Combine(folder,"test.vwpbundle");config.Playlist=new(){"preset:0",PlaybackRules.Key(custom)};config.Scene="preset:0";
            await BundleService.Export(bundle,"Test collection","VWP",new[]{items[0],custom},config,LayersFor);var imported=await BundleService.Import(bundle,Path.Combine(folder,"imported"));
            Check("bundle scene and settings roundtrip",imported.Scenes.Count==2 && imported.Settings.Playlist.Count==2 && imported.Scenes[0].Layers?.Foreground is string fg && File.Exists(fg));
            string corrupt=Path.Combine(folder,"corrupt.vwpbundle");File.Copy(bundle,corrupt,true);using(var archive=ZipFile.Open(corrupt,ZipArchiveMode.Update)){using var input=archive.GetEntry("manifest.json")!.Open();var manifest=JsonSerializer.Deserialize<BundleManifest>(input)!;input.Close();var video=archive.GetEntry(manifest.Scenes[0].Video)!;string path=video.FullName;video.Delete();using var file=archive.CreateEntry(path).Open();file.WriteByte(7);}
            bool rejected=false;try{await BundleService.Import(corrupt,Path.Combine(folder,"corrupt-import"));}catch(InvalidDataException){rejected=true;}Check("corrupt bundle rejected",rejected);
            string traversal=Path.Combine(folder,"traversal.vwpbundle");using(var archive=ZipFile.Open(traversal,ZipArchiveMode.Create)){using var file=archive.CreateEntry("manifest.json").Open();JsonSerializer.Serialize(file,new BundleManifest{Scenes=new(){new(){Video="../escape.mp4"}},Hashes=new(){{"../escape.mp4",new string('a',64)}}});}
            rejected=false;try{await BundleService.Import(traversal,Path.Combine(folder,"unsafe-import"));}catch(InvalidDataException){rejected=true;}Check("bundle path traversal rejected",rejected);
            catalog=new(){new("Sakura depth","domovoyproj","Layered anime scene","Аниме","https://github.com/domovoyproj/VWP/releases/download/v0.3.0/Sakura-Depth.vwpbundle",new string('a',64),"https://github.com/domovoyproj")};GalleryOverlay.Visibility=Visibility.Visible;FilterGallery();await Task.Delay(100);RenderVerification("gallery");Check("gallery author search",Gallery.Entries.Items.Count==1);Gallery.Search.Text="nobody";Check("gallery search filters",Gallery.Entries.Items.Count==0);GalleryOverlay.Visibility=Visibility.Collapsed;
            string outputFolder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","Gallery-0.3.0"));Directory.CreateDirectory(outputFolder);
            await BundleService.Export(Path.Combine(outputFolder,"Sakura-Depth.vwpbundle"),"Sakura Depth","domovoyproj",new[]{items[0]},new(){Scene="preset:0",Interactive=true,MusicReactive=true},LayersFor);
            await BundleService.Export(Path.Combine(outputFolder,"Night-Drive.vwpbundle"),"Night Drive","domovoyproj",items.Where(s=>s.PresetId is 1 or 6 or 9),new(){Scene="preset:1",Playlist=new(){"preset:1","preset:6","preset:9"},PlaylistEnabled=true},LayersFor);
            await BundleService.Export(Path.Combine(outputFolder,"Quiet-Orbit.vwpbundle"),"Quiet Orbit","domovoyproj",items.Where(s=>s.PresetId is 4 or 11 or 16),new(){Scene="preset:4",Playlist=new(){"preset:4","preset:11","preset:16"},PlaylistEnabled=true,Interactive=true},LayersFor);
            results.Add(new{name="gallery seed packs",directory=outputFolder});
        }
        catch(Exception e){results.Add(new{name="error",passed=false,error=e.ToString()});}
        finally{preferences=saved;foreach(var player in hosts.Values)player.Stop();File.WriteAllText(Path.Combine(folder,"studio-result.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));QuitClick(this,new());}
    }
}
