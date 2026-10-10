using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;
namespace VWP;
public record RegistrySetting(string Key,string Name,bool Exists,RegistryValueKind Kind,string Value);
internal static class PersonalizationBackup
{
    public static List<RegistrySetting> Capture(IEnumerable<(string Key,string Name)> targets)
    {
        var result=new List<RegistrySetting>();
        foreach(var target in targets)
        {
            using var key=Registry.CurrentUser.OpenSubKey(target.Key);
            bool exists=key?.GetValueNames().Contains(target.Name,StringComparer.OrdinalIgnoreCase)==true;
            var kind=exists?key!.GetValueKind(target.Name):RegistryValueKind.String;
            object? value=exists?key!.GetValue(target.Name,null,RegistryValueOptions.DoNotExpandEnvironmentNames):null;
            result.Add(new(target.Key,target.Name,exists,kind,value is byte[] bytes?Convert.ToBase64String(bytes):Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)??""));
        }
        return result;
    }
    public static void Restore(IEnumerable<RegistrySetting> settings)
    {
        foreach(var value in settings)
        {
            using var key=Registry.CurrentUser.CreateSubKey(value.Key);
            if(!value.Exists){key.DeleteValue(value.Name,false);continue;}
            object data=value.Kind switch{RegistryValueKind.Binary=>Convert.FromBase64String(value.Value),RegistryValueKind.DWord=>int.Parse(value.Value,System.Globalization.CultureInfo.InvariantCulture),RegistryValueKind.QWord=>long.Parse(value.Value,System.Globalization.CultureInfo.InvariantCulture),_=>value.Value};
            key.SetValue(value.Name,data,value.Kind);
        }
    }
    public static void Write(string path,object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
        System.IO.File.Move(path+".tmp",path,true);
    }
}
