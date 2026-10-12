using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Media;
namespace VWP;
internal sealed class CityWindow : Window
{
    readonly CitySurface city=new();
    readonly bool fullscreen;
    readonly string profile;
    readonly string? initial;
    readonly int palette;
    bool closing,canClose;
    public string LastState {get;private set;}="null";
    public CityWindow(string profile,string? initial=null,int palette=0,bool fullscreen=false)
    {
        this.profile=profile;this.initial=initial;this.palette=palette;this.fullscreen=fullscreen;
        Title="VWP · Afterlight";Width=1200;Height=760;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=new SolidColorBrush(Color.FromRgb(5,8,20));
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResize;Content=new WindowsFormsHost{Child=city.Control};
        Loaded+=async(_,_)=>{if(fullscreen)WindowState=WindowState.Maximized;try{await city.Initialize(profile,true,initial,palette,fullscreen);}catch(Exception e){MessageBox.Show(this,"Не удалось открыть город. Если WebView2 Runtime не установлен, установи его с сайта Microsoft.\n"+e.Message,"VWP");Close();}};
        city.Message+=json=>{using var message=JsonDocument.Parse(json);if(message.RootElement.GetProperty("type").GetString()=="close"){LastState=message.RootElement.GetProperty("state").GetRawText();Close();}};
        Closing+=async(_,e)=>
        {
            if(canClose){city.Dispose();return;}
            e.Cancel=true;if(closing)return;closing=true;
            try{LastState=await city.State();}catch{ /* A failed browser must still be closable. */ }
            finally{canClose=true;Close();}
        };
    }
}
