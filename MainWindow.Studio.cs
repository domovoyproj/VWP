using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
namespace VWP;
public partial class MainWindow
{
    readonly AudioReaction audio=new();
    readonly Dictionary<int,PresetDefinition> definitions=new();
    readonly Dictionary<string,int> applyGeneration=new();
    List<GalleryItem> catalog=new();
    MiniPlayer? mini;
    bool studioReady,miniLoading;
    TimeSpan cpuSample;
    DateTime cpuTime=DateTime.UtcNow;
    MonitorPreferences? bundleSettings;
    bool galleryBusy;
    void InitializeStudio()
    {
        foreach(var preset in JsonSerializer.Deserialize<List<PresetDefinition>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"assets","presets.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!)definitions[preset.Id]=preset;
        studioReady=true;UpdateTheme();
        tray!.MouseClick+=(_,e)=>{if(e.Button==Forms.MouseButtons.Left)Dispatcher.Invoke(ShowMini);};
        Editor.CloseButton.Click+=(_,_)=>{Editor.Close();EditorOverlay.Visibility=Visibility.Collapsed;};
        Editor.Saved+=(path,name)=>AddLocalScene(path,name,Path.ChangeExtension(path,".jpg"));
        Gallery.CloseButton.Click+=(_,_)=>GalleryOverlay.Visibility=Visibility.Collapsed;
        Gallery.RefreshButton.Click+=async(_,_)=>await RefreshGallery();
        Gallery.Search.TextChanged+=(_,_)=>FilterGallery();Gallery.Category.SelectionChanged+=(_,_)=>FilterGallery();Gallery.Category.SelectedIndex=0;
        Gallery.DownloadButton.Click+=async(_,_)=>await DownloadCollection();
        Gallery.AuthorButton.Click+=(_,_)=>{if(Gallery.Entries.SelectedItem is GalleryItem selected){Gallery.Search.Text=selected.Author;Gallery.Status.Text="Коллекции автора "+selected.Author;if(Uri.TryCreate(selected.Profile,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="github.com")Process.Start(new ProcessStartInfo(uri.ToString()){UseShellExecute=true});}};
        Gallery.PublishButton.Click+=(_,_)=>{Publish.Status.Text="";Publish.Rights.IsChecked=false;PublishOverlay.Visibility=Visibility.Visible;};
        Publish.CloseButton.Click+=(_,_)=>{if(!galleryBusy){Publish.Token.Clear();PublishOverlay.Visibility=Visibility.Collapsed;}};
        Publish.PublishButton.Click+=async(_,_)=>await PublishCollection();
        Settings.BackgroundButton.Click+=(_,_)=>ChooseLayer(false);Settings.ForegroundButton.Click+=(_,_)=>ChooseLayer(true);
        SystemEvents.UserPreferenceChanged+=ThemeChanged;
        if(Environment.GetCommandLineArgs().Contains("--verify-studio"))Loaded+=async(_,_)=>await VerifyStudio();
    }
    void ThemeChanged(object sender,UserPreferenceChangedEventArgs e)=>Dispatcher.BeginInvoke(new Action(UpdateTheme));
    void UpdateTheme()
    {
        if(!studioReady)return;string accent="#9B89D1";
        if(preferences.SceneAccent && Library.SelectedItem is Wallpaper scene)accent=LayersFor(scene)?.Accent??accent;
        ThemeService.Apply(preferences.Theme,accent);
    }
    SceneLayers? LayersFor(Wallpaper scene)
    {
        if(preferences.Layers.TryGetValue(PlaybackRules.Key(scene),out var custom))return custom;
        if(scene.Thumbnail is null)return null;
        var preset=scene.PresetId is int id?definitions.GetValueOrDefault(id):null;
        if(scene.PresetId==0)return new(){Background=Path.Combine(AppContext.BaseDirectory,"assets","layers","sakura-background.png"),Foreground=Path.Combine(AppContext.BaseDirectory,"assets","layers","sakura-character.png"),Accent="#F877B8",Effect="Petals"};
        return new(){Background=scene.Thumbnail,Accent=preset?.Accent??"#A698EE",Effect=preset?.Motion switch{"rain"=>"Rain","petals"=>"Petals","embers"=>"Embers",_=>"Stars"}};
    }
    void LoadStudioSettings()
    {
        var config=Config(SelectedScreen);Settings.Interactive.IsChecked=config.Interactive;Settings.Music.IsChecked=config.MusicReactive;Settings.Depth.Value=config.Depth;
        Settings.Performance.SelectedIndex=config.Performance switch{"Eco"=>0,"Quality"=>2,_=>1};
        Settings.Theme.SelectedIndex=preferences.Theme switch{"Light"=>0,"Dark"=>1,_=>2};Settings.SceneAccent.IsChecked=preferences.SceneAccent;
        Settings.Exceptions.Text=string.Join(", ",preferences.PauseExceptions);Settings.GallerySource.Text=preferences.GallerySource;
        Settings.LayersLabel.Text=Library.SelectedItem is Wallpaper scene && LayersFor(scene)?.Foreground is not null?"Фон + отдельный персонаж":"Фон + частицы. Можно добавить PNG персонажа.";
        UpdateResources();
    }
    void SaveStudioSettings()
    {
        var config=Config(SelectedScreen);config.Interactive=Settings.Interactive.IsChecked==true;config.MusicReactive=Settings.Music.IsChecked==true;config.Depth=Settings.Depth.Value;
        config.Performance=Settings.Performance.SelectedIndex switch{0=>"Eco",2=>"Quality",_=>"Balance"};
        preferences.Theme=Settings.Theme.SelectedIndex switch{0=>"Light",1=>"Dark",_=>"System"};preferences.SceneAccent=Settings.SceneAccent.IsChecked==true;
        preferences.PauseExceptions=Settings.Exceptions.Text.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(Path.GetFileNameWithoutExtension).Where(s=>!string.IsNullOrEmpty(s)).Select(s=>s!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        preferences.GallerySource=string.IsNullOrWhiteSpace(Settings.GallerySource.Text)?GalleryService.DefaultSource:Settings.GallerySource.Text.Trim();UpdateTheme();
    }
    void ChooseLayer(bool foreground)
    {
        var scene=FindScene(Config(SelectedScreen).Scene)??Library.SelectedItem as Wallpaper;if(scene is null)return;
        var dialog=new OpenFileDialog{Filter=foreground?"PNG с прозрачностью|*.png":"Изображения|*.png;*.jpg;*.jpeg;*.webp"};if(dialog.ShowDialog()!=true)return;
        string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","layers");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,Guid.NewGuid().ToString("N")+Path.GetExtension(dialog.FileName));File.Copy(dialog.FileName,file);
        var layers=LayersFor(scene)??new();if(foreground)layers.Foreground=file;else layers.Background=file;preferences.Layers[PlaybackRules.Key(scene)]=layers;Settings.LayersLabel.Text=layers.Foreground is not null?"Фон + отдельный персонаж":"Фон + частицы";Save();
    }
    void UpdateResources()
    {
        using var process=Process.GetCurrentProcess();DateTime now=DateTime.UtcNow;
        double cpu=(process.TotalProcessorTime-cpuSample).TotalMilliseconds/Math.Max(1,(now-cpuTime).TotalMilliseconds)/Environment.ProcessorCount*100;
        cpuSample=process.TotalProcessorTime;cpuTime=now;
        Settings.ResourcesLabel.Text=$"VWP · CPU {cpu:0.0}% · RAM {process.WorkingSet64/1048576.0:0} МБ · профиль {PerformanceProfile.Resolve(Config(SelectedScreen).Performance).Name}";
    }
    void TickStudio()
    {
        if(!studioReady)return;audio.SetEnabled(hosts.Any(pair=>pair.Value.Active&&!pair.Value.Paused&&pair.Value.Interactive is not null && preferences.Monitors.TryGetValue(pair.Key,out var config)&&config.MusicReactive));
        if(SettingsOverlay.Visibility==Visibility.Visible)UpdateResources();
        if(mini?.IsVisible==true)UpdateMini();
    }
    void EditorClick(object sender,RoutedEventArgs e){if(Library.SelectedItem is not Wallpaper scene)return;StopPreview();StopHover();Editor.Open(scene);EditorOverlay.Visibility=Visibility.Visible;}
    void ShowMini()
    {
        if(mini is null)
        {
            mini=new MiniPlayer();mini.OpenButton.Click+=(_,_)=>{mini.Hide();Show();Activate();};mini.Previous.Click+=(_,_)=>Advance(SelectedScreen,-1);mini.Next.Click+=(_,_)=>Advance(SelectedScreen,1);mini.Pause.Click+=(_,_)=>PauseClick(this,new());
            mini.Display.SelectionChanged+=(_,_)=>{if(!miniLoading)Monitor.SelectedIndex=mini.Display.SelectedIndex;};mini.Sound.ValueChanged+=(_,e)=>{if(!miniLoading)Volume.Value=e.NewValue;};
        }
        miniLoading=true;mini.Display.ItemsSource=Monitor.Items.Cast<object>().ToArray();mini.Display.SelectedIndex=Monitor.SelectedIndex;miniLoading=false;UpdateMini();
        var work=Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;var monitor=MonitorFromPoint(Forms.Cursor.Position,2);GetDpiForMonitor(monitor,0,out uint dpi,out _);double scale=(dpi>0?dpi:96)/96.0;
        mini.Show();var handle=new System.Windows.Interop.WindowInteropHelper(mini).Handle;PositionMini(handle,IntPtr.Zero,work.Right-(int)(mini.Width*scale)-16,work.Bottom-(int)(mini.Height*scale)-12,(int)(mini.Width*scale),(int)(mini.Height*scale),0x14);mini.Activate();
    }
    void UpdateMini()
    {
        if(mini is null)return;var config=Config(SelectedScreen);var scene=FindScene(config.Scene);miniLoading=true;
        mini.TitleLabel.Text=scene?.Name??"Обои остановлены";mini.Cover.Source=scene?.Thumbnail is string cover && File.Exists(cover)?new BitmapImage(new Uri(cover)):null;mini.Sound.Value=config.Volume;mini.Pause.Content=host?.Paused==true?"▶ Играть":"Ⅱ Пауза";miniLoading=false;
    }
    void AddLocalScene(string path,string name,string? cover=null,SceneLayers? layers=null)
    {
        if(items.Any(s=>s.Path==path))return;preferences.Imports.Add(path);preferences.ImportNames[path]=name;
        if(cover is not null)preferences.ImportThumbnails[path]=cover;if(layers is not null)preferences.Layers[path]=layers;
        var scene=new Wallpaper(name,path,cover,"LOCAL COLLECTION");items.Add(scene);Library.SelectedItem=scene;Save();RefreshFilter();
    }
    List<Wallpaper> ExportScenes()
    {
        var config=Config(SelectedScreen);var scenes=config.Playlist.Select(FindScene).Where(s=>s is not null).Select(s=>s!).ToList();
        if(FindScene(config.Scene) is Wallpaper active)scenes.Add(active);if(scenes.Count==0 && Library.SelectedItem is Wallpaper selected)scenes.Add(selected);
        return scenes.DistinctBy(PlaybackRules.Key).ToList();
    }
    async void ExportBundleClick(object sender,RoutedEventArgs e)
    {
        var dialog=new SaveFileDialog{Filter="Набор VWP|*.vwpbundle",FileName=Config(SelectedScreen).PlaylistName+".vwpbundle"};if(dialog.ShowDialog()!=true)return;
        try{Status.Text="Собираю набор…";await BundleService.Export(dialog.FileName,Config(SelectedScreen).PlaylistName,"VWP user",ExportScenes(),Config(SelectedScreen),LayersFor);Status.Text="Набор сохранён: "+dialog.FileName;}
        catch(Exception error){Status.Text="Экспорт: "+error.Message;}
    }
    async void ImportBundleClick(object sender,RoutedEventArgs e){var dialog=new OpenFileDialog{Filter="Набор VWP|*.vwpbundle"};if(dialog.ShowDialog()==true)try{await ImportCollection(dialog.FileName);}catch{}}
    async Task ImportCollection(string path)
    {
        string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","bundles",Guid.NewGuid().ToString("N"));
        try
        {
            var bundle=await BundleService.Import(path,folder);var mapping=new Dictionary<string,string>();
            foreach(var scene in bundle.Scenes){AddLocalScene(scene.Video,scene.Name,scene.Cover,scene.Layers);mapping[scene.Id]=scene.Video;}
            bundle.Settings.Scene=bundle.Settings.Scene is not null?mapping.GetValueOrDefault(bundle.Settings.Scene):null;bundle.Settings.Playlist=bundle.Settings.Playlist.Where(mapping.ContainsKey).Select(k=>mapping[k]).ToList();
            bundleSettings=bundle.Settings;BundleApplyButton.Visibility=Visibility.Visible;Status.Text="Импортирован набор «"+bundle.Name+"». Настройки можно применить отдельно.";
        }
        catch(Exception e){Status.Text="Набор: "+e.Message;throw;}
    }
    void ApplyBundleClick(object sender,RoutedEventArgs e){if(bundleSettings is null)return;preferences.Monitors[SelectedScreen.DeviceName]=bundleSettings;bundleSettings=null;BundleApplyButton.Visibility=Visibility.Collapsed;Save();Restore();}
    async void GalleryClick(object sender,RoutedEventArgs e){StopHover();StopPreview();GalleryOverlay.Visibility=Visibility.Visible;await RefreshGallery();}
    async Task RefreshGallery()
    {
        if(galleryBusy)return;galleryBusy=true;Gallery.RefreshButton.IsEnabled=false;Gallery.Status.Text="Загружаю каталог…";
        try{catalog=await GalleryService.Catalog(preferences.GallerySource);FilterGallery();Gallery.Status.Text=catalog.Count+" коллекций · выберите автора или скачайте набор";}
        catch(Exception e){Gallery.Status.Text="Каталог недоступен: "+e.Message;}
        finally{galleryBusy=false;Gallery.RefreshButton.IsEnabled=true;}
    }
    void FilterGallery(){if(!studioReady)return;string category=(Gallery.Category.SelectedItem as ComboBoxItem)?.Content as string??"Все категории";Gallery.Entries.ItemsSource=catalog.Where(i=>(category=="Все категории"||i.Category==category)&&(i.Name+" "+i.Author).Contains(Gallery.Search.Text,StringComparison.OrdinalIgnoreCase)).ToList();}
    async Task DownloadCollection()
    {
        if(galleryBusy||Gallery.Entries.SelectedItem is not GalleryItem selected)return;galleryBusy=true;Gallery.DownloadButton.IsEnabled=false;
        try{var path=await GalleryService.Download(selected,new Progress<double>(value=>Gallery.Status.Text=$"Скачивание · {value:0}%"));await ImportCollection(path);Gallery.Status.Text="Коллекция добавлена в библиотеку.";}
        catch(Exception e){Gallery.Status.Text="Загрузка: "+e.Message;}
        finally{galleryBusy=false;Gallery.DownloadButton.IsEnabled=true;}
    }
    async Task PublishCollection()
    {
        if(galleryBusy)return;if(Publish.Rights.IsChecked!=true||string.IsNullOrWhiteSpace(Publish.Token.Password)){Publish.Status.Text="Укажите токен и подтвердите право публикации.";return;}
        galleryBusy=true;Publish.PublishButton.IsEnabled=false;string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","exports");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,Guid.NewGuid().ToString("N")+".vwpbundle");
        try{await BundleService.Export(file,Publish.NameField.Text,"VWP user",ExportScenes(),Config(SelectedScreen),LayersFor);string source=await GalleryService.Publish(file,Publish.Repository.Text.Trim(),Publish.Token.Password,Library.SelectedItem is Wallpaper selected?selected.Category:"Импорт",new Progress<string>(message=>Publish.Status.Text=message));preferences.GallerySource=source;Save();Publish.Status.Text="Набор опубликован. Галерея переключена на каталог вашего репозитория.";}
        catch(Exception e){Publish.Status.Text="Публикация: "+e.Message;}
        finally{Publish.Token.Clear();galleryBusy=false;Publish.PublishButton.IsEnabled=true;}
    }
    void CloseStudio(){SystemEvents.UserPreferenceChanged-=ThemeChanged;Editor.Close();mini?.Close();audio.Dispose();}
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(System.Drawing.Point point,uint flags);
    [System.Runtime.InteropServices.DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SetWindowPos")] static extern bool PositionMini(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
}
