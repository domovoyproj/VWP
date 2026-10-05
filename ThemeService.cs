using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
namespace VWP;
public static class ThemeService
{
    public static void Apply(string mode,string accent)
    {
        bool dark=mode=="Dark" || mode=="System" && (int)(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1)??1)==0;
        var palette=new Dictionary<string,string>{
            ["CanvasBrush"]=dark?"#14151C":"#F7F7FA",["PanelBrush"]=dark?"#1C1E28":"#EEEFF4",["CardBrush"]=dark?"#EE232631":"#F7FFFFFF",
            ["ControlBrush"]=dark?"#303340":"#EDEDF3",["InkBrush"]=dark?"#F0F1F7":"#202127",["MutedBrush"]=dark?"#A5A8BA":"#8B8C9D",["LineBrush"]=dark?"#3B3E4D":"#E3E4EB",
            ["SelectedBrush"]=dark?"#3B3654":"#F0EDF7",["PrimaryBrush"]=dark?MutedAccent(accent):"#272832",["AccentBrush"]=accent};
        foreach(var color in palette){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color.Value));brush.Freeze();Application.Current.Resources[color.Key]=brush;}
    }
    static string MutedAccent(string value){var color=(Color)ColorConverter.ConvertFromString(value);return $"#{(int)(color.R*.7):X2}{(int)(color.G*.7):X2}{(int)(color.B*.7):X2}";}
}
