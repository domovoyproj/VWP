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
public record Wallpaper(string Name,string Path,string? Thumbnail,string Subtitle);
public sealed class Preferences
{
    public List<string> Imports { get; set; }=new();
    public string? Last { get; set; }
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
    public MainWindow()
    {
        Trace("construct");
        InitializeComponent();
        Trace("xaml");
        HeroVideo.MediaEnded+=(_,_)=>{HeroVideo.Position=TimeSpan.Zero;HeroVideo.Play();};
        HeroVideo.MediaFailed+=(_,_)=>{StopPreview();Status.Text="Формат превью не поддерживается Windows. Попробуйте применить обои через VLC.";};
        try { if (File.Exists(settingsPath)) preferences=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(settingsPath))??new(); }
        catch (Exception e) { Status.Text="Настройки сброшены: "+e.Message; }
        Core.Initialize(); host=new DesktopHost();
        Trace("vlc");
        host.Failed += ()=>Dispatcher.BeginInvoke(new Action(()=> { Status.Text="Ошибка воспроизведения. Проверьте файл или выберите другое видео."; }));
        host.LoopRequired += ()=>Dispatcher.BeginInvoke(new Action(()=>host.Replay()));
        string assets=System.IO.Path.Combine(AppContext.BaseDirectory,"assets");
        string[] names={"Sakura Midnight","Neon Tokyo","Crimson Moon"};
        for(int i=0;i<3;i++)items.Add(new(names[i],System.IO.Path.Combine(assets,$"{i}.mp4"),System.IO.Path.Combine(assets,$"{i}.jpg"),"ORIGINAL · 6 SEC · LOOP"));
        foreach(string path in preferences.Imports)items.Add(new(System.IO.Path.GetFileNameWithoutExtension(path),path,null,"LOCAL VIDEO"));
        Library.ItemsSource=items; Library.SelectedIndex=0; CountLabel.Text=$"{items.Count} сцен";
        RefreshScreens(); Volume.Value=preferences.Volume; host.Volume=preferences.Volume; Autostart.IsChecked=preferences.Autostart;
        var menu=new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть VWP",null,(_,_)=>Dispatcher.Invoke(()=>{Show();Activate();}));
        menu.Items.Add("Пауза / продолжить",null,(_,_)=>Dispatcher.Invoke(()=>PauseClick(this,new())));
        menu.Items.Add("Остановить",null,(_,_)=>Dispatcher.Invoke(()=>StopClick(this,new())));
        menu.Items.Add("Выход",null,(_,_)=>Dispatcher.Invoke(()=>{exiting=true;Close();}));
        tray=new Forms.NotifyIcon { Icon=System.Drawing.SystemIcons.Application,Text="VWP — видеообои",Visible=true,ContextMenuStrip=menu };
        tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(()=>{Show();Activate();});
        var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(3) };
        timer.Tick+=(_,_)=> { if(host is {Healthy:false}) { host.Stop(); Status.Text="Explorer перезапущен. Примените обои снова."; } };timer.Start();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        Loaded+=(_,_)=> {
            Trace("loaded "+string.Join(" ",Environment.GetCommandLineArgs()));
            Height=Math.Min(760,SystemParameters.WorkArea.Height-24);
            Width=Math.Min(1160,SystemParameters.WorkArea.Width-24);
            if(Environment.GetCommandLineArgs().Contains("--autostart")) { Restore(); Hide(); }
            if(Environment.GetCommandLineArgs().Contains("--verify")) Verify();
            if(Environment.GetCommandLineArgs().Contains("--verify-ui")) VerifyUi();
        };
    }
    static string ScreenLayout()=>string.Join(";",Forms.Screen.AllScreens.Select(s=>$"{s.DeviceName}:{s.Bounds}:{s.Primary}"));
    void RefreshScreens() {screenLayout=ScreenLayout();Monitor.Items.Clear(); int i=0;foreach(var s in Forms.Screen.AllScreens)Monitor.Items.Add((s.Primary?"Основной":"Дисплей "+(++i))+$" · {s.Bounds.Width} × {s.Bounds.Height}"); Monitor.SelectedIndex=0; }
    void DisplayChanged(object? sender,EventArgs e)=>Dispatcher.BeginInvoke(new Action(()=>{if(ScreenLayout()==screenLayout)return;host?.Stop();RefreshScreens();Status.Text="Дисплеи изменились. Примените обои снова.";}));
    void SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(Library.SelectedItem is not Wallpaper item)return;
        StopPreview();
        HeroTitle.Text=item.Name;
        HeroImage.Source=item.Thumbnail is not null && File.Exists(item.Thumbnail)?new BitmapImage(new Uri(item.Thumbnail)):null;
    }
    void RoundHero(object sender,SizeChangedEventArgs e) { Hero.Clip=new System.Windows.Media.RectangleGeometry(new Rect(0,0,Hero.ActualWidth,Hero.ActualHeight),22,22); }
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
        try { host.Play(item.Path,Forms.Screen.AllScreens[Math.Max(0,Monitor.SelectedIndex)]); host.Volume=(int)Volume.Value;
            preferences.Last=item.Path; preferences.Monitor=Forms.Screen.AllScreens[Math.Max(0,Monitor.SelectedIndex)].DeviceName;Save();Status.Text="Сейчас на рабочем столе · "+item.Name;PauseButton.Content="Ⅱ  Пауза"; }
        catch(Exception ex){host.Stop();Status.Text="Не удалось применить: "+ex.Message;}
    }
    void Restore() { var item=items.FirstOrDefault(x=>x.Path==preferences.Last); if(item is null)return; Library.SelectedItem=item;var screens=Forms.Screen.AllScreens;int index=Array.FindIndex(screens,s=>s.DeviceName==preferences.Monitor);Monitor.SelectedIndex=Math.Max(0,index);ApplyClick(this,new()); }
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
        try { using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(enabled)key.SetValue("VWP",$"\"{Environment.ProcessPath}\" --autostart");else key.DeleteValue("VWP",false);preferences.Autostart=enabled;Save(); }
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
        report.Add(new{kind="window-modes",compactWidth,compactHeight,fullWidth,fullHeight,compactRestored,trayHidden});
        foreach(var item in items.Take(3))
        {
            try {
                host!.Play(item.Path,Forms.Screen.PrimaryScreen!);host.Volume=0;
                for(int retry=0;retry<12 && !host.Playing;retry++)await System.Threading.Tasks.Task.Delay(500);
                await System.Threading.Tasks.Task.Delay(7200);
                bool playing=host.Playing;long time=host.Position;
                host.TogglePause();await System.Threading.Tasks.Task.Delay(300);bool paused=host.Paused;
                host.TogglePause();await System.Threading.Tasks.Task.Delay(300);
                report.Add(new {item.Name,playing,time,paused,healthy=host.Healthy,active=host.Active,status=Status.Text});host.Stop();
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
        File.WriteAllText(System.IO.Path.Combine(folder,"ui-result.json"),JsonSerializer.Serialize(new {previewWidth=HeroVideo.NaturalVideoWidth,previewPosition=HeroVideo.Position.TotalMilliseconds,previewVisible=HeroVideo.Visibility==Visibility.Visible}));
        QuitClick(this,new());
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if(!exiting){e.Cancel=true;TrayClick(this,new());return;}
        StopPreview();SystemEvents.DisplaySettingsChanged-=DisplayChanged;host?.Dispose();tray?.Dispose();base.OnClosing(e);
    }
}
