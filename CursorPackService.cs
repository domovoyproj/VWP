using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace VWP;
public static class CursorPackService
{
    public static readonly string[] Roles={"Arrow","Help","AppStarting","Wait","Crosshair","IBeam","NWPen","No","SizeNS","SizeWE","SizeNWSE","SizeNESW","SizeAll","UpArrow","Hand","Pin","Person"};
    const string Key=@"Control Panel\Cursors";
    static string Backup=>Path.Combine(AppearanceCatalog.Data,"cursor-backup.json");
    public static bool CanRestore=>System.IO.File.Exists(Backup);
    internal static List<RegistrySetting> CaptureState()=>PersonalizationBackup.Capture(Roles.Concat(new[]{"","Scheme Source"}).Select(name=>(Key,name)));
    internal static void RestoreState(List<RegistrySetting> state){PersonalizationBackup.Restore(state);Reload();}
    internal static void DiscardBackup(){if(CanRestore)System.IO.File.Delete(Backup);}
    public static void Validate(CursorPack pack)
    {
        if(!pack.Files.ContainsKey("Arrow"))throw new InvalidDataException("В наборе нет основного указателя Arrow.");
        foreach(var file in pack.Files)
        {
            if(!Roles.Contains(file.Key,StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("Неизвестная роль курсора: "+file.Key);
            string path=pack.File(file.Key);
            if(!new[]{".cur",".ani"}.Contains(Path.GetExtension(path).ToLowerInvariant()) || !System.IO.File.Exists(path) || new FileInfo(path).Length>8*1024*1024)throw new InvalidDataException("Некорректный файл курсора.");
            IntPtr handle=LoadImage(IntPtr.Zero,path,2,0,0,0x10);
            if(handle==IntPtr.Zero)throw new InvalidDataException("Windows не смогла прочитать курсор: "+Path.GetFileName(path));
            DestroyCursor(handle);
        }
    }
    public static CursorPack Install(CursorPack source)
    {
        Validate(source);
        string folder=Path.Combine(AppearanceCatalog.Data,"cursors",SafeId(source.Id));Directory.CreateDirectory(folder);
        var copy=JsonSerializer.Deserialize<CursorPack>(JsonSerializer.Serialize(source))!;copy.Folder=folder;
        foreach(var role in source.Files.Keys){string file=source.File(role);string target=Path.Combine(folder,Path.GetFileName(file));if(!file.Equals(target,StringComparison.OrdinalIgnoreCase))System.IO.File.Copy(file,target,true);copy.Files[role]=Path.GetFileName(file);}
        foreach(string asset in new[]{"preview.png","sheet.png","LICENSE.txt","source.zip"})if(System.IO.File.Exists(Path.Combine(source.Folder,asset)) && !source.Folder.Equals(folder,StringComparison.OrdinalIgnoreCase))System.IO.File.Copy(Path.Combine(source.Folder,asset),Path.Combine(folder,asset),true);
        PersonalizationBackup.Write(Path.Combine(folder,"pack.json"),copy);return copy;
    }
    public static void Apply(CursorPack source)
    {
        var pack=Install(source);var previous=CaptureState();
        if(!CanRestore)PersonalizationBackup.Write(Backup,previous);
        try
        {
            using var key=Registry.CurrentUser.CreateSubKey(Key);
            // A complete scheme resets missing roles to Windows defaults instead of inheriting another pack.
            foreach(string role in Roles)key.SetValue(role,pack.Files.ContainsKey(role)?pack.File(role):"",RegistryValueKind.ExpandString);
            key.SetValue("","VWP · "+pack.Name);key.SetValue("Scheme Source",1,RegistryValueKind.DWord);
            using var schemes=key.CreateSubKey("Schemes");schemes.SetValue("VWP · "+pack.Name,string.Join(",",Roles.Select(role=>pack.Files.ContainsKey(role)?pack.File(role):"")),RegistryValueKind.ExpandString);
            Reload();
        }
        catch{RestoreState(previous);throw;}
    }
    public static void Restore()
    {
        if(!CanRestore)return;
        RestoreState(JsonSerializer.Deserialize<List<RegistrySetting>>(System.IO.File.ReadAllText(Backup))!);System.IO.File.Delete(Backup);
    }
    public static CursorPack Import(string file)
    {
        string folder=Path.Combine(AppearanceCatalog.Data,"cursors","import-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            if(Path.GetExtension(file).Equals(".zip",StringComparison.OrdinalIgnoreCase))
            {
                using var zip=ZipFile.OpenRead(file);
                if(zip.Entries.Count>300 || zip.Entries.Sum(e=>e.Length)>32*1024*1024)throw new InvalidDataException("Набор курсоров слишком большой.");
                foreach(var entry in zip.Entries)
                {
                    string name=Path.GetFileName(entry.FullName);
                    if(name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new InvalidDataException("Некорректное имя файла в наборе.");
                    if(!new[]{".cur",".ani"}.Contains(Path.GetExtension(name).ToLowerInvariant()) && name!="pack.json" && name!="LICENSE.txt" && name!="source.zip")continue;
                    string target=Path.Combine(folder,name);if(System.IO.File.Exists(target))throw new InvalidDataException("Повторяющееся имя файла в наборе.");entry.ExtractToFile(target);
                }
            }
            else System.IO.File.Copy(file,Path.Combine(folder,"Arrow"+Path.GetExtension(file).ToLowerInvariant()));
            CursorPack pack=System.IO.File.Exists(Path.Combine(folder,"pack.json"))?JsonSerializer.Deserialize<CursorPack>(System.IO.File.ReadAllText(Path.Combine(folder,"pack.json")))!:new(){Name=Path.GetFileNameWithoutExtension(file),Author="Локальный импорт",Description="Импортированный набор",License="Лицензия автора"};
            pack.Id=Path.GetFileName(folder);pack.Folder=folder;
            if(pack.Files.Count==0)
            {
                var aliases=new[]{"pointer","help","work","busy","cross","text","handwriting","unavailable","vert","horz","dgn1","dgn2","move","alternate","link","pin","person"};
                foreach(string cursor in Directory.EnumerateFiles(folder).Where(p=>new[]{".cur",".ani"}.Contains(Path.GetExtension(p))))
                {
                    string stem=Path.GetFileNameWithoutExtension(cursor);int index=Array.FindIndex(Roles,r=>r.Equals(stem,StringComparison.OrdinalIgnoreCase));if(index<0)index=Array.FindIndex(aliases,r=>r.Equals(stem,StringComparison.OrdinalIgnoreCase));
                    if(index>=0)pack.Files[Roles[index]]=Path.GetFileName(cursor);
                }
                if(pack.Files.Count==0 && Directory.GetFiles(folder).Length==1)pack.Files["Arrow"]=Path.GetFileName(Directory.GetFiles(folder)[0]);
            }
            Validate(pack);CreatePreview(pack);PersonalizationBackup.Write(Path.Combine(folder,"pack.json"),pack);return pack;
        }
        catch{foreach(string path in Directory.EnumerateFiles(folder))System.IO.File.Delete(path);Directory.Delete(folder);throw;}
    }
    public static void Export(CursorPack pack,string output)
    {
        Validate(pack);
        string temp=output+".tmp";
        try
        {
            using(var zip=new ZipArchive(System.IO.File.Create(temp),ZipArchiveMode.Create))
            {
                foreach(var file in pack.Files.Values.Distinct())zip.CreateEntryFromFile(Path.Combine(pack.Folder,file),file);
                var entry=zip.CreateEntry("pack.json");using(var stream=entry.Open())JsonSerializer.Serialize(stream,pack);
                if(System.IO.File.Exists(Path.Combine(pack.Folder,"LICENSE.txt")))zip.CreateEntryFromFile(Path.Combine(pack.Folder,"LICENSE.txt"),"LICENSE.txt");
                if(System.IO.File.Exists(Path.Combine(pack.Folder,"source.zip")))zip.CreateEntryFromFile(Path.Combine(pack.Folder,"source.zip"),"source.zip");
            }
            System.IO.File.Move(temp,output,true);
        }
        finally{if(System.IO.File.Exists(temp))System.IO.File.Delete(temp);}
    }
    public static void CreatePreview(CursorPack pack)
    {
        IntPtr handle=LoadImage(IntPtr.Zero,pack.File("Arrow"),2,64,64,0x10);
        if(handle==IntPtr.Zero)return;
        try{var image=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(handle,System.Windows.Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(96,96));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=System.IO.File.Create(pack.Preview);encoder.Save(stream);}finally{DestroyCursor(handle);}
    }
    static string SafeId(string value){string id=new(value.Where(c=>char.IsAsciiLetterOrDigit(c)||c=='-').ToArray());return id.Length>0?id:throw new InvalidDataException("У набора нет идентификатора.");}
    static void Reload(){if(!SystemParametersInfo(0x57,0,IntPtr.Zero,0))throw new Win32Exception(Marshal.GetLastWin32Error());}
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="LoadImageW")] static extern IntPtr LoadImage(IntPtr instance,string file,uint type,int width,int height,uint flags);
    [DllImport("user32.dll")] static extern bool DestroyCursor(IntPtr cursor);
    [DllImport("user32.dll",SetLastError=true)] static extern bool SystemParametersInfo(uint action,uint parameter,IntPtr value,uint flags);
}
