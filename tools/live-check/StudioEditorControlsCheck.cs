using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using VWP;

internal static class StudioEditorControlsCheck
{
    public static void Run()
    {
        string folder=Path.GetFullPath("artifacts/studio-editor-controls-check");Directory.CreateDirectory(folder);
        var app=new Application();app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/VWP;component/StudioResources.xaml",UriKind.Relative)});
        ThemeService.Apply("Dark","#A5C6FF");StudioEditor? editor=null;
        try
        {
            string assets=Path.GetFullPath("assets");var source=StudioLibrary.Read(assets).Select(e=>StudioLibrary.Load(StudioLibrary.FileName(assets,e))).First(p=>p.Weather.AfterLayer>0&&p.Layers.Any(l=>l.Motion=="character"));
            editor=new StudioEditor(source);
            var field=typeof(StudioEditor).GetField("working",BindingFlags.Instance|BindingFlags.NonPublic)!;var working=(StudioProject)field.GetValue(editor)!;
            int before=working.Weather.AfterLayer;var count=(Slider)editor.FindName("ParticleCount");count.Value=count.Value+1;
            if(working.Weather.AfterLayer!=before)throw new Exception("Changing weather count moved precipitation between layers.");
            var kind=(ComboBox)editor.FindName("Weather");kind.SelectedIndex=kind.SelectedIndex==1?4:1;
            if(working.Weather.AfterLayer!=before)throw new Exception("Changing precipitation kind lost foreground occlusion.");
            var placement=(ComboBox)editor.FindName("WeatherPlacement");placement.SelectedIndex=0;
            if(working.Weather.AfterLayer!=-1)throw new Exception("Front placement failed.");
            placement.SelectedIndex=1;int subject=working.Layers.FindIndex(l=>l.Motion=="character");
            if(working.Weather.AfterLayer!=subject-1)throw new Exception("Behind placement must follow every background material and precede the subject.");
            File.WriteAllText(Path.Combine(folder,"status.txt"),"PASS: actual editor count/kind controls preserve multi-layer weather occlusion; placement respects background material layers and character foreground.");
        }
        catch(Exception error){File.WriteAllText(Path.Combine(folder,"status.txt"),error.ToString());Environment.ExitCode=1;}
        finally{if(editor is not null){typeof(StudioEditor).GetField("dirty",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(editor,false);editor.Close();}app.Shutdown();}
    }
}
