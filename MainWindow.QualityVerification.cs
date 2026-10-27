using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
namespace VWP;
public partial class MainWindow
{
    async Task VerifyQualityStudio()
    {
        string folder=Path.Combine(AppContext.BaseDirectory,"verification");Directory.CreateDirectory(folder);
        try
        {
            featureTimer.Stop();preferences.CheckUpdates=false;preferences.SceneAccent=false;preferences.HoverPreview=false;ThemeService.Apply("Dark","#A5C6FF");
            var originals=items.Where(s=>StudioProject.IsProject(s.Path)&&s.Category=="Оригиналы VWP").ToArray();if(originals.Length<3)throw new Exception("Curated originals are missing.");
            SelectCategory("Оригиналы VWP");
            foreach(var scene in originals)
            {
                StudioProject.Load(scene.Path).Validate();Library.SelectedItem=scene;PresentScene(scene);UpdateLayout();
                if(VideoEditorButton.IsVisible||RestoreOriginalButton.IsVisible||!CustomizeButton.IsEnabled)throw new Exception("GPU scene actions are inconsistent.");
            }
            Library.SelectedItem=originals.First(s=>s.Path.Contains("silver-shore",StringComparison.Ordinal));
            foreach(var size in new[]{new Size(980,640),new Size(1160,760),new Size(1480,900)})
            {
                Width=size.Width;Height=size.Height;await Task.Delay(250);UpdateLayout();if(Hero.ActualWidth<200||Library.ActualWidth<300)throw new Exception("Originals library is too narrow.");RenderVerification("quality-library-"+(int)size.Width);
            }
            SelectCategory("Все сцены");if(items.Count(s=>s.PresetId is not null)<151)throw new Exception("Legacy wallpaper library was reduced.");
            var source=StudioProject.Load(originals[0].Path);var first=source.CloneTo(Path.Combine(StudioProject.DataRoot,"qa-personal-"+Guid.NewGuid().ToString("N")));var personal=RegisterGpuEdit(first,originals[0]);
            string originalKey=PlaybackRules.Key(personal);preferences.Favorites.Add(originalKey);preferences.RecentScenes.Add(originalKey);var config=Config(SelectedScreen);config.Scene=originalKey;config.Playlist.Add(originalKey);
            int count=items.Count;var next=first.CloneTo(Path.Combine(StudioProject.DataRoot,"qa-edit-"+Guid.NewGuid().ToString("N")));var edited=RegisterGpuEdit(next,personal);string nextKey=PlaybackRules.Key(edited);
            if(items.Count!=count||!preferences.Favorites.Contains(nextKey)||!preferences.RecentScenes.Contains(nextKey)||config.Scene!=nextKey||!config.Playlist.Contains(nextKey)||!preferences.Imports.Contains(next.FileName))throw new Exception("Editing a personal scene lost library references or created a duplicate.");
            if(!File.Exists(source.FileName)||!items.Contains(originals[0]))throw new Exception("Editing a personal scene changed the built-in original.");
            using(var surface=new StudioSurface())
            {
                var preview=new Window{Title="VWP · GPU verification",Width=640,Height=400,ShowInTaskbar=false,ShowActivated=false,Owner=this,Content=new System.Windows.Forms.Integration.WindowsFormsHost{Child=surface.Control}};
                try
                {
                    preview.Show();await surface.Initialize(StudioProject.Load(originals.First(s=>s.StudioId=="silver-shore").Path),"Eco",true);await Task.Delay(1200);
                    var stats=await surface.ReadStatistics();if(stats.Frames<2||stats.Width<300||stats.Height<150)throw new Exception("The application did not render its packaged GPU scene.");
                    await surface.Capture(Path.Combine(folder,"quality-native.png"));
                }
                finally{preview.Close();}
            }
            File.WriteAllText(Path.Combine(folder,"quality-ui-status.txt"),"PASS: three editable GPU originals; relevant actions; library at 980/1160/1480; legacy catalogue preserved; personal edits retain favorites/playlist/selection without duplicates; packaged application renders native GPU scene.");
        }
        catch(Exception error){File.WriteAllText(Path.Combine(folder,"quality-ui-status.txt"),error.ToString());Environment.ExitCode=1;}
        finally{exiting=true;Close();}
    }
}
