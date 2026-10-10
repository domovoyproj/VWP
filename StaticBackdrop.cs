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
    internal interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor,[MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor,[MarshalAs(UnmanagedType.LPWStr)] out string wallpaper);
        void GetMonitorDevicePathAt(uint index,[MarshalAs(UnmanagedType.LPWStr)] out string monitor);
        void GetMonitorDevicePathCount(out uint count);
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitor,out Rect rect);
    }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect {public int Left,Top,Right,Bottom;}
    public sealed record Backup(string Monitor,string Original,string Applied);
    static readonly string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP");
    readonly string journalFolder=folder;
    string? journal;
    IDesktopWallpaper? desktop;
    Backup? backup;
    public StaticBackdrop()
    {
        desktop=(IDesktopWallpaper)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"))!)!;
    }
    internal StaticBackdrop(IDesktopWallpaper desktop,string journalFolder){this.desktop=desktop;this.journalFolder=journalFolder;}
    public static void RecoverAll()
    {
        Directory.CreateDirectory(folder);
        foreach(string file in Directory.GetFiles(folder,"desktop-backup*.json"))
        {
            using var recovery=new StaticBackdrop();
            try {recovery.journal=file;recovery.backup=JsonSerializer.Deserialize<Backup>(File.ReadAllText(file));recovery.Restore();}
            catch { recovery.backup=null; } // Keep the journal for a temporarily disconnected monitor.
        }
    }
    public void Apply(string image,Forms.Screen screen)
    {
        if(desktop is null || !File.Exists(image))return;
        desktop.GetMonitorDevicePathCount(out uint count);
        for(uint i=0;i<count;i++)
        {
            desktop.GetMonitorDevicePathAt(i,out string monitor);desktop.GetMonitorRECT(monitor,out var rect);
            if(rect.Left!=screen.Bounds.Left || rect.Top!=screen.Bounds.Top || rect.Right!=screen.Bounds.Right || rect.Bottom!=screen.Bounds.Bottom)continue;
            // Replace our current picture directly. Restoring the original between
            // scenes causes a visible flash even when the video transition is smooth.
            if(backup is not null && !string.Equals(backup.Monitor,monitor,StringComparison.OrdinalIgnoreCase))Restore();
            desktop.GetWallpaper(monitor,out string original);
            if(backup is not null && string.Equals(original,backup.Applied,StringComparison.OrdinalIgnoreCase))original=backup.Original;
            var previous=backup;
            backup=new Backup(monitor,original,Path.GetFullPath(image));
            journal=Path.Combine(journalFolder,"desktop-backup-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(monitor)))[..16]+".json");
            Directory.CreateDirectory(journalFolder);File.WriteAllText(journal,JsonSerializer.Serialize(backup));
            try{desktop.SetWallpaper(monitor,backup.Applied);}
            catch
            {
                backup=previous;
                if(backup is null)File.Delete(journal);else File.WriteAllText(journal,JsonSerializer.Serialize(backup));
                throw;
            }
            return;
        }
    }
    public void Restore()
    {
        if(backup is null || desktop is null)return;
        desktop.GetWallpaper(backup.Monitor,out string current);
        // Preserve a wallpaper the user independently selected while VWP was running.
        if(string.Equals(current,backup.Applied,StringComparison.OrdinalIgnoreCase))desktop.SetWallpaper(backup.Monitor,backup.Original);
        backup=null;if(journal is not null)File.Delete(journal);
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
    public void Dispose() {Restore();if(desktop is not null){if(Marshal.IsComObject(desktop))Marshal.ReleaseComObject(desktop);desktop=null;}}
}
