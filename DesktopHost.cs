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
    readonly StaticBackdrop? backdrop;
    Forms.Screen? targetScreen;
    bool recoveryPending,recoveryPaused;
    long recoveryPosition;
    public int RecoveryCount {get;private set;}
    public string? BackdropError {get;private set;}
    IntPtr window, parent;
    public bool Active => currentMedia is not null;
    public bool Paused { get; private set; }
    public event Action? Failed;
    public event Action? LoopRequired;
    public event Action? SurfaceReady;
    public DesktopHost() {
        try {backdrop=new StaticBackdrop();}catch(Exception e){BackdropError=e.Message;}
        player = new MediaPlayer(vlc); player.EncounteredError += (_,_) => Failed?.Invoke();
        player.EndReached += (_,_)=>LoopRequired?.Invoke();
        player.Playing += (_,_)=>SurfaceReady?.Invoke();
    }
    public int Volume { set => player.Volume = value; }
    public bool Healthy => !Active || (IsWindow(parent) && IsWindow(window));
    internal bool Playing => player.IsPlaying;
    internal long Position => player.Time;
    public void Play(string path, Forms.Screen screen,string? cover=null)
    {
        Stop();
        RecoveryCount=0;
        targetScreen=screen;
        if(cover is not null && backdrop is not null)
        {
            try {backdrop.Apply(cover,screen);BackdropError=null;}catch(Exception e){BackdropError=e.Message;}
        }
        CreateSurface(screen);
        player.Hwnd=window;
        currentMedia=new Media(vlc,path,FromType.FromPath);
        if (!player.Play(currentMedia)) { Stop(); throw new InvalidOperationException("Не удалось открыть видео."); }
        Paused=false;
    }
    void CreateSurface(Forms.Screen screen)
    {
        IntPtr progman = FindWindow("Progman",null);
        if (progman == IntPtr.Zero) throw new InvalidOperationException("Explorer не найден.");
        // New Windows 11 uses a WorkerW child. Prefer it over transient legacy siblings.
        SendMessageTimeout(progman,0x052C,new IntPtr(0xD),new IntPtr(1),2,1000,out _);
        parent=FindWindowEx(progman,IntPtr.Zero,"WorkerW",null);
        if(parent==IntPtr.Zero)
        {
            SendMessageTimeout(progman,0x052C,IntPtr.Zero,IntPtr.Zero,2,1000,out _);
            EnumWindows((hwnd,_) => {
                if(FindWindowEx(hwnd,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero)parent=FindWindowEx(IntPtr.Zero,hwnd,"WorkerW",null);
                return parent==IntPtr.Zero;
            },IntPtr.Zero);
        }
        if (parent==IntPtr.Zero) throw new InvalidOperationException("Слой WorkerW недоступен на этой версии Windows.");
        var rect=screen.Bounds;
        var point=new Point { X=rect.X,Y=rect.Y };
        MapWindowPoints(IntPtr.Zero,parent,ref point,1);
        window=CreateWindowEx(0x08000080,"STATIC","VWP Wallpaper",0x40000000|0x10000000|0x06000000,point.X,point.Y,rect.Width,rect.Height,parent,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        if (window==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        int enabled=1;
        DwmSetWindowAttribute(window,12,ref enabled,4); // EXCLUDED_FROM_PEEK
        DwmSetWindowAttribute(window,3,ref enabled,4); // TRANSITIONS_FORCEDISABLED
    }
    public void Recover()
    {
        if(Healthy || currentMedia is null || targetScreen is null)return;
        recoveryPosition=Math.Max(0,player.Time);recoveryPaused=Paused;recoveryPending=true;
        player.Stop();if(IsWindow(window))DestroyWindow(window);window=IntPtr.Zero;parent=IntPtr.Zero;
        CreateSurface(targetScreen);player.Hwnd=window;
        if(!player.Play(currentMedia))throw new InvalidOperationException("Не удалось восстановить видеообои.");
        RecoveryCount++;
    }
    public void CompleteRecovery()
    {
        if(!recoveryPending || !Active)return;
        recoveryPending=false;if(recoveryPosition>0)player.Time=recoveryPosition;
        player.SetPause(recoveryPaused);Paused=recoveryPaused;
    }
    public void TogglePause() { if (!Active) return; Paused=!Paused; player.SetPause(Paused); }
    public void Replay() { if(!Active || currentMedia is null || Paused)return;player.Stop();player.Play(currentMedia); }
    internal bool BackdropApplied=>backdrop?.Applied==true;
    internal string? StaticPicture(Forms.Screen screen)=>backdrop?.ReadCurrent(screen);
    public void UpdateBackdrop()
    {
        if(!Active || BackdropApplied || !player.IsPlaying || backdrop is null || targetScreen is null)return;
        try {
            string folder=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","snapshots");
            System.IO.Directory.CreateDirectory(folder);string picture=System.IO.Path.Combine(folder,"current.png");
            if(player.TakeSnapshot(0,picture,(uint)targetScreen.Bounds.Width,(uint)targetScreen.Bounds.Height) && System.IO.File.Exists(picture))backdrop.Apply(picture,targetScreen);
        }catch(Exception e){BackdropError=e.Message;}
    }
    public void Stop() {recoveryPending=false;player.Stop();currentMedia?.Dispose();currentMedia=null;if(IsWindow(window))DestroyWindow(window);window=IntPtr.Zero;parent=IntPtr.Zero;targetScreen=null;Paused=false;try{backdrop?.Restore();}catch(Exception e){BackdropError=e.Message;}}
    public void Dispose() {Stop();try{backdrop?.Dispose();}catch(Exception e){BackdropError=e.Message;}player.Dispose();vlc.Dispose();}
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
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,uint attribute,ref int value,uint size);
}
