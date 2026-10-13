using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace VWP;
public partial class MainWindow
{
    sealed record LivingDefinition(int Id,string Name,string Category,string Description,string Accent,string? Season=null);
    void LoadLiving(string assets)
    {
        foreach(string collection in new[]{"living","anime","vibes","seasons"})LoadArtworkCollection(assets,collection);
    }
    void LoadArtworkCollection(string assets,string collection)
    {
        string folder=Path.Combine(assets,collection),file=Path.Combine(folder,"catalog.json");
        if(!File.Exists(file))return;
        var catalog=JsonSerializer.Deserialize<List<LivingDefinition>>(File.ReadAllText(file),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        if(catalog is null)return;
        foreach(var scene in catalog)
        {
            string poster=Path.Combine(folder,scene.Id+".poster.png");if(!File.Exists(poster))poster=Path.Combine(folder,scene.Id+".png");
            string label=(scene.Season is null?"":scene.Season+" · ")+scene.Category+" · 4K · 60 FPS · ORIGINAL";
            items.Add(new(scene.Name,Path.Combine(folder,scene.Id+".mp4"),poster,label,scene.Category,scene.Description,scene.Id));
        }
    }
}
