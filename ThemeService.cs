using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
namespace VWP;
public static class ThemeService
{
    static readonly Dictionary<string,string[]> atelier=File.Exists(Path.Combine(AppContext.BaseDirectory,"assets","themes","palettes.json"))?JsonSerializer.Deserialize<Dictionary<string,string[]>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"assets","themes","palettes.json")))!:new();
    public static void Apply(string mode,string accent,string paletteName="")
    {
        bool dark=mode=="Dark" || mode=="System" && (int)(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1)??1)==0;
        var palette=new Dictionary<string,string>{
            ["CanvasBrush"]=dark?"#0C111A":"#F6F7FB",["PanelBrush"]=dark?"#121925":"#EFF0F6",["CardBrush"]=dark?"#192332":"#FFFFFF",
            ["ControlBrush"]=dark?"#243146":"#E9EBF3",["InkBrush"]=dark?"#F2F3FA":"#212635",["MutedBrush"]=dark?"#A0A6B8":"#727B90",["LineBrush"]=dark?"#2A394E":"#DFE3ED",
            ["SelectedBrush"]=dark?"#293F5E":"#E4E8F8",["PrimaryBrush"]=dark?MutedAccent(accent):"#30384F",["AccentBrush"]=accent};
        if(dark && paletteName=="Catppuccin"){palette["CanvasBrush"]="#1E1E2E";palette["PanelBrush"]="#181825";palette["CardBrush"]="#242436";palette["ControlBrush"]="#313244";palette["InkBrush"]="#CDD6F4";palette["MutedBrush"]="#A6ADC8";}
        if(dark && paletteName=="Nord"){palette["CanvasBrush"]="#2E3440";palette["PanelBrush"]="#272D38";palette["CardBrush"]="#3B4252";palette["ControlBrush"]="#434C5E";palette["InkBrush"]="#ECEFF4";palette["MutedBrush"]="#B8C2D4";}
        if(dark)
        {
            var gaming=paletteName switch
            {
                "Sakura"=>new[]{"#17121F","#1D1628","#251B31","#34243F"},
                "Neon"=>new[]{"#08131E","#0E1A29","#122334","#1C3445"},
                "Crimson"=>new[]{"#191016","#21151C","#2B1B25","#3B2630"},
                "Astral"=>new[]{"#121122","#1A1730","#211D3B","#2F294E"},
                "Abyss"=>new[]{"#0A1920","#10242C","#17323B","#23434B"},
                "Cyber"=>new[]{"#101915","#17231D","#203027","#304434"},
                "Aurora"=>new[]{"#0D1A23","#14262F","#1B333C","#294650"},
                "Ember"=>new[]{"#1C1515","#281C1C","#352525","#49332E"},
                _=>null
            };
            if(atelier.TryGetValue(paletteName,out var custom))gaming=custom;
            if(gaming is not null){palette["CanvasBrush"]=gaming[0];palette["PanelBrush"]=gaming[1];palette["CardBrush"]=gaming[2];palette["ControlBrush"]=gaming[3];palette["SelectedBrush"]=gaming[3];}
        }
        if(!dark && (paletteName.StartsWith("Vibe-",StringComparison.Ordinal)||paletteName.StartsWith("Season-",StringComparison.Ordinal)) && atelier.TryGetValue(paletteName,out var light) && light.Length>=4)
        {
            palette["CanvasBrush"]=light[0];palette["PanelBrush"]=light[1];palette["CardBrush"]=light[2];palette["ControlBrush"]=light[3];palette["SelectedBrush"]=light[3];
        }
        foreach(var color in palette){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color.Value));brush.Freeze();Application.Current.Resources[color.Key]=brush;}
    }
    static string MutedAccent(string value){var color=(Color)ColorConverter.ConvertFromString(value);return $"#{(int)(color.R*.7):X2}{(int)(color.G*.7):X2}{(int)(color.B*.7):X2}";}
}
