using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Forms=System.Windows.Forms;
namespace VWP;
/// <summary>Matching desktop picture underneath video during shell Peek/Alt+Tab transitions.</summary>
public sealed class StaticBackdrop : IDisposable
{
    [ComImport,Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor,[MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor,[MarshalAs(UnmanagedType.LPWStr)] out string wallpaper);
        void GetMonitorDevicePathAt(uint index,[MarshalAs(UnmanagedType.LPWStr)] out string monitor);
        void GetMonitorDevicePathCount(out uint count);
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitor,out Rect rect);
    }
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
    public sealed record Backup(string Monitor,string Original,string Applied);
    readonly string journal=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","desktop-backup.json");
    IDesktopWallpaper? desktop;
    Backup? backup;
    public StaticBackdrop()
    {
        desktop=(IDesktopWallpaper)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"))!)!;
        if(File.Exists(journal)) { backup=JsonSerializer.Deserialize<Backup>(File.ReadAllText(journal));Restore(); }
    }
    public void Apply(string image,Forms.Screen screen)
    {
        if(desktop is null || !File.Exists(image))return;
        Restore();desktop.GetMonitorDevicePathCount(out uint count);
        for(uint i=0;i<count;i++)
        {
            desktop.GetMonitorDevicePathAt(i,out string monitor);desktop.GetMonitorRECT(monitor,out var rect);
            if(rect.Left!=screen.Bounds.Left || rect.Top!=screen.Bounds.Top || rect.Right!=screen.Bounds.Right || rect.Bottom!=screen.Bounds.Bottom)continue;
            desktop.GetWallpaper(monitor,out string original);backup=new Backup(monitor,original,Path.GetFullPath(image));
            Directory.CreateDirectory(Path.GetDirectoryName(journal)!);File.WriteAllText(journal,JsonSerializer.Serialize(backup));
            desktop.SetWallpaper(monitor,backup.Applied);return;
        }
    }
    public void Restore()
    {
        if(backup is null || desktop is null)return;
        desktop.GetWallpaper(backup.Monitor,out string current);
        // Preserve a wallpaper the user independently selected while VWP was running.
        if(string.Equals(current,backup.Applied,StringComparison.OrdinalIgnoreCase))desktop.SetWallpaper(backup.Monitor,backup.Original);
        backup=null;File.Delete(journal);
    }
    internal bool Applied => backup is not null;
    internal string? ReadCurrent(Forms.Screen screen)
    {
        if(desktop is null)return null;
        desktop.GetMonitorDevicePathCount(out uint count);
        for(uint i=0;i<count;i++)
        {
            desktop.GetMonitorDevicePathAt(i,out string monitor);desktop.GetMonitorRECT(monitor,out var rect);
            if(rect.Left==screen.Bounds.Left && rect.Top==screen.Bounds.Top && rect.Right==screen.Bounds.Right && rect.Bottom==screen.Bounds.Bottom){desktop.GetWallpaper(monitor,out string current);return current;}
        }
        return null;
    }
    public void Dispose() {Restore();if(desktop is not null){Marshal.ReleaseComObject(desktop);desktop=null;}}
}
