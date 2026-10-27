using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms=System.Windows.Forms;
namespace VWP;
public partial class MainWindow
{
    void QualityClick(object sender,RoutedEventArgs e){UpdateQualityUi();QualityPopup.IsOpen=!QualityPopup.IsOpen;}
    void CollectionMenuClick(object sender,RoutedEventArgs e){var button=(Button)sender;button.ContextMenu.PlacementTarget=button;button.ContextMenu.IsOpen=true;}
    void QualityPresetClick(object sender,RoutedEventArgs e)=>ApplyQuickQuality((string)((Button)sender).Tag);
    void ApplyQuickQuality(string mode)
    {
        StopPreview();StopHover();cityWindow?.Close();QualityPopup.IsOpen=false;
        foreach(var screen in Forms.Screen.AllScreens)
        {
            var config=Config(screen);PerformancePolicy.Apply(config,mode);
            var scene=pendingScenes.GetValueOrDefault(screen.DeviceName)??FindScene(config.Scene);
            if(scene is not null&&(pendingScenes.ContainsKey(screen.DeviceName)||hosts.TryGetValue(screen.DeviceName,out var player)&&player.Active))ApplyScene(scene,screen);
        }
        Save();UpdateQualityUi();
        Status.Text=mode=="Eco"?"Экономия включена · облегчённые видео и живые сцены":"Режим применён ко всем экранам · "+QualityCaption(mode);
    }
    static string QualityCaption(string mode)=>mode switch{"Eco"=>"Экономия","Balance"=>"Баланс","Quality"=>"Максимум","Auto"=>"Авто",_=>"Вручную"};
    void UpdateQualityUi()
    {
        if(QualityLabel is null||!featuresReady)return;
        string mode=PerformancePolicy.Mode(Config(SelectedScreen));QualityLabel.Text="Режим · "+QualityCaption(mode);
        foreach(var button in new[]{QualityEco,QualityAuto,QualityBalance,QualityHigh})button.SetResourceReference(Control.BackgroundProperty,(string)button.Tag==mode?"SelectedBrush":"ControlBrush");
        if(City is not null)City.Quality.SelectedIndex=Config(SelectedScreen).CityPerformance switch{"Eco"=>0,"Balance"=>1,"Quality"=>2,_=>3};
    }
    void UpdatePauseUi(bool paused)
    {
        PauseLabel.Text=paused?"Продолжить":"Пауза";
        PauseGlyph.Data=Geometry.Parse(paused?"M 2 1 L 10 7 L 2 13 Z":"M 3 1 L 3 13 M 9 1 L 9 13");
        if(paused)PauseGlyph.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,"InkBrush");else PauseGlyph.Fill=Brushes.Transparent;
    }
}
