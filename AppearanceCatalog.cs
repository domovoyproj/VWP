using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace VWP;
public sealed class AppearanceTheme
{
    public string Id {get;set;}="";
    public string Name {get;set;}="";
    public string Description {get;set;}="";
    public string Author {get;set;}="VWP";
    public string License {get;set;}="";
    public string Source {get;set;}="";
    public string Mode {get;set;}="Dark";
    public string Accent {get;set;}="#A3BCF7";
    public string Palette {get;set;}="";
    public string Background {get;set;}="";
    public string CursorId {get;set;}="";
    public string Meta=>$"{(Mode=="Light"?"Светлая":"Тёмная")} · {Author}";
}
public sealed class CursorPack
{
    public string Id {get;set;}="";
    public string Name {get;set;}="";
    public string Author {get;set;}="";
    public string Description {get;set;}="";
    public string License {get;set;}="";
    public string Source {get;set;}="";
    public string Accent {get;set;}="#A3BCF7";
    public string SourceArchive {get;set;}="";
    public int Order {get;set;}=100;
    public Dictionary<string,string> Files {get;set;}=new(StringComparer.OrdinalIgnoreCase);
    [JsonIgnore] public string Folder {get;set;}="";
    [JsonIgnore] public string Preview=>Path.Combine(Folder,"preview.png");
    [JsonIgnore] public string Sheet=>Path.Combine(Folder,"sheet.png");
    [JsonIgnore] public string Meta=>$"{Files.Count} ролей · {Author}";
    public string File(string role)
    {
        string path=Path.GetFullPath(Path.Combine(Folder,Files[role]));
        if(!path.StartsWith(Path.GetFullPath(Folder)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Курсор находится вне папки набора.");
        return path;
    }
}
public static class AppearanceCatalog
{
    public static readonly string Data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","personalization");
    public static List<AppearanceTheme> Themes()
    {
        string assets=Path.Combine(AppContext.BaseDirectory,"assets","themes");
        var result=JsonSerializer.Deserialize<List<AppearanceTheme>>(System.IO.File.ReadAllText(Path.Combine(assets,"themes.json")))!;
        foreach(var theme in result)theme.Background=Path.GetFullPath(Path.Combine(assets,theme.Background));
        string custom=Path.Combine(Data,"themes");
        if(Directory.Exists(custom))foreach(string file in Directory.EnumerateFiles(custom,"theme.json",SearchOption.AllDirectories))
            try{var theme=JsonSerializer.Deserialize<AppearanceTheme>(System.IO.File.ReadAllText(file));if(theme is not null)result.Add(theme);}catch(JsonException){}
        return result;
    }
    public static List<CursorPack> Cursors()
    {
        var result=new List<CursorPack>();
        foreach(string root in new[]{Path.Combine(AppContext.BaseDirectory,"assets","cursors"),Path.Combine(Data,"cursors")})
            if(Directory.Exists(root))foreach(string folder in Directory.EnumerateDirectories(root))
                if(System.IO.File.Exists(Path.Combine(folder,"pack.json")))try{
                    var pack=JsonSerializer.Deserialize<CursorPack>(System.IO.File.ReadAllText(Path.Combine(folder,"pack.json")))!;pack.Folder=folder;
                    if(pack.Files.ContainsKey("Arrow") && !result.Any(p=>p.Id==pack.Id))result.Add(pack);
                }catch(JsonException){}
        return result.OrderBy(pack=>pack.Order).ThenBy(pack=>pack.Name).ToList();
    }
}
