using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace VWP;
public partial class MainWindow
{
    List<AppearanceTheme> appearanceThemes=new();
    List<CursorPack> cursorPacks=new();
    readonly List<Cursor> sampleCursors=new();
    bool cursorPage;
    record AppAppearance(string Mode,string Accent,string Palette,bool SceneAccent,Dictionary<string,MonitorPreferences>? Monitors=null);
    string AppAppearanceBackup=>Path.Combine(AppearanceCatalog.Data,"app-theme-backup.json");
    void InitializeAppearance()
    {
        ReloadAppearance();
        Appearance.ThemeLibrary.SelectionChanged+=(_,_)=>SelectAppearance();
        Appearance.CursorLibrary.SelectionChanged+=(_,_)=>SelectAppearance();
        Appearance.ApplyButton.Click+=(_,_)=>ApplyAppearance();
        Appearance.RestoreButton.Click+=(_,_)=>RestoreAppearance();
        Appearance.ImportButton.Click+=(_,_)=>ImportAppearance();
        Appearance.ExportButton.Click+=(_,_)=>ExportAppearance();
        Appearance.SourceButton.Click+=(_,_)=>{
            string source=cursorPage?(Appearance.CursorLibrary.SelectedItem as CursorPack)?.Source??"":(Appearance.ThemeLibrary.SelectedItem as AppearanceTheme)?.Source??"";
            string license=cursorPage?(Appearance.CursorLibrary.SelectedItem as CursorPack)?.License??"":(Appearance.ThemeLibrary.SelectedItem as AppearanceTheme)?.License??"";
            if(Uri.TryCreate(source,UriKind.Absolute,out var url)&&url.Scheme=="https"&&url.Host=="github.com")Process.Start(new ProcessStartInfo(source){UseShellExecute=true});
            else Appearance.Status.Text=license+" · Исходники и лицензии включены в приложение";
        };
        Appearance.ThemePreview.SizeChanged+=(_,_)=>Appearance.ThemePreview.Clip=new RectangleGeometry(new Rect(0,0,Appearance.ThemePreview.ActualWidth,Appearance.ThemePreview.ActualHeight),19,19);
        Loaded+=async(_,_)=>{if(Environment.GetCommandLineArgs().Contains("--verify-personalization"))await VerifyPersonalization();};
        Appearance.ThemeLibrary.SelectedIndex=0;Appearance.CursorLibrary.SelectedIndex=0;SelectAppearance();
    }
    void ReloadAppearance()
    {
        appearanceThemes=AppearanceCatalog.Themes();cursorPacks=AppearanceCatalog.Cursors();
        Appearance.ThemeLibrary.ItemsSource=appearanceThemes;Appearance.CursorLibrary.ItemsSource=cursorPacks;
    }
    void AppearanceNavigation(object sender,RoutedEventArgs e)=>NavigateAppearance((string)((Button)sender).Tag);
    void NavigateAppearance(string page)
    {
        StopPreview();StopHover();cursorPage=page=="Cursors";bool wallpaper=page=="Wallpaper";
        WallpaperPage.Visibility=wallpaper?Visibility.Visible:Visibility.Collapsed;
        Appearance.Visibility=wallpaper?Visibility.Collapsed:Visibility.Visible;
        SidebarCategories.Visibility=wallpaper?Visibility.Visible:Visibility.Collapsed;
        SidebarImport.Visibility=wallpaper?Visibility.Visible:Visibility.Collapsed;
        SidebarNote.Visibility=wallpaper?Visibility.Collapsed:Visibility.Visible;
        foreach(var button in new[]{WallpaperNav,ThemesNav,CursorsNav})if((string)button.Tag==page)button.SetResourceReference(Control.BackgroundProperty,"SelectedBrush");else button.Background=Brushes.Transparent;
        Appearance.PageTitle.Text=cursorPage?"Курсоры":"Темы";
        Appearance.CollectionLabel.Text=cursorPage?"Точность в каждой детали":"Подобранные стили";
        Appearance.ThemeLibrary.Visibility=cursorPage?Visibility.Collapsed:Visibility.Visible;
        Appearance.CursorLibrary.Visibility=cursorPage?Visibility.Visible:Visibility.Collapsed;
        Appearance.ThemePreview.Visibility=cursorPage?Visibility.Collapsed:Visibility.Visible;
        Appearance.CursorPreview.Visibility=cursorPage?Visibility.Visible:Visibility.Collapsed;
        Appearance.ThemeOptions.Visibility=cursorPage?Visibility.Collapsed:Visibility.Visible;
        Appearance.CursorHint.Visibility=cursorPage?Visibility.Visible:Visibility.Collapsed;
        Appearance.ApplyButton.Content=cursorPage?"Применить курсоры":"Применить тему";
        SelectAppearance();
    }
    void SelectAppearance()
    {
        if(Appearance is null)return;
        Appearance.Count.Text=$"{(cursorPage?cursorPacks.Count:appearanceThemes.Count)} в коллекции";
        object? selected=cursorPage?Appearance.CursorLibrary.SelectedItem:Appearance.ThemeLibrary.SelectedItem;
        Appearance.ApplyButton.IsEnabled=selected is not null;Appearance.ExportButton.IsEnabled=selected is not null;
        foreach(var cursor in sampleCursors)cursor.Dispose();sampleCursors.Clear();Appearance.TryButton.Cursor=null;Appearance.TryText.Cursor=null;Appearance.TryBusy.Cursor=null;
        if(selected is AppearanceTheme theme)
        {
            Appearance.SelectedTitle.Text=theme.Name;Appearance.SelectedDescription.Text=theme.Description;
            Appearance.SelectedMeta.Text=theme.Meta;Appearance.Badge.Text=theme.Author=="VWP"?"VWP ORIGINAL":"OPEN PALETTE · WINDOWS";
            Appearance.ColorSwatch.Background=(Brush)new BrushConverter().ConvertFromString(theme.Accent)!;
            Appearance.ThemeImage.Source=File.Exists(theme.Background)?new BitmapImage(new Uri(theme.Background)):null;
            Appearance.Status.Text=preferences.AppearanceThemeId==theme.Id?"Применено · "+theme.Name:"Windows и лаунчер · Фон можно оставить прежним";
        }
        else if(selected is CursorPack pack)
        {
            Appearance.SelectedTitle.Text=pack.Name;Appearance.SelectedDescription.Text=pack.Description;
            Appearance.SelectedMeta.Text=pack.Meta;Appearance.Badge.Text=pack.Author=="VWP"?"VWP ORIGINAL":"OPEN SOURCE · "+pack.License;
            Appearance.ColorSwatch.Background=(Brush)new BrushConverter().ConvertFromString(pack.Accent)!;
            Appearance.CursorImage.Source=File.Exists(pack.Preview)?new BitmapImage(new Uri(pack.Preview)):null;
            foreach(var (role,control) in new (string,FrameworkElement)[]{("Hand",Appearance.TryButton),("IBeam",Appearance.TryText),("Wait",Appearance.TryBusy)})
                if(pack.Files.ContainsKey(role))try{var cursor=new Cursor(pack.File(role));sampleCursors.Add(cursor);control.Cursor=cursor;}catch{}
            Appearance.Status.Text=preferences.CursorPackId==pack.Id?"Применено · "+pack.Name:"Наведи на кнопку и выдели текст, чтобы попробовать";
        }
        Appearance.RestoreButton.IsEnabled=cursorPage?CursorPackService.CanRestore:WindowsAppearanceService.CanRestore;
    }
    void ApplyAppearance()
    {
        try
        {
            if(cursorPage && Appearance.CursorLibrary.SelectedItem is CursorPack cursor)
            {CursorPackService.Apply(cursor);preferences.CursorPackId=cursor.Id;Appearance.Status.Text="Курсоры применены во всех приложениях Windows";}
            else if(Appearance.ThemeLibrary.SelectedItem is AppearanceTheme theme)
                ApplyTheme(theme);
            Save();Appearance.RestoreButton.IsEnabled=true;
        }
        catch(Exception e){Appearance.Status.Text="Не удалось применить: "+e.Message;}
    }
    void ApplyTheme(AppearanceTheme theme)
    {
        bool background=Appearance.ApplyBackground.IsChecked==true;
        if(background&&!File.Exists(theme.Background))throw new FileNotFoundException("У этой темы нет установленного фона.");
        var pack=Appearance.ApplyCursors.IsChecked==true?cursorPacks.FirstOrDefault(p=>p.Id==theme.CursorId):null;
        if(pack is not null)CursorPackService.Validate(pack);
        var original=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences))!;
        var windows=WindowsAppearanceService.CaptureState();var cursors=CursorPackService.CaptureState();
        bool hadWindowsBackup=WindowsAppearanceService.CanRestore,hadCursorBackup=CursorPackService.CanRestore;
        byte[]? appBackup=File.Exists(AppAppearanceBackup)?File.ReadAllBytes(AppAppearanceBackup):null;
        try
        {
            var before=appBackup is not null?JsonSerializer.Deserialize<AppAppearance>(appBackup)!:new AppAppearance(preferences.Theme,preferences.AppearanceAccent,preferences.AppearancePalette,preferences.SceneAccent);
            if(background && before.Monitors is null)before=before with{Monitors=original.Monitors};
            PersonalizationBackup.Write(AppAppearanceBackup,before);
            if(background)foreach(var player in hosts.Values)player.Stop();
            WindowsAppearanceService.Apply(theme,background);
            if(pack is not null){CursorPackService.Apply(pack);preferences.CursorPackId=pack.Id;}
            if(background)foreach(var config in preferences.Monitors.Values){config.Scene=null;config.PlaylistEnabled=false;}
            preferences.Theme=theme.Mode;preferences.AppearanceAccent=theme.Accent;preferences.AppearancePalette=theme.Palette;preferences.SceneAccent=false;preferences.AppearanceThemeId=theme.Id;
            UpdateTheme();Appearance.Status.Text="Применено · "+theme.Name+(background?" · Фон темы":" · Живые обои сохранены");
        }
        catch
        {
            WindowsAppearanceService.RestoreState(windows);CursorPackService.RestoreState(cursors);
            if(!hadWindowsBackup)WindowsAppearanceService.DiscardBackup();if(!hadCursorBackup)CursorPackService.DiscardBackup();
            if(appBackup is null)File.Delete(AppAppearanceBackup);else File.WriteAllBytes(AppAppearanceBackup,appBackup);
            preferences=original;UpdateTheme();if(background)Restore();throw;
        }
    }
    void RestoreAppearance()
    {
        try
        {
            CursorPackService.Restore();preferences.CursorPackId="";
            if(!cursorPage)
            {
                WindowsAppearanceService.Restore();preferences.AppearanceThemeId="";
                if(File.Exists(AppAppearanceBackup)){var before=JsonSerializer.Deserialize<AppAppearance>(File.ReadAllText(AppAppearanceBackup))!;preferences.Theme=before.Mode;preferences.AppearanceAccent=before.Accent;preferences.AppearancePalette=before.Palette;preferences.SceneAccent=before.SceneAccent;if(before.Monitors is not null){preferences.Monitors=before.Monitors;Restore();}File.Delete(AppAppearanceBackup);UpdateTheme();}
            }
            Save();Appearance.Status.Text="Прежнее оформление восстановлено";Appearance.RestoreButton.IsEnabled=false;
        }
        catch(Exception e){Appearance.Status.Text="Не удалось восстановить: "+e.Message;}
    }
    void ImportAppearance()
    {
        var dialog=new OpenFileDialog{Filter=cursorPage?"Курсоры Windows|*.zip;*.cur;*.ani":"Темы Windows|*.theme"};if(dialog.ShowDialog()!=true)return;
        try
        {
            string id=cursorPage?CursorPackService.Import(dialog.FileName).Id:WindowsAppearanceService.Import(dialog.FileName).Id;
            ReloadAppearance();if(cursorPage)Appearance.CursorLibrary.SelectedItem=cursorPacks.First(p=>p.Id==id);else Appearance.ThemeLibrary.SelectedItem=appearanceThemes.First(t=>t.Id==id);
            Appearance.Status.Text="Импортировано · Готово к применению";
        }
        catch(Exception e){Appearance.Status.Text="Импорт: "+e.Message;}
    }
    void ExportAppearance()
    {
        var dialog=new SaveFileDialog{Filter=cursorPage?"Набор курсоров|*.zip":"Тема Windows|*.theme",FileName=(cursorPage?(Appearance.CursorLibrary.SelectedItem as CursorPack)?.Name:(Appearance.ThemeLibrary.SelectedItem as AppearanceTheme)?.Name)??"VWP"};if(dialog.ShowDialog()!=true)return;
        try
        {
            if(cursorPage && Appearance.CursorLibrary.SelectedItem is CursorPack cursor)CursorPackService.Export(cursor,dialog.FileName);
            else if(Appearance.ThemeLibrary.SelectedItem is AppearanceTheme theme)WindowsAppearanceService.Export(theme,cursorPacks.FirstOrDefault(p=>p.Id==theme.CursorId),dialog.FileName);
            Appearance.Status.Text="Экспорт сохранён · "+dialog.FileName;
        }
        catch(Exception e){Appearance.Status.Text="Экспорт: "+e.Message;}
    }
    void RemoveRetiredWallpapers()
    {
        bool Retired(string? key)=>key is not null && Enumerable.Range(100,15).Any(id=>key=="preset:"+id);
        preferences.Favorites.RemoveAll(key=>Retired(key));
        foreach(var config in preferences.Monitors.Values){if(Retired(config.Scene))config.Scene="preset:0";config.Playlist.RemoveAll(key=>Retired(key));}
        foreach(string key in preferences.Layers.Keys.Where(key=>Retired(key)).ToArray())preferences.Layers.Remove(key);
        if(preferences.LastPresetId is >=100 and <=114){preferences.LastPresetId=0;preferences.Last=Path.Combine(AppContext.BaseDirectory,"assets","0.mp4");}
    }
}
