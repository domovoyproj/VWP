using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace VWP;
public partial class MainWindow
{
    const int CityId=900,CityHotkey=701;
    readonly StarAccess starAccess=new();
    CityWindow? cityWindow;
    bool cityChecking;
    DateTime cityChecked=DateTime.MinValue;
    string CityCover=>Path.Combine(AppContext.BaseDirectory,"assets","city","cover.png");
    string CityProfile=>City.Quality.SelectedIndex switch{0=>"Eco",1=>"Balance",_=>"Quality"};
    void InitializeCity()
    {
        City.RepositoryButton.Click+=(_,_)=>OpenCityLink("https://github.com/domovoyproj/VWP");
        City.TokenHelpButton.Click+=(_,_)=>OpenCityLink("https://github.com/settings/personal-access-tokens/new");
        City.CheckButton.Click+=async(_,_)=>await CheckCityAccess(City.Token.Password);
        City.DisconnectButton.Click+=(_,_)=>{starAccess.Disconnect();StopLockedCities();UpdateCityAccess();};
        City.PreviewButton.Click+=async(_,_)=>await ExploreCity(false);
        City.ExploreButton.Click+=async(_,_)=>await ExploreCity(true);
        City.ApplyButton.Click+=(_,_)=>{var scene=items.First(s=>s.PresetId==CityId);Config(SelectedScreen).Performance=CityProfile;ApplyScene(scene,SelectedScreen);};
        if(File.Exists(CityCover))City.Cover.Source=new BitmapImage(new Uri(CityCover));
        SourceInitialized+=(_,_)=>{var handle=new WindowInteropHelper(this).Handle;HwndSource.FromHwnd(handle)?.AddHook(CityMessage);if(!RegisterHotKey(handle,CityHotkey,0x4003,0x4E))City.ControlsHint.Text="Горячая клавиша занята. Используй кнопку «Войти в свои обои».";};
        Closed+=(_,_)=>{UnregisterHotKey(new WindowInteropHelper(this).Handle,CityHotkey);cityWindow?.Close();starAccess.Dispose();};
        UpdateCityAccess();
    }
    static void OpenCityLink(string link)=>Process.Start(new ProcessStartInfo(link){UseShellExecute=true});
    IntPtr CityMessage(IntPtr hwnd,int message,IntPtr wp,IntPtr lp,ref bool handled){if(message==0x0312&&wp.ToInt32()==CityHotkey){handled=true;_ = ExploreCity(true);}return IntPtr.Zero;}
    void CityNavigation(object sender,RoutedEventArgs e)
    {
        StopPreview();StopHover();WallpaperPage.Visibility=Visibility.Collapsed;Appearance.Visibility=Visibility.Collapsed;City.Visibility=Visibility.Visible;
        SidebarCategories.Visibility=Visibility.Collapsed;SidebarImport.Visibility=Visibility.Collapsed;SidebarNote.Visibility=Visibility.Collapsed;
        foreach(var button in new[]{WallpaperNav,ThemesNav,CursorsNav})button.Background=Brushes.Transparent;CityNav.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty,"SelectedBrush");
        UpdateCityAccess();
    }
    async Task<bool> CheckCityAccess(string? token=null)
    {
        if(cityChecking)return starAccess.Unlocked;cityChecking=true;City.CheckButton.IsEnabled=false;City.Status.Text="Проверяю аккаунт и звезду…";
        try{bool result=await starAccess.CheckAsync(string.IsNullOrWhiteSpace(token)?null:token);cityChecked=DateTime.UtcNow;UpdateCityAccess();if(!result)StopLockedCities();return result;}
        catch{City.Status.Text="Не удалось проверить GitHub. Повтори попытку.";return false;}
        finally{City.Token.Clear();cityChecking=false;City.CheckButton.IsEnabled=true;}
    }
    void UpdateCityAccess(){City.ApplyButton.IsEnabled=starAccess.Unlocked;City.AccessLabel.Text=starAccess.Unlocked?"★ Открыто · @"+starAccess.Login:"★ Открывается за звезду";City.Status.Text=starAccess.Status;}
    void StopLockedCities(){foreach(var pair in hosts)if(pair.Value.IsCity){pair.Value.Stop();Status.Text="Эксклюзивный город заблокирован: подтверди звезду GitHub.";}}
    async Task ExploreCity(bool wallpaper)
    {
        if(cityWindow is not null){cityWindow.Activate();return;}
        if(wallpaper&&(host?.IsCity!=true||!starAccess.Unlocked)){CityNavigation(this,new());City.Status.Text="Сначала открой город и примени его к рабочему столу.";return;}
        var desktop=wallpaper?host:null;string? state=desktop is null?null:await desktop.CityState();
        bool wasPaused=desktop?.Paused==true;desktop?.SetUserPaused(true);
        cityWindow=new CityWindow(CityProfile,state,City.Palette.SelectedIndex,wallpaper){Owner=wallpaper?null:this};
        var window=cityWindow;window.Closed+=(_,_)=>{if(desktop?.IsCity==true){desktop.RestoreCityState(window.LastState);desktop.SetUserPaused(wasPaused);}cityWindow=null;};window.Show();
    }
    void TickCity()
    {
        if(hosts.Values.Any(h=>h.IsCity)&&!cityChecking&&DateTime.UtcNow-cityChecked>TimeSpan.FromMinutes(15))_ = CheckCityAccess();
        if(!starAccess.Unlocked&&hosts.Values.Any(h=>h.IsCity))StopLockedCities();
    }
    [DllImport("user32.dll")]static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr hwnd,int id);
}
