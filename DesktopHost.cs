using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Windows.Interop;
using LibVLCSharp.Shared;
using Forms = System.Windows.Forms;
namespace VWP;
public sealed class DesktopHost : IDisposable
{
    // Transition snapshots must never put their filename or preview on the desktop.
    readonly LibVLC vlc = new LibVLC("--no-video-title-show","--no-osd","--no-snapshot-preview","--input-repeat=-1");
    readonly MediaPlayer player;
    Media? currentMedia;
    readonly StaticBackdrop? backdrop;
    Forms.Screen? targetScreen;
    bool recoveryPending,recoveryPaused;
    long recoveryPosition;
    public int RecoveryCount {get;private set;}
    public string? BackdropError {get;private set;}
    IntPtr window, parent, videoWindow,coverWindow,coverBitmap;
    string? coverPath;
    MonitorPreferences framing=new();
    bool userPaused, automaticPaused;
    public string? PauseReason {get;private set;}
    public System.Drawing.Rectangle VideoFrame {get;private set;}
    DispatcherTimer? fadeTimer;
    bool fading;
    string? nextCover;
    HwndSource? visualSource;
    InteractiveVisual? interactiveVisual;
    SceneLayers? sceneLayers;
    AudioReaction? audioReaction;
    public InteractiveVisual? Interactive=>interactiveVisual;
    public bool Active => currentMedia is not null || interactiveVisual is not null;
    public bool Paused { get; private set; }
    public event Action? Failed;
    public event Action? LoopRequired;
    public event Action? SurfaceReady;
    public DesktopHost() {
        try {backdrop=new StaticBackdrop();}catch(Exception e){BackdropError=e.Message;}
        player = new MediaPlayer(vlc); player.SetVideoTitleDisplay(LibVLCSharp.Shared.Position.Disable,0);
        player.EncounteredError += (_,_) => Failed?.Invoke();
        player.EndReached += (_,_)=>LoopRequired?.Invoke();
        player.Playing += (_,_)=>SurfaceReady?.Invoke();
    }
    public int Volume { set => player.Volume = value; }
    public bool Healthy => !Active || (IsWindow(parent) && IsWindow(window));
    internal bool Playing => interactiveVisual is not null?!Paused:player.IsPlaying;
    internal long Position => player.Time;
    public void Play(string path, Forms.Screen screen,string? cover=null,bool transition=false)
    {
        string? snapshot=null;
        if(transition && Active && backdrop is not null)
        {
            string folder=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","transitions");System.IO.Directory.CreateDirectory(folder);
            string file=System.IO.Path.Combine(folder,Guid.NewGuid().ToString("N")+".png");
            if(TakeFramedSnapshot(file,screen))snapshot=file;
        }
        StopPlayback(snapshot is null);
        fading=snapshot is not null;nextCover=cover;coverPath=snapshot??cover;
        RecoveryCount=0;
        targetScreen=screen;
        if((snapshot??cover) is string picture && backdrop is not null)
        {
            try {backdrop.Apply(picture,screen);BackdropError=null;}catch(Exception e){BackdropError=e.Message;fading=false;}
        }
        CreateSurface(screen);
        player.Hwnd=videoWindow;
        currentMedia=new Media(vlc,path,FromType.FromPath);
        if (!player.Play(currentMedia)) { Stop(); throw new InvalidOperationException("Не удалось открыть видео."); }
        Paused=false;userPaused=false;automaticPaused=false;
    }
    public void PlayInteractive(SceneLayers layers,Forms.Screen screen,MonitorPreferences config,AudioReaction audio,string? cover)
    {
        Stop();targetScreen=screen;framing=config;sceneLayers=layers;audioReaction=audio;coverPath=cover;
        if(cover is not null)try{backdrop?.Apply(cover,screen);}catch(Exception e){BackdropError=e.Message;}
        CreateSurface(screen);CreateInteractive();Paused=false;userPaused=false;automaticPaused=false;
    }
    void CreateInteractive()
    {
        if(targetScreen is null || sceneLayers is null || audioReaction is null)return;
        ShowWindow(videoWindow,0);
        interactiveVisual=new InteractiveVisual(sceneLayers,framing,targetScreen,audioReaction);
        visualSource=new HwndSource(new HwndSourceParameters("VWP Interactive"){ParentWindow=window,WindowStyle=0x40000000|0x10000000,Width=targetScreen.Bounds.Width,Height=targetScreen.Bounds.Height});
        // Explorer redirects its wallpaper host. Hardware WPF content can update
        // off-screen while the user sees only the static Windows wallpaper.
        if(visualSource.CompositionTarget is not null)
            visualSource.CompositionTarget.RenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
        visualSource.RootVisual=interactiveVisual;
    }
    void CreateSurface(Forms.Screen screen)
    {
        IntPtr progman = FindWindow("Progman",null);
        if (progman == IntPtr.Zero) throw new InvalidOperationException("Explorer не найден.");
        // New Windows 11 uses a WorkerW child. Prefer it over transient legacy siblings.
        parent=FindWindowEx(progman,IntPtr.Zero,"WorkerW",null);
        if(parent==IntPtr.Zero){SendMessageTimeout(progman,0x052C,new IntPtr(0xD),new IntPtr(1),2,1000,out _);parent=FindWindowEx(progman,IntPtr.Zero,"WorkerW",null);}
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
        VideoFrame=System.Drawing.Rectangle.Empty;
        var point=new Point { X=rect.X,Y=rect.Y };
        MapWindowPoints(IntPtr.Zero,parent,ref point,1);
        window=CreateWindowEx(0x08000080,"STATIC","VWP Wallpaper",0x40000000|0x10000000|0x06000000,point.X,point.Y,rect.Width,rect.Height,parent,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        if (window==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        if(fading){SetWindowLongPtr(window,-20,new IntPtr(0x08080080));SetLayeredWindowAttributes(window,0,0,2);}
        coverWindow=CreateWindowEx(0,"STATIC","VWP Cover",0x40000000|0x10000000|0x06000000|0x4E,0,0,rect.Width,rect.Height,window,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        if(coverWindow==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
        SetCoverBitmap(coverPath,screen);
        videoWindow=CreateWindowEx(0,"STATIC","VWP Video",0x40000000|0x10000000|0x06000000,0,0,rect.Width,rect.Height,window,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        if(videoWindow==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
        SetWindowPos(coverWindow,new IntPtr(1),0,0,0,0,0x3); // HWND_BOTTOM: keep the poster under VLC.
        int enabled=1;
        DwmSetWindowAttribute(window,12,ref enabled,4); // EXCLUDED_FROM_PEEK
        DwmSetWindowAttribute(window,3,ref enabled,4); // TRANSITIONS_FORCEDISABLED
    }
    public void Recover()
    {
        if(Healthy || !Active || targetScreen is null)return;
        if(interactiveVisual is not null)
        {
            bool paused=Paused;interactiveVisual.Dispose();visualSource?.Dispose();interactiveVisual=null;visualSource=null;
            if(IsWindow(window))DestroyWindow(window);CreateSurface(targetScreen);CreateInteractive();interactiveVisual?.SetPaused(paused);RecoveryCount++;return;
        }
        recoveryPosition=Math.Max(0,player.Time);recoveryPaused=Paused;recoveryPending=true;
        player.Stop();if(IsWindow(window))DestroyWindow(window);ReleaseCoverBitmap();window=IntPtr.Zero;parent=IntPtr.Zero;coverWindow=IntPtr.Zero;
        CreateSurface(targetScreen);player.Hwnd=videoWindow;
        if(currentMedia is null || !player.Play(currentMedia))throw new InvalidOperationException("Не удалось восстановить видеообои.");
        RecoveryCount++;
    }
    public void CompleteRecovery()
    {
        UpdateFrame();
        if(fading && fadeTimer is null)
        {
            int alpha=0;fadeTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};
            fadeTimer.Tick+=(_,_)=>{
                alpha=Math.Min(255,alpha+10);SetLayeredWindowAttributes(window,0,(byte)alpha,2);
                if(alpha<255)return;fadeTimer?.Stop();fadeTimer=null;fading=false;SetWindowLongPtr(window,-20,new IntPtr(0x08000080));
                if(nextCover is not null && targetScreen is not null){coverPath=nextCover;SetCoverBitmap(nextCover,targetScreen);try{backdrop?.Apply(nextCover,targetScreen);}catch(Exception e){BackdropError=e.Message;}}
                nextCover=null;
            };fadeTimer.Start();
        }
        if(!recoveryPending || !Active)return;
        recoveryPending=false;if(recoveryPosition>0)player.Time=recoveryPosition;
        player.SetPause(recoveryPaused);Paused=recoveryPaused;
    }
    public void TogglePause() { SetUserPaused(!userPaused); }
    public void SetUserPaused(bool value) {userPaused=value;SyncPause();}
    public void SetAutomaticPause(string? reason) {PauseReason=reason;automaticPaused=reason is not null;SyncPause();}
    void SyncPause() {if(!Active)return;bool value=userPaused||automaticPaused;if(Paused==value)return;Paused=value;if(interactiveVisual is not null)interactiveVisual.SetPaused(value);else player.SetPause(value);}
    public void SetFraming(MonitorPreferences settings) {framing=settings;interactiveVisual?.Configure(settings);UpdateFrame();}
    public void RefreshFrame()=>UpdateFrame();
    void UpdateFrame()
    {
        if(targetScreen is null || videoWindow==IntPtr.Zero || interactiveVisual is not null)return;
        uint width=0,height=0;player.Size(0,ref width,ref height);
        if(width==0||height==0)return;
        var next=PlaybackRules.Frame((int)width,(int)height,targetScreen.Bounds.Width,targetScreen.Bounds.Height,framing.Fit,framing.FocusX,framing.FocusY);
        if(next==VideoFrame)return;VideoFrame=next;
        SetWindowPos(videoWindow,IntPtr.Zero,VideoFrame.X,VideoFrame.Y,VideoFrame.Width,VideoFrame.Height,0x14);
    }
    public void Replay() { if(!Active || currentMedia is null || Paused)return;player.Stop();player.Play(currentMedia); }
    void SetCoverBitmap(string? path,Forms.Screen screen)
    {
        if(coverWindow==IntPtr.Zero || path is null || !System.IO.File.Exists(path))return;
        try
        {
            using var source=System.Drawing.Image.FromFile(path);
            using var target=new System.Drawing.Bitmap(screen.Bounds.Width,screen.Bounds.Height);
            using(var graphics=System.Drawing.Graphics.FromImage(target))
            {
                graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source,0,0,target.Width,target.Height);
            }
            IntPtr bitmap=target.GetHbitmap();
            SendMessage(coverWindow,0x172,IntPtr.Zero,bitmap); // STM_SETIMAGE / IMAGE_BITMAP
            ReleaseCoverBitmap();coverBitmap=bitmap;
        }
        catch(Exception e){BackdropError=e.Message;}
    }
    void ReleaseCoverBitmap(){if(coverBitmap!=IntPtr.Zero){DeleteObject(coverBitmap);coverBitmap=IntPtr.Zero;}}
    internal bool BackdropApplied=>backdrop?.Applied==true;
    internal string? StaticPicture(Forms.Screen screen)=>backdrop?.ReadCurrent(screen);
    public void UpdateBackdrop()
    {
        if(!Active || BackdropApplied || !player.IsPlaying || backdrop is null || targetScreen is null)return;
        try {
            string folder=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","snapshots");
            System.IO.Directory.CreateDirectory(folder);string picture=System.IO.Path.Combine(folder,"current-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(targetScreen.DeviceName)))[..12]+".png");
            if(TakeFramedSnapshot(picture,targetScreen))backdrop.Apply(picture,targetScreen);
        }catch(Exception e){BackdropError=e.Message;}
    }
    bool TakeFramedSnapshot(string picture,Forms.Screen screen)
    {
        string raw=picture+".raw.png";
        try
        {
            if(!player.TakeSnapshot(0,raw,0,0)||!System.IO.File.Exists(raw))return false;
            using var source=System.Drawing.Image.FromFile(raw);using var image=new System.Drawing.Bitmap(screen.Bounds.Width,screen.Bounds.Height);using var graphics=System.Drawing.Graphics.FromImage(image);
            graphics.Clear(System.Drawing.Color.FromArgb(20,20,28));graphics.DrawImage(source,PlaybackRules.Frame(source.Width,source.Height,image.Width,image.Height,framing.Fit,framing.FocusX,framing.FocusY));image.Save(picture,System.Drawing.Imaging.ImageFormat.Png);return true;
        }
        catch{return false;}
        finally{if(System.IO.File.Exists(raw))System.IO.File.Delete(raw);}
    }
    public void Stop()=>StopPlayback(true);
    void StopPlayback(bool restore) {interactiveVisual?.Dispose();visualSource?.Dispose();interactiveVisual=null;visualSource=null;sceneLayers=null;audioReaction=null;fadeTimer?.Stop();fadeTimer=null;fading=false;nextCover=null;recoveryPending=false;player.Stop();currentMedia?.Dispose();currentMedia=null;if(IsWindow(window))DestroyWindow(window);ReleaseCoverBitmap();window=IntPtr.Zero;videoWindow=IntPtr.Zero;coverWindow=IntPtr.Zero;parent=IntPtr.Zero;coverPath=null;targetScreen=null;Paused=false;if(restore)try{backdrop?.Restore();}catch(Exception e){BackdropError=e.Message;}}
    public void Dispose() {Stop();try{backdrop?.Dispose();}catch(Exception e){BackdropError=e.Message;}player.Dispose();vlc.Dispose();}
    [StructLayout(LayoutKind.Sequential)] struct Point { public int X,Y; }
    delegate bool EnumProc(IntPtr hwnd,IntPtr param);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string name,string? title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string name,string? title);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc,IntPtr param);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp,uint flags,uint timeout,out IntPtr result);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr objectHandle);
    [DllImport("user32.dll")] static extern int MapWindowPoints(IntPtr from,IntPtr to,ref Point point,uint count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateWindowEx(uint ex,string cls,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr window,uint key,byte alpha,uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,uint attribute,ref int value,uint size);
}
