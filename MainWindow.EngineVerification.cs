using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
namespace VWP;
public partial class MainWindow
{
    async Task VerifyEngineStudio()
    {
        string folder=Path.Combine(AppContext.BaseDirectory,"verification");Directory.CreateDirectory(folder);
        try
        {
            featureTimer.Stop();preferences.CheckUpdates=false;preferences.HoverPreview=false;preferences.SceneAccent=false;ThemeService.Apply("Dark","#A3BCF7");
            category="Деревня · Зима";RefreshFilter();Library.SelectedItem=items.First(s=>s.PresetId==1701);
            foreach(var size in new[]{new Size(1160,760),new Size(1480,900),new Size(980,640)})
            {
                Width=size.Width;Height=size.Height;await Task.Delay(250);UpdateLayout();
                if(Hero.ActualWidth<200||Library.ActualWidth<300)throw new Exception("Library/preview width at "+size);
                RenderVerification("engine-library-"+(int)size.Width);
            }
            File.WriteAllText(Path.Combine(folder,"engine-ui-status.txt"),"PASS: library rendered at 980, 1160 and 1480 pixels; preview and library have usable widths.");
        }
        catch(Exception error){File.WriteAllText(Path.Combine(folder,"engine-ui-status.txt"),error.ToString());Environment.ExitCode=1;}
        finally{exiting=true;Close();}
    }
}
