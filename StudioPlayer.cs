using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Threading;
namespace VWP;
internal sealed class StudioPlayer : Window
{
    readonly StudioSurface surface=new();
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(2)};
    public StudioPlayer(StudioProject project,string profile)
    {
        StudioWindowChrome.Apply(this);
        Title="VWP · "+project.Name;Width=1280;Height=800;MinWidth=800;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=new SolidColorBrush(Color.FromRgb(9,13,21));Foreground=Brushes.White;
        var root=new DockPanel();Content=root;
        var bar=new DockPanel{Margin=new Thickness(20,14,20,14)};DockPanel.SetDock(bar,Dock.Top);root.Children.Add(bar);
        var close=new Button{Content="Закрыть",Padding=new Thickness(16,8,16,8)};close.Click+=(_,_)=>Close();DockPanel.SetDock(close,Dock.Right);bar.Children.Add(close);
        bool paused=false;var pause=new Button{Content="Ⅱ Пауза",Padding=new Thickness(16,8,16,8),Margin=new Thickness(0,0,12,0)};pause.Click+=(_,_)=>{paused=!paused;surface.SetPaused(paused);pause.Content=paused?"▷ Продолжить":"Ⅱ Пауза";};DockPanel.SetDock(pause,Dock.Right);bar.Children.Add(pause);
        bar.Children.Add(new TextBlock{Text=project.Name,FontSize=19,VerticalAlignment=VerticalAlignment.Center});
        var stats=new TextBlock{Margin=new Thickness(20,12,20,12),Foreground=new SolidColorBrush(Color.FromRgb(138,161,193)),FontSize=11};DockPanel.SetDock(stats,Dock.Bottom);root.Children.Add(stats);
        root.Children.Add(new WindowsFormsHost{Child=surface.Control});
        surface.Message+=json=>{using var message=System.Text.Json.JsonDocument.Parse(json);string? type=message.RootElement.GetProperty("type").GetString();if(type=="error"){timer.Stop();stats.Text=message.RootElement.GetProperty("message").GetString();}if(type=="recovering"){timer.Stop();stats.Text="Восстанавливаю сцену…";}if(type=="restored")timer.Start();if(type=="escape")Close();if(type=="togglepause"){paused=!paused;surface.SetPaused(paused);pause.Content=paused?"▷ Продолжить":"Ⅱ Пауза";}};
        Loaded+=async(_,_)=>{try{await surface.Initialize(project,profile,true);timer.Start();}catch(Exception error){stats.Text=error.Message;}};
        timer.Tick+=(_,_)=>{var s=surface.Statistics;stats.Text=$"{s.Width} × {s.Height}   ·   {s.Fps:0} FPS   ·   GPU текстуры и буферы {s.TextureMb+s.TargetMb:0} МБ   ·   {s.Layers} слоя";};
        Deactivated+=(_,_)=>surface.SetPaused(true);Activated+=(_,_)=>surface.SetPaused(paused);
        Closed+=(_,_)=>{timer.Stop();surface.Dispose();};KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape)Close();};
    }
}
