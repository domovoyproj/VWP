using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace VWP;
public partial class MainWindow
{
    async Task VerifyRedesign()
    {
        string folder=Path.Combine(AppContext.BaseDirectory,"verification");Directory.CreateDirectory(folder);
        var checks=new List<object>();featureTimer.Stop();
        void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new InvalidOperationException(name);}
        void Capture(FrameworkElement visual,string name){visual.UpdateLayout();var image=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth),(int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(folder,name+".png"));encoder.Save(file);}
        Rect Bounds(FrameworkElement element)=>element.TransformToAncestor((Visual)Content).TransformBounds(new Rect(0,0,element.ActualWidth,element.ActualHeight));
        try
        {
            await Task.Delay(400);preferences.Theme="Dark";UpdateTheme();Width=Math.Min(980,launchArea.Width-24);Height=Math.Min(640,launchArea.Height-24);await Task.Delay(150);UpdateLayout();Capture((FrameworkElement)Content,"design-compact");
            Check("profile button visible in compact mode",Bounds(QualityButton).Right<=ActualWidth&&QualityButton.ActualWidth>100);
            Check("playback controls do not overlap display selector",Bounds(Monitor).Right+8<Bounds(PauseButton).Left);
            Check("filters do not overlap count",Bounds(MoodPicker).Right+4<=Bounds(CountLabel).Left);
            Check("vector pause glyph is retained",PauseButton.Content is System.Windows.Controls.StackPanel&&PauseGlyph.ActualHeight>0);
            Check("compact stays in working area",Left>=launchArea.Left-1&&Top>=launchArea.Top-1&&Left+ActualWidth<=launchArea.Right+1&&Top+ActualHeight<=launchArea.Bottom+1);
            foreach(string mode in new[]{"Eco","Balance","Quality","Auto"})
            {
                ApplyQuickQuality(mode);
                Check("global "+mode+" policy",System.Windows.Forms.Screen.AllScreens.All(s=>PerformancePolicy.Mode(Config(s))==mode));
                Check("video "+mode+" frame budget",PerformanceProfile.Resolve(Config(SelectedScreen).Performance).Fps==(mode=="Eco"?15:mode=="Quality"?60:30));
            }
            QualityClick(this,new());await Task.Delay(150);Capture((FrameworkElement)QualityPopup.Child,"design-quality");QualityPopup.IsOpen=false;
            preferences.Theme="Light";UpdateTheme();await Task.Delay(100);Capture((FrameworkElement)Content,"design-light");
            preferences.Theme="Dark";UpdateTheme();ModeClick(this,new());await Task.Delay(150);Capture((FrameworkElement)Content,"design-fullscreen");
            var screen=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).Bounds;var device=PresentationSource.FromVisual(this)!.CompositionTarget!.TransformFromDevice;
            Check("fullscreen fills the screen",Math.Abs(Left-screen.Left*device.M11)<1&&Math.Abs(Top-screen.Top*device.M22)<1&&Math.Abs(ActualWidth-screen.Width*device.M11)<1&&Math.Abs(ActualHeight-screen.Height*device.M22)<1);ModeClick(this,new());
            NavigateAppearance("Themes");await Task.Delay(150);Capture((FrameworkElement)Content,"design-themes");NavigateAppearance("Cursors");await Task.Delay(150);Capture((FrameworkElement)Content,"design-cursors");
            var textHost=(System.Windows.Controls.ScrollViewer)Appearance.TryText.Template.FindName("PART_ContentHost",Appearance.TryText);
            Check("cursor demo text is not clipped",textHost.ViewportHeight>=Appearance.TryText.FontSize*1.3&&textHost.ViewportWidth>80);
            CityNavigation(this,new());await Task.Delay(100);Capture((FrameworkElement)Content,"design-city");
            Check("UI verification settings are isolated",settingsPath.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));
        }
        catch(Exception e){checks.Add(new{name="error",passed=false,error=e.ToString()});Environment.ExitCode=1;}
        finally{File.WriteAllText(Path.Combine(folder,"design-result.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));QuitClick(this,new());}
    }
}
