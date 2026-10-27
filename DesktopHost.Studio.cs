using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Forms=System.Windows.Forms;

namespace VWP;
public sealed partial class DesktopHost
{
    StudioSurface? studioSurface;
    StudioProject? studioProject;
    string studioProfile="Balance";
    int studioGeneration;
    public string? StudioError {get;private set;}
    internal StudioStatistics? StudioStats=>studioSurface?.Statistics;
    internal bool StudioAudio=>studioSurface?.WantsAudio==true;
    public async Task PlayStudio(StudioProject project,Forms.Screen screen,string profile,AudioReaction audio,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();
        string? previous=await CitySnapshot();string? snapshot;
        try{cancellation.ThrowIfCancellationRequested();snapshot=PrepareTransition(screen,previous);}
        finally{if(previous is not null)try{File.Delete(previous);}catch{}}
        StopPlayback(snapshot is null,snapshot is not null);
        targetScreen=screen;studioProject=project;studioProfile=profile;audioReaction=audio;coverPath=project.Asset(project.Cover);
        transitionSnapshot=snapshot;nextCover=coverPath;transitionWaiting=sceneTransition is not null;
        try{backdrop?.Apply(snapshot??coverPath,screen);}catch(Exception error){BackdropError=error.Message;}
        CreateSurface(screen);ShowWindow(videoWindow,0);Paused=false;userPaused=false;automaticPaused=false;
        await CreateStudio(cancellation);
    }
    async Task CreateStudio(CancellationToken cancellation=default)
    {
        if(targetScreen is null||studioProject is null)return;
        int generation=++studioGeneration;var surface=new StudioSurface();studioSurface=surface;
        surface.Control.Dock=Forms.DockStyle.None;surface.Control.Size=targetScreen.Bounds.Size;surface.Control.CreateControl();
        SetParent(surface.Control.Handle,window);SetWindowPos(surface.Control.Handle,IntPtr.Zero,0,0,targetScreen.Bounds.Width,targetScreen.Bounds.Height,0x14);ShowWindow(surface.Control.Handle,0);
        surface.Message+=json=>{using var message=System.Text.Json.JsonDocument.Parse(json);if(generation!=studioGeneration)return;string? type=message.RootElement.GetProperty("type").GetString();if(type=="recovering")ShowWindow(surface.Control.Handle,0);if(type=="restored")ShowWindow(surface.Control.Handle,5);if(type=="error"){StudioError=message.RootElement.GetProperty("message").GetString();surface.SetPaused(true);ShowWindow(surface.Control.Handle,0);Failed?.Invoke();}};
        try
        {
            await surface.Initialize(studioProject,studioProfile,false,targetScreen,audioReaction,cancellation:cancellation);
            cancellation.ThrowIfCancellationRequested();if(generation!=studioGeneration)return;
            surface.SetFraming(framing);
            ShowWindow(surface.Control.Handle,5);surface.SetPaused(Paused);StartTransition();SurfaceReady?.Invoke();
        }
        catch{if(generation==studioGeneration)Stop();throw;}
    }
    async void RecoverStudio()
    {
        if(targetScreen is null)return;
        if(sceneTransition is not null)FinishTransition();
        studioSurface?.Dispose();studioSurface=null;studioGeneration++;
        if(IsWindow(window))DestroyWindow(window);ReleaseCoverBitmap();CreateSurface(targetScreen);ShowWindow(videoWindow,0);
        try{await CreateStudio();RecoveryCount++;}catch{Failed?.Invoke();}
    }
    internal async Task<string?> StudioSnapshot()
    {
        if(studioSurface?.Ready!=true)return null;
        string folder=Environment.GetCommandLineArgs().AnyVerification()?Path.Combine(StudioProject.DataRoot,"transitions"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","transitions");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"studio-"+Guid.NewGuid().ToString("N")+".png");
        try{await studioSurface.Capture(file);return file;}catch{try{File.Delete(file);}catch{}return null;}
    }
}
