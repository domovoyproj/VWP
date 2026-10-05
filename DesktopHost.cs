using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using Forms = System.Windows.Forms;
namespace VWP;
public sealed class DesktopHost : IDisposable
{
    readonly LibVLC vlc = new LibVLC("--no-video-title-show");
    readonly MediaPlayer player;
    Media? currentMedia;
    IntPtr window, parent;
    public bool Active => window != IntPtr.Zero;
    public bool Paused { get; private set; }
    public event Action? Failed;
    public event Action? LoopRequired;
    public DesktopHost() {
        player = new MediaPlayer(vlc); player.EncounteredError += (_,_) => Failed?.Invoke();
        player.EndReached += (_,_)=>LoopRequired?.Invoke();
    }
    public int Volume { set => player.Volume = value; }
    public bool Healthy => !Active || IsWindow(parent);
    internal bool Playing => player.IsPlaying;
    internal long Position => player.Time;
    public void Play(string path, Forms.Screen screen)
    {
        Stop();
        IntPtr progman = FindWindow("Progman",null);
        if (progman == IntPtr.Zero) throw new InvalidOperationException("Explorer не найден.");
        SendMessageTimeout(progman,0x052C,IntPtr.Zero,IntPtr.Zero,2,1000,out _);
        parent = IntPtr.Zero;
        EnumWindows((hwnd,_) => {
            if (FindWindowEx(hwnd,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero)
                parent = FindWindowEx(IntPtr.Zero,hwnd,"WorkerW",null);
            return parent == IntPtr.Zero;
        },IntPtr.Zero);
        if(parent==IntPtr.Zero)
        {
            SendMessageTimeout(progman,0x052C,new IntPtr(0xD),new IntPtr(1),2,1000,out _);
            parent=FindWindowEx(progman,IntPtr.Zero,"WorkerW",null);
        }
        if (parent==IntPtr.Zero) throw new InvalidOperationException("Слой WorkerW недоступен на этой версии Windows.");
        var rect=screen.Bounds;
        var point=new Point { X=rect.X,Y=rect.Y };
        MapWindowPoints(IntPtr.Zero,parent,ref point,1);
        window=CreateWindowEx(0,"STATIC","VWP Wallpaper",0x40000000|0x10000000,point.X,point.Y,rect.Width,rect.Height,parent,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        if (window==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        player.Hwnd=window;
        currentMedia=new Media(vlc,path,FromType.FromPath);
        if (!player.Play(currentMedia)) { Stop(); throw new InvalidOperationException("Не удалось открыть видео."); }
        Paused=false;
    }
    public void TogglePause() { if (!Active) return; Paused=!Paused; player.SetPause(Paused); }
    public void Replay() { if(!Active || currentMedia is null || Paused)return;player.Stop();player.Play(currentMedia); }
    public void Stop() { player.Stop(); currentMedia?.Dispose();currentMedia=null; if (window!=IntPtr.Zero) DestroyWindow(window); window=IntPtr.Zero; parent=IntPtr.Zero; Paused=false; }
    public void Dispose() { Stop(); player.Dispose(); vlc.Dispose(); }
    [StructLayout(LayoutKind.Sequential)] struct Point { public int X,Y; }
    delegate bool EnumProc(IntPtr hwnd,IntPtr param);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string name,string? title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string name,string? title);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc,IntPtr param);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp,uint flags,uint timeout,out IntPtr result);
    [DllImport("user32.dll")] static extern int MapWindowPoints(IntPtr from,IntPtr to,ref Point point,uint count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateWindowEx(uint ex,string cls,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
}
