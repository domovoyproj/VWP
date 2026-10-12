using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace VWP;
public sealed partial class DesktopHost
{
    CitySurface? citySurface;
    bool cityReady;
    int cityGeneration;
    string cityProfile="Quality";
    int cityPalette;
    public bool IsCity=>citySurface is not null;
    public async Task PlayCity(StarAccess access,Forms.Screen screen,string cover,string profile,int palette=0)
    {
        if(!access.Unlocked)throw new InvalidOperationException("Город открывается после подтверждения звезды GitHub.");
        int intent=cityGeneration;string? previous=await CitySnapshot();
        string? snapshot;
        try{if(intent!=cityGeneration||!access.Unlocked)return;snapshot=PrepareTransition(screen,previous);}
        finally{if(previous is not null)try{File.Delete(previous);}catch{}}
        StopPlayback(snapshot is null,snapshot is not null);targetScreen=screen;coverPath=cover;cityProfile=profile;cityPalette=palette;
        transitionSnapshot=snapshot;nextCover=cover;transitionWaiting=sceneTransition is not null;
        try{backdrop?.Apply(snapshot??cover,screen);}catch(Exception e){BackdropError=e.Message;}
        CreateSurface(screen);ShowWindow(videoWindow,0);Paused=false;userPaused=false;automaticPaused=false;
        await CreateCity();
    }
    async Task CreateCity()
    {
        if(targetScreen is null)return;int generation=++cityGeneration;
        var city=new CitySurface();citySurface=city;
        city.Control.Dock=Forms.DockStyle.None;city.Control.Size=targetScreen.Bounds.Size;city.Control.CreateControl();
        SetParent(city.Control.Handle,window);SetWindowPos(city.Control.Handle,IntPtr.Zero,0,0,targetScreen.Bounds.Width,targetScreen.Bounds.Height,0x14);
        ShowWindow(city.Control.Handle,0);
        city.Message+=json=>{using var message=System.Text.Json.JsonDocument.Parse(json);if(message.RootElement.GetProperty("type").GetString()=="error")Failed?.Invoke();};
        try
        {
            await city.Initialize(cityProfile,false,null,cityPalette);
            if(generation!=cityGeneration)return;
            ShowWindow(city.Control.Handle,5);cityReady=true;city.SetPaused(Paused);StartTransition();SurfaceReady?.Invoke();
        }
        catch{if(generation==cityGeneration)Stop();throw;}
    }
    async void RecoverCity()
    {
        if(targetScreen is null)return;
        if(sceneTransition is not null)FinishTransition();
        citySurface?.Dispose();citySurface=null;cityReady=false;cityGeneration++;
        if(IsWindow(window))DestroyWindow(window);ReleaseCoverBitmap();CreateSurface(targetScreen);ShowWindow(videoWindow,0);
        try{await CreateCity();RecoveryCount++;}catch{Failed?.Invoke();}
    }
    internal async Task<string> CityState()=>citySurface is null?"null":await citySurface.State();
    internal async Task<string?> CitySnapshot()
    {
        var city=citySurface;if(city is null||!cityReady)return null;
        string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","transitions");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"city-"+Guid.NewGuid().ToString("N")+".png");
        try{await city.Capture(file);if(File.Exists(file))return file;}catch{}
        try{File.Delete(file);}catch{}return null;
    }
    internal void RestoreCityState(string state)=>citySurface?.SetState(state);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child,IntPtr parent);
}
