using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Forms = System.Windows.Forms;
namespace VWP;

public static class PausePolicy
{
    static DateTime sampledAt;
    static HashSet<string> running=new(StringComparer.OrdinalIgnoreCase);
    public static string[] ApplicationNames(string value)=>value.Split(new[]{',',';','\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(x=>System.IO.Path.GetFileNameWithoutExtension(x.Trim())).Where(x=>x.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public static string? MatchingApplication(IEnumerable<string> rules,IEnumerable<string> processes)=>rules.FirstOrDefault(rule=>processes.Contains(rule,StringComparer.OrdinalIgnoreCase));
    static string? RunningRule(IEnumerable<string> rules)
    {
        if(!rules.Any())return null;
        if(DateTime.UtcNow-sampledAt>TimeSpan.FromSeconds(3))
        {
            var snapshot=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var process in Process.GetProcesses())using(process)try{snapshot.Add(process.ProcessName);}catch{}
            running=snapshot;sampledAt=DateTime.UtcNow;
        }
        return MatchingApplication(rules,running);
    }
    public static string? Reason(Forms.Screen screen, bool fullscreen, bool battery, bool locked,IEnumerable<string>? exceptions=null,bool maximized=false,IEnumerable<string>? applications=null)
    {
        if (locked) return "сеанс заблокирован";
        if (battery && GetSystemPowerStatus(out var power) && power.ACLineStatus == 0) return "питание от батареи";
        if(applications is not null && RunningRule(applications) is string app)return "приложение · "+app;
        if (!fullscreen&&!maximized) return null;
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || !GetWindowRect(window, out var rect) || IsIconic(window)) return null;
        GetWindowThreadProcessId(window, out uint process);
        if (process == Environment.ProcessId) return null;
        if(exceptions is not null)try{using var foreground=Process.GetProcessById((int)process);if(exceptions.Contains(foreground.ProcessName,StringComparer.OrdinalIgnoreCase))return null;}catch{}
        var cls = new StringBuilder(256); GetClassName(window, cls, cls.Capacity);
        if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return null;
        var bounds = screen.Bounds;
        if(fullscreen && rect.Left <= bounds.Left + 2 && rect.Top <= bounds.Top + 2 && rect.Right >= bounds.Right - 2 && rect.Bottom >= bounds.Bottom - 2)return "полноэкранное приложение";
        if(maximized && IsZoomed(window) && Forms.Screen.FromHandle(window).DeviceName==screen.DeviceName)return "развёрнутое приложение";
        return null;
    }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct Power { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public uint BatteryLifeTime, BatteryFullLifeTime; }
    [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out Power status);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int size);
}
