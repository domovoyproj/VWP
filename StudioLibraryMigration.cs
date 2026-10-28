using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VWP;

internal sealed record StudioLibraryMigrationResult(bool Changed,IReadOnlyList<Wallpaper> Customized);

internal static class StudioLibraryMigration
{
    internal static StudioLibraryMigrationResult Apply(Preferences preferences,IReadOnlyList<StudioLibraryEntry> entries,string assets)
    {
        var byId=entries.ToDictionary(e=>e.Id);var customized=new List<Wallpaper>();var aliases=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var entry in entries)
        {
            foreach(string prefix in new[]{"preset:","spatial:"})
            {
                string old=prefix+entry.Id;
                if(preferences.Layers.TryGetValue(old,out var layers))
                {
                    // Keep the exact saved layer settings and key. These belong to the
                    // user and must survive deduplication of stock video/live variants.
                    string fallback=Source(assets,entry),background=layers.Background is string file&&File.Exists(file)?file:fallback;
                    string legacyVideo=entry.Group=="base"?Path.Combine(assets,entry.Id+".mp4"):Path.Combine(assets,entry.Group,entry.Id+".mp4");
                    string path=File.Exists(legacyVideo)?legacyVideo:background;
                    customized.Add(new(entry.Name+" · Моя настройка",path,background,"СОХРАНЁННАЯ НАСТРОЙКА","Мои сцены",entry.Description,entry.Id,prefix=="spatial:",layers.Clock,LegacyCustomized:true));
                }
                else aliases[old]="studio:library-"+entry.Id;
            }
        }
        string Map(string key)=>aliases.GetValueOrDefault(key)??key;
        List<string> MapList(List<string> list)=>list.Select(Map).Distinct(StringComparer.Ordinal).ToList();
        string before=System.Text.Json.JsonSerializer.Serialize(preferences);
        preferences.Favorites=MapList(preferences.Favorites);preferences.RecentScenes=MapList(preferences.RecentScenes);
        foreach(var monitor in preferences.Monitors.Values)
        {
            if(monitor.Scene is string selected)monitor.Scene=Map(selected);
            monitor.Playlist=MapList(monitor.Playlist);
        }
        if(preferences.LastPresetId is int id&&byId.TryGetValue(id,out var last))
        {
            var custom=customized.FirstOrDefault(c=>c.PresetId==id);
            preferences.Last=custom?.Path??StudioLibrary.FileName(assets,last);
        }
        preferences.StudioLibraryVersion=1;
        return new(before!=System.Text.Json.JsonSerializer.Serialize(preferences),customized);
    }
    internal static string Source(string assets,StudioLibraryEntry entry)=>Path.Combine(assets,entry.Group switch
    {
        "base"=>$"scene-{entry.Id}.png",
        "cinematic"=>$"cinematic/{entry.Id}.png",
        "living"=>$"studio-library/art/{entry.Id}-background.png",
        _=>$"{entry.Group}/{entry.Id}.png"
    });
}
