using System;
using System.Linq;
namespace VWP;
public static class LibrarySearch
{
    public static bool Matches(Wallpaper scene,string query)
    {
        string text=scene.Name+" "+scene.Category+" "+scene.Description+" "+scene.Subtitle;
        return query.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).All(word=>text.Contains(word,StringComparison.OrdinalIgnoreCase));
    }
}
