using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace VWP;
public partial class VideoEditor : UserControl
{
    string? input,name;
    CancellationTokenSource? cancellation;
    public event Action<string,string>? Saved;
    public VideoEditor()
    {
        InitializeComponent();Video.MediaEnded+=(_,_)=>{Video.Position=TimeSpan.Zero;Video.Play();};
        Video.MediaFailed+=(_,_)=>Status.Text="Превью не поддерживается Windows. Экспорт через FFmpeg доступен.";
        Video.MediaOpened+=(_,_)=>{if(Video.NaturalDuration.HasTimeSpan && Video.Source?.LocalPath==input)End.Text=Video.NaturalDuration.TimeSpan.TotalSeconds.ToString("0.###",CultureInfo.InvariantCulture);};
        PreviewButton.Click+=async(_,_)=>await Render(false);ExportButton.Click+=async(_,_)=>await Render(true);CancelButton.Click+=(_,_)=>cancellation?.Cancel();
    }
    public void Open(Wallpaper scene)
    {
        Close();input=scene.Path;name=scene.Name;SceneName.Text=name;Start.Text="0";End.Text="6";Speed.Text="1";Seam.Text="0.5";Brightness.Value=0;Contrast.Value=1;Saturation.Value=1;
        Video.Source=new Uri(input);Video.Play();Status.Text="Исходное видео сохраняется. Результат добавится отдельной сценой.";
    }
    internal VideoRecipe ReadRecipe()
    {
        double Read(string value)=>double.Parse(value.Replace(',','.'),CultureInfo.InvariantCulture);
        return new(){Start=Read(Start.Text),End=Read(End.Text),Speed=Read(Speed.Text),Brightness=Brightness.Value,Contrast=Contrast.Value,Saturation=Saturation.Value,Seam=Read(Seam.Text)};
    }
    async Task Render(bool export)
    {
        if(input is null||cancellation is not null)return;
        string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP",export?"library":"editor");Directory.CreateDirectory(folder);
        string output=Path.Combine(folder,Guid.NewGuid().ToString("N")+".mp4");
        try
        {
            var recipe=ReadRecipe();cancellation=new();PreviewButton.IsEnabled=false;ExportButton.IsEnabled=false;CancelButton.Visibility=Visibility.Visible;Video.Stop();Video.Source=null;
            await VideoRender.Run(input,output,recipe,PerformanceProfile.Resolve("Balance"),new Progress<double>(value=>Status.Text=$"Обработка · {value:0}%"),cancellation.Token);
            if(export){string thumbnail=Path.ChangeExtension(output,".jpg");await VideoRender.Thumbnail(output,thumbnail);Saved?.Invoke(output,(name??"Видео")+" · Edit");Status.Text="Новая сцена добавлена в библиотеку.";}
            else{Video.Source=new Uri(output);Video.Play();Status.Text="Готовый цикл. Проверьте переход при повторе.";}
        }
        catch(OperationCanceledException){if(File.Exists(output))File.Delete(output);Status.Text="Обработка отменена.";}
        catch(Exception e){if(File.Exists(output))File.Delete(output);Status.Text="Редактор: "+e.Message;}
        finally{cancellation?.Dispose();cancellation=null;PreviewButton.IsEnabled=true;ExportButton.IsEnabled=true;CancelButton.Visibility=Visibility.Collapsed;}
    }
    public void Close(){cancellation?.Cancel();Video.Stop();Video.Source=null;}
}
