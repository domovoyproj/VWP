using System.IO;
using System.Text.Json;
namespace VWP;
public partial class MainWindow
{
    void LoadExpansion(string assets)
    {
        string file=Path.Combine(assets,"expansion.json");if(!File.Exists(file))return;
        using var catalog=JsonDocument.Parse(File.ReadAllText(file));
        foreach(var entry in catalog.RootElement.GetProperty("spatial").EnumerateArray())
        {
            int id=entry.GetProperty("id").GetInt32();string cover=Path.Combine(assets,"spatial",$"{id}.png");
            string cinematic=Path.Combine(assets,"cinematic",$"{id}.png");
            string category=entry.GetProperty("category").GetString()!;
            items.Add(new(entry.GetProperty("name").GetString()+" · Живая сцена",Path.Combine(assets,"cinematic",$"{id}.mp4"),File.Exists(cinematic)?cinematic:cover,category+" · ЖИВАЯ СЦЕНА",category,SpatialScene.Descriptions[id],id,true));
        }
        foreach(var entry in catalog.RootElement.GetProperty("live").EnumerateArray())
        {
            int id=entry.GetProperty("id").GetInt32();string category=entry.GetProperty("category").GetString()!;
            items.Add(new(entry.GetProperty("name").GetString()!,Path.Combine(assets,"motion",$"{id}.mp4"),Path.Combine(assets,"motion",$"{id}.jpg"),category+" · 4K · 60 FPS",category,entry.GetProperty("description").GetString()!,id));
        }
    }
}
