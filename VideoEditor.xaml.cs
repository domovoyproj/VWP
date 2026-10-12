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
    string? input,name,previewInput;
    CancellationTokenSource? cancellation;
    public event Action<string,string>? Saved;
    public VideoEditor()
    {
        InitializeComponent();Video.MediaEnded+=(_,_)=>{Video.Position=TimeSpan.Zero;Video.Play();};
        Video.MediaFailed+=(_,_)=>{Video.Visibility=Visibility.Collapsed;Status.Text="Превью не поддерживается Windows. Экспорт через FFmpeg доступен.";};
        Video.MediaOpened+=(_,_)=>{Video.Visibility=Visibility.Visible;if(Video.NaturalDuration.HasTimeSpan && Video.Source?.LocalPath==previewInput)End.Text=Video.NaturalDuration.TimeSpan.TotalSeconds.ToString("0.###",CultureInfo.InvariantCulture);};
        PreviewButton.Click+=async(_,_)=>await Render(false);ExportButton.Click+=async(_,_)=>await Render(true);CancelButton.Click+=(_,_)=>cancellation?.Cancel();
    }
    public void Open(Wallpaper scene)
    {
        Close();input=scene.Path;name=scene.Name;SceneName.Text=name;Start.Text="0";End.Text="6";Speed.Text="1";Seam.Text="0.5";Brightness.Value=0;Contrast.Value=1;Saturation.Value=1;
        Poster.Source=scene.Thumbnail is string cover && File.Exists(cover)?new System.Windows.Media.Imaging.BitmapImage(new Uri(cover)):null;
        string proxy=Path.ChangeExtension(input,".preview.mp4");
        previewInput=scene.PresetId is not null && File.Exists(proxy)?proxy:input;
        Video.Source=new Uri(previewInput);Video.Play();Status.Text="Исходное видео сохраняется. Результат добавится отдельной сценой.";
    }
    internal VideoRecipe ReadRecipe()
    {
        double Read(string value)=>double.Parse(value.Replace(',','.'),CultureInfo.InvariantCulture);
        return new(){Start=Read(Start.Text),End=Read(End.Text),Speed=Read(Speed.Text),Brightness=Brightness.Value,Contrast=Contrast.Value,Saturation=Saturation.Value,Seam=Read(Seam.Text)};
    }
    internal async Task Render(bool export)
    {
        if(input is null||cancellation is not null)return;
        string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP",export?"library":"editor");Directory.CreateDirectory(folder);
        string output=Path.Combine(folder,Guid.NewGuid().ToString("N")+".mp4");
        try
        {
            var recipe=ReadRecipe();cancellation=new();var cancel=cancellation.Token;PreviewButton.IsEnabled=false;ExportButton.IsEnabled=false;CancelButton.Visibility=Visibility.Visible;Video.Stop();Video.Source=null;Video.Visibility=Visibility.Collapsed;
            await VideoRender.Run(input,output,recipe,PerformanceProfile.Resolve(export?"Quality":"Balance"),new Progress<double>(value=>Status.Text=$"Обработка · {value:0}%"),cancellation.Token);
            if(export){string thumbnail=Path.ChangeExtension(output,".jpg");await VideoRender.Thumbnail(output,thumbnail,cancel);cancel.ThrowIfCancellationRequested();Saved?.Invoke(output,(name??"Видео")+" · Edit");Status.Text="Новая сцена добавлена в библиотеку.";}
            else{Video.Source=new Uri(output);Video.Play();Status.Text="Готовый цикл. Проверьте переход при повторе.";}
        }
        catch(OperationCanceledException){if(File.Exists(output))File.Delete(output);if(File.Exists(Path.ChangeExtension(output,".jpg")))File.Delete(Path.ChangeExtension(output,".jpg"));Status.Text="Обработка отменена.";}
        catch(Exception e){if(File.Exists(output))File.Delete(output);if(File.Exists(Path.ChangeExtension(output,".jpg")))File.Delete(Path.ChangeExtension(output,".jpg"));Status.Text="Редактор: "+e.Message;}
        finally{cancellation?.Dispose();cancellation=null;PreviewButton.IsEnabled=true;ExportButton.IsEnabled=true;CancelButton.Visibility=Visibility.Collapsed;}
    }
    public void Close(){cancellation?.Cancel();Video.Stop();Video.Source=null;Video.Visibility=Visibility.Collapsed;}
}
