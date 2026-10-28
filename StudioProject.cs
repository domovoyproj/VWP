using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VWP;

public sealed class StudioProject
{
    internal static string DataRoot=>Environment.GetCommandLineArgs().AnyVerification()?Path.Combine(Environment.CurrentDirectory,"artifacts","studio-data"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","studio");
    internal static void DiscardWorkingCopy(string folder)
    {
        string root=Path.GetFullPath(DataRoot)+Path.DirectorySeparatorChar,full=Path.GetFullPath(folder),name=Path.GetFileName(full);
        if(name.StartsWith("staging-",StringComparison.Ordinal))name=name[8..];
        if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!Guid.TryParseExact(name,"N",out _)||!Directory.Exists(full))return;
        try{if((File.GetAttributes(full)&FileAttributes.ReparsePoint)==0)Directory.Delete(full,true);}catch{/* A browser may still be releasing a file; never remove unrelated data. */}
    }
    public int Schema {get;set;}=3;
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Name {get;set;}="Новая сцена";
    public string Description {get;set;}="";
    public string Author {get;set;}="VWP";
    public string Cover {get;set;}="background.png";
    public double Duration {get;set;}=24;
    public List<StudioLayer> Layers {get;set;}=new();
    public StudioSettings Settings {get;set;}=new();
    public StudioWeather Weather {get;set;}=new();
    [JsonIgnore] public string FileName {get;private set;}="";
    [JsonIgnore] string? bundledAssets;
    [JsonIgnore] public string Folder=>bundledAssets??Path.GetDirectoryName(FileName)!;
    public static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true,WriteIndented=true};
    public static bool IsProject(string path)=>Path.GetExtension(path).Equals(".vwpscene",StringComparison.OrdinalIgnoreCase);
    internal static double[] ImageRect(string file,bool fill)
    {
        CheckImage(file);
        using var input=File.OpenRead(file);var decoder=System.Windows.Media.Imaging.BitmapDecoder.Create(input,System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,System.Windows.Media.Imaging.BitmapCacheOption.None);var frame=decoder.Frames[0];
        double ratio=(double)frame.PixelWidth/frame.PixelHeight/(16d/9),height=fill?Math.Max(1.05,1.05/ratio):Math.Min(.9,.9/ratio),width=height*ratio;
        if(width>4||height>4)throw new InvalidDataException("У изображения слишком большое соотношение сторон. Обрежь его перед импортом.");
        return new[]{(1-width)/2,(1-height)/2,width,height};
    }
    internal static void CheckImage(string file)
    {
        if(!File.Exists(file)||new FileInfo(file).Length>64L*1024*1024)throw new InvalidDataException("Изображение отсутствует или превышает 64 МБ.");
        using var input=File.OpenRead(file);var decoder=System.Windows.Media.Imaging.BitmapDecoder.Create(input,System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,System.Windows.Media.Imaging.BitmapCacheOption.None);var frame=decoder.Frames[0];
        if(frame.PixelWidth>4096||frame.PixelHeight>4096)throw new InvalidDataException("Максимальный размер изображения — 4096 пикселей по каждой стороне.");
    }
    public static StudioProject Load(string file,bool requireAssets=true)
        =>Read(file,requireAssets,null);
    // Only the application catalogue can grant a shared, installed asset root.
    // This value is never read from project JSON or accepted by bundle import.
    internal static StudioProject LoadBundled(string file,string assets)=>Read(file,true,Path.GetFullPath(assets));
    static StudioProject Read(string file,bool requireAssets,string? assets)
    {
        if(new FileInfo(file).Length>256*1024)throw new InvalidDataException("Файл проекта превышает 256 КБ.");
        var project=JsonSerializer.Deserialize<StudioProject>(File.ReadAllText(file),Json)??throw new InvalidDataException("Проект повреждён.");
        project.FileName=Path.GetFullPath(file);project.bundledAssets=assets;project.Validate(requireAssets);return project;
    }
    public StudioProject Copy(){var copy=JsonSerializer.Deserialize<StudioProject>(JsonSerializer.Serialize(this,Json),Json)!;copy.FileName=FileName;copy.bundledAssets=bundledAssets;return copy;}
    public IEnumerable<string> Assets()=>new[]{Cover}.Concat(Layers.SelectMany(l=>new[]{l.Source}.Concat(l.MaskFiles.Values))).Concat(Weather.MaskFile is string mask?new[]{mask}:Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase);
    public string Asset(string relative)
    {
        if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.IndexOfAny(new[]{':','?','#','%','\0'})>=0)throw new InvalidDataException("Неверный путь ресурса сцены.");
        string root=Path.GetFullPath(Folder)+Path.DirectorySeparatorChar;
        string path=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
        if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Ресурс находится вне проекта.");
        if(!new[]{".png",".jpg",".jpeg"}.Contains(Path.GetExtension(path).ToLowerInvariant()))throw new InvalidDataException("Сцена поддерживает PNG и JPEG.");
        return path;
    }
    public void Validate(bool requireAssets=true)
    {
        if(Schema is not (2 or 3)||Name is null||Name.Length is <1 or >120||Description is null||Description.Length>1000||Id is null||Id.Length is <1 or >100||Settings is null||Weather is null||Layers is null||Layers.Any(l=>l is null)||Layers.Count is <1 or >12||!double.IsFinite(Duration)||Duration<4||Duration>120)throw new InvalidDataException("Неподдерживаемые параметры проекта.");
        if(Layers.Select(l=>l.Id).Distinct().Count()!=Layers.Count)throw new InvalidDataException("Идентификаторы слоёв должны различаться.");
        Settings.Validate();Weather.Validate();
        foreach(var layer in Layers)layer.Validate();
        foreach(string relative in Assets())
        {
            string path=Asset(relative);
            if(requireAssets&&(!File.Exists(path)||new FileInfo(path).Length>64L*1024*1024))throw new InvalidDataException("Ресурс отсутствует или слишком велик: "+relative);
            if(requireAssets)
            {
                using var input=File.OpenRead(path);var decoder=System.Windows.Media.Imaging.BitmapDecoder.Create(input,System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,System.Windows.Media.Imaging.BitmapCacheOption.None);var frame=decoder.Frames[0];
                if(frame.PixelWidth>4096||frame.PixelHeight>4096)throw new InvalidDataException("Ресурс превышает 4096 пикселей: "+relative);
            }
        }
    }
    public void ReplaceAsset(string before,string after)
    {
        if(Cover.Equals(before,StringComparison.OrdinalIgnoreCase))Cover=after;
        if(Weather.MaskFile?.Equals(before,StringComparison.OrdinalIgnoreCase)==true)Weather.MaskFile=after;
        foreach(var layer in Layers){if(layer.Source.Equals(before,StringComparison.OrdinalIgnoreCase))layer.Source=after;foreach(string key in layer.MaskFiles.Keys.ToArray())if(layer.MaskFiles[key].Equals(before,StringComparison.OrdinalIgnoreCase))layer.MaskFiles[key]=after;}
    }
    public StudioProject CloneTo(string folder)
    {
        Validate();Directory.CreateDirectory(folder);var copy=Copy();
        int index=0;foreach(string relative in Assets())
        {
            string name="resource-"+(index++)+Path.GetExtension(relative).ToLowerInvariant();File.Copy(Asset(relative),Path.Combine(folder,name));copy.ReplaceAsset(relative,name);
        }
        copy.Id=Guid.NewGuid().ToString("N");copy.Save(Path.Combine(folder,"scene.vwpscene"));return copy;
    }
    public void Save(string file)
    {
        FileName=Path.GetFullPath(file);bundledAssets=null;Schema=3;Validate();Directory.CreateDirectory(Folder);
        File.WriteAllText(FileName+".tmp",JsonSerializer.Serialize(this,Json));File.Move(FileName+".tmp",FileName,true);
    }
}

public sealed class StudioLayer
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Name {get;set;}="Слой";
    public string Source {get;set;}="";
    public Dictionary<string,string> MaskFiles {get;set;}=new();
    public bool Visible {get;set;}=true;
    public double[] Rect {get;set;}=new[]{0d,0,1,1};
    public double Opacity {get;set;}=1;
    public bool ClipToSurface {get;set;}
    public double Depth {get;set;}=.1;
    public string Motion {get;set;}="still";
    public string Surface {get;set;}="water";
    public string Light {get;set;}="warm";
    public string Tint {get;set;}="#B5D7F5";
    public int Seed {get;set;}
    public double SurfaceStrength {get;set;}=1;
    public double LightStrength {get;set;}=1;
    public string Anchor {get;set;}="bottom";
    public double Strength {get;set;}=1;
    public double Waterline {get;set;}=.48;
    public double[] Sun {get;set;}=new[]{.75,.4};
    public List<StudioMask> Masks {get;set;}=new();
    public void Validate()
    {
        if(Id is null||Id.Length is <1 or >100||Name is null||Name.Length>120||Rect is null||Rect.Length!=4||Rect.Any(v=>!double.IsFinite(v))||Rect[0]<-2||Rect[1]<-2||Rect[0]>2||Rect[1]>2||Rect[2]<=0||Rect[3]<=0||Rect[2]>4||Rect[3]>4||!double.IsFinite(Opacity)||Opacity<0||Opacity>1||!double.IsFinite(Depth)||Depth<0||Depth>2||!double.IsFinite(Strength)||Strength<0||Strength>3||Sun is null||Sun.Length!=2||Sun.Any(v=>!double.IsFinite(v)||v<0||v>1))throw new InvalidDataException("Неверные свойства слоя.");
        if(!new[]{"still","flow","foliage","cloth","character","rain","landscape"}.Contains(Motion)||!new[]{"top","bottom","center"}.Contains(Anchor))throw new InvalidDataException("Неподдерживаемая анимация слоя.");
        if(!new[]{"water","ocean","rain","mist","aurora","cosmic","glass","energy","waterfall","mirror"}.Contains(Surface)||!new[]{"warm","cool","stars","lightning","fire"}.Contains(Light)||Tint is null||!System.Text.RegularExpressions.Regex.IsMatch(Tint,"^#[0-9a-fA-F]{6}$")||Seed is <0 or >65535||!double.IsFinite(SurfaceStrength)||SurfaceStrength is <0 or >2||!double.IsFinite(LightStrength)||LightStrength is <0 or >2)throw new InvalidDataException("Неверные свойства поверхности и света.");
        if(MaskFiles is null||MaskFiles.Count>4||MaskFiles.Keys.Any(k=>!new[]{"water","clouds","wind","light"}.Contains(k))||!double.IsFinite(Waterline)||Waterline<0||Waterline>1||Masks is null||Masks.Any(m=>m is null)||Masks.Count>32)throw new InvalidDataException("Неверные маски слоя.");foreach(var mask in Masks)mask.Validate();
    }
}

public sealed class StudioMask
{
    public int Channel {get;set;}
    public double Feather {get;set;}=.02;
    public bool Erase {get;set;}
    public List<double[]> Points {get;set;}=new();
    public void Validate()
    {
        if(Channel<0||Channel>3||!double.IsFinite(Feather)||Feather<0||Feather>.2||Points is null||Points.Count is <3 or >100||Points.Any(p=>p is null||p.Length!=2||p.Any(v=>!double.IsFinite(v)||v<0||v>1)))throw new InvalidDataException("Неверная маска эффекта.");
    }
}

public sealed class StudioSettings
{
    public double Exposure {get;set;}
    public double Bloom {get;set;}=.12;
    public double Motion {get;set;}=1;
    public double Wind {get;set;}=.7;
    public double Parallax {get;set;}=.4;
    public bool AudioReactive {get;set;}
    public bool Clock {get;set;}
    public void Validate()
    {
        if(!double.IsFinite(Exposure)||Exposure< -1||Exposure>1||!double.IsFinite(Bloom)||Bloom<0||Bloom>.4||!double.IsFinite(Motion)||Motion<0||Motion>2||!double.IsFinite(Wind)||Wind<0||Wind>2||!double.IsFinite(Parallax)||Parallax<0||Parallax>1)throw new InvalidDataException("Настройки сцены выходят за допустимые границы.");
    }
    public StudioSettings Copy()=>JsonSerializer.Deserialize<StudioSettings>(JsonSerializer.Serialize(this,StudioProject.Json),StudioProject.Json)!;
}

public sealed class StudioWeather
{
    public string Kind {get;set;}="none";
    public int Count {get;set;}=80;
    public double[] Region {get;set;}=new[]{0d,0,1,1};
    public string Color {get;set;}="#EAF5FF";
    public string? MaskFile {get;set;}
    public List<StudioMask> Masks {get;set;}=new();
    public int AfterLayer {get;set;}=-1;
    public void Validate()
    {
        if(!new[]{"none","snow","dust","fireflies","rain","petals","leaves","embers","meteors","bubbles"}.Contains(Kind)||Count<0||Count>400||Region is null||Region.Length!=4||Region.Any(v=>!double.IsFinite(v)||v<0||v>1)||Region[2]<=0||Region[3]<=0||Color is null||!System.Text.RegularExpressions.Regex.IsMatch(Color,"^#[0-9a-fA-F]{6}$")||AfterLayer is < -1 or >11||Masks is null||Masks.Count>64||Masks.Any(m=>m is null||m.Channel!=0))throw new InvalidDataException("Неверные параметры частиц.");
        foreach(var mask in Masks)mask.Validate();
    }
}

internal sealed record StudioQuality(int MaxHeight,int Fps,double Particles,bool Bloom)
{
    internal static StudioQuality Resolve(string key,bool preview=false)=>preview?new(720,30,.5,false):key switch{"Eco"=>new(720,30,.35,false),"Quality"=>new(2160,60,1,true),_=>new(1080,60,.7,true)};
}
