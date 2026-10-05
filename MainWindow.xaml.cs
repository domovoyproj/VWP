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
public record Wallpaper(string Name,string Path,string? Thumbnail,string Subtitle,string Category="Импорт",string Description="Твоё видео",int? PresetId=null);
public record PresetDefinition(int Id,string Name,string Category,string Description,string Accent,string Motion);
public sealed class Preferences
{
    public List<string> Imports { get; set; }=new();
    public string? Last { get; set; }
    public int? LastPresetId {get;set;}
    public string? Monitor { get; set; }
    public int Volume { get; set; }
    public bool Autostart { get; set; }
}
public partial class MainWindow : Window
{
    readonly ObservableCollection<Wallpaper> items=new();
    readonly string settingsPath=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","settings.json");
    Preferences preferences=new();
    DesktopHost? host;
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
        Core.Initialize(); host=new DesktopHost();
        Trace("vlc");
        host.Failed += ()=>Dispatcher.BeginInvoke(new Action(()=> { Status.Text="Ошибка воспроизведения. Проверьте файл или выберите другое видео."; }));
        host.LoopRequired += ()=>Dispatcher.BeginInvoke(new Action(()=>host.Replay()));
        host.SurfaceReady += ()=>Dispatcher.BeginInvoke(new Action(()=>host.CompleteRecovery()));
        string assets=System.IO.Path.Combine(AppContext.BaseDirectory,"assets");
        var presets=JsonSerializer.Deserialize<List<PresetDefinition>>(File.ReadAllText(System.IO.Path.Combine(assets,"presets.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        foreach(var preset in presets)items.Add(new(preset.Name,System.IO.Path.Combine(assets,$"{preset.Id}.mp4"),System.IO.Path.Combine(assets,$"{preset.Id}.jpg"),preset.Category+" · 6 SEC · LOOP",preset.Category,preset.Description,preset.Id));
        foreach(string path in preferences.Imports)items.Add(new(System.IO.Path.GetFileNameWithoutExtension(path),path,null,"LOCAL VIDEO"));
        Library.ItemsSource=items;System.Windows.Data.CollectionViewSource.GetDefaultView(items).Filter=FilterScene; Library.SelectedIndex=0;CountLabel.Text=$"{items.Count} сцен";
        RefreshScreens(); Volume.Value=preferences.Volume; host.Volume=preferences.Volume; Autostart.IsChecked=preferences.Autostart;
        var menu=new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть VWP",null,(_,_)=>Dispatcher.Invoke(()=>{Show();Activate();}));
        menu.Items.Add("Пауза / продолжить",null,(_,_)=>Dispatcher.Invoke(()=>PauseClick(this,new())));
        menu.Items.Add("Остановить",null,(_,_)=>Dispatcher.Invoke(()=>StopClick(this,new())));
        menu.Items.Add("Выход",null,(_,_)=>Dispatcher.Invoke(()=>{exiting=true;Close();}));
        tray=new Forms.NotifyIcon { Icon=new System.Drawing.Icon(System.IO.Path.Combine(assets,"app.ico")),Text="VWP — видеообои",Visible=true,ContextMenuStrip=menu };
        tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(()=>{Show();Activate();});
        var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(3) };
        timer.Tick+=(_,_)=> {try{if(host is {Healthy:false}){host.Recover();Status.Text="Обои восстановлены";}else host?.UpdateBackdrop();}catch(Exception e){Status.Text="Ожидание Explorer: "+e.Message;}};timer.Start();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        Loaded+=(_,_)=> {
            Trace("loaded "+string.Join(" ",Environment.GetCommandLineArgs()));
            if(Environment.GetCommandLineArgs().Contains("--autostart")) { Restore(); Hide(); }
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
    void RefreshScreens() {screenLayout=ScreenLayout();Monitor.Items.Clear(); int i=0;foreach(var s in Forms.Screen.AllScreens)Monitor.Items.Add((s.Primary?"Основной":"Дисплей "+(++i))+$" · {s.Bounds.Width} × {s.Bounds.Height}"); Monitor.SelectedIndex=0; }
    void DisplayChanged(object? sender,EventArgs e)=>Dispatcher.BeginInvoke(new Action(()=>{if(ScreenLayout()==screenLayout)return;host?.Stop();RefreshScreens();Status.Text="Дисплеи изменились. Примените обои снова.";}));
    void SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(Library.SelectedItem is not Wallpaper item)return;
        StopPreview();
        HeroTitle.Text=item.Name;
        HeroDescription.Text=item.Description;
        HeroImage.Source=item.Thumbnail is not null && File.Exists(item.Thumbnail)?new BitmapImage(new Uri(item.Thumbnail)):null;
    }
    void RoundHero(object sender,SizeChangedEventArgs e) { Hero.Clip=new System.Windows.Media.RectangleGeometry(new Rect(0,0,Hero.ActualWidth,Hero.ActualHeight),22,22); }
    void RoundCard(object sender,SizeChangedEventArgs e) {var element=(FrameworkElement)sender;element.Clip=new System.Windows.Media.RectangleGeometry(new Rect(0,0,element.ActualWidth,element.ActualHeight),8,8);}
    void ImportClick(object sender,RoutedEventArgs e)
    {
        var dialog=new Microsoft.Win32.OpenFileDialog { Multiselect=true,Filter="Видео|*.mp4;*.webm;*.mkv;*.mov;*.avi" };
        if(dialog.ShowDialog()!=true)return;
        foreach(string path in dialog.FileNames)if(!items.Any(x=>x.Path==path)){items.Add(new(System.IO.Path.GetFileNameWithoutExtension(path),path,null,"LOCAL VIDEO"));preferences.Imports.Add(path);}
        CountLabel.Text=$"{items.Count} сцен";Library.SelectedIndex=items.Count-1;Save();
    }
    void ApplyClick(object sender,RoutedEventArgs e)
    {
        if(Library.SelectedItem is not Wallpaper item || host is null)return;
        if(!File.Exists(item.Path)){Status.Text="Файл не найден. Импортируйте видео заново.";return;}
        try { host.Play(item.Path,Forms.Screen.AllScreens[Math.Max(0,Monitor.SelectedIndex)],item.Thumbnail); host.Volume=(int)Volume.Value;
            preferences.Last=item.Path;preferences.LastPresetId=item.PresetId; preferences.Monitor=Forms.Screen.AllScreens[Math.Max(0,Monitor.SelectedIndex)].DeviceName;Save();Status.Text="Сейчас на рабочем столе · "+item.Name+(host.BackdropError is null?"":" · Подложка: "+host.BackdropError);PauseButton.Content="Ⅱ  Пауза"; }
        catch(Exception ex){host.Stop();Status.Text="Не удалось применить: "+ex.Message;}
    }
    void Restore()
    {
        var item=items.FirstOrDefault(x=>x.Path==preferences.Last);
        int? presetId=preferences.LastPresetId;
        if(presetId is null && preferences.Last is not null && System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(preferences.Last))=="assets" && int.TryParse(System.IO.Path.GetFileNameWithoutExtension(preferences.Last),out int legacy))presetId=legacy;
        item??=items.FirstOrDefault(x=>x.PresetId is not null && x.PresetId==presetId);
        if(item is null)return;
        Library.SelectedItem=item;var screens=Forms.Screen.AllScreens;int index=Array.FindIndex(screens,s=>s.DeviceName==preferences.Monitor);Monitor.SelectedIndex=Math.Max(0,index);ApplyClick(this,new());
    }
    bool FilterScene(object value) {var item=(Wallpaper)value;return (category=="Все сцены" || item.Category==category) && item.Name.Contains(SearchBox.Text,StringComparison.OrdinalIgnoreCase);}
    void RefreshFilter() {if(Library.ItemsSource is null)return;var view=System.Windows.Data.CollectionViewSource.GetDefaultView(items);view.Refresh();CountLabel.Text=$"{view.Cast<Wallpaper>().Count()} / {items.Count} сцен";}
    void SearchChanged(object sender,TextChangedEventArgs e) {if(Library is not null)RefreshFilter();}
    void CategoryClick(object sender,RoutedEventArgs e) {category=(string)((Button)sender).Tag;foreach(Button button in SidebarCategories.Children)button.Background=button.Tag as string==category?System.Windows.Media.Brushes.White:System.Windows.Media.Brushes.Transparent;RefreshFilter();}
    void PauseClick(object sender,RoutedEventArgs e) { host?.TogglePause();PauseButton.Content=host?.Paused==true?"▶  Играть":"Ⅱ  Пауза"; }
    void StopClick(object sender,RoutedEventArgs e) {host?.Stop();Status.Text="Обои остановлены";PauseButton.Content="Ⅱ  Пауза";}
    void QuitClick(object sender,RoutedEventArgs e) { exiting=true;Close(); }
    void TrayClick(object sender,RoutedEventArgs e) { StopPreview();Hide();tray?.ShowBalloonTip(1500,"VWP","Лаунчер в трее. Обои продолжают работать.",Forms.ToolTipIcon.Info); }
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
    void VolumeChanged(object sender,RoutedPropertyChangedEventArgs<double> e) {if(host is null)return;host.Volume=(int)e.NewValue;preferences.Volume=(int)e.NewValue;Save();}
    void PreviewClick(object sender,RoutedEventArgs e)
    {
        if(Library.SelectedItem is not Wallpaper item || !File.Exists(item.Path))return;
        if(HeroVideo.Visibility==Visibility.Visible){StopPreview();return;}
        HeroVideo.Source=new Uri(item.Path);HeroVideo.Visibility=Visibility.Visible;HeroVideo.Play();PreviewButton.Content="□  Остановить превью";
    }
    void StopPreview() {HeroVideo.Stop();HeroVideo.Source=null;HeroVideo.Visibility=Visibility.Collapsed;PreviewButton.Content="▷  Смотреть превью";}
    void AutostartClick(object sender,RoutedEventArgs e)
    {
        bool enabled=Autostart.IsChecked==true;
        string command=$"\"{Environment.ProcessPath}\" --autostart";
        if(string.Equals(System.IO.Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))command=$"\"{Environment.ProcessPath}\" \"{typeof(MainWindow).Assembly.Location}\" --autostart";
        try { using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(enabled)key.SetValue("VWP",command);else key.DeleteValue("VWP",false);preferences.Autostart=enabled;Save(); }
        catch(Exception ex){Autostart.IsChecked=preferences.Autostart;Status.Text="Автозапуск: "+ex.Message;}
    }
    void Save(){try{Directory.CreateDirectory(System.IO.Path.GetDirectoryName(settingsPath)!);string temp=settingsPath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(preferences));File.Move(temp,settingsPath,true);}catch(Exception e){Status.Text="Настройки не сохранены: "+e.Message;}}
    async void Verify()
    {
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
                string? original=host!.StaticPicture(Forms.Screen.PrimaryScreen!);
                host!.Play(item.Path,Forms.Screen.PrimaryScreen!,item.Thumbnail);host.Volume=0;
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
        PreviewClick(this,new());await System.Threading.Tasks.Task.Delay(2500);
        bool startupOnscreen=Top>=launchArea.Top && Left>=launchArea.Left && Top+ActualHeight<=launchArea.Bottom+1 && Left+ActualWidth<=launchArea.Right+1;
        File.WriteAllText(System.IO.Path.Combine(folder,"ui-result.json"),JsonSerializer.Serialize(new {previewWidth=HeroVideo.NaturalVideoWidth,previewPosition=HeroVideo.Position.TotalMilliseconds,previewVisible=HeroVideo.Visibility==Visibility.Visible,startupOnscreen,left=Left,top=Top}));
        QuitClick(this,new());
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if(!exiting){e.Cancel=true;TrayClick(this,new());return;}
        StopPreview();SystemEvents.DisplaySettingsChanged-=DisplayChanged;host?.Dispose();tray?.Dispose();base.OnClosing(e);
    }
}
