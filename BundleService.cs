using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
namespace VWP;
public sealed class BundleScene
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Name {get;set;}="";
    public string Video {get;set;}="";
    public string? Cover {get;set;}
    public SceneLayers? Layers {get;set;}
}
public sealed class BundleManifest
{
    public int Format {get;set;}=1;
    public string Name {get;set;}="";
    public string Author {get;set;}="";
    public List<BundleScene> Scenes {get;set;}=new();
    public MonitorPreferences Settings {get;set;}=new();
    public Dictionary<string,string> Hashes {get;set;}=new();
}
public static class BundleService
{
    public static async Task Export(string path,string name,string author,IEnumerable<Wallpaper> scenes,MonitorPreferences config,Func<Wallpaper,SceneLayers?> layers)
    {
        await Task.Run(()=>{
            var manifest=new BundleManifest{Name=name,Author=author,Settings=JsonSerializer.Deserialize<MonitorPreferences>(JsonSerializer.Serialize(config))!};
            var mapping=new Dictionary<string,string>();
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
            using(var archive=ZipFile.Open(temporary,ZipArchiveMode.Create))
            {
                string Add(string file,string suffix,string id)
                {
                    string entry="scenes/"+id+"/"+suffix+Path.GetExtension(file).ToLowerInvariant();archive.CreateEntryFromFile(file,entry,CompressionLevel.Fastest);
                    using var input=File.OpenRead(file);manifest.Hashes[entry]=Convert.ToHexString(SHA256.HashData(input));return entry;
                }
                foreach(var scene in scenes.DistinctBy(PlaybackRules.Key))
                {
                    if(!File.Exists(scene.Path))throw new FileNotFoundException(scene.Name);
                    var item=new BundleScene{Name=scene.Name};
                    if(StudioProject.IsProject(scene.Path))
                    {
                        manifest.Format=2;var original=StudioProject.Load(scene.Path);var project=original.Copy();int resource=0;
                        foreach(string asset in original.Assets()){string added=Add(original.Asset(asset),"resource-"+(resource++),item.Id);project.ReplaceAsset(asset,Path.GetFileName(added));}
                        item.Video="scenes/"+item.Id+"/scene.vwpscene";
                        byte[] json=JsonSerializer.SerializeToUtf8Bytes(project,StudioProject.Json);using(var output=archive.CreateEntry(item.Video).Open())output.Write(json);
                        manifest.Hashes[item.Video]=Convert.ToHexString(SHA256.HashData(json));
                    }
                    else item.Video=Add(scene.Path,"video",item.Id);
                    if(!StudioProject.IsProject(scene.Path)&&scene.Thumbnail is not null && File.Exists(scene.Thumbnail))item.Cover=Add(scene.Thumbnail,"cover",item.Id);
                    var layered=StudioProject.IsProject(scene.Path)?null:layers(scene);
                    if(layered?.Background is not null && File.Exists(layered.Background))
                    {
                        item.Layers=layered.Copy();item.Layers.Background=Add(layered.Background,"background",item.Id);
                        item.Layers.Foreground=layered.Foreground is not null && File.Exists(layered.Foreground)?Add(layered.Foreground,"foreground",item.Id):null;
                    }
                    manifest.Scenes.Add(item);mapping[PlaybackRules.Key(scene)]=item.Id;
                }
                manifest.Settings.Scene=config.Scene is not null?mapping.GetValueOrDefault(config.Scene):null;
                manifest.Settings.Playlist=config.Playlist.Where(mapping.ContainsKey).Select(key=>mapping[key]).ToList();
                using var writer=new StreamWriter(archive.CreateEntry("manifest.json").Open());writer.Write(JsonSerializer.Serialize(manifest));
            }
            File.Move(temporary,path,true);
            }
            finally{try{File.Delete(temporary);}catch{}}
        });
    }
    public static async Task<BundleManifest> Import(string path,string folder)
    {
        return await Task.Run(()=>{
            using var archive=ZipFile.OpenRead(path);
            if(archive.Entries.Count>1000||archive.Entries.Sum(e=>e.Length)>8L*1024*1024*1024)throw new InvalidDataException("Набор слишком большой.");
            var entry=archive.GetEntry("manifest.json")??throw new InvalidDataException("Нет манифеста VWP.");if(entry.Length>1024*1024)throw new InvalidDataException("Большой манифест.");
            using var stream=entry.Open();var manifest=JsonSerializer.Deserialize<BundleManifest>(stream)??throw new InvalidDataException("Манифест повреждён.");
            if(manifest.Format is not (1 or 2)||manifest.Scenes.Count==0)throw new InvalidDataException("Формат набора не поддерживается.");
            if(manifest.Scenes.Count>200 || manifest.Scenes.Select(s=>s.Id).Distinct().Count()!=manifest.Scenes.Count)throw new InvalidDataException("Неверный список сцен.");
            foreach(var scene in manifest.Scenes)
                if(scene.Name.Length>256||scene.Layers is not null && (!scene.Layers.ValidProperties()||!Regex.IsMatch(scene.Layers.Accent,"^#[0-9a-fA-F]{6}$")||scene.Layers.MotionId is <0 or >=SpatialScene.SceneCount))throw new InvalidDataException("Неверные настройки сцены.");
            var settings=manifest.Settings;
            if(!double.IsFinite(settings.Depth)||settings.Depth<0||settings.Depth>1||!double.IsFinite(settings.FocusX)||settings.FocusX<0||settings.FocusX>1||!double.IsFinite(settings.FocusY)||settings.FocusY<0||settings.FocusY>1||settings.Volume<0||settings.Volume>100||settings.IntervalMinutes<1||settings.IntervalMinutes>1440)
                throw new InvalidDataException("Настройки набора выходят за допустимые границы.");
            if(archive.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=archive.Entries.Count)throw new InvalidDataException("Повторяющиеся файлы.");
            Directory.CreateDirectory(folder);string root=Path.GetFullPath(folder)+Path.DirectorySeparatorChar;
            string Extract(string relative,long maxBytes=4L*1024*1024*1024)
            {
                string full=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
                if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||relative.Contains(':')||!manifest.Hashes.TryGetValue(relative,out string? digest))throw new InvalidDataException("Неверный путь файла.");
                var file=archive.GetEntry(relative)??throw new InvalidDataException("Нет файла "+relative);
                if((file.ExternalAttributes>>16 & 0xF000)==0xA000)throw new InvalidDataException("Ссылки в наборе запрещены.");
                if(file.Length>maxBytes)throw new InvalidDataException("Файл превышает допустимый размер.");
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                using(var input=file.Open())using(var output=File.Create(full+".tmp"))input.CopyTo(output);
                string actual;using(var input=File.OpenRead(full+".tmp"))actual=Convert.ToHexString(SHA256.HashData(input));
                if(!actual.Equals(digest,StringComparison.OrdinalIgnoreCase)){File.Delete(full+".tmp");throw new InvalidDataException("Контрольная сумма не совпала.");}
                File.Move(full+".tmp",full,true);return full;
            }
            foreach(var scene in manifest.Scenes)
            {
                string extension=Path.GetExtension(scene.Video).ToLowerInvariant();
                if(extension==".vwpscene"&&manifest.Format==2)
                {
                    string relative=scene.Video;scene.Video=Extract(relative,256*1024);var project=StudioProject.Load(scene.Video,false);
                    foreach(string asset in project.Assets())
                    {
                        string assetPath=project.Asset(asset);string archivePath=Path.GetRelativePath(root,assetPath).Replace(Path.DirectorySeparatorChar,'/');Extract(archivePath,64L*1024*1024);
                    }
                    project.Validate();scene.Cover=project.Asset(project.Cover);continue;
                }
                if(!new[]{".mp4",".webm",".mkv",".mov",".avi"}.Contains(extension)&&!(scene.Layers?.Realtime==true&&new[]{".png",".jpg",".jpeg",".bmp"}.Contains(extension)))throw new InvalidDataException("Неподдерживаемый файл сцены.");
                scene.Video=Extract(scene.Video);
                if(scene.Cover is not null)scene.Cover=Extract(scene.Cover);
                if(scene.Layers?.Background is not null){scene.Layers.Background=Extract(scene.Layers.Background);if(scene.Layers.Foreground is not null)scene.Layers.Foreground=Extract(scene.Layers.Foreground);}
            }
            return manifest;
        });
    }
}
