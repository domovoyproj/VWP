using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
namespace VWP;
public partial class SceneDesigner : Window
{
    public static readonly Dictionary<string,string> Effects=new(){["None"]="Без частиц",["Snow"]="Снег",["Rain"]="Дождь",["Petals"]="Лепестки",["Leaves"]="Листья",["Embers"]="Искры",["Stars"]="Звёзды",["Fireflies"]="Светлячки",["Mist"]="Туман",["Lightning"]="Электрические разряды",["Water"]="Рябь воды"};
    public SceneLayers Result {get;private set;}
    public string SceneTitle=>SceneName.Text.Trim();
    readonly AudioReaction audio=new();
    InteractiveVisual? preview;
    bool loading,paused;
    public SceneDesigner(string title,SceneLayers layers)
    {
        InitializeComponent();Result=layers.Copy();Result.Realtime=true;Result.MotionId=null;Result.Video=null;
        SceneName.Text=title;EffectPicker.DisplayMemberPath="";EffectPicker.SelectedValuePath="Tag";
        foreach(var effect in Effects)EffectPicker.Items.Add(new ComboBoxItem{Content=effect.Value,Tag=effect.Key});
        LoadControls();
        foreach(var slider in new[]{SpeedSlider,DensitySlider,SizeSlider,WindSlider,ParallaxSlider,TopSlider,BottomSlider})slider.ValueChanged+=(_,_)=>UpdateProperties();
        EffectPicker.SelectionChanged+=(_,_)=>UpdateProperties();AccentBox.TextChanged+=(_,_)=>UpdateProperties();AudioCheck.Click+=(_,_)=>UpdateProperties();ClockCheck.Click+=(_,_)=>UpdateProperties();
        QualityPicker.SelectionChanged+=(_,_)=>RebuildPreview();
        BackgroundButton.Click+=(_,_)=>ChooseImage(false);ForegroundButton.Click+=(_,_)=>ChooseImage(true);
        RemoveForeground.Click+=(_,_)=>{Result.Foreground=null;RebuildPreview();};
        CloseEditor.Click+=(_,_)=>Close();
        PausePreview.Click+=(_,_)=>{paused=!paused;preview?.SetPaused(paused);PausePreview.Content=paused?"▶ Продолжить":"Ⅱ Пауза";audio.SetEnabled(!paused&&Result.AudioReactive);};
        ResetButton.Click+=(_,_)=>{Result=new(){Background=Result.Background,Foreground=Result.Foreground,Realtime=true,Effect="Snow"};LoadControls();RebuildPreview();};
        SaveScene.Click+=(_,_)=>{if(UpdateProperties()&&!string.IsNullOrWhiteSpace(SceneTitle))DialogResult=true;else EditorStatus.Text="Проверь название, цвет и область эффекта.";};
        Loaded+=(_,_)=>RebuildPreview();
        Deactivated+=(_,_)=>{preview?.SetPaused(true);audio.SetEnabled(false);};
        Activated+=(_,_)=>{preview?.SetPaused(paused);audio.SetEnabled(!paused&&Result.AudioReactive);};
        Closed+=(_,_)=>{preview?.Dispose();audio.Dispose();};
    }
    void LoadControls()
    {
        loading=true;EffectPicker.SelectedValue=Effects.ContainsKey(Result.Effect)?Result.Effect:"Snow";AccentBox.Text=Result.Accent;
        SpeedSlider.Value=Result.Speed;DensitySlider.Value=Result.Density;SizeSlider.Value=Result.ParticleSize;WindSlider.Value=Result.Wind;ParallaxSlider.Value=Result.Parallax;TopSlider.Value=Result.RegionTop;BottomSlider.Value=Result.RegionBottom;AudioCheck.IsChecked=Result.AudioReactive;ClockCheck.IsChecked=Result.Clock;loading=false;UpdateProperties();
    }
    bool UpdateProperties()
    {
        if(loading)return false;
        if(!Regex.IsMatch(AccentBox.Text,"^#[0-9a-fA-F]{6}$")){EditorStatus.Text="Цвет: #RRGGBB, например #C8E8FF.";return false;}
        if(TopSlider.Value>=BottomSlider.Value){EditorStatus.Text="Верхняя граница должна быть выше нижней.";return false;}
        Result.Accent=AccentBox.Text;Result.Effect=EffectPicker.SelectedValue as string??"None";Result.Speed=SpeedSlider.Value;Result.Density=DensitySlider.Value;Result.ParticleSize=SizeSlider.Value;Result.Wind=WindSlider.Value;Result.Parallax=ParallaxSlider.Value;Result.RegionTop=TopSlider.Value;Result.RegionBottom=BottomSlider.Value;Result.AudioReactive=AudioCheck.IsChecked==true;Result.Clock=ClockCheck.IsChecked==true;
        SpeedLabel.Text=$"Скорость · {Result.Speed:0.0}×";DensityLabel.Text=$"Количество частиц · {Result.Density:0.0}×";
        preview?.RefreshEnvironment();audio.SetEnabled(!paused&&IsActive&&Result.AudioReactive);EditorStatus.Text="Изменения видны в предпросмотре. На рабочем столе действует выбранный профиль качества.";return true;
    }
    void RebuildPreview()
    {
        if(!IsLoaded)return;
        preview?.Dispose();PreviewSurface.Children.Clear();
        try{preview=new InteractiveVisual(Result,new(){Performance=QualityPicker.SelectedIndex switch{0=>"Eco",2=>"Quality",_=>"Balance"}},Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle),audio);PreviewSurface.Children.Add(preview);preview.SetPaused(paused);}
        catch(Exception error){EditorStatus.Text="Не удалось открыть изображение: "+error.Message;}
    }
    void ChooseImage(bool foreground)
    {
        var picker=new OpenFileDialog{Filter=foreground?"Передний план PNG|*.png":"Изображения|*.png;*.jpg;*.jpeg;*.webp;*.bmp"};
        if(picker.ShowDialog(this)!=true)return;
        try{_ = InteractiveVisual.Load(picker.FileName,1080);if(foreground)Result.Foreground=picker.FileName;else Result.Background=picker.FileName;RebuildPreview();}
        catch(Exception error){EditorStatus.Text="Изображение не поддерживается: "+error.Message;}
    }
    void DesignerKeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Escape){Close();e.Handled=true;}}
}
