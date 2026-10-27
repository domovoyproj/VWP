using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Windows.Interop;
using LibVLCSharp.Shared;
using Forms = System.Windows.Forms;
namespace VWP;
public sealed partial class DesktopHost : IDisposable
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
    DesktopTransition? sceneTransition;
    bool transitionWaiting;
    string? transitionSnapshot;
    readonly IntPtr surfaceParent;
    string? nextCover;
    HwndSource? visualSource;
    InteractiveVisual? interactiveVisual;
    SceneLayers? sceneLayers;
    AudioReaction? audioReaction;
    public InteractiveVisual? Interactive=>interactiveVisual;
    public bool Active => currentMedia is not null || interactiveVisual is not null || citySurface is not null || studioSurface is not null;
    public bool Paused { get; private set; }
    public event Action? Failed;
    public event Action? LoopRequired;
    public event Action? SurfaceReady;
    public DesktopHost():this(IntPtr.Zero){}
    internal DesktopHost(IntPtr embeddedParent) {
        surfaceParent=embeddedParent;
        if(surfaceParent==IntPtr.Zero)try {backdrop=new StaticBackdrop();}catch(Exception e){BackdropError=e.Message;}
        player = new MediaPlayer(vlc); player.SetVideoTitleDisplay(LibVLCSharp.Shared.Position.Disable,0);
        player.EncounteredError += (_,_) => Failed?.Invoke();
        player.EndReached += (_,_)=>LoopRequired?.Invoke();
        player.Playing += (_,_)=>SurfaceReady?.Invoke();
    }
    public int Volume { set => player.Volume = value; }
    public bool Healthy => !Active || (IsWindow(parent) && IsWindow(window));
    internal bool Playing => interactiveVisual is not null||citySurface is not null||studioSurface is not null?!Paused:player.IsPlaying;
    internal long Position => player.Time;
    public void Play(string path, Forms.Screen screen,string? cover=null,bool transition=false,string? previousFrame=null)
    {
        string? snapshot=transition?PrepareTransition(screen,previousFrame):null;
        StopPlayback(snapshot is null,snapshot is not null);
        transitionSnapshot=snapshot;nextCover=cover;coverPath=cover;transitionWaiting=sceneTransition is not null;
        RecoveryCount=0;
        targetScreen=screen;
        if((snapshot??cover) is string picture && backdrop is not null)
        {
            try {backdrop.Apply(picture,screen);BackdropError=null;}catch(Exception e){BackdropError=e.Message;}
        }
        CreateSurface(screen);
        player.Hwnd=videoWindow;
        currentMedia=new Media(vlc,path,FromType.FromPath);
        if (!player.Play(currentMedia)) { Stop(); throw new InvalidOperationException("Не удалось открыть видео."); }
        Paused=false;userPaused=false;automaticPaused=false;
        StartTransition();
    }
    string? PrepareTransition(Forms.Screen screen,string? previousFrame=null)
    {
        if(!Active||!Healthy)return null;
        string folder=Environment.GetCommandLineArgs().AnyVerification()?System.IO.Path.Combine(StudioProject.DataRoot,"transitions"):System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","transitions");System.IO.Directory.CreateDirectory(folder);
        string file=System.IO.Path.Combine(folder,Guid.NewGuid().ToString("N")+".png");
        string? snapshot=null;
        if(previousFrame is not null&&System.IO.File.Exists(previousFrame)){System.IO.File.Copy(previousFrame,file);snapshot=file;}
        else if(TakeFramedSnapshot(file,screen))snapshot=file;
        else if(coverPath is not null&&System.IO.File.Exists(coverPath)){System.IO.File.Copy(coverPath,file);snapshot=file;}
        if(snapshot is not null){sceneTransition?.ComposeOver(snapshot);var overlay=new DesktopTransition(parent,screen.Bounds,snapshot);sceneTransition?.Dispose();sceneTransition=overlay;}
        return snapshot;
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
        if(surfaceParent!=IntPtr.Zero)parent=surfaceParent;
        else
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
        }
        var rect=screen.Bounds;
        VideoFrame=System.Drawing.Rectangle.Empty;
        var point=new Point { X=rect.X,Y=rect.Y };
        MapWindowPoints(IntPtr.Zero,parent,ref point,1);
        window=CreateWindowEx(0x08000080,"STATIC","VWP Wallpaper",0x40000000|0x10000000|0x06000000,point.X,point.Y,rect.Width,rect.Height,parent,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        if (window==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        coverWindow=CreateWindowEx(0,"STATIC","VWP Cover",0x40000000|0x10000000|0x06000000|0x4E,0,0,rect.Width,rect.Height,window,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        if(coverWindow==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
        SetCoverBitmap(coverPath,screen);
        videoWindow=CreateWindowEx(0,"STATIC","VWP Video",0x40000000|0x10000000|0x06000000,0,0,rect.Width,rect.Height,window,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        if(videoWindow==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
        SetWindowPos(coverWindow,new IntPtr(1),0,0,0,0,0x3); // HWND_BOTTOM: keep the poster under VLC.
        int enabled=1;
        DwmSetWindowAttribute(window,12,ref enabled,4); // EXCLUDED_FROM_PEEK
        DwmSetWindowAttribute(window,3,ref enabled,4); // TRANSITIONS_FORCEDISABLED
        sceneTransition?.BringToFront();
    }
    public void Recover()
    {
        if(Healthy || !Active || targetScreen is null)return;
        if(studioSurface is not null){RecoverStudio();return;}
        if(citySurface is not null){RecoverCity();return;}
        if(sceneTransition is not null)FinishTransition();
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
        if(studioSurface is not null){studioSurface.SetPaused(Paused);return;}
        if(citySurface is not null){citySurface.SetPaused(Paused);return;}
        if(Active && interactiveVisual is null && (userPaused||automaticPaused) && !transitionWaiting)player.SetPause(true);
        if(!recoveryPending || !Active)return;
        recoveryPending=false;if(recoveryPosition>0)player.Time=recoveryPosition;
        player.SetPause(recoveryPaused);Paused=recoveryPaused;
    }
    void StartTransition()
    {
        if(sceneTransition is null)return;
        var waiting=Stopwatch.StartNew();Stopwatch? blend=null;
        fadeTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};
        fadeTimer.Tick+=(_,_)=>
        {
            if(sceneTransition is null)return;
            if(blend is null)
            {
                uint width=0,height=0;player.Size(0,ref width,ref height);
                // Playing precedes the first displayed frame. Keep the old image during decoder warm-up.
                bool ready=waiting.ElapsedMilliseconds>=180 && (studioSurface is not null?studioSurface.Ready:citySurface is not null?cityReady:width>0&&height>0&&player.Time>0);
                if(!ready && waiting.ElapsedMilliseconds<8000)return;
                UpdateFrame();transitionWaiting=false;if(Paused&&citySurface is null)player.SetPause(true);blend=Stopwatch.StartNew();
            }
            double progress=blend.Elapsed.TotalMilliseconds/1000;
            if(!Healthy||!sceneTransition.Valid){FinishTransition();return;}
            try{sceneTransition.SetProgress(progress);}
            catch(Win32Exception){FinishTransition();return;} // Explorer can destroy its child windows during a restart.
            if(progress<1)return;
            FinishTransition();
        };
        fadeTimer.Start();
    }
    void FinishTransition()
    {
        fadeTimer?.Stop();fadeTimer=null;sceneTransition?.Dispose();sceneTransition=null;transitionWaiting=false;
        bool replaced=backdrop is null;
        if(nextCover is not null && targetScreen is not null)try{backdrop?.Apply(nextCover,targetScreen);replaced=System.IO.File.Exists(nextCover);BackdropError=null;}catch(Exception e){BackdropError=e.Message;}
        nextCover=null;
        if(replaced){DeleteTransitionSnapshot();}
        else if(transitionSnapshot is not null && targetScreen is not null)
        {
            // Keep this file while Windows uses it as its fallback picture.
            try{if(TakeFramedSnapshot(transitionSnapshot,targetScreen))backdrop?.Apply(transitionSnapshot,targetScreen);}catch(Exception e){BackdropError=e.Message;}
        }
    }
    public void TogglePause() { SetUserPaused(!userPaused); }
    public void SetUserPaused(bool value) {userPaused=value;SyncPause();}
    public void SetAutomaticPause(string? reason) {PauseReason=reason;automaticPaused=reason is not null;SyncPause();}
    void SyncPause() {if(!Active)return;bool value=userPaused||automaticPaused;if(Paused==value)return;Paused=value;if(studioSurface is not null)studioSurface.SetPaused(value);else if(citySurface is not null)citySurface.SetPaused(value);else if(interactiveVisual is not null)interactiveVisual.SetPaused(value);else if(!transitionWaiting)player.SetPause(value);}
    public void SetFraming(MonitorPreferences settings) {framing=settings;interactiveVisual?.Configure(settings);studioSurface?.SetFraming(settings);UpdateFrame();}
    public void RefreshFrame()=>UpdateFrame();
    void UpdateFrame()
    {
        if(targetScreen is null || videoWindow==IntPtr.Zero || interactiveVisual is not null || citySurface is not null || studioSurface is not null)return;
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
    void DeleteTransitionSnapshot(){if(transitionSnapshot is not null)try{System.IO.File.Delete(transitionSnapshot);}catch{}transitionSnapshot=null;}
    void StopPlayback(bool restore,bool preserveTransition=false)
    {
        ShowClock(false);
        StudioError=null;
        studioSurface?.Dispose();studioSurface=null;studioProject=null;studioGeneration++;
        citySurface?.Dispose();citySurface=null;cityReady=false;cityGeneration++;
        interactiveVisual?.Dispose();visualSource?.Dispose();interactiveVisual=null;visualSource=null;sceneLayers=null;audioReaction=null;
        fadeTimer?.Stop();fadeTimer=null;transitionWaiting=false;
        if(!preserveTransition){sceneTransition?.Dispose();sceneTransition=null;}
        nextCover=null;recoveryPending=false;
        player.Stop();currentMedia?.Dispose();currentMedia=null;
        if(IsWindow(window))DestroyWindow(window);ReleaseCoverBitmap();
        window=IntPtr.Zero;videoWindow=IntPtr.Zero;coverWindow=IntPtr.Zero;parent=IntPtr.Zero;coverPath=null;targetScreen=null;Paused=false;
        if(restore)try{backdrop?.Restore();}catch(Exception e){BackdropError=e.Message;}
        DeleteTransitionSnapshot();
    }
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
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,uint attribute,ref int value,uint size);
}
