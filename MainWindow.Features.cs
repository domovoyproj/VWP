using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace VWP;
public partial class MainWindow
{
    bool featuresReady,loadingControls,sessionLocked,suspended,importing;
    readonly DispatcherTimer featureTimer=new(){Interval=TimeSpan.FromSeconds(1)};
    readonly Dictionary<string,DateTime> nextScene=new();
    readonly Random random=new();
    int hoverGeneration;
    MediaElement? hoverVideo;
    InteractiveVisual? hoverMotion;
    string mood="Все настроения";
    string sceneFormat="Все форматы";
    Forms.Screen SelectedScreen => Forms.Screen.AllScreens[Math.Clamp(Monitor?.SelectedIndex??0,0,Forms.Screen.AllScreens.Length-1)];
    MonitorPreferences Config(Forms.Screen screen)
    {
        if(!preferences.Monitors.TryGetValue(screen.DeviceName,out var config))preferences.Monitors[screen.DeviceName]=config=new(){Volume=preferences.Volume};
        return config;
    }
    Wallpaper? FindScene(string? key)=>items.FirstOrDefault(scene=>PlaybackRules.Key(scene)==key);
    static MonitorPreferences PlaybackConfig(Wallpaper? scene,MonitorPreferences config)=>scene?.PresetId is not null?config.ForScene(scene.IsSpatial):config;
    DesktopHost GetHost(Forms.Screen screen)
    {
        if(hosts.TryGetValue(screen.DeviceName,out var player))return player;
        player=new DesktopHost();hosts[screen.DeviceName]=player;
        player.Failed+=()=>Dispatcher.BeginInvoke(new Action(()=>Status.Text="Ошибка видео на "+screen.DeviceName));
        player.LoopRequired+=()=>Dispatcher.BeginInvoke(new Action(player.Replay));
        player.SurfaceReady+=()=>Dispatcher.BeginInvoke(new Action(()=>{player.CompleteRecovery();player.SetFraming(PlaybackConfig(FindScene(Config(screen).Scene),Config(screen)));player.SetUserPaused(Config(screen).UserPaused);EvaluatePause(screen,player);}));
        return player;
    }
    void InitializeFeatures()
    {
        MigrateLegacySettings();
        featuresReady=true;MoodPicker.SelectedIndex=0;LoadMonitorControls();
        featureTimer.Tick+=(_,_)=>TickFeatures();featureTimer.Start();
        SystemEvents.SessionSwitch+=SessionChanged;
        SystemEvents.PowerModeChanged+=PowerChanged;
        Settings.CloseButton.Click+=(_,_)=>SettingsOverlay.Visibility=Visibility.Collapsed;
        Settings.SaveButton.Click+=(_,_)=>SaveFeatureSettings();
        Settings.MoveUp.Click+=(_,_)=>ReorderPlaylist(-1);
        Settings.MoveDown.Click+=(_,_)=>ReorderPlaylist(1);
        Settings.RemoveEntry.Click+=(_,_)=>{if(Settings.PlaylistEntries.SelectedItem is Wallpaper scene){Config(SelectedScreen).Playlist.Remove(PlaybackRules.Key(scene));ShowPlaylistEntries();Save();UpdateSceneActions();RefreshFilter();}};
        Settings.CheckUpdateButton.Click+=async(_,_)=>await CheckForUpdates();
        Settings.InstallUpdateButton.Click+=async(_,_)=>await InstallUpdate();
        Settings.VersionLabel.Text="VWP "+UpdateService.CurrentVersion;
        Loaded+=async(_,_)=>{if(preferences.CheckUpdates && !Environment.GetCommandLineArgs().Any(a=>a.StartsWith("--verify")))await CheckForUpdates();};
        if(Environment.GetCommandLineArgs().Contains("--verify-features"))Loaded+=async(_,_)=>await VerifyFeatures();
    }
    void MigrateLegacySettings()
    {
        if(preferences.SettingsVersion>=2)return;
        var scene=items.FirstOrDefault(x=>x.Path==preferences.Last || x.PresetId==preferences.LastPresetId && x.PresetId is not null);
        if(scene is null && preferences.Last is not null && System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(preferences.Last))=="assets" && int.TryParse(System.IO.Path.GetFileNameWithoutExtension(preferences.Last),out int legacy))scene=items.FirstOrDefault(x=>x.PresetId==legacy);
        string monitor=preferences.Monitor??SelectedScreen.DeviceName;
        if(scene is not null && !preferences.Monitors.ContainsKey(monitor))preferences.Monitors[monitor]=new(){Scene=PlaybackRules.Key(scene),Volume=preferences.Volume};
        preferences.SettingsVersion=2;Save();
    }
    void SessionChanged(object? sender,SessionSwitchEventArgs e)=>Dispatcher.BeginInvoke(new Action(()=>{if(e.Reason==SessionSwitchReason.SessionLock){sessionLocked=true;StopPreview();StopHover();}else if(e.Reason==SessionSwitchReason.SessionUnlock)sessionLocked=false;TickFeatures();}));
    void PowerChanged(object sender,PowerModeChangedEventArgs e)=>Dispatcher.BeginInvoke(new Action(()=>{if(e.Mode==PowerModes.Suspend){suspended=true;StopPreview();StopHover();}else if(e.Mode==PowerModes.Resume)suspended=false;TickFeatures();}));
    void EvaluatePause(Forms.Screen screen,DesktopHost player)=>player.SetAutomaticPause(PausePolicy.Reason(screen,preferences.PauseFullscreen,preferences.PauseBattery,sessionLocked||suspended,preferences.PauseExceptions));
    void TickFeatures()
    {
        foreach(var screen in Forms.Screen.AllScreens)
        {
            var config=Config(screen);
            try
            {
                if(hosts.TryGetValue(screen.DeviceName,out var player))
                {
                    if(!player.Healthy)player.Recover();else player.UpdateBackdrop();
                    player.RefreshFrame();
                    EvaluatePause(screen,player);
                }
                if(!config.PlaylistEnabled || !PlaybackRules.InSchedule(DateTime.Now,config.ScheduleStart,config.ScheduleEnd))continue;
                if(player?.Paused==true){nextScene[screen.DeviceName]=DateTime.Now.AddMinutes(config.IntervalMinutes);continue;}
                if(!nextScene.TryGetValue(screen.DeviceName,out var due))nextScene[screen.DeviceName]=DateTime.Now.AddMinutes(config.IntervalMinutes);
                else if(DateTime.Now>=due)Advance(screen,1);
            }
            catch(Exception e){Status.Text="Дисплей: "+e.Message;}
        }
        UpdatePlaybackStatus();
        TickStudio();
    }
    void UpdatePlaybackStatus()
    {
        var player=host;PauseButton.Content=player?.Paused==true?"▶  Играть":"Ⅱ  Пауза";
        if(player?.Active==true)
        {
            var config=Config(SelectedScreen);var scene=FindScene(config.Scene);
            Status.Text=(player.PauseReason is not null?"Автопауза · "+player.PauseReason:config.UserPaused?"Пауза":"На рабочем столе")+" · "+(scene?.Name??"Видео")+(config.PlaylistEnabled?" · ♫ "+config.PlaylistName:"");
        }
    }
    async void ApplyScene(Wallpaper scene,Forms.Screen screen)
    {
        if(!File.Exists(scene.Path)){Status.Text="Файл не найден: "+scene.Name;return;}
        var config=Config(screen);var playback=PlaybackConfig(scene,config);var player=GetHost(screen);
        try
        {
            int generation=applyGeneration.GetValueOrDefault(screen.DeviceName)+1;applyGeneration[screen.DeviceName]=generation;
            if(LayersFor(scene) is SceneLayers layers && layers.Background is not null && (scene.IsSpatial || config.Interactive || playback.SceneAnimation && layers.MotionId is not null))
                player.PlayInteractive(layers,screen,playback,audio,FramedCover(scene,screen,playback));
            else
            {
                string path=scene.Path;var profile=PerformanceProfile.Resolve(config.Performance);
                if(profile.Key!="Quality")
                {
                    Status.Text="Готовлю видео для профиля «"+profile.Name+"»…";path=await VideoRender.Optimized(scene.Path,profile);
                    if(exiting||applyGeneration.GetValueOrDefault(screen.DeviceName)!=generation)return;
                }
                player.Play(path,screen,FramedCover(scene,screen,config),true);
            }
            player.SetFraming(playback);player.Volume=config.Volume;player.SetUserPaused(config.UserPaused);EvaluatePause(screen,player);
            config.Scene=PlaybackRules.Key(scene);preferences.Last=scene.Path;preferences.LastPresetId=scene.PresetId;preferences.Monitor=screen.DeviceName;
            nextScene[screen.DeviceName]=DateTime.Now.AddMinutes(config.IntervalMinutes);Save();UpdatePlaybackStatus();
        }
        catch(Exception e){if(player.Active)player.Stop();Status.Text="Не удалось применить: "+e.Message;}
    }
    static string? FramedCover(Wallpaper scene,Forms.Screen screen,MonitorPreferences config)
    {
        string spatial=System.IO.Path.Combine(AppContext.BaseDirectory,"assets","spatial",$"{scene.PresetId}.png");
        string? cover=scene.IsSpatial && File.Exists(spatial)?spatial:FullResolutionCover(scene);
        if(cover is null || !File.Exists(cover))return null;
        var info=new FileInfo(cover);
        string folder=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","covers");Directory.CreateDirectory(folder);
        string key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(screen.DeviceName+cover+info.Length+info.LastWriteTimeUtc.Ticks+config.Fit+config.FocusX.ToString(System.Globalization.CultureInfo.InvariantCulture)+config.FocusY.ToString(System.Globalization.CultureInfo.InvariantCulture)+screen.Bounds)));
        string output=System.IO.Path.Combine(folder,key+".jpg");if(File.Exists(output))return output;
        using var source=System.Drawing.Image.FromFile(cover);using var target=new System.Drawing.Bitmap(screen.Bounds.Width,screen.Bounds.Height);
        using var graphics=System.Drawing.Graphics.FromImage(target);graphics.Clear(System.Drawing.Color.FromArgb(20,20,28));graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source,PlaybackRules.Frame(source.Width,source.Height,target.Width,target.Height,config.Fit,config.FocusX,config.FocusY));
        using var jpegOptions=new System.Drawing.Imaging.EncoderParameters(1);
        jpegOptions.Param[0]=new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality,95L);
        var jpegCodec=System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().First(c=>c.FormatID==System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
        target.Save(output,jpegCodec,jpegOptions);return output;
    }
    static string? FullResolutionCover(Wallpaper scene)
    {
        string poster=System.IO.Path.ChangeExtension(scene.Path,".cover.jpg");
        return scene.PresetId is not null && File.Exists(poster)?poster:scene.Thumbnail;
    }
    static string PreviewPath(Wallpaper scene)
    {
        string preview=System.IO.Path.ChangeExtension(scene.Path,".preview.mp4");
        return scene.PresetId is not null && File.Exists(preview)?preview:scene.Path;
    }
    void MonitorChanged(object sender,SelectionChangedEventArgs e){if(featuresReady)LoadMonitorControls();}
    void LoadMonitorControls()
    {
        if(!featuresReady)return;loadingControls=true;var config=Config(SelectedScreen);Volume.Value=config.Volume;RefreshScenePresentations();
        if(FindScene(config.Scene) is Wallpaper scene)Library.SelectedItem=scene;
        loadingControls=false;UpdateSceneActions();UpdatePlaybackStatus();RefreshFilter();
    }
    void RefreshScenePresentations()
    {
        string? selected=Library.SelectedItem is Wallpaper current?PlaybackRules.Key(current):null;
        StopHover();StopPreview();
        for(int i=0;i<items.Count;i++)
        {
            var item=items[i];if(item.PresetId is not int id)continue;
            bool spatial=item.IsSpatial;
            string cover=System.IO.Path.Combine(AppContext.BaseDirectory,"assets",spatial?$"spatial/{id}.png":$"{id}.jpg");
            if(!File.Exists(cover))continue;
            string description=spatial?SpatialScene.Descriptions[id]:definitions.GetValueOrDefault(id)?.Description??item.Description;
            string subtitle=item.Category+(spatial?" · 3D · REALTIME":" · 4K · 60 FPS");
            if(item.Thumbnail!=cover || item.Subtitle!=subtitle)items[i]=item with {Thumbnail=cover,Subtitle=subtitle,Description=description};
        }
        if(selected is not null)Library.SelectedItem=FindScene(selected);
    }
    void UpdateSceneActions()
    {
        if(!featuresReady || Library.SelectedItem is not Wallpaper scene)return;
        FavoriteButton.Content=preferences.Favorites.Contains(PlaybackRules.Key(scene))?"♥":"♡";
        PlaylistButton.Content=Config(SelectedScreen).Playlist.Contains(PlaybackRules.Key(scene))?"✓ Плейлист":"＋ Плейлист";
    }
    void FavoriteClick(object sender,RoutedEventArgs e)
    {
        if(Library.SelectedItem is not Wallpaper scene)return;string key=PlaybackRules.Key(scene);
        if(!preferences.Favorites.Remove(key))preferences.Favorites.Add(key);Save();UpdateSceneActions();RefreshFilter();
    }
    void PlaylistClick(object sender,RoutedEventArgs e)
    {
        if(Library.SelectedItem is not Wallpaper scene)return;var playlist=Config(SelectedScreen).Playlist;string key=PlaybackRules.Key(scene);
        if(!playlist.Remove(key))playlist.Add(key);Save();UpdateSceneActions();RefreshFilter();
    }
    bool MatchesCollection(Wallpaper scene)
    {
        bool collection=category switch{"Все сцены"=>true,"Избранное"=>preferences.Favorites.Contains(PlaybackRules.Key(scene)),"Плейлист"=>Config(SelectedScreen).Playlist.Contains(PlaybackRules.Key(scene)),_=>scene.Category==category};
        return collection && (mood switch{"Ночной Токио"=>scene.PresetId is 0 or 1 or 6 or 9 or 14,"Спокойствие"=>scene.PresetId is 3 or 5 or 7 or 8 or 10 or 12 or 17,"Космос"=>scene.Category=="Космос","Работа"=>scene.PresetId is 5 or 8 or 10 or 14 or 15,_=>true});
    }
    void FormatChanged(object sender,SelectionChangedEventArgs e){sceneFormat=(FormatPicker.SelectedItem as ComboBoxItem)?.Content as string??"Все форматы";if(Library is not null)RefreshFilter();}
    void MoodChanged(object sender,SelectionChangedEventArgs e){mood=(MoodPicker.SelectedItem as ComboBoxItem)?.Content as string??"Все настроения";RefreshFilter();}
    void PreviousClick(object sender,RoutedEventArgs e)=>Advance(SelectedScreen,-1);
    void NextClick(object sender,RoutedEventArgs e)=>Advance(SelectedScreen,1);
    void Advance(Forms.Screen screen,int direction)
    {
        var config=Config(screen);var available=config.Playlist.Where(key=>FindScene(key) is Wallpaper scene && File.Exists(scene.Path)).ToArray();
        var key=PlaybackRules.Next(available,config.Scene,config.Shuffle,random,direction);
        if(FindScene(key) is Wallpaper scene){ApplyScene(scene,screen);if(screen.DeviceName==SelectedScreen.DeviceName)Library.SelectedItem=scene;}
        else {config.PlaylistEnabled=false;Save();Status.Text="Добавьте доступные сцены в плейлист";}
    }
    void SettingsClick(object sender,RoutedEventArgs e)
    {
        StopHover();StopPreview();var config=Config(SelectedScreen);
        Settings.DisplayLabel.Text=Monitor.SelectedItem+" · отдельные настройки";
        Settings.FitMode.SelectedIndex=config.Fit=="Fit"?1:0;Settings.FocusX.Value=config.FocusX;Settings.FocusY.Value=config.FocusY;
        string? activeCover=FindScene(config.Scene)?.Thumbnail;
        Settings.CropImage.Source=activeCover is not null && File.Exists(activeCover)?new BitmapImage(new Uri(activeCover)):HeroImage.Source;Settings.Aspect=(double)SelectedScreen.Bounds.Width/SelectedScreen.Bounds.Height;
        Settings.PlaylistName.Text=config.PlaylistName;Settings.PlaylistEnabled.IsChecked=config.PlaylistEnabled;Settings.Shuffle.IsChecked=config.Shuffle;
        Settings.Interval.Text=config.IntervalMinutes.ToString();Settings.ScheduleStart.Text=config.ScheduleStart;Settings.ScheduleEnd.Text=config.ScheduleEnd;
        Settings.PauseFullscreen.IsChecked=preferences.PauseFullscreen;Settings.PauseBattery.IsChecked=preferences.PauseBattery;Settings.HoverPreview.IsChecked=preferences.HoverPreview;Settings.CheckUpdates.IsChecked=preferences.CheckUpdates;
        LoadStudioSettings();
        ShowPlaylistEntries();Settings.Error.Text="";SettingsOverlay.Visibility=Visibility.Visible;Settings.RenderCrop();
        Settings.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)));
    }
    void ShowPlaylistEntries(){Settings.PlaylistEntries.ItemsSource=Config(SelectedScreen).Playlist.Select(FindScene).Where(s=>s is not null).ToArray();Settings.PlaylistCount.Text=Config(SelectedScreen).Playlist.Count+" сцен";}
    void ReorderPlaylist(int delta)
    {
        if(Settings.PlaylistEntries.SelectedItem is not Wallpaper scene)return;var playlist=Config(SelectedScreen).Playlist;int index=playlist.IndexOf(PlaybackRules.Key(scene)),next=index+delta;
        if(next<0||next>=playlist.Count)return;(playlist[index],playlist[next])=(playlist[next],playlist[index]);ShowPlaylistEntries();Settings.PlaylistEntries.SelectedItem=scene;Save();
    }
    void SaveFeatureSettings()
    {
        if(!int.TryParse(Settings.Interval.Text,out int interval)||interval<1||interval>1440){Settings.Error.Text="Интервал: от 1 до 1440 минут.";return;}
        if(!TimeSpan.TryParseExact(Settings.ScheduleStart.Text,@"hh\:mm",null,out _)||!TimeSpan.TryParseExact(Settings.ScheduleEnd.Text,@"hh\:mm",null,out _)){Settings.Error.Text="Время: ЧЧ:ММ, например 22:00.";return;}
        var config=Config(SelectedScreen);if(Settings.PlaylistEnabled.IsChecked==true && config.Playlist.Count==0){Settings.Error.Text="Добавьте хотя бы одну сцену в плейлист.";return;}
        config.Fit=Settings.FitMode.SelectedIndex==1?"Fit":"Fill";config.FocusX=Settings.FocusX.Value;config.FocusY=Settings.FocusY.Value;
        config.PlaylistName=string.IsNullOrWhiteSpace(Settings.PlaylistName.Text)?"Мой плейлист":Settings.PlaylistName.Text.Trim();config.IntervalMinutes=interval;config.ScheduleStart=Settings.ScheduleStart.Text;config.ScheduleEnd=Settings.ScheduleEnd.Text;config.PlaylistEnabled=Settings.PlaylistEnabled.IsChecked==true;config.Shuffle=Settings.Shuffle.IsChecked==true;
        preferences.PauseFullscreen=Settings.PauseFullscreen.IsChecked==true;preferences.PauseBattery=Settings.PauseBattery.IsChecked==true;preferences.HoverPreview=Settings.HoverPreview.IsChecked==true;preferences.CheckUpdates=Settings.CheckUpdates.IsChecked==true;
        SaveStudioSettings();
        RefreshScenePresentations();
        Save();SettingsOverlay.Visibility=Visibility.Collapsed;
        if(host?.Active==true && FindScene(config.Scene) is Wallpaper scene)ApplyScene(scene,SelectedScreen);
        else if(config.PlaylistEnabled)Advance(SelectedScreen,1);
        TickFeatures();
    }
    async void ImportFiles(IEnumerable<string> paths)
    {
        if(importing)return;importing=true;StopHover();
        try
        {
            string folder=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","library");Directory.CreateDirectory(folder);
            foreach(string path in paths.Where(File.Exists).Where(p=>new[]{".mp4",".webm",".mkv",".mov",".avi"}.Contains(System.IO.Path.GetExtension(p).ToLowerInvariant())))
            {
                Status.Text="Импорт · "+System.IO.Path.GetFileName(path);
                string destination=System.IO.Path.GetFullPath(path).StartsWith(folder+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)?path:System.IO.Path.Combine(folder,Guid.NewGuid().ToString("N")+System.IO.Path.GetExtension(path));
                if(destination!=path)await Task.Run(()=>File.Copy(path,destination));
                if(items.Any(s=>s.Path==destination))continue;
                preferences.Imports.Add(destination);preferences.ImportNames[destination]=System.IO.Path.GetFileNameWithoutExtension(path);
                string? cover=null;try{string thumbnail=Path.ChangeExtension(destination,".jpg");await VideoRender.Thumbnail(destination,thumbnail);cover=thumbnail;preferences.ImportThumbnails[destination]=thumbnail;}catch{}
                var scene=new Wallpaper(System.IO.Path.GetFileNameWithoutExtension(path),destination,cover,"LOCAL VIDEO");items.Add(scene);Library.SelectedItem=scene;Save();
            }
            RefreshFilter();Status.Text="Видео сохранены в библиотеку VWP";
        }
        catch(Exception e){Status.Text="Импорт: "+e.Message;}
        finally{importing=false;}
    }
    async void FilesDropped(object sender,DragEventArgs e){if(e.Data.GetData(DataFormats.FileDrop) is string[] files){foreach(var bundle in files.Where(f=>Path.GetExtension(f).Equals(".vwpbundle",StringComparison.OrdinalIgnoreCase)))try{await ImportCollection(bundle);}catch{}ImportFiles(files.Where(f=>!Path.GetExtension(f).Equals(".vwpbundle",StringComparison.OrdinalIgnoreCase)));}e.Handled=true;}
    void FilesDragOver(object sender,DragEventArgs e){e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;}
    async void CardEnter(object sender,MouseEventArgs e)
    {
        if(!featuresReady||!preferences.HoverPreview||SettingsOverlay.Visibility==Visibility.Visible||sender is not Grid card||card.DataContext is not Wallpaper scene||!File.Exists(scene.Path))return;
        StopHover();int generation=hoverGeneration;await Task.Delay(650);
        if(generation!=hoverGeneration||!card.IsMouseOver||!IsVisible||HeroVideo.Visibility==Visibility.Visible||scenePreview is not null)return;
        if((scene.IsSpatial || Config(SelectedScreen).SceneAnimation) && LayersFor(scene) is SceneLayers layers && layers.MotionId is not null)
        {
            hoverMotion=new InteractiveVisual(layers,new MonitorPreferences{Performance="Balance",SceneAnimation=true},SelectedScreen,audio){IsHitTestVisible=false};
            card.Children.Add(hoverMotion);return;
        }
        hoverVideo=new MediaElement{Source=new Uri(PreviewPath(scene)),Volume=0,LoadedBehavior=MediaState.Manual,UnloadedBehavior=MediaState.Close,Stretch=Stretch.UniformToFill,IsHitTestVisible=false};
        var video=hoverVideo;video.MediaEnded+=(_,_)=>{video.Position=TimeSpan.Zero;video.Play();};video.MediaFailed+=(_,_)=>StopHover();card.Children.Add(video);video.Play();
    }
    void CardLeave(object sender,MouseEventArgs e)=>StopHover();
    void StopHover(){hoverGeneration++;if(hoverMotion is not null){hoverMotion.Dispose();if(hoverMotion.Parent is Panel parent)parent.Children.Remove(hoverMotion);hoverMotion=null;}if(hoverVideo is null)return;var video=hoverVideo;hoverVideo=null;video.Stop();video.Source=null;if(video.Parent is Panel panel)panel.Children.Remove(video);}
}
