using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using LibVLCSharp.Shared;
using Forms = System.Windows.Forms;
namespace VWP;
public record Wallpaper(string Name,string Path,string? Thumbnail,string Subtitle,string Category="Импорт",string Description="Твоё видео",int? PresetId=null,bool IsSpatial=false);
public record PresetDefinition(int Id,string Name,string Category,string Description,string Accent,string Motion);
public sealed class Preferences
{
    public int SettingsVersion {get;set;}
    public List<string> Imports { get; set; }=new();
    public Dictionary<string,string> ImportNames {get;set;}=new();
    public Dictionary<string,string> ImportThumbnails {get;set;}=new();
    public Dictionary<string,SceneLayers> Layers {get;set;}=new();
    public string Theme {get;set;}="System";
    public bool SceneAccent {get;set;}=true;
    public List<string> PauseExceptions {get;set;}=new();
    public string GallerySource {get;set;}="https://raw.githubusercontent.com/domovoyproj/VWP/main/gallery/catalog.json";
    public string? Last { get; set; }
    public int? LastPresetId {get;set;}
    public string? Monitor { get; set; }
    public int Volume { get; set; }
    public bool Autostart { get; set; }
    public bool PauseFullscreen { get; set; }=true;
    public bool PauseBattery { get; set; }=true;
    public bool HoverPreview { get; set; }=true;
    public bool CheckUpdates { get; set; }=true;
    public List<string> Favorites { get; set; }=new();
    public Dictionary<string,MonitorPreferences> Monitors { get; set; }=new();
}
public partial class MainWindow : Window
{
    readonly ObservableCollection<Wallpaper> items=new();
    readonly string settingsPath=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","settings.json");
    Preferences preferences=new();
    readonly Dictionary<string,DesktopHost> hosts=new();
    DesktopHost? host => hosts.GetValueOrDefault(SelectedScreen.DeviceName);
    Forms.NotifyIcon? tray;
    bool exiting;
    bool fullscreen;
    Rect compactBounds;
    string screenLayout="";
    string category="Все сцены";
    Rect launchArea;
    public MainWindow()
    {
        Trace("construct");
        InitializeComponent();
        WindowStartupLocation=WindowStartupLocation.Manual;
        SourceInitialized+=(_,_)=>PlaceInitialWindow();
        Trace("xaml");
        HeroVideo.MediaEnded+=(_,_)=>{HeroVideo.Position=TimeSpan.Zero;HeroVideo.Play();};
        HeroVideo.MediaFailed+=(_,_)=>{StopPreview();Status.Text="Формат превью не поддерживается Windows. Попробуйте применить обои через VLC.";};
        try { if (File.Exists(settingsPath)) preferences=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(settingsPath))??new(); }
        catch (Exception e) { Status.Text="Настройки сброшены: "+e.Message; }
        Core.Initialize(); StaticBackdrop.RecoverAll();
        Trace("vlc");
        string assets=System.IO.Path.Combine(AppContext.BaseDirectory,"assets");
        var presets=JsonSerializer.Deserialize<List<PresetDefinition>>(File.ReadAllText(System.IO.Path.Combine(assets,"presets.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        foreach(var preset in presets)items.Add(new(preset.Name,System.IO.Path.Combine(assets,$"{preset.Id}.mp4"),System.IO.Path.Combine(assets,$"{preset.Id}.jpg"),preset.Category+" · 4K · 60 FPS",preset.Category,preset.Description,preset.Id));
        foreach(var preset in presets)items.Add(new(preset.Name+" · Живая сцена",System.IO.Path.Combine(assets,$"{preset.Id}.mp4"),System.IO.Path.Combine(assets,$"scene-{preset.Id}.png"),preset.Category+" · ЖИВАЯ СЦЕНА",preset.Category,SpatialScene.Descriptions[preset.Id],preset.Id,true));
        LoadExpansion(assets);
        foreach(string path in preferences.Imports)items.Add(new(preferences.ImportNames.GetValueOrDefault(path)??System.IO.Path.GetFileNameWithoutExtension(path),path,preferences.ImportThumbnails.GetValueOrDefault(path),"LOCAL VIDEO",IsSpatial:preferences.Layers.GetValueOrDefault(path)?.MotionId is not null));
        Library.ItemsSource=items;System.Windows.Data.CollectionViewSource.GetDefaultView(items).Filter=FilterScene; Library.SelectedIndex=0;CountLabel.Text=$"{items.Count} сцен";
        RefreshScreens(); Volume.Value=preferences.Volume; if(host is not null)host.Volume=preferences.Volume; Autostart.IsChecked=preferences.Autostart;
        var menu=new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть VWP",null,(_,_)=>Dispatcher.Invoke(()=>{Show();Activate();}));
        menu.Items.Add("Пауза / продолжить",null,(_,_)=>Dispatcher.Invoke(()=>PauseClick(this,new())));
        menu.Items.Add("Остановить",null,(_,_)=>Dispatcher.Invoke(()=>StopClick(this,new())));
        menu.Items.Add("Выход",null,(_,_)=>Dispatcher.Invoke(()=>{exiting=true;Close();}));
        tray=new Forms.NotifyIcon { Icon=new System.Drawing.Icon(System.IO.Path.Combine(assets,"app.ico")),Text="VWP — видеообои",Visible=true,ContextMenuStrip=menu };
        tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(()=>{Show();Activate();});
        InitializeFeatures();
        InitializeStudio();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        Loaded+=(_,_)=> {
            Trace("loaded "+string.Join(" ",Environment.GetCommandLineArgs()));
            if(!Environment.GetCommandLineArgs().Any(a=>a.StartsWith("--verify",StringComparison.Ordinal)))Restore();
            if(Environment.GetCommandLineArgs().Contains("--autostart"))Hide();
            if(Environment.GetCommandLineArgs().Contains("--verify")) Verify();
            if(Environment.GetCommandLineArgs().Contains("--verify-ui")) VerifyUi();
            if(preferences.Autostart && !Environment.GetCommandLineArgs().Any(a=>a.StartsWith("--verify",StringComparison.Ordinal)))AutostartClick(this,new());
        };
    }
    static string ScreenLayout()=>string.Join(";",Forms.Screen.AllScreens.Select(s=>$"{s.DeviceName}:{s.Bounds}:{s.Primary}"));
    void PlaceInitialWindow()
    {
        var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var screen=Forms.Screen.FromHandle(handle);uint dpi=GetDpiForWindow(handle);double scale=dpi>0?dpi/96.0:System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX;var work=screen.WorkingArea;
        var area=new Rect(work.Left/scale,work.Top/scale,work.Width/scale,work.Height/scale);
        launchArea=area;
        MinWidth=Math.Min(980,area.Width-24);MinHeight=Math.Min(640,area.Height-24);
        Width=Math.Min(1160,area.Width-24);Height=Math.Min(760,area.Height-24);
        Left=area.Left+(area.Width-Width)/2;Top=area.Top+(area.Height-Height)/2;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    void RefreshScreens() {screenLayout=ScreenLayout();string? selected=Monitor.SelectedItem as string;Monitor.Items.Clear();int i=0;foreach(var screen in Forms.Screen.AllScreens)Monitor.Items.Add((screen.Primary?"Основной":"Дисплей "+(++i))+$" · {screen.Bounds.Width} × {screen.Bounds.Height}");Monitor.SelectedIndex=0;}
    void DisplayChanged(object? sender,EventArgs e)=>Dispatcher.BeginInvoke(new Action(()=>{if(ScreenLayout()==screenLayout)return;foreach(var key in applyGeneration.Keys.ToArray())applyGeneration[key]++;foreach(var player in hosts.Values)player.Dispose();hosts.Clear();RefreshScreens();Restore();Status.Text="Обои восстановлены после смены дисплеев";}));
    void SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(Library.SelectedItem is not Wallpaper item)return;
        StopPreview();StopHover();
        HeroTitle.Text=item.Name;
        HeroDescription.Text=item.Description;UpdateSceneActions();
        HeroImage.Source=item.Thumbnail is not null && File.Exists(item.Thumbnail)?new BitmapImage(new Uri(item.Thumbnail)):null;
        UpdateTheme();
    }
    void RoundHero(object sender,SizeChangedEventArgs e) { Hero.Clip=new System.Windows.Media.RectangleGeometry(new Rect(0,0,Hero.ActualWidth,Hero.ActualHeight),22,22); }
    void RoundCard(object sender,SizeChangedEventArgs e) {var element=(FrameworkElement)sender;element.Clip=new System.Windows.Media.RectangleGeometry(new Rect(0,0,element.ActualWidth,element.ActualHeight),8,8);}
    void ImportClick(object sender,RoutedEventArgs e)
    {
        var dialog=new Microsoft.Win32.OpenFileDialog { Multiselect=true,Filter="Видео|*.mp4;*.webm;*.mkv;*.mov;*.avi" };
        if(dialog.ShowDialog()==true)ImportFiles(dialog.FileNames);
    }
    void ApplyClick(object sender,RoutedEventArgs e)
    {
        if(Library.SelectedItem is Wallpaper item)ApplyScene(item,SelectedScreen);
    }
    void Restore()
    {
        foreach(var screen in Forms.Screen.AllScreens)
            if(preferences.Monitors.TryGetValue(screen.DeviceName,out var config) && FindScene(config.Scene) is Wallpaper scene)ApplyScene(scene,screen);
        LoadMonitorControls();
    }
    bool FilterScene(object value) {var item=(Wallpaper)value;return (sceneFormat=="Все форматы" || sceneFormat=="3D" == item.IsSpatial) && MatchesCollection(item) && item.Name.Contains(SearchBox.Text,StringComparison.OrdinalIgnoreCase);}
    void RefreshFilter() {if(Library.ItemsSource is null)return;var view=System.Windows.Data.CollectionViewSource.GetDefaultView(items);view.Refresh();CountLabel.Text=$"{view.Cast<Wallpaper>().Count()} / {items.Count} сцен";}
    void SearchChanged(object sender,TextChangedEventArgs e) {if(Library is not null)RefreshFilter();}
    void CategoryClick(object sender,RoutedEventArgs e) {category=(string)((Button)sender).Tag;foreach(Button button in SidebarCategories.Children)button.Background=button.Tag as string==category?System.Windows.Media.Brushes.White:System.Windows.Media.Brushes.Transparent;RefreshFilter();}
    void PauseClick(object sender,RoutedEventArgs e) {var config=Config(SelectedScreen);config.UserPaused=!config.UserPaused;host?.SetUserPaused(config.UserPaused);Save();UpdatePlaybackStatus();}
    void StopClick(object sender,RoutedEventArgs e) {applyGeneration[SelectedScreen.DeviceName]=applyGeneration.GetValueOrDefault(SelectedScreen.DeviceName)+1;host?.Stop();var config=Config(SelectedScreen);config.Scene=null;config.PlaylistEnabled=false;LoadMonitorControls();Save();Status.Text="Обои остановлены";PauseButton.Content="Ⅱ  Пауза";}
    void QuitClick(object sender,RoutedEventArgs e) { exiting=true;Close(); }
    void TrayClick(object sender,RoutedEventArgs e) { StopPreview();StopHover();Hide();tray?.ShowBalloonTip(1500,"VWP","Лаунчер в трее. Обои продолжают работать.",Forms.ToolTipIcon.Info); }
    void ModeClick(object sender,RoutedEventArgs e)
    {
        if(!fullscreen)
        {
            compactBounds=new Rect(Left,Top,ActualWidth,ActualHeight);
            var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var bounds=Forms.Screen.FromHandle(handle).Bounds;
            var transform=System.Windows.PresentationSource.FromVisual(this)!.CompositionTarget!.TransformFromDevice;
            ResizeMode=ResizeMode.NoResize;WindowState=WindowState.Normal;
            Left=bounds.X*transform.M11;Top=bounds.Y*transform.M22;
            Width=bounds.Width*transform.M11;Height=bounds.Height*transform.M22;
            fullscreen=true;ModeLabel.Text="VWP · Полноэкранный режим · Esc для выхода";
        }
        else
        {
            fullscreen=false;ResizeMode=ResizeMode.CanResize;
            Left=compactBounds.Left;Top=compactBounds.Top;Width=compactBounds.Width;Height=compactBounds.Height;
            ModeLabel.Text="VWP · Компактный режим";
        }
    }
    void WindowKeyDown(object sender,System.Windows.Input.KeyEventArgs e) {if(e.Key==System.Windows.Input.Key.Escape && fullscreen){ModeClick(this,new());e.Handled=true;}}
    void VolumeChanged(object sender,RoutedPropertyChangedEventArgs<double> e) {if(!featuresReady || loadingControls)return;var config=Config(SelectedScreen);config.Volume=(int)e.NewValue;if(host is not null)host.Volume=config.Volume;Save();}
    void PreviewClick(object sender,RoutedEventArgs e)
    {
        StopHover();
        if(Library.SelectedItem is not Wallpaper item || !File.Exists(item.Path))return;
        if(HeroVideo.Visibility==Visibility.Visible || scenePreview is not null){StopPreview();return;}
        if((item.IsSpatial || Config(SelectedScreen).SceneAnimation) && LayersFor(item) is SceneLayers layers && layers.MotionId is not null)
        {
            scenePreview=new InteractiveVisual(layers,PlaybackConfig(item,Config(SelectedScreen)),SelectedScreen,audio);
            HeroMotion.Children.Add(scenePreview);HeroMotion.Visibility=Visibility.Visible;
            PreviewButton.Content="□  Остановить превью";return;
        }
        HeroVideo.Source=new Uri(PreviewPath(item));HeroVideo.Visibility=Visibility.Visible;HeroVideo.Play();PreviewButton.Content="□  Остановить превью";
    }
    InteractiveVisual? scenePreview;
    void StopPreview() {scenePreview?.Dispose();scenePreview=null;HeroMotion.Children.Clear();HeroMotion.Visibility=Visibility.Collapsed;HeroVideo.Stop();HeroVideo.Source=null;HeroVideo.Visibility=Visibility.Collapsed;PreviewButton.Content="▷  Смотреть превью";}
    void AutostartClick(object sender,RoutedEventArgs e)
    {
        bool enabled=Autostart.IsChecked==true;
        string command=$"\"{Environment.ProcessPath}\" --autostart";
        if(string.Equals(System.IO.Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))command=$"\"{Environment.ProcessPath}\" \"{typeof(MainWindow).Assembly.Location}\" --autostart";
        try { using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(enabled)key.SetValue("VWP",command);else key.DeleteValue("VWP",false);preferences.Autostart=enabled;Save(); }
        catch(Exception ex){Autostart.IsChecked=preferences.Autostart;Status.Text="Автозапуск: "+ex.Message;}
    }
    void Save(){if(Environment.GetCommandLineArgs().Any(a=>a.StartsWith("--verify")))return;try{Directory.CreateDirectory(System.IO.Path.GetDirectoryName(settingsPath)!);string temp=settingsPath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(preferences));File.Move(temp,settingsPath,true);}catch(Exception e){Status.Text="Настройки не сохранены: "+e.Message;}}
    async void Verify()
    {
        preferences=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences))!;preferences.PauseBattery=false;preferences.PauseFullscreen=false;foreach(var config in preferences.Monitors.Values)config.UserPaused=false;
        Trace("verify");
        string folder=System.IO.Path.Combine(AppContext.BaseDirectory,"verification");Directory.CreateDirectory(folder);
        await System.Threading.Tasks.Task.Delay(700);
        var visual=(FrameworkElement)Content;
        var bitmap=new RenderTargetBitmap((int)visual.ActualWidth,(int)visual.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(var stream=File.Create(System.IO.Path.Combine(folder,"launcher.png")))encoder.Save(stream);
        var report=new List<object>();
        double compactWidth=ActualWidth,compactHeight=ActualHeight;
        ModeClick(this,new());await System.Threading.Tasks.Task.Delay(300);
        double fullWidth=ActualWidth,fullHeight=ActualHeight;
        var fullVisual=(FrameworkElement)Content;
        var fullBitmap=new RenderTargetBitmap((int)fullVisual.ActualWidth,(int)fullVisual.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
        fullBitmap.Render(fullVisual);var fullEncoder=new PngBitmapEncoder();fullEncoder.Frames.Add(BitmapFrame.Create(fullBitmap));
        using(var stream=File.Create(System.IO.Path.Combine(folder,"fullscreen.png")))fullEncoder.Save(stream);
        ModeClick(this,new());await System.Threading.Tasks.Task.Delay(300);
        bool compactRestored=Math.Abs(ActualWidth-compactWidth)<2 && Math.Abs(ActualHeight-compactHeight)<2;
        TrayClick(this,new());bool trayHidden=!IsVisible;Show();
        bool startupOnscreen=Top>=launchArea.Top && Left>=launchArea.Left && Top+ActualHeight<=launchArea.Bottom+1 && Left+ActualWidth<=launchArea.Right+1;
        report.Add(new{kind="window-modes",compactWidth,compactHeight,fullWidth,fullHeight,compactRestored,trayHidden,startupOnscreen,left=Left,top=Top});
        SearchBox.Text="Ocean";int searchCount=System.Windows.Data.CollectionViewSource.GetDefaultView(items).Cast<Wallpaper>().Count();SearchBox.Text="";
        CategoryClick(SidebarCategories.Children.OfType<Button>().First(b=>b.Tag as string=="Космос"),new());int spaceCount=System.Windows.Data.CollectionViewSource.GetDefaultView(items).Cast<Wallpaper>().Count();
        CategoryClick(SidebarCategories.Children.OfType<Button>().First(b=>b.Tag as string=="Все сцены"),new());
        report.Add(new{kind="library",count=items.Count,searchCount,spaceCount});
        foreach(var item in items.Where(x=>x.PresetId is 0 or 1 or 2 or 3 or 7 or 16))
        {
            try {
                GetHost(Forms.Screen.PrimaryScreen!);string? original=host!.StaticPicture(Forms.Screen.PrimaryScreen!);
                var player=GetHost(Forms.Screen.PrimaryScreen!);player.Play(item.Path,Forms.Screen.PrimaryScreen!,item.Thumbnail);player.Volume=0;
                for(int retry=0;retry<12 && !host.Playing;retry++)await System.Threading.Tasks.Task.Delay(500);
                await System.Threading.Tasks.Task.Delay(7200);
                bool playing=host.Playing;long time=host.Position;
                host.TogglePause();await System.Threading.Tasks.Task.Delay(300);bool paused=host.Paused && !host.Playing;
                host.TogglePause();await System.Threading.Tasks.Task.Delay(300);
                bool resumed=host.Playing;bool healthy=host.Healthy;bool active=host.Active;bool backdropApplied=host.BackdropApplied;
                bool matchingPicture=string.Equals(host.StaticPicture(Forms.Screen.PrimaryScreen!),item.Thumbnail,StringComparison.OrdinalIgnoreCase);string? backdropError=host.BackdropError;
                host.Stop();bool originalRestored=string.Equals(original,host.StaticPicture(Forms.Screen.PrimaryScreen!),StringComparison.OrdinalIgnoreCase);
                report.Add(new {item.Name,playing,time,paused,resumed,healthy,active,backdrop=backdropApplied,matchingPicture,originalRestored,backdropError,status=Status.Text});
            } catch(Exception e){report.Add(new {item.Name,error=e.Message});host?.Stop();}
        }
        File.WriteAllText(System.IO.Path.Combine(folder,"result.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        QuitClick(this,new());
    }
    static void Trace(string message) {if(Environment.GetCommandLineArgs().Contains("--verify"))File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"verify-stage.log"),message+Environment.NewLine);}
    async void VerifyUi()
    {
        await System.Threading.Tasks.Task.Delay(700);
        string folder=System.IO.Path.Combine(AppContext.BaseDirectory,"verification");Directory.CreateDirectory(folder);
        var visual=(FrameworkElement)Content;
        var image=new RenderTargetBitmap((int)visual.ActualWidth,(int)visual.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
        image.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using(var stream=File.Create(System.IO.Path.Combine(folder,"launcher.png")))encoder.Save(stream);
        Library.SelectedItem=items.First(item=>item.PresetId is not null && !item.IsSpatial);
        PreviewClick(this,new());await System.Threading.Tasks.Task.Delay(2500);
        bool originalPreview=HeroVideo.Visibility==Visibility.Visible && HeroVideo.NaturalVideoWidth>0;
        StopPreview();Library.SelectedItem=items.First(item=>item.IsSpatial);
        PreviewClick(this,new());await System.Threading.Tasks.Task.Delay(1500);
        bool startupOnscreen=Top>=launchArea.Top && Left>=launchArea.Left && Top+ActualHeight<=launchArea.Bottom+1 && Left+ActualWidth<=launchArea.Right+1;
        File.WriteAllText(System.IO.Path.Combine(folder,"ui-result.json"),JsonSerializer.Serialize(new {originalPreview,originalPresets=items.Count(item=>item.PresetId is not null && !item.IsSpatial),spatialPresets=items.Count(item=>item.IsSpatial),uniqueKeys=items.Select(PlaybackRules.Key).Distinct().Count()==items.Count,independentSelection=items.Where(item=>item.PresetId is not null).All(item=>PlaybackConfig(item,new MonitorPreferences{SceneAnimation=false}).SceneAnimation==item.IsSpatial),spatialPreview=scenePreview?.Spatial is not null || scenePreview?.CinematicActive==true,spatialSeconds=scenePreview?.Spatial?.Time??scenePreview?.AnimationSeconds,spatialFrames=scenePreview?.RenderedFrames,spatialCovers=items.Count(item=>item.PresetId is not null && item.IsSpatial && item.Thumbnail is not null),startupOnscreen,left=Left,top=Top}));
        QuitClick(this,new());
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if(!exiting){e.Cancel=true;TrayClick(this,new());return;}
        StopPreview();StopHover();featureTimer.Stop();CloseStudio();SystemEvents.PowerModeChanged-=PowerChanged;SystemEvents.SessionSwitch-=SessionChanged;SystemEvents.DisplaySettingsChanged-=DisplayChanged;foreach(var player in hosts.Values)player.Dispose();tray?.Dispose();base.OnClosing(e);
    }
}
