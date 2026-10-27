using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
namespace VWP;
public partial class MainWindow
{
    void InitializeEngineLibrary()
    {
        LibrarySort.SelectedIndex=preferences.LibraryOrder switch{"Recent"=>1,"Favorites"=>2,"Category"=>3,_=>0};
        ApplyLibrarySort();
    }
    void LibrarySortChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!studioReady)return;
        preferences.LibraryOrder=LibrarySort.SelectedIndex switch{1=>"Recent",2=>"Favorites",3=>"Category",_=>"Name"};ApplyLibrarySort();Save();
    }
    void ApplyLibrarySort()
    {
        if(CollectionViewSource.GetDefaultView(items) is ListCollectionView view)view.CustomSort=new SceneOrder(preferences);
    }
    sealed class SceneOrder(Preferences settings) : IComparer
    {
        public int Compare(object? x,object? y)
        {
            if(x is not Wallpaper a||y is not Wallpaper b)return 0;
            int rank=0;
            if(settings.LibraryOrder=="Recent")rank=Recent(a).CompareTo(Recent(b));
            if(settings.LibraryOrder=="Favorites")rank=settings.Favorites.Contains(PlaybackRules.Key(b)).CompareTo(settings.Favorites.Contains(PlaybackRules.Key(a)));
            if(settings.LibraryOrder=="Category")rank=StringComparer.CurrentCultureIgnoreCase.Compare(a.Category,b.Category);
            return rank!=0?rank:StringComparer.CurrentCultureIgnoreCase.Compare(a.Name,b.Name);
        }
        int Recent(Wallpaper scene){int index=settings.RecentScenes.IndexOf(PlaybackRules.Key(scene));return index<0?int.MaxValue:index;}
    }
    void RememberScene(Wallpaper scene)
    {
        string key=PlaybackRules.Key(scene);preferences.RecentScenes.Remove(key);preferences.RecentScenes.Insert(0,key);
        if(preferences.RecentScenes.Count>80)preferences.RecentScenes.RemoveRange(80,preferences.RecentScenes.Count-80);
        if(preferences.LibraryOrder=="Recent")ApplyLibrarySort();Save();
    }
    void CreateSceneClick(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFileDialog{Title="Фон новой живой сцены",Filter="Изображения|*.png;*.jpg;*.jpeg;*.bmp"};
        if(dialog.ShowDialog()==true)OpenSceneDesigner(null,new(){Background=dialog.FileName,Effect="Snow",Accent="#CCE9FF",Realtime=true});
    }
    void CustomizeSceneClick(object sender,RoutedEventArgs e)
    {
        if(Library.SelectedItem is not Wallpaper scene)return;
        if(scene.PresetId==CityId){Status.Text="Настройки города находятся в разделе «Эксклюзивы».";return;}
        var layers=LayersFor(scene)?.Copy()??new SceneLayers();
        string source=Path.ChangeExtension(scene.Path,".png");
        layers.Background=layers.Background??(File.Exists(source)?source:FullResolutionCover(scene));
        if(layers.Background is null||!File.Exists(layers.Background)){Status.Text="Для сцены нужно изображение. Нажми «Создать сцену».";return;}
        if(!layers.Realtime)
        {
            layers.Effect="Stars";layers.Accent="#D6E9FF";layers.Clock=scene.Clock;
            string catalog=Path.Combine(Path.GetDirectoryName(scene.Path)!,"catalog.json");
            if(File.Exists(catalog))try
            {
                using var document=JsonDocument.Parse(File.ReadAllText(catalog));
                foreach(var entry in document.RootElement.EnumerateArray())if(entry.TryGetProperty("id",out var id)&&id.GetInt32()==scene.PresetId)
                {
                    string effect=entry.TryGetProperty("effect",out var effectValue)?effectValue.GetString()??"stars":"stars";
                    layers.Effect=effect switch{"snow" or "aurora"=>"Snow","rain"=>"Rain","water"=>"Water","lightning" or "energy"=>"Lightning","petals"=>"Petals","leaves"=>"Leaves","embers"=>"Embers","mist"=>"Mist","fireflies"=>"Fireflies",_=>"Stars"};
                    layers.Accent=entry.TryGetProperty("accent",out var accentValue)?accentValue.GetString()??"#D6E9FF":"#D6E9FF";
                    if(layers.Effect=="Water")layers.RegionTop=entry.TryGetProperty("waterline",out var line)?line.GetDouble():.6;
                    break;
                }
            }
            catch(JsonException){}
        }
        OpenSceneDesigner(scene,layers);
    }
    void OpenSceneDesigner(Wallpaper? scene,SceneLayers layers)
    {
        StopHover();StopPreview();
        try
        {
            var editor=new SceneDesigner(scene?.Name??Path.GetFileNameWithoutExtension(layers.Background!),layers){Owner=this};
            editor.SceneName.IsReadOnly=scene?.PresetId is not null;
            if(editor.ShowDialog()!=true)return;
            var result=editor.Result.Copy();
            string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","scenes",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            string CopyAsset(string source,string name){string destination=Path.Combine(folder,name+Path.GetExtension(source).ToLowerInvariant());File.Copy(source,destination);return destination;}
            result.Background=CopyAsset(result.Background!,"background");if(result.Foreground is not null)result.Foreground=CopyAsset(result.Foreground,"foreground");
            if(scene is null){category="Все сцены";SearchBox.Clear();FormatPicker.SelectedIndex=0;MoodPicker.SelectedIndex=0;AddLocalScene(result.Background,editor.SceneTitle,result.Background,result);scene=items.First(s=>s.Path==result.Background);}
            else
            {
                preferences.Layers[PlaybackRules.Key(scene)]=result;
                if(scene.PresetId is null){preferences.ImportNames[scene.Path]=editor.SceneTitle;preferences.ImportThumbnails[scene.Path]=result.Background;var renamed=scene with{Name=editor.SceneTitle,Thumbnail=result.Background};items[items.IndexOf(scene)]=renamed;scene=renamed;}
                Save();
            }
            RefreshFilter();Library.SelectedItem=scene;PresentScene(scene);ApplyScene(scene,SelectedScreen);Status.Text="Живая сцена сохранена · "+scene.Name;
        }
        catch(Exception error){Status.Text="Редактор: "+error.Message;}
    }
    void ResetSceneClick(object sender,RoutedEventArgs e)
    {
        if(Library.SelectedItem is not Wallpaper scene||scene.PresetId is null)return;
        preferences.Layers.Remove(PlaybackRules.Key(scene));Save();RefreshFilter();PresentScene(scene);ApplyScene(scene,SelectedScreen);
    }
}
