using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace VWP;

internal sealed record StudioLibraryEntry(int Id,string Name,string Category,string Description,string Project,string Group,string? Season=null);

internal static class StudioLibrary
{
    internal static IReadOnlyList<StudioLibraryEntry> Read(string assets)
    {
        string file=Path.Combine(assets,"studio-library","catalog.json");
        if(!File.Exists(file))return Array.Empty<StudioLibraryEntry>();
        var entries=JsonSerializer.Deserialize<List<StudioLibraryEntry>>(File.ReadAllText(file),StudioProject.Json)??throw new InvalidDataException("Каталог живых сцен повреждён.");
        if(entries.Select(e=>e.Id).Distinct().Count()!=entries.Count||entries.Any(e=>e.Project!=$"{e.Id}.vwpscene"||e.Id<0||e.Id>9999||string.IsNullOrWhiteSpace(e.Name)||string.IsNullOrWhiteSpace(e.Category)))throw new InvalidDataException("Каталог живых сцен содержит неверные записи.");
        return entries;
    }
    internal static string FileName(string assets,StudioLibraryEntry entry)=>Path.Combine(assets,"studio-library","scenes",entry.Project);
    internal static StudioProject Load(string file)
    {
        string full=Path.GetFullPath(file);
        var roots=new List<string>{Path.Combine(AppContext.BaseDirectory,"assets")};
        if(Environment.GetCommandLineArgs().AnyVerification())roots.Add(Path.Combine(Environment.CurrentDirectory,"assets"));
        foreach(string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string parent=Path.GetFullPath(Path.Combine(root,"studio-library","scenes"));
            if(!string.Equals(Path.GetDirectoryName(full),parent,StringComparison.OrdinalIgnoreCase))continue;
            if(!Read(root).Any(entry=>string.Equals(entry.Project,Path.GetFileName(full),StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Проекта нет в установленном каталоге.");
            return StudioProject.LoadBundled(full,root);
        }
        return StudioProject.Load(full);
    }
}
