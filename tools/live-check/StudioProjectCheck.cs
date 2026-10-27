using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VWP;

internal static class StudioProjectCheck
{
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static async Task Run()
    {
        string folder=Path.GetFullPath("artifacts/studio-project-check/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            var projects=Directory.GetFiles("assets/studio-originals","*.vwpscene",SearchOption.AllDirectories).Select(p=>StudioProject.Load(Path.GetFullPath(p))).ToArray();Require(projects.Length>=3,"Three curated projects are required.");
            foreach(var project in projects)
            {
                project.Validate();
                foreach(string malicious in new[]{"../outside.png","C:/outside.png","/outside.png","%2e%2e/outside.png","background.png?other=1"})
                {
                    bool rejected=false;try{project.Asset(malicious);}catch(InvalidDataException){rejected=true;}Require(rejected,"Path escaped project root: "+malicious);
                }
                var clone=project.CloneTo(Path.Combine(folder,project.Id));clone.Validate();Require(clone.Id!=project.Id,"Copies must have independent identifiers.");Require(clone.Assets().All(a=>File.Exists(clone.Asset(a))),"Clone missing an asset.");
            }
            var scenes=projects.Select(p=>new Wallpaper(p.Name,p.FileName,p.Asset(p.Cover),"Studio",IsSpatial:true,StudioId:p.Id)).ToArray();
            Require(PlaybackRules.Key(scenes[0])==PlaybackRules.Key(scenes[0] with{Path="C:/moved-install/scene.vwpscene"}),"Built-in scene identity must survive moving the install folder.");
            string bundle=Path.Combine(folder,"originals.vwpbundle");await BundleService.Export(bundle,"VWP Originals","VWP",scenes,new(){Scene=PlaybackRules.Key(scenes[0]),Playlist=scenes.Select(PlaybackRules.Key).ToList()},_=>null);
            var imported=await BundleService.Import(bundle,Path.Combine(folder,"imported"));Require(imported.Format==2&&imported.Scenes.Count==projects.Length,"Project bundle roundtrip failed.");
            Require(imported.Settings.Scene==imported.Scenes[0].Id&&imported.Settings.Playlist.Count==projects.Length,"Project bundle lost selection or playlist mappings.");
            for(int i=0;i<projects.Length;i++)
            {
                var before=projects[i];var after=StudioProject.Load(imported.Scenes[i].Video);Require(after.Layers.Count==before.Layers.Count,"Lost layers in bundle.");
                for(int layer=0;layer<before.Layers.Count;layer++)Require(File.ReadAllBytes(before.Asset(before.Layers[layer].Source)).SequenceEqual(File.ReadAllBytes(after.Asset(after.Layers[layer].Source))),"Layer image changed during export/import.");
                Require(JsonSerializer.Serialize(before.Settings)==JsonSerializer.Serialize(after.Settings),"Settings changed during roundtrip.");
            }
            string damaged=Path.Combine(folder,"tampered.vwpbundle");File.Copy(bundle,damaged);
            using(var zip=ZipFile.Open(damaged,ZipArchiveMode.Update))
            {
                var entry=zip.Entries.First(e=>e.FullName.EndsWith(".png",StringComparison.Ordinal));string name=entry.FullName;entry.Delete();using var bytes=zip.CreateEntry(name).Open();bytes.Write(new byte[]{1,2,3,4});
            }
            bool corrupt=false;try{await BundleService.Import(damaged,Path.Combine(folder,"bad"));}catch(InvalidDataException){corrupt=true;}Require(corrupt,"Tampered project texture was accepted.");
            File.WriteAllText("artifacts/studio-project-check/status.txt","PASS: three curated projects validate; unsafe asset paths rejected; editable clones preserve assets; project bundle roundtrip preserves layer bytes/settings; corrupted resources rejected by SHA-256.");
        }
        catch(Exception error){File.WriteAllText("artifacts/studio-project-check/status.txt",error.ToString());Environment.ExitCode=1;}
    }
}
