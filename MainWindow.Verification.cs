using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;
namespace VWP;
public partial class MainWindow
{
    void RenderVerification(string name)
    {
        var visual=(FrameworkElement)Content;var bitmap=new RenderTargetBitmap((int)visual.ActualWidth,(int)visual.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(AppContext.BaseDirectory,"verification",name+".png"));encoder.Save(file);
    }
    async Task VerifyFeatures()
    {
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory,"verification"));var report=new List<object>();
        void Check(string name,bool passed){report.Add(new{name,passed});if(!passed)throw new InvalidOperationException(name);}
        featureTimer.Stop();var original=preferences;preferences=new(){PauseFullscreen=false,PauseBattery=false,CheckUpdates=false,HoverPreview=false};
        try
        {
            await Task.Delay(700);RenderVerification("launcher");
            preferences.LastPresetId=7;preferences.Monitor=SelectedScreen.DeviceName;preferences.Volume=23;MigrateLegacySettings();
            Check("legacy settings migration",Config(SelectedScreen).Scene=="preset:7" && Config(SelectedScreen).Volume==23 && preferences.SettingsVersion==2);
            preferences=new(){PauseFullscreen=false,PauseBattery=false,CheckUpdates=false,HoverPreview=false};
            Check("overnight schedule before midnight",PlaybackRules.InSchedule(new DateTime(2026,1,1,23,0,0),"22:00","06:00"));
            Check("overnight schedule after midnight",PlaybackRules.InSchedule(new DateTime(2026,1,1,5,0,0),"22:00","06:00"));
            Check("overnight schedule excludes daytime",!PlaybackRules.InSchedule(new DateTime(2026,1,1,12,0,0),"22:00","06:00"));
            Check("bad schedule rejected",!PlaybackRules.InSchedule(DateTime.Now,"bad","06:00"));
            var list=new[]{"a","b","c"};Check("playlist wraps forward",PlaybackRules.Next(list,"c",false,new Random(1))=="a");Check("playlist wraps back",PlaybackRules.Next(list,"a",false,new Random(1),-1)=="c");
            Check("shuffle avoids current",Enumerable.Range(0,30).All(_=>PlaybackRules.Next(list,"b",true,random)!="b"));
            var fill=PlaybackRules.Frame(720,1280,1920,1080,"Fill",.5,.25);Check("portrait fill crops vertically",fill.Width==1920 && fill.Height>1080 && fill.Y<0);
            var fit=PlaybackRules.Frame(720,1280,1920,1080,"Fit",.5,.5);Check("portrait fit letterboxes",fit.Width<1920 && fit.Height==1080 && fit.X>0);
            Library.SelectedItem=items[0];FavoriteClick(this,new());category="Избранное";RefreshFilter();Check("favorites filter",System.Windows.Data.CollectionViewSource.GetDefaultView(items).Cast<Wallpaper>().Count()==1);category="Все сцены";RefreshFilter();
            PlaylistClick(this,new());Library.SelectedItem=items[1];PlaylistClick(this,new());var config=Config(SelectedScreen);Check("playlist membership",config.Playlist.SequenceEqual(new[]{"preset:0","preset:1"}));
            var roundtrip=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences))!;Check("preferences roundtrip",roundtrip.Monitors[SelectedScreen.DeviceName].Playlist.SequenceEqual(config.Playlist) && roundtrip.Favorites.SequenceEqual(preferences.Favorites));
            SettingsClick(this,new());await Task.Delay(200);RenderVerification("settings");SettingsOverlay.Visibility=Visibility.Collapsed;
            var screen=SelectedScreen;var player=GetHost(screen);ApplyScene(items[0],screen);
            for(int i=0;i<20&&!player.Playing;i++)await Task.Delay(250);await Task.Delay(300);Check("video started",player.Playing);
            player.SetAutomaticPause("test battery");await Task.Delay(300);long frozen=player.Position;await Task.Delay(700);Check("automatic pause freezes playback",player.Paused && Math.Abs(player.Position-frozen)<150);
            player.SetUserPaused(true);player.SetAutomaticPause(null);await Task.Delay(200);Check("manual pause survives automatic resume",player.Paused);
            player.SetUserPaused(false);await Task.Delay(300);Check("automatic resume",player.Playing);
            config.PlaylistEnabled=true;nextScene[screen.DeviceName]=DateTime.Now.AddSeconds(-1);TickFeatures();await Task.Delay(2000);
            Check("timer advances playlist",config.Scene=="preset:1" && player.Playing);Check("transition surface remains healthy",player.Healthy);
            string portrait=Path.Combine(AppContext.BaseDirectory,"verification","portrait.mp4");
            if(File.Exists(portrait)){player.Play(portrait,screen);config.Fit="Fill";config.FocusY=.25;player.SetFraming(config);await Task.Delay(1700);player.SetFraming(config);Check("native portrait cropping",player.VideoFrame.Height>screen.Bounds.Height && player.VideoFrame.Y<0);config.Fit="Fit";player.SetFraming(config);Check("native portrait fit",player.VideoFrame.Width<screen.Bounds.Width && player.VideoFrame.X>0);}
            foreach(var actual in Forms.Screen.AllScreens){Config(actual).Scene="preset:0";ApplyScene(items[0],actual);}
            await Task.Delay(1500);Check("all connected monitors have independent hosts",Forms.Screen.AllScreens.All(s=>hosts[s.DeviceName].Active && hosts[s.DeviceName].Healthy));
            report.Add(new{name="physical monitor count",count=Forms.Screen.AllScreens.Length});
            string json="{\"draft\":false,\"prerelease\":false,\"tag_name\":\"v0.3.0\",\"assets\":[{\"name\":\"VWP-0.3.0-Setup.exe\",\"browser_download_url\":\"https://github.com/domovoyproj/VWP/releases/download/v0.3.0/VWP-0.3.0-Setup.exe\",\"digest\":\"sha256:"+new string('a',64)+"\"}]}";
            using var document=JsonDocument.Parse(json);Check("update manifest parsed",UpdateService.ParseRelease(document.RootElement)?.Version==new Version(0,3,0));
            using var old=JsonDocument.Parse(json.Replace("v0.3.0","v0.1.0"));Check("old update ignored",UpdateService.ParseRelease(old.RootElement) is null);
            bool rejected=false;try{using var invalid=JsonDocument.Parse(json.Replace("github.com/domovoyproj","example.com/domovoyproj"));UpdateService.ParseRelease(invalid.RootElement);}catch(InvalidDataException){rejected=true;}Check("untrusted update URL rejected",rejected);
        }
        catch(Exception e){report.Add(new{name="error",error=e.ToString(),passed=false});}
        finally{preferences=original;foreach(var player in hosts.Values)player.Stop();File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"verification","features-result.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));QuitClick(this,new());}
    }
}
