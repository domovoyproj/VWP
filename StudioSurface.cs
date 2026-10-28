using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Forms=System.Windows.Forms;

namespace VWP;

internal sealed class StudioSurface : IDisposable
{
    public WebView2 Control {get;}=new(){DefaultBackgroundColor=Color.FromArgb(11,16,24),Dock=Forms.DockStyle.Fill};
    readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly Forms.Timer inputTimer=new(){Interval=33};
    StudioProject? project;
    string assetHost="";
    StudioSettings? settings;
    StudioQuality quality=StudioQuality.Resolve("Balance");
    object framing=new{fit="Fill",focusX=.5,focusY=.5};
    Forms.Screen? screen;
    AudioReaction? audio;
    bool disposed,paused,preview,manual,recovering,lightweight;
    int recoveries;
    string? cachedState;
    TaskCompletionSource? recoveryReady;
    TaskCompletionSource? stateRestored;
    public StudioStatistics Statistics {get;private set;}=new();
    public bool WantsAudio=>settings?.AudioReactive==true;
    public bool Ready {get;private set;}
    internal int RecoveryCount=>recoveries;
    internal bool Recovering=>recovering;
    public event Action<string>? Message;
    public event Action? Changed;
    public StudioSurface(){inputTimer.Tick+=(_,_)=>SendInput();}
    public async Task Initialize(StudioProject source,string profile,bool preview,Forms.Screen? screen=null,AudioReaction? audio=null,StudioSettings? overrides=null,bool manual=false,CancellationToken cancellation=default,bool lightweight=false)
    {
        source.Validate();project=source;settings=(overrides??source.Settings).Copy();settings.Validate();
        this.preview=preview;this.screen=screen;this.audio=audio;this.manual=manual;this.lightweight=lightweight;quality=StudioQuality.Resolve(profile,lightweight);
        string data=Environment.GetCommandLineArgs().AnyVerification()?Path.Combine(Environment.CurrentDirectory,"artifacts","studio-browser-"+Environment.ProcessId):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP","studio-browser");
        var environment=await CoreWebView2Environment.CreateAsync(null,data,new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required --disable-background-timer-throttling --disable-renderer-backgrounding --disable-backgrounding-occluded-windows"));
        cancellation.ThrowIfCancellationRequested();if(disposed)return;
        await Control.EnsureCoreWebView2Async(environment);if(disposed)return;
        var web=Control.CoreWebView2;web.Settings.AreDefaultContextMenusEnabled=false;web.Settings.AreDevToolsEnabled=false;web.Settings.AreBrowserAcceleratorKeysEnabled=false;web.Settings.IsStatusBarEnabled=false;
        web.SetVirtualHostNameToFolderMapping("vwp-studio.local",Path.Combine(AppContext.BaseDirectory,"assets","studio","runtime"),CoreWebView2HostResourceAccessKind.DenyCors);
        assetHost=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source.Folder+File.GetLastWriteTimeUtc(source.FileName).Ticks)))[..16].ToLowerInvariant()+".vwp-project.local";
        web.SetVirtualHostNameToFolderMapping(assetHost,source.Folder,CoreWebView2HostResourceAccessKind.Allow);
        web.NavigationStarting+=(_,e)=>{if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out var url)||url.Scheme!="https"||url.Host!="vwp-studio.local")e.Cancel=true;};
        web.NewWindowRequested+=(_,e)=>e.Handled=true;web.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
        web.NavigationCompleted+=(_,e)=>{if(!e.IsSuccess&&e.WebErrorStatus!=CoreWebView2WebErrorStatus.OperationCanceled)Fail("Не удалось открыть сцену: "+e.WebErrorStatus);};
        web.WebMessageReceived+=async(_,e)=>
        {
            if(disposed||!e.Source.StartsWith("https://vwp-studio.local/",StringComparison.Ordinal))return;
            try
            {
                using var json=JsonDocument.Parse(e.WebMessageAsJson);string type=json.RootElement.GetProperty("type").GetString()??"";
                if(type=="boot"){SendProject();return;}
                if(type=="ready")
                {
                    if(cachedState is not null){await web.ExecuteScriptAsync("window.studio.restore("+cachedState+").then(()=>window.chrome.webview.postMessage({type:'restored'}))");return;}
                    cachedState=await State();CompleteReady();
                }
                if(type=="restored")
                {
                    CompleteReady();
                }
                if(type=="state-restored")stateRestored?.TrySetResult();
                if(type=="stats")Statistics=JsonSerializer.Deserialize<StudioStatistics>(json.RootElement.GetProperty("stats").GetRawText(),StudioProject.Json)??new();
                if(type=="error")Fail(json.RootElement.GetProperty("message").GetString()??"Ошибка сцены.");
                if(type is "contextlost" or "reload")await Recover();
                if(type=="dirty"){cachedState=await State();Changed?.Invoke();}
                Message?.Invoke(e.WebMessageAsJson);
            }
            catch(Exception error){if(!disposed)Fail("Сцена: "+error.Message);}
        };
        web.ProcessFailed+=async(_,e)=>{if(e.ProcessFailedKind is CoreWebView2ProcessFailedKind.RenderProcessExited or CoreWebView2ProcessFailedKind.BrowserProcessExited)await Recover();};
        web.Navigate("https://vwp-studio.local/index.html");
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(90),cancellation);
    }
    void CompleteReady(){Ready=true;SetPaused(paused);if(!preview&&!manual)inputTimer.Start();ready.TrySetResult();recoveryReady?.TrySetResult();}
    void SendProject()
    {
        if(disposed||project is null||Control.CoreWebView2 is null)return;
        var payload=project.Copy();payload.Settings=settings!.Copy();
        Post(new{type="load",project=payload,@base="https://"+assetHost+"/",quality,options=new{preview,manual,framing}});
    }
    void Post(object value){if(!disposed&&Control.CoreWebView2 is not null)Control.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(value,StudioProject.Json));}
    public void SetPaused(bool value){paused=value;Post(new{type="pause",value});if(value)inputTimer.Stop();else if(Ready&&!preview&&!manual)inputTimer.Start();}
    public void Configure(string profile,StudioSettings value)
    {
        value.Validate();settings=value.Copy();quality=StudioQuality.Resolve(profile,lightweight);Post(new{type="settings",value=settings});Post(new{type="quality",value=quality});
    }
    internal void SetFraming(MonitorPreferences value){framing=new{fit=value.Fit=="Fit"?"Fit":"Fill",focusX=double.IsFinite(value.FocusX)?Math.Clamp(value.FocusX,0,1):.5,focusY=double.IsFinite(value.FocusY)?Math.Clamp(value.FocusY,0,1):.5};Post(new{type="framing",value=framing});}
    void SendInput()
    {
        if(paused||screen is null||!GetCursorPos(out var point))return;
        Post(new{type="input",cursor=new[]{Math.Clamp((point.X-screen.Bounds.Left)/(double)screen.Bounds.Width-.5,-.5,.5),Math.Clamp((point.Y-screen.Bounds.Top)/(double)screen.Bounds.Height-.5,-.5,.5)},audio=WantsAudio?audio?.Level??0:0});
    }
    public async Task<string> Execute(string code)
    {
        if(disposed||Control.CoreWebView2 is null)throw new ObjectDisposedException(nameof(StudioSurface));
        return await Control.CoreWebView2.ExecuteScriptAsync(code).WaitAsync(TimeSpan.FromSeconds(10));
    }
    public async Task<string> State()=>await Execute("window.studio.state()");
    internal async Task Restore(string value)
    {
        stateRestored=new(TaskCreationOptions.RunContinuationsAsynchronously);
        try{await Execute("window.studio.restore("+value+").then(()=>window.chrome.webview.postMessage({type:'state-restored'}))");await stateRestored.Task.WaitAsync(TimeSpan.FromSeconds(30));cachedState=value;}
        finally{stateRestored=null;}
    }
    public async Task<StudioStatistics> ReadStatistics()=>Statistics=JsonSerializer.Deserialize<StudioStatistics>(await Execute("window.studio.stats()"),StudioProject.Json)??new();
    public Task Seek(double seconds)=>Execute("window.studio.seek("+JsonSerializer.Serialize(seconds)+")");
    public Task Tool(int layer,int channel,double radius,bool erase)=>Execute("window.studio.tool("+JsonSerializer.Serialize(new{layer,channel,radius,erase})+")");
    public Task StopTool()=>Execute("window.studio.tool(null)");
    public Task Layer(int index,StudioLayer layer)=>Execute("window.studio.layer("+index+","+JsonSerializer.Serialize(layer,StudioProject.Json)+")");
    public async Task Capture(string path)
    {
        if(Control.CoreWebView2 is null)throw new InvalidOperationException("Сцена ещё не готова.");
        using var file=File.Create(path);await Control.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
    }
    internal async Task SetExportViewport(int width,int height)
    {
        await Control.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride",JsonSerializer.Serialize(new{width,height,deviceScaleFactor=1,mobile=false,screenWidth=width,screenHeight=height}));
    }
    internal async Task<byte[]> ExportFrame(int width,int height)
    {
        if(!Ready)throw new InvalidOperationException("Сцена восстанавливается. Дождись готовности и повтори сохранение.");
        // Read the freshly rendered canvas in the same JS turn. Browser compositor
        // screenshots can stall or return stale pixels while a native window is hidden.
        using var json=JsonDocument.Parse(await Execute("window.studio.capture()"));
        if(json.RootElement.GetProperty("width").GetInt32()!=width||json.RootElement.GetProperty("height").GetInt32()!=height)throw new InvalidOperationException("Размер кадра не совпадает с разрешением экспорта.");
        return Convert.FromBase64String(json.RootElement.GetProperty("png").GetString()!);
    }
    internal async Task CaptureExport(string path,int width,int height)=>await File.WriteAllBytesAsync(path,await ExportFrame(width,height));
    internal Task ResetExportViewport()=>Control.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.clearDeviceMetricsOverride","{}");
    async Task Recover()
    {
        if(disposed||recovering)return;
        if(++recoveries>2){Fail("Сцена потеряла соединение с видеокартой. Открой её снова или выбери «Экономия».");return;}
        recovering=true;inputTimer.Stop();Ready=false;
        Message?.Invoke(JsonSerializer.Serialize(new{type="recovering"}));
        try
        {
            try{cachedState=await State();}catch
            {
                if(cachedState is not null)
                {
                    var cache=System.Text.Json.Nodes.JsonNode.Parse(cachedState)!;cache["time"]=Statistics.Time;cache["paused"]=paused;
                    cache["settings"]=JsonSerializer.SerializeToNode(settings,StudioProject.Json);cache["project"]=JsonSerializer.SerializeToNode(project,StudioProject.Json);cachedState=cache.ToJsonString();
                }
            }
            recoveryReady=new(TaskCreationOptions.RunContinuationsAsynchronously);Control.CoreWebView2.Reload();await recoveryReady.Task.WaitAsync(TimeSpan.FromSeconds(90));
        }
        catch(Exception error){if(!disposed)Fail("Не удалось восстановить сцену: "+error.Message);}
        finally{recovering=false;recoveryReady=null;}
    }
    void Fail(string message){Ready=false;inputTimer.Stop();ready.TrySetException(new InvalidOperationException(message));recoveryReady?.TrySetException(new InvalidOperationException(message));Message?.Invoke(JsonSerializer.Serialize(new{type="error",message}));}
    public void Dispose(){if(disposed)return;disposed=true;inputTimer.Stop();inputTimer.Dispose();ready.TrySetCanceled();recoveryReady?.TrySetCanceled();stateRestored?.TrySetCanceled();Control.Dispose();}
    [StructLayout(LayoutKind.Sequential)]struct CursorPoint{public int X,Y;}
    [DllImport("user32.dll")]static extern bool GetCursorPos(out CursorPoint point);
}

internal static class VerificationArguments
{
    internal static bool AnyVerification(this string[] args)=>Array.Exists(args,a=>a.StartsWith("--verify",StringComparison.Ordinal)||a.StartsWith("--studio-",StringComparison.Ordinal));
}

internal sealed class StudioStatistics
{
    public string Adapter {get;set;}="";
    public int Width {get;set;}
    public int Height {get;set;}
    public int Frames {get;set;}
    public double Time {get;set;}
    public bool Paused {get;set;}
    public double TextureMb {get;set;}
    public double TargetMb {get;set;}
    public double Fps {get;set;}
    public double FrameP95 {get;set;}
    public double SubmitP95 {get;set;}
    public int Layers {get;set;}
    public double[] Frame {get;set;}=Array.Empty<double>();
}
