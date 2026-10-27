using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace VWP;
public partial class StudioEditor : Window
{
    static readonly string[] motions={"still","flow","foliage","cloth","character","rain"};
    static readonly string[] anchors={"top","bottom","center"};
    static readonly string[] channels={"water","clouds","wind","light"};
    StudioProject working;
    StudioSurface? surface;
    bool loading=true,ready,dirty,busy,scrubbing,paused,allowClose,canvasFocused;
    double baseWidth=1,baseHeight=1;
    double playbackTime;
    readonly DispatcherTimer clock=new(){Interval=TimeSpan.FromMilliseconds(500)};
    public StudioProject? Result {get;private set;}
    public StudioEditor(StudioProject project)
    {
        working=project.CloneTo(Path.Combine(StudioProject.DataRoot,Guid.NewGuid().ToString("N")));
        InitializeComponent();StudioWindowChrome.Apply(this);SceneName.Text=working.Name;Timeline.Maximum=working.Duration;
        SceneMotion.Value=working.Settings.Motion;Wind.Value=working.Settings.Wind;Parallax.Value=working.Settings.Parallax;Exposure.Value=working.Settings.Exposure;Bloom.Value=working.Settings.Bloom;Clock.IsChecked=working.Settings.Clock;Weather.SelectedIndex=Array.IndexOf(new[]{"none","snow","dust","fireflies"},working.Weather.Kind);ParticleCount.Value=working.Weather.Count;
        MaskChannel.SelectedIndex=0;RefreshLayers(0);loading=false;
        SceneName.PreviewMouseDown+=(_,_)=>canvasFocused=false;LayerName.PreviewMouseDown+=(_,_)=>canvasFocused=false;
        Loaded+=async(_,_)=>await StartSurface();
        clock.Tick+=async(_,_)=>
        {
            if(!ready||surface is null||busy)return;
            try{var stats=await surface.ReadStatistics();if(!scrubbing){loading=true;Timeline.Value=stats.Time%working.Duration;loading=false;}TimeLabel.Text=$"{Timeline.Value:00.0} / {working.Duration:0} с";Statistics.Text=$"{stats.Width} × {stats.Height}   {stats.Fps:0} FPS   GPU {stats.TextureMb+stats.TargetMb:0} МБ";}catch{}
        };
        Activated+=(_,_)=>{if(ready)surface?.SetPaused(paused);};Deactivated+=(_,_)=>surface?.SetPaused(true);
        Closing+=(_,e)=>{if(busy&&!allowClose){e.Cancel=true;return;}if(dirty&&!allowClose&&MessageBox.Show(this,"Закрыть сцену без сохранения изменений?","Scene Studio",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes){e.Cancel=true;return;}clock.Stop();surface?.Dispose();surface=null;if(Result is null)StudioProject.DiscardWorkingCopy(working.Folder);};
    }
    async Task StartSurface()
    {
        ready=false;Workspace.IsEnabled=false;SaveButton.IsEnabled=false;surface?.Dispose();surface=new StudioSurface();Viewport.Child=surface.Control;StageSized(this,null!);
        surface.Changed+=()=>dirty=true;
        // WebView2 raises accelerator keys on its WinForms control before DOM/WPF routing.
        // Defer script calls: the browser is synchronously blocked in this callback.
        surface.Control.KeyDown+=(_,e)=>
        {
            if(!ready||!e.Control||e.KeyCode!=System.Windows.Forms.Keys.Z)return;
            bool redo=e.Shift;e.Handled=true;e.SuppressKeyPress=true;
            Dispatcher.BeginInvoke(new Action(()=>{if(redo)RedoClick(this,new());else UndoClick(this,new());}));
        };
        surface.Message+=json=>{using var data=JsonDocument.Parse(json);string? type=data.RootElement.GetProperty("type").GetString();if(type=="error"){ready=false;Workspace.IsEnabled=true;SaveButton.IsEnabled=false;Status.Text=data.RootElement.GetProperty("message").GetString();}if(type=="recovering"){ready=false;Workspace.IsEnabled=false;SaveButton.IsEnabled=false;Status.Text="Восстанавливаю графику…";}if(type=="restored"){ready=true;Workspace.IsEnabled=true;SaveButton.IsEnabled=true;Status.Text="Сцена восстановлена";_=SetBrush();}if(type=="focuscanvas"){canvasFocused=true;Keyboard.Focus(Viewport);surface?.Control.Focus();}if(type=="togglepause")PauseClick(this,new());if(type=="escape"){Painting.IsChecked=false;_=SetBrush();}};
        try{await surface.Initialize(working,"Balance",true);await surface.Seek(playbackTime);ready=true;Workspace.IsEnabled=true;SaveButton.IsEnabled=true;surface.SetPaused(paused);clock.Start();Status.Text="Готово · изменения видны сразу";await SetBrush();}
        catch(Exception error){Status.Text="Не удалось открыть сцену: "+error.Message;Workspace.IsEnabled=true;SaveButton.IsEnabled=false;}
    }
    void StageSized(object sender,SizeChangedEventArgs e){if(Stage is null||Viewport is null)return;double width=Math.Max(2,Math.Min(Stage.ActualWidth,Stage.ActualHeight*16/9));Viewport.Width=width;Viewport.Height=width*9/16;}
    void RefreshLayers(int index){LayerList.ItemsSource=null;LayerList.ItemsSource=working.Layers;LayerList.SelectedIndex=Math.Clamp(index,0,working.Layers.Count-1);}
    StudioLayer? Selected=>LayerList?.SelectedItem as StudioLayer;
    void LayerSelected(object sender,SelectionChangedEventArgs e)
    {
        if(Selected is not StudioLayer layer)return;
        bool previous=loading;loading=true;LayerName.Text=layer.Name;LayerVisible.IsChecked=layer.Visible;Motion.SelectedIndex=Array.IndexOf(motions,layer.Motion);Anchor.SelectedIndex=Array.IndexOf(anchors,layer.Anchor);Strength.Value=layer.Strength;Depth.Value=layer.Depth;LayerOpacity.Value=layer.Opacity;LayerX.Value=layer.Rect[0];LayerY.Value=layer.Rect[1];baseWidth=layer.Rect[2];baseHeight=layer.Rect[3];LayerScale.Value=1;Waterline.Value=layer.Waterline;SunX.Value=layer.Sun[0];loading=previous;
        if(!loading)_=SetBrush();
    }
    void NameChanged(object sender,TextChangedEventArgs e){if(loading)return;working.Name=SceneName.Text;dirty=true;}
    async void LayerNameChanged(object sender,TextChangedEventArgs e){if(loading||Selected is null)return;Selected.Name=LayerName.Text;dirty=true;LayerList.Items.Refresh();await PushLayer();}
    async void PropertiesChanged(object sender,RoutedEventArgs e)
    {
        if(loading||Selected is not StudioLayer layer)return;
        layer.Visible=LayerVisible.IsChecked==true;layer.Motion=motions[Math.Max(0,Motion.SelectedIndex)];layer.Anchor=anchors[Math.Max(0,Anchor.SelectedIndex)];layer.Strength=Strength.Value;layer.Depth=Depth.Value;layer.Opacity=LayerOpacity.Value;layer.Rect[0]=LayerX.Value;layer.Rect[1]=LayerY.Value;layer.Waterline=Waterline.Value;layer.Sun[0]=SunX.Value;dirty=true;await PushLayer();
    }
    async void ScaleChanged(object sender,RoutedPropertyChangedEventArgs<double> e){if(loading||Selected is not StudioLayer layer)return;double scale=Math.Min(e.NewValue,Math.Min(4/baseWidth,4/baseHeight));layer.Rect[2]=baseWidth*scale;layer.Rect[3]=baseHeight*scale;dirty=true;await PushLayer();}
    async Task PushLayer(){if(!ready||surface is null||Selected is null)return;try{await surface.Layer(LayerList.SelectedIndex,Selected);}catch(Exception error){Status.Text=error.Message;}}
    void SettingsChanged(object sender,RoutedEventArgs e)
    {
        if(loading)return;var settings=working.Settings;settings.Motion=SceneMotion.Value;settings.Wind=Wind.Value;settings.Parallax=Parallax.Value;settings.Exposure=Exposure.Value;settings.Bloom=Bloom.Value;settings.Clock=Clock.IsChecked==true;dirty=true;if(ready)surface?.Configure("Balance",settings);
    }
    async void WeatherChanged(object sender,RoutedEventArgs e)
    {
        if(loading)return;working.Weather.Kind=new[]{"none","snow","dust","fireflies"}[Math.Max(0,Weather.SelectedIndex)];
        if(ReferenceEquals(sender,Weather)){if(working.Weather.Kind!="none"&&ParticleCount.Value<1){loading=true;ParticleCount.Value=80;loading=false;}working.Weather.Color=working.Weather.Kind switch{"fireflies"=>"#D7F6AA","dust"=>"#ECD8B6",_=>"#DFEFFF"};}
        working.Weather.Count=(int)ParticleCount.Value;dirty=true;
        if(ready&&surface is not null)try{await surface.Execute("window.studio.weather("+JsonSerializer.Serialize(working.Weather,StudioProject.Json)+")");}catch(Exception error){Status.Text=error.Message;}
    }
    async void BrushChanged(object sender,RoutedEventArgs e){if(!loading)await SetBrush();}
    async Task SetBrush(){if(!ready||surface is null)return;try{if(Painting.IsChecked==true&&Selected is not null){await surface.Tool(LayerList.SelectedIndex,Math.Max(0,MaskChannel.SelectedIndex),BrushRadius.Value,Erase.IsChecked==true);ViewLabel.Text="Маска · "+Selected.Name;}else{await surface.StopTool();ViewLabel.Text="Живой просмотр";}}catch(Exception error){Status.Text=error.Message;}}
    async void UndoClick(object sender,RoutedEventArgs e){if(ready&&surface is not null)await surface.Execute("window.studio.undo()");}
    async void RedoClick(object sender,RoutedEventArgs e){if(ready&&surface is not null)await surface.Execute("window.studio.redo()");}
    void PauseClick(object sender,RoutedEventArgs e){paused=!paused;surface?.SetPaused(paused);PauseButton.Content=paused?"▷ Продолжить":"Ⅱ Пауза";}
    void TimelineDown(object sender,MouseButtonEventArgs e){scrubbing=true;surface?.SetPaused(true);}
    void TimelineUp(object sender,MouseButtonEventArgs e){scrubbing=false;surface?.SetPaused(paused);}
    async void TimelineChanged(object sender,RoutedPropertyChangedEventArgs<double> e){if(!loading&&ready&&surface is not null){try{await surface.Seek(e.NewValue);}catch{}}}
    async Task PersistMasks()
    {
        if(!ready||surface is null)return;
        using var data=JsonDocument.Parse(await surface.Execute("window.studio.masks()"));int index=0;
        foreach(var mask in data.RootElement.EnumerateArray())
        {
            foreach(string channel in channels){string name=$"mask-{index}-{channel}.png";await File.WriteAllBytesAsync(Path.Combine(working.Folder,name),Convert.FromBase64String(mask.GetProperty(channel).GetString()!));working.Layers[index].MaskFiles[channel]=name;}
            working.Layers[index++].Masks.Clear();
        }
        working.Save(working.FileName);
    }
    async Task Rebuild(Action change,int selection)
    {
        if(busy)return;busy=true;StudioProject? before=null;int oldSelection=LayerList.SelectedIndex;
        try{if(ready&&surface is not null)playbackTime=(await surface.ReadStatistics()).Time;await PersistMasks();before=working.Copy();change();working.Save(working.FileName);RefreshLayers(selection);dirty=true;await StartSurface();}
        catch(Exception error){if(before is not null){working.Layers=before.Layers;RefreshLayers(oldSelection);}Status.Text=error.Message;}finally{busy=false;}
    }
    async void AddClick(object sender,RoutedEventArgs e)
    {
        if(working.Layers.Count>=12){Status.Text="В сцене может быть до 12 слоёв.";return;}
        var dialog=new OpenFileDialog{Filter="Изображение|*.png;*.jpg;*.jpeg"};if(dialog.ShowDialog(this)!=true)return;
        string name="layer-"+Guid.NewGuid().ToString("N")+Path.GetExtension(dialog.FileName);int selection=working.Layers.Count;
        await Rebuild(()=>{var rect=StudioProject.ImageRect(dialog.FileName,false);File.Copy(dialog.FileName,Path.Combine(working.Folder,name));working.Layers.Add(new(){Name=Path.GetFileNameWithoutExtension(dialog.FileName),Source=name,Depth=.5,Rect=rect});},selection);
    }
    async void RemoveClick(object sender,RoutedEventArgs e){int index=LayerList.SelectedIndex;if(index<=0){Status.Text="Нижний слой фона должен остаться в сцене.";return;}await Rebuild(()=>working.Layers.RemoveAt(index),index-1);}
    async void UpClick(object sender,RoutedEventArgs e){int index=LayerList.SelectedIndex;if(index<1||index>=working.Layers.Count-1)return;await Rebuild(()=>(working.Layers[index],working.Layers[index+1])=(working.Layers[index+1],working.Layers[index]),index+1);}
    async void DownClick(object sender,RoutedEventArgs e){int index=LayerList.SelectedIndex;if(index<2)return;await Rebuild(()=>(working.Layers[index],working.Layers[index-1])=(working.Layers[index-1],working.Layers[index]),index-1);}
    async void SaveClick(object sender,RoutedEventArgs e)
    {
        if(busy||!ready||surface is null)return;busy=true;SaveButton.IsEnabled=false;
        try
        {
            if(string.IsNullOrWhiteSpace(working.Name))throw new InvalidDataException("Укажи название сцены.");
            await PersistMasks();await surface.StopTool();await surface.Execute("window.studio.settings({clock:false});window.studio.pause(true)");await surface.SetExportViewport(1920,1080);await Task.Delay(150);
            string cover="poster.png";await surface.CaptureExport(Path.Combine(working.Folder,cover),1920,1080);working.Cover=cover;working.Save(working.FileName);Result=working;dirty=false;allowClose=true;DialogResult=true;
        }
        catch(Exception error){Status.Text="Сохранение: "+error.Message;if(surface is not null){await surface.ResetExportViewport();surface.Configure("Balance",working.Settings);surface.SetPaused(paused);await SetBrush();}}
        finally{busy=false;SaveButton.IsEnabled=true;}
    }
    void CancelClick(object sender,RoutedEventArgs e)=>Close();
    void EditorKeyDown(object sender,KeyEventArgs e)
    {
        if(!ready)return;
        if(e.Key==Key.Tab)canvasFocused=false;
        if(Keyboard.FocusedElement is TextBox&&!canvasFocused)return;
        if(e.Key==Key.Space){PauseClick(sender,e);e.Handled=true;}
        if(e.Key==Key.Escape){Painting.IsChecked=false;_=SetBrush();e.Handled=true;}
        if(e.Key==Key.Z&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){if(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))RedoClick(sender,e);else UndoClick(sender,e);e.Handled=true;}
    }
}
