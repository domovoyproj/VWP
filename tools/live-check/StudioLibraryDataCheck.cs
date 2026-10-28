using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VWP;

internal static class StudioLibraryDataCheck
{
    static void Require(bool value,string label){if(!value)throw new Exception(label);}
    internal static async Task Run()
    {
        string output=Path.GetFullPath("artifacts/studio-library-data-check");Directory.CreateDirectory(output);
        try
        {
            string assets=Path.GetFullPath("assets");var entries=StudioLibrary.Read(assets);Require(entries.Count==132,"132 unique compositions required");
            foreach(var entry in entries)
            {
                var project=StudioLibrary.Load(StudioLibrary.FileName(assets,entry));project.Validate();Require(project.Id=="library-"+entry.Id,"Stable library identity");
                Require(project.Layers.Any(l=>l.Masks.Count>0)||project.Weather.Kind!="none","Individually authored motion");
            }
            var custom=new SceneLayers{Background=Path.Combine(assets,"scene-1.png"),Foreground=Path.Combine(assets,"scene-2.png"),Realtime=true,Speed=1.7,Wind=-.4,Density=.8,Effect="Rain",Clock=true};
            var settings=new Preferences{Last="C:/old-install/assets/1.mp4",LastPresetId=1,Favorites=new(){"preset:0","spatial:0","preset:1","C:/my-video.mp4"},RecentScenes=new(){"spatial:1701"},Layers=new(){["preset:1"]=custom},Imports=new(){"C:/my-video.mp4"},Monitors=new(){["TEST"]=new(){Scene="spatial:1701",Playlist=new(){"preset:0","spatial:0","preset:1","C:/my-video.mp4"},UserPaused=true}}};
            string customBefore=JsonSerializer.Serialize(custom);var result=StudioLibraryMigration.Apply(settings,entries,assets);
            Require(result.Changed&&result.Customized.Count==1,"Keep customized variant");
            Require(settings.Favorites.SequenceEqual(new[]{"studio:library-0","preset:1","C:/my-video.mp4"}),"Favorites migrate and deduplicate stock aliases");
            Require(settings.Monitors["TEST"].Scene=="studio:library-1701"&&settings.Monitors["TEST"].Playlist.Count==3&&settings.Monitors["TEST"].UserPaused,"Monitor playback and playlist migration");
            Require(settings.Imports.Single()=="C:/my-video.mp4"&&JsonSerializer.Serialize(settings.Layers["preset:1"])==customBefore,"Personal imports and exact custom properties preserved");
            Require(PlaybackRules.Key(result.Customized.Single())=="preset:1"&&result.Customized.Single().LegacyCustomized,"Customized scene retains its own stable key");
            Require(!StudioLibraryMigration.Apply(settings,entries,assets).Changed,"Migration is idempotent");
            var selected=entries.Where(e=>e.Id is 1906 or 1601 or 1112).Select(e=>StudioLibrary.Load(StudioLibrary.FileName(assets,e))).ToArray();
            foreach(var source in selected)
            {
                var copy=source.CloneTo(Path.Combine(output,Guid.NewGuid().ToString("N")));copy.Validate();
                Require(copy.Folder!=source.Folder&&copy.Assets().All(a=>!a.Contains('/')),"Editable copies are self contained");
                Require(JsonSerializer.Serialize(copy.Weather,StudioProject.Json)==JsonSerializer.Serialize(source.Weather,StudioProject.Json),"Weather masks and occlusion preserved by clone");
                string untrusted=Path.Combine(output,Guid.NewGuid()+".vwpscene");File.Copy(source.FileName,untrusted);
                bool rejected=false;try{StudioProject.Load(untrusted);}catch(InvalidDataException){rejected=true;}Require(rejected,"Untrusted projects cannot grant themselves the installed asset root");
            }
            var walls=selected.Select(p=>new Wallpaper(p.Name,p.FileName,p.Asset(p.Cover),"GPU",IsSpatial:true,StudioId:p.Id)).ToArray();
            string bundle=Path.Combine(output,"library.vwpbundle");await BundleService.Export(bundle,"Library QA","VWP",walls,new(){Playlist=walls.Select(PlaybackRules.Key).ToList()},_=>null);
            var imported=await BundleService.Import(bundle,Path.Combine(output,"imported-"+Guid.NewGuid().ToString("N")));
            for(int i=0;i<selected.Length;i++)
            {
                var after=StudioProject.Load(imported.Scenes[i].Video);var before=selected[i];Require(after.Schema==3&&after.Layers.Count==before.Layers.Count,"Schema 3 bundle roundtrip");
                Require(JsonSerializer.Serialize(after.Weather,StudioProject.Json)==JsonSerializer.Serialize(before.Weather,StudioProject.Json),"Bundle preserves weather clipping");
                for(int l=0;l<after.Layers.Count;l++)
                {
                    var a=before.Layers[l];var b=after.Layers[l];Require(a.Surface==b.Surface&&a.Light==b.Light&&a.ClipToSurface==b.ClipToSurface&&a.Seed==b.Seed,"Bundle preserves physical materials");
                    Require(File.ReadAllBytes(before.Asset(a.Source)).SequenceEqual(File.ReadAllBytes(after.Asset(b.Source))),"Bundle preserves original image bytes");
                }
            }
            File.WriteAllText(Path.Combine(output,"status.txt"),"PASS: 132 valid scenes; stable IDs; aliases, favorites, playlists and custom edits preserved; idempotent migration; trusted root confinement; editable copies; schema 3 bundle roundtrip with materials, weather masks and alpha layers.");
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"status.txt"),error.ToString());Environment.ExitCode=1;}
    }
    internal static void Posters()
    {
        string root=Environment.CurrentDirectory,check=Path.Combine(root,"artifacts","studio-library-check"),output=Path.Combine(root,"assets","studio-library","posters");
        using var summary=JsonDocument.Parse(File.ReadAllText(Path.Combine(check,"results.json")));Require(summary.RootElement.GetProperty("complete").GetBoolean()&&summary.RootElement.GetProperty("count").GetInt32()==132,"Posters require a completed 132-scene native rendering pass");
        Directory.CreateDirectory(output);var encoder=ImageCodecInfo.GetImageEncoders().Single(c=>c.MimeType=="image/jpeg");
        foreach(var entry in StudioLibrary.Read(Path.Combine(root,"assets")))
        {
            using var bitmap=new Bitmap(Path.Combine(check,entry.Id.ToString(),"frame-4.png"));Require(bitmap.Width==1920&&bitmap.Height==1080,"Posters must be actual FHD native frames");
            using var parameters=new EncoderParameters(1);parameters.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,96L);bitmap.Save(Path.Combine(output,entry.Id+".jpg"),encoder,parameters);
        }
        Console.WriteLine("Saved 132 native-rendered FHD library posters.");
    }
}
