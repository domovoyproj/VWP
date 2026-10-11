using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace VWP;
public partial class MainWindow
{
    sealed record LivingDefinition(int Id,string Name,string Category,string Description,string Accent);
    void LoadLiving(string assets)
    {
        string folder=Path.Combine(assets,"living"),file=Path.Combine(folder,"catalog.json");
        if(!File.Exists(file))return;
        var catalog=JsonSerializer.Deserialize<List<LivingDefinition>>(File.ReadAllText(file),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        if(catalog is null)return;
        foreach(var scene in catalog)
            items.Add(new(scene.Name,Path.Combine(folder,scene.Id+".mp4"),Path.Combine(folder,scene.Id+".png"),scene.Category+" · 4K · 60 FPS · ORIGINAL",scene.Category,scene.Description,scene.Id));
    }
}
