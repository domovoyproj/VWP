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
            using(var archive=ZipFile.Open(path+".tmp",ZipArchiveMode.Create))
            {
                string Add(string file,string suffix,string id)
                {
                    string entry="scenes/"+id+"/"+suffix+Path.GetExtension(file).ToLowerInvariant();archive.CreateEntryFromFile(file,entry,CompressionLevel.Fastest);
                    using var input=File.OpenRead(file);manifest.Hashes[entry]=Convert.ToHexString(SHA256.HashData(input));return entry;
                }
                foreach(var scene in scenes.DistinctBy(PlaybackRules.Key))
                {
                    if(!File.Exists(scene.Path))throw new FileNotFoundException(scene.Name);
                    var item=new BundleScene{Name=scene.Name};item.Video=Add(scene.Path,"video",item.Id);
                    if(scene.Thumbnail is not null && File.Exists(scene.Thumbnail))item.Cover=Add(scene.Thumbnail,"cover",item.Id);
                    var layered=layers(scene);
                    if(layered?.Background is not null && File.Exists(layered.Background))item.Layers=new(){Background=Add(layered.Background,"background",item.Id),Foreground=layered.Foreground is not null && File.Exists(layered.Foreground)?Add(layered.Foreground,"foreground",item.Id):null,Accent=layered.Accent,Effect=layered.Effect};
                    manifest.Scenes.Add(item);mapping[PlaybackRules.Key(scene)]=item.Id;
                }
                manifest.Settings.Scene=config.Scene is not null?mapping.GetValueOrDefault(config.Scene):null;
                manifest.Settings.Playlist=config.Playlist.Where(mapping.ContainsKey).Select(key=>mapping[key]).ToList();
                using var writer=new StreamWriter(archive.CreateEntry("manifest.json").Open());writer.Write(JsonSerializer.Serialize(manifest));
            }
            File.Move(path+".tmp",path,true);
        });
    }
    public static async Task<BundleManifest> Import(string path,string folder)
    {
        return await Task.Run(()=>{
            using var archive=ZipFile.OpenRead(path);
            if(archive.Entries.Count>1000||archive.Entries.Sum(e=>e.Length)>8L*1024*1024*1024)throw new InvalidDataException("Набор слишком большой.");
            var entry=archive.GetEntry("manifest.json")??throw new InvalidDataException("Нет манифеста VWP.");if(entry.Length>1024*1024)throw new InvalidDataException("Большой манифест.");
            using var stream=entry.Open();var manifest=JsonSerializer.Deserialize<BundleManifest>(stream)??throw new InvalidDataException("Манифест повреждён.");
            if(manifest.Format!=1||manifest.Scenes.Count==0)throw new InvalidDataException("Формат набора не поддерживается.");
            if(manifest.Scenes.Count>200 || manifest.Scenes.Select(s=>s.Id).Distinct().Count()!=manifest.Scenes.Count)throw new InvalidDataException("Неверный список сцен.");
            foreach(var scene in manifest.Scenes)
                if(scene.Name.Length>256||scene.Layers is not null && !Regex.IsMatch(scene.Layers.Accent,"^#[0-9a-fA-F]{6}$"))throw new InvalidDataException("Неверные настройки сцены.");
            if(archive.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=archive.Entries.Count)throw new InvalidDataException("Повторяющиеся файлы.");
            Directory.CreateDirectory(folder);string root=Path.GetFullPath(folder)+Path.DirectorySeparatorChar;
            string Extract(string relative)
            {
                string full=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
                if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||relative.Contains(':')||!manifest.Hashes.TryGetValue(relative,out string? digest))throw new InvalidDataException("Неверный путь файла.");
                var file=archive.GetEntry(relative)??throw new InvalidDataException("Нет файла "+relative);
                if((file.ExternalAttributes>>16 & 0xF000)==0xA000)throw new InvalidDataException("Ссылки в наборе запрещены.");
                if(file.Length>4L*1024*1024*1024)throw new InvalidDataException("Файл превышает 4 ГБ.");
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                using(var input=file.Open())using(var output=File.Create(full+".tmp"))input.CopyTo(output);
                string actual;using(var input=File.OpenRead(full+".tmp"))actual=Convert.ToHexString(SHA256.HashData(input));
                if(!actual.Equals(digest,StringComparison.OrdinalIgnoreCase)){File.Delete(full+".tmp");throw new InvalidDataException("Контрольная сумма не совпала.");}
                File.Move(full+".tmp",full,true);return full;
            }
            foreach(var scene in manifest.Scenes)
            {
                scene.Video=Extract(scene.Video);if(!new[]{".mp4",".webm",".mkv",".mov",".avi"}.Contains(Path.GetExtension(scene.Video)))throw new InvalidDataException("В наборе должен быть видеофайл.");
                if(scene.Cover is not null)scene.Cover=Extract(scene.Cover);
                if(scene.Layers?.Background is not null){scene.Layers.Background=Extract(scene.Layers.Background);if(scene.Layers.Foreground is not null)scene.Layers.Foreground=Extract(scene.Layers.Foreground);}
            }
            return manifest;
        });
    }
}
