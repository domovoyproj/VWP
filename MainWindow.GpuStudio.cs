using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
namespace VWP;
public partial class MainWindow
{
    StudioPlayer? studioPlayer;
    void LoadStudioOriginals(string assets)
    {
        string folder=Path.Combine(assets,"studio-originals");if(!Directory.Exists(folder))return;
        foreach(string file in Directory.EnumerateFiles(folder,"*.vwpscene",SearchOption.AllDirectories).OrderBy(f=>f))
        {
            try{var project=StudioProject.Load(file);items.Add(new(project.Name,file,project.Asset(project.Cover),"VWP ORIGINAL · ЖИВАЯ СЦЕНА","Оригиналы VWP",project.Description,IsSpatial:true,StudioId:project.Id));}
            catch(Exception error){Trace("Studio project: "+error.Message);}
        }
    }
    void OpenGpuEditor(Wallpaper scene)
    {
        StopPreview();StopHover();try{EditGpuProject(StudioLibrary.Load(scene.Path),scene);}catch(Exception error){Status.Text="Scene Studio: "+error.Message;}
    }
    void CreateGpuScene()
    {
        var dialog=new OpenFileDialog{Title="Фон новой сцены",Filter="Изображения|*.png;*.jpg;*.jpeg"};if(dialog.ShowDialog()!=true)return;
        string folder=Path.Combine(StudioProject.DataRoot,"staging-"+Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            string name="background"+Path.GetExtension(dialog.FileName);File.Copy(dialog.FileName,Path.Combine(folder,name));
            var project=new StudioProject{Name=Path.GetFileNameWithoutExtension(dialog.FileName),Cover=name,Layers=new(){new(){Name="Фон",Source=name,Rect=StudioProject.ImageRect(dialog.FileName,true),Motion="flow"}}};project.Save(Path.Combine(folder,"scene.vwpscene"));EditGpuProject(project);
        }
        catch(Exception error){Status.Text="Новая сцена: "+error.Message;}
        finally{StudioProject.DiscardWorkingCopy(folder);}
    }
    void OpenArtworkStudio(Wallpaper scene,SceneLayers layers)
    {
        StopPreview();StopHover();
        string folder=Path.Combine(StudioProject.DataRoot,"staging-"+Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            string CopyImage(string source,string name)
            {
                string extension=Path.GetExtension(source).ToLowerInvariant(),file=name+(new[]{".png",".jpg",".jpeg"}.Contains(extension)?extension:".png");
                if(new[]{".png",".jpg",".jpeg"}.Contains(extension))File.Copy(source,Path.Combine(folder,file));
                else{using var input=File.OpenRead(source);var decoder=System.Windows.Media.Imaging.BitmapDecoder.Create(input,System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(decoder.Frames[0]);using var output=File.Create(Path.Combine(folder,file));encoder.Save(output);}
                return file;
            }
            string background=CopyImage(layers.Background!,"background");var project=new StudioProject{Name=scene.Name,Description=scene.Description,Cover=background,Settings=new(){Clock=layers.Clock||scene.Clock,Parallax=Math.Clamp(layers.Parallax,0,1)},Layers=new(){new(){Name="Фон",Source=background,Rect=StudioProject.ImageRect(Path.Combine(folder,background),true),Motion="flow"}}};
            if(layers.Effect=="Water")
            {
                double line=Math.Clamp(layers.RegionTop,0,1);project.Layers[0].Waterline=line;project.Layers[0].Masks.Add(new(){Channel=0,Points=new(){new[]{0d,line},new[]{1d,line},new[]{1d,1d},new[]{0d,1d}}});
            }
            if(layers.Foreground is string foreground&&File.Exists(foreground)){string file=CopyImage(foreground,"foreground");project.Layers.Add(new(){Name="Передний план",Source=file,Depth=.7,Rect=StudioProject.ImageRect(Path.Combine(folder,file),false)});}
            project.Weather.Kind=layers.Effect switch{"Snow"=>"snow","Fireflies"=>"fireflies",_=>"none"};project.Weather.Count=project.Weather.Kind=="none"?0:Math.Clamp((int)(80*layers.Density),1,400);
            project.Save(Path.Combine(folder,"scene.vwpscene"));EditGpuProject(project);
        }
        catch(Exception error){Status.Text="Редактор: "+error.Message;}
        finally{StudioProject.DiscardWorkingCopy(folder);}
    }
    Wallpaper RegisterGpuEdit(StudioProject result,Wallpaper? previous)
    {
        if(previous is null||previous.StudioId is not null||!StudioProject.IsProject(previous.Path)){AddLocalScene(result.FileName,result.Name,result.Asset(result.Cover));return items.First(s=>s.Path==result.FileName);}
        string before=PlaybackRules.Key(previous),after=result.FileName;
        void Rewrite(System.Collections.Generic.List<string> values){for(int i=0;i<values.Count;i++)if(values[i]==before)values[i]=after;}
        Rewrite(preferences.Imports);if(!preferences.Imports.Contains(after))preferences.Imports.Add(after);
        preferences.ImportNames.Remove(previous.Path);preferences.ImportThumbnails.Remove(previous.Path);preferences.ImportNames[after]=result.Name;preferences.ImportThumbnails[after]=result.Asset(result.Cover);
        foreach(var config in preferences.Monitors.Values){if(config.Scene==before)config.Scene=after;Rewrite(config.Playlist);}
        Rewrite(preferences.Favorites);Rewrite(preferences.RecentScenes);if(preferences.Last==previous.Path)preferences.Last=after;
        if(preferences.Layers.Remove(before,out var legacy))preferences.Layers[after]=legacy;
        var updated=previous with{Name=result.Name,Path=after,Thumbnail=result.Asset(result.Cover),Description=result.Description,Subtitle="МОЯ ЖИВАЯ СЦЕНА",IsSpatial=true};items[items.IndexOf(previous)]=updated;Save();return updated;
    }
    void EditGpuProject(StudioProject project,Wallpaper? previous=null)
    {
        StopPreview();StopHover();var editor=new StudioEditor(project){Owner=this};
        if(editor.ShowDialog()!=true||editor.Result is not StudioProject result)return;
        bool personal=previous is not null&&previous.StudioId is null&&StudioProject.IsProject(previous.Path);
        var otherScreens=personal?System.Windows.Forms.Screen.AllScreens.Where(s=>s.DeviceName!=SelectedScreen.DeviceName&&Config(s).Scene==PlaybackRules.Key(previous!)&&hosts.GetValueOrDefault(s.DeviceName)?.Active==true).ToArray():Array.Empty<System.Windows.Forms.Screen>();
        if(personal){result.Id=project.Id;result.Save(result.FileName);}
        SelectCategory("Все сцены");SearchBox.Clear();FormatPicker.SelectedIndex=0;MoodPicker.SelectedIndex=0;
        var scene=RegisterGpuEdit(result,previous);RefreshFilter();Library.SelectedItem=scene;Library.ScrollIntoView(scene);ApplyScene(scene,SelectedScreen);foreach(var screen in otherScreens)ApplyScene(scene,screen);Status.Text="Сцена сохранена · "+scene.Name;
    }
    void PreviewGpuScene(Wallpaper scene)
    {
        StopPreview();StopHover();try{studioPlayer=new StudioPlayer(StudioLibrary.Load(scene.Path),Config(SelectedScreen).Performance){Owner=this};studioPlayer.Closed+=(_,_)=>studioPlayer=null;studioPlayer.Show();}catch(Exception error){Status.Text="Превью: "+error.Message;}
    }
}
