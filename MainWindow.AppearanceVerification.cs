using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
namespace VWP;
public partial class MainWindow
{
    async Task VerifyPersonalization()
    {
        string folder=Path.Combine(AppContext.BaseDirectory,"verification");Directory.CreateDirectory(folder);
        var report=new List<object>();var original=preferences;
        void Check(string name,bool value){report.Add(new{name,passed=value});if(!value)throw new InvalidOperationException(name);}
        string cursorBackup=Path.Combine(AppearanceCatalog.Data,"cursor-backup.json"),themeBackup=Path.Combine(AppearanceCatalog.Data,"theme-backup.json"),appBackup=AppAppearanceBackup;
        byte[]? savedCursor=File.Exists(cursorBackup)?File.ReadAllBytes(cursorBackup):null,savedTheme=File.Exists(themeBackup)?File.ReadAllBytes(themeBackup):null;
        byte[]? savedApp=File.Exists(appBackup)?File.ReadAllBytes(appBackup):null;
        var importedFolders=new List<string>();
        try
        {
            if(File.Exists(cursorBackup))File.Delete(cursorBackup);if(File.Exists(themeBackup))File.Delete(themeBackup);if(File.Exists(appBackup))File.Delete(appBackup);
            preferences=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(original))!;
            Check("retired collection is absent",items.All(item=>item.PresetId is not (>=100 and <=114)) && items.Count(item=>item.PresetId is not null)==46);
            var config=Config(SelectedScreen);config.Scene="preset:114";config.Playlist.Add("preset:100");preferences.Favorites.Add("preset:114");RemoveRetiredWallpapers();
            Check("retired scene migration",config.Scene=="preset:0"&&!config.Playlist.Contains("preset:100")&&!preferences.Favorites.Contains("preset:114"));
            Check("thirteen themes and fourteen packs installed",appearanceThemes.Count>=13 && cursorPacks.Count>=14);
            foreach(var pack in cursorPacks){CursorPackService.Validate(pack);Check("Windows loads all roles: "+pack.Name,pack.Files.Count==17);}
            using var before=Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors");
            string originalArrow=Convert.ToString(before?.GetValue("Arrow",null,RegistryValueOptions.DoNotExpandEnvironmentNames))??"";
            CursorPackService.Apply(cursorPacks.First(p=>p.Id=="vwp-pearl"));
            Check("system pointer changes",Convert.ToString(Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Cursors","Arrow",""))?.Contains("vwp-pearl")==true);
            CursorPackService.Restore();
            using(var restored=Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors"))Check("previous pointer restored",originalArrow==(Convert.ToString(restored?.GetValue("Arrow",null,RegistryValueOptions.DoNotExpandEnvironmentNames))??""));
            string export=Path.Combine(folder,"cursor-roundtrip.zip");CursorPackService.Export(cursorPacks.First(p=>p.Id=="vwp-rose"),export);
            var imported=CursorPackService.Import(export);importedFolders.Add(imported.Folder);Check("cursor ZIP roundtrip",imported.Files.Count==17);
            string nativeTheme=Path.Combine(folder,"Obsidian.theme");WindowsAppearanceService.Export(appearanceThemes.First(t=>t.Id=="obsidian"),cursorPacks.First(p=>p.Id=="vwp-obsidian"),nativeTheme);
            var themeImport=WindowsAppearanceService.Import(nativeTheme);importedFolders.Add(Path.Combine(AppearanceCatalog.Data,"themes",themeImport.Id));importedFolders.Add(Path.Combine(AppearanceCatalog.Data,"cursors",themeImport.CursorId));
            Check("native theme roundtrip",themeImport.Accent=="#DDBA86"&&File.Exists(themeImport.Background)&&themeImport.CursorId.Length>0);
            int oldMode=Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1));
            var theme=appearanceThemes.First(t=>t.Id=="pearl");WindowsAppearanceService.Apply(theme,false);
            Check("Windows light theme changes",Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",0))==1);
            WindowsAppearanceService.Restore();
            Check("previous Windows mode restored",Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1))==oldMode);
            string oldAppMode=preferences.Theme;string oldScene=config.Scene!;
            preferences.PauseBattery=false;preferences.PauseFullscreen=false;preferences.CheckUpdates=false;
            Appearance.ApplyBackground.IsChecked=true;Appearance.ApplyCursors.IsChecked=true;
            ApplyTheme(appearanceThemes.First(t=>t.Id=="obsidian"));
            Check("complete theme updates app and Windows",preferences.AppearanceThemeId=="obsidian"&&preferences.CursorPackId=="vwp-obsidian"&&Config(SelectedScreen).Scene is null);
            Check("optional theme background applied",string.Equals(Convert.ToString(Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop","Wallpaper","")),appearanceThemes.First(t=>t.Id=="obsidian").Background,StringComparison.OrdinalIgnoreCase));
            cursorPage=false;RestoreAppearance();await Task.Delay(400);
            Check("theme restore recovers app and live assignments",preferences.Theme==oldAppMode&&Config(SelectedScreen).Scene==oldScene);
            foreach(var player in hosts.Values)player.Stop();Appearance.ApplyBackground.IsChecked=false;
            preferences.Theme="Dark";preferences.SceneAccent=false;preferences.AppearanceAccent="#A3BCF7";UpdateTheme();
            NavigateAppearance("Wallpaper");await Task.Delay(300);RenderVerification("studio-wallpapers");
            NavigateAppearance("Themes");Appearance.ThemeLibrary.SelectedIndex=0;await Task.Delay(300);RenderVerification("studio-themes");
            NavigateAppearance("Cursors");Appearance.CursorLibrary.SelectedItem=cursorPacks.First(p=>p.Id=="vwp-sakura");await Task.Delay(300);RenderVerification("studio-cursors");
            Check("cursor preview has real native and animated cursors",Appearance.TryButton.Cursor is not null && Appearance.TryText.Cursor is not null && Appearance.TryBusy.Cursor is not null);
            preferences.Theme="Light";UpdateTheme();NavigateAppearance("Themes");Appearance.ThemeLibrary.SelectedItem=appearanceThemes.First(t=>t.Id=="pearl");await Task.Delay(300);RenderVerification("studio-light");
        }
        catch(Exception e){report.Add(new{name="error",passed=false,error=e.ToString()});}
        finally
        {
            try{CursorPackService.Restore();WindowsAppearanceService.Restore();}catch(Exception e){report.Add(new{name="restore error",passed=false,error=e.ToString()});}
            foreach(var player in hosts.Values)player.Stop();
            if(savedCursor is not null)File.WriteAllBytes(cursorBackup,savedCursor);if(savedTheme is not null)File.WriteAllBytes(themeBackup,savedTheme);if(savedApp is not null)File.WriteAllBytes(appBackup,savedApp);else File.Delete(appBackup);
            foreach(string target in importedFolders.Where(path=>Directory.Exists(path)))
            {
                string full=Path.GetFullPath(target);if(!full.StartsWith(Path.GetFullPath(AppearanceCatalog.Data)+Path.DirectorySeparatorChar))continue;
                foreach(string file in Directory.EnumerateFiles(full))File.Delete(file);Directory.Delete(full);
            }
            preferences=original;File.WriteAllText(Path.Combine(folder,"personalization-result.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));QuitClick(this,new());
        }
    }
}
