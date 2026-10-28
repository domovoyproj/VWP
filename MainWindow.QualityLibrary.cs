using System;
using System.IO;
using System.Linq;

namespace VWP;
public partial class MainWindow
{
    void LoadQualityLibrary(string assets)
    {
        var entries=StudioLibrary.Read(assets);
        if(entries.Count!=132)throw new InvalidDataException("Библиотека VWP установлена не полностью. Переустанови приложение: требуется 132 живые сцены.");
        foreach(var entry in entries)
        {
            var project=StudioLibrary.Load(StudioLibrary.FileName(assets,entry));
            items.Add(new(project.Name,project.FileName,project.Asset(project.Cover),"ЖИВАЯ СЦЕНА · GPU · ДО 60 FPS",entry.Category,project.Description,entry.Id,true,StudioId:project.Id));
        }
        var result=StudioLibraryMigration.Apply(preferences,entries,assets);
        foreach(var custom in result.Customized)items.Add(custom);
        if(result.Changed)Save();
    }
}
