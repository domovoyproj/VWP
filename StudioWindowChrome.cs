using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
namespace VWP;
internal static class StudioWindowChrome
{
    internal static void Apply(Window window)
    {
        foreach(var (key,color) in new[]{("CanvasBrush","#0C1018"),("PanelBrush","#111722"),("CardBrush","#161F2E"),("ControlBrush","#1B2332"),("InkBrush","#EBEFF7"),("MutedBrush","#A5B2C8"),("LineBrush","#26354A"),("SelectedBrush","#263A56"),("PrimaryBrush","#A5C6FF"),("AccentBrush","#A5C6FF")})
        {
            var brush=new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));brush.Freeze();window.Resources[key]=brush;
        }
        string icon=Path.Combine(AppContext.BaseDirectory,"assets","app.ico");if(File.Exists(icon))window.Icon=BitmapFrame.Create(new Uri(icon));
        window.SourceInitialized+=(_,_)=>{int dark=1;DwmSetWindowAttribute(new WindowInteropHelper(window).Handle,20,ref dark,4);};
    }
    [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
}
