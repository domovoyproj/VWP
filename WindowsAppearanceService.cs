using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using Microsoft.Win32;
namespace VWP;
public static class WindowsAppearanceService
{
    const string Personalize=@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    const string Dwm=@"Software\Microsoft\Windows\DWM";
    const string Accent=@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    static string Backup=>Path.Combine(AppearanceCatalog.Data,"theme-backup.json");
    public static bool CanRestore=>System.IO.File.Exists(Backup);
    static readonly (string,string)[] Targets={
        (Personalize,"AppsUseLightTheme"),(Personalize,"SystemUsesLightTheme"),(Dwm,"ColorizationColor"),(Dwm,"AccentColor"),
        (Accent,"AccentColorMenu"),(Accent,"StartColorMenu"),(@"Control Panel\Desktop","Wallpaper"),(@"Control Panel\Desktop","WallpaperStyle"),(@"Control Panel\Desktop","TileWallpaper")};
    internal static List<RegistrySetting> CaptureState()=>PersonalizationBackup.Capture(Targets);
    internal static void DiscardBackup(){if(CanRestore)System.IO.File.Delete(Backup);}
    internal static void RestoreState(List<RegistrySetting> values)
    {
        PersonalizationBackup.Restore(values);
        var wallpaper=values.First(v=>v.Key==@"Control Panel\Desktop" && v.Name=="Wallpaper");
        SetBackground(Environment.ExpandEnvironmentVariables(wallpaper.Value));Notify();
    }
    public static void Apply(AppearanceTheme theme,bool background)
    {
        var color=ParseColor(theme.Accent);
        if(theme.Mode is not ("Dark" or "Light"))throw new InvalidDataException("Тема должна быть светлой или тёмной.");
        if(background && !System.IO.File.Exists(theme.Background))throw new FileNotFoundException("Фон темы не найден.");
        var previous=CaptureState();
        if(!CanRestore)PersonalizationBackup.Write(Backup,previous);
        try
        {
            using(var key=Registry.CurrentUser.CreateSubKey(Personalize)){key.SetValue("AppsUseLightTheme",theme.Mode=="Light"?1:0);key.SetValue("SystemUsesLightTheme",theme.Mode=="Light"?1:0);}
            int argb=unchecked((int)(0xff000000u|((uint)color.R<<16)|((uint)color.G<<8)|color.B));
            int abgr=unchecked((int)(0xff000000u|((uint)color.B<<16)|((uint)color.G<<8)|color.R));
            using(var key=Registry.CurrentUser.CreateSubKey(Dwm)){key.SetValue("ColorizationColor",argb);key.SetValue("AccentColor",abgr);}
            using(var key=Registry.CurrentUser.CreateSubKey(Accent)){key.SetValue("AccentColorMenu",abgr);key.SetValue("StartColorMenu",abgr);}
            if(background){using var key=Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop");key.SetValue("WallpaperStyle","10");key.SetValue("TileWallpaper","0");SetBackground(theme.Background);}
            Notify();
        }
        catch{RestoreState(previous);throw;}
    }
    public static void Restore()
    {
        if(!CanRestore)return;
        var values=JsonSerializer.Deserialize<List<RegistrySetting>>(System.IO.File.ReadAllText(Backup))!;
        RestoreState(values);System.IO.File.Delete(Backup);
    }
    public static AppearanceTheme Import(string file)
    {
        if(!Path.GetExtension(file).Equals(".theme",StringComparison.OrdinalIgnoreCase) || new FileInfo(file).Length>512*1024)throw new InvalidDataException("Выберите файл .theme.");
        var ini=ParseIni(System.IO.File.ReadAllText(file));
        string Get(string section,string name,string fallback="")=>ini.GetValueOrDefault(section+"/"+name)??fallback;
        string id="import-"+Guid.NewGuid().ToString("N");
        string folder=Path.Combine(AppearanceCatalog.Data,"themes",id);Directory.CreateDirectory(folder);
        try
        {
            string color=Get("VisualStyles","ColorizationColor","0xFFA3BCF7").Replace("0x","",StringComparison.OrdinalIgnoreCase).TrimStart('#');
            if(color.Length==8)color=color[2..];string accent="#"+color;ParseColor(accent);
            string name=Get("Theme","DisplayName",Path.GetFileNameWithoutExtension(file));if(name.StartsWith('@'))name=Path.GetFileNameWithoutExtension(file);
            var theme=new AppearanceTheme{Id=id,Name=name,Description="Импортированная тема Windows",Author="Локальный импорт",License="Лицензия автора",Mode=Get("VisualStyles","AppMode","Dark").Equals("Light",StringComparison.OrdinalIgnoreCase)?"Light":"Dark",Accent=accent};
            string Resolve(string path)
            {
                path=Environment.ExpandEnvironmentVariables(path.Trim('"'));
                if(path.StartsWith("\\\\") || path.Contains("://"))throw new InvalidDataException("Тема содержит сетевой путь.");
                return Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine(Path.GetDirectoryName(file)!,path));
            }
            string picture=Get(@"Control Panel\Desktop","Wallpaper");
            if(picture.Length>0)
            {
                string path=Resolve(picture);
                if(System.IO.File.Exists(path))
                {
                    if(!new[]{".png",".jpg",".jpeg",".bmp"}.Contains(Path.GetExtension(path).ToLowerInvariant()) || new FileInfo(path).Length>64*1024*1024)throw new InvalidDataException("Неподдерживаемый фон темы.");
                    using var image=System.Drawing.Image.FromFile(path);
                    string target=Path.Combine(folder,"background"+Path.GetExtension(path));System.IO.File.Copy(path,target);theme.Background=target;
                }
            }
            var cursors=new CursorPack{Id=id+"-cursors",Name=name+" · Курсоры",Author="Локальный импорт",Description="Курсоры из темы Windows",License="Лицензия автора",Folder=folder};
            foreach(string role in CursorPackService.Roles)
            {
                string path=Get(@"Control Panel\Cursors",role);if(path.Length==0)continue;
                path=Resolve(path);if(!System.IO.File.Exists(path))continue;
                string target=role+Path.GetExtension(path).ToLowerInvariant();System.IO.File.Copy(path,Path.Combine(folder,target));cursors.Files[role]=target;
            }
            if(cursors.Files.ContainsKey("Arrow")){var installed=CursorPackService.Install(cursors);CursorPackService.CreatePreview(installed);theme.CursorId=installed.Id;}
            PersonalizationBackup.Write(Path.Combine(folder,"theme.json"),theme);return theme;
        }
        catch{foreach(string path in Directory.EnumerateFiles(folder))System.IO.File.Delete(path);Directory.Delete(folder);throw;}
    }
    internal static Dictionary<string,string> ParseIni(string text)
    {
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);string section="";
        foreach(string raw in text.Split('\n'))
        {
            string line=raw.Trim();if(line.Length==0||line.StartsWith(';'))continue;
            if(line.StartsWith('[')&&line.EndsWith(']')){section=line[1..^1];continue;}
            int equals=line.IndexOf('=');if(equals>0)result[section+"/"+line[..equals].Trim()]=line[(equals+1)..].Trim();
        }
        return result;
    }
    public static void Export(AppearanceTheme theme,CursorPack? cursor,string path)
    {
        ParseColor(theme.Accent);
        string Clean(string value)=>value.Replace("\r","").Replace("\n","");
        var text=new StringBuilder("[Theme]\r\nDisplayName="+Clean(theme.Name)+"\r\n\r\n[VisualStyles]\r\nPath=%SystemRoot%\\resources\\Themes\\Aero\\Aero.msstyles\r\nColorStyle=NormalColor\r\nSize=NormalSize\r\nAutoColorization=0\r\nColorizationColor=0xFF"+theme.Accent.TrimStart('#')+"\r\nAppMode="+theme.Mode+"\r\nSystemMode="+theme.Mode+"\r\n\r\n[MasterThemeSelector]\r\nMTSM=DABJDKT\r\n");
        // Keep companion files together so VWP can import the exported theme on another computer.
        string assets=Path.Combine(Path.GetDirectoryName(path)!,Path.GetFileNameWithoutExtension(path)+".assets");Directory.CreateDirectory(assets);
        if(System.IO.File.Exists(theme.Background))
        {
            string picture=Path.Combine(assets,"background"+Path.GetExtension(theme.Background));System.IO.File.Copy(theme.Background,picture,true);
            text.Append("\r\n[Control Panel\\Desktop]\r\nWallpaper="+Clean(Path.GetRelativePath(Path.GetDirectoryName(path)!,picture))+"\r\nWallpaperStyle=10\r\nTileWallpaper=0\r\n");
        }
        if(cursor is not null)
        {
            CursorPackService.Validate(cursor);text.Append("\r\n[Control Panel\\Cursors]\r\n");
            foreach(string role in cursor.Files.Keys){string file=Path.Combine(assets,role+Path.GetExtension(cursor.File(role)));System.IO.File.Copy(cursor.File(role),file,true);text.Append(role+"="+Clean(Path.GetRelativePath(Path.GetDirectoryName(path)!,file))+"\r\n");}
            foreach(string legal in new[]{"LICENSE.txt","source.zip"})if(System.IO.File.Exists(Path.Combine(cursor.Folder,legal)))System.IO.File.Copy(Path.Combine(cursor.Folder,legal),Path.Combine(assets,legal),true);
        }
        System.IO.File.WriteAllText(Path.Combine(assets,"credits.txt"),theme.Name+"\n"+theme.Author+"\n"+theme.License+"\n"+theme.Source+"\n"+cursor?.Source);
        System.IO.File.WriteAllText(path,text.ToString(),Encoding.Unicode);
    }
    static Color ParseColor(string value)=>value.Length==7 && value[0]=='#'?(Color)ColorConverter.ConvertFromString(value):throw new InvalidDataException("Некорректный цвет темы.");
    static void SetBackground(string path){if(!SystemParametersInfo(20,0,path,3))throw new Win32Exception(Marshal.GetLastWin32Error());}
    static void Notify(){SendMessageTimeout(new IntPtr(0xffff),0x1a,IntPtr.Zero,"ImmersiveColorSet",2,1000,out _);}
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="SystemParametersInfoW")] static extern bool SystemParametersInfo(uint action,uint parameter,string value,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint message,IntPtr wp,string lp,uint flags,uint timeout,out IntPtr result);
}
