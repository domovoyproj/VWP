using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace VWP;

internal sealed class CitySurface : IDisposable
{
    public WebView2 Control {get;}=new(){DefaultBackgroundColor=Color.FromArgb(5,8,20),Dock=System.Windows.Forms.DockStyle.Fill};
    bool disposed;
    readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource? recoveryReady;
    bool offline,recovering,failed,paused;
    int recoveries,unresponsiveReports;
    string entryUrl="",lastState="null",profile="";
    string? stateScript;
    readonly string? browserDataName;
    public CitySurface(string? browserDataName=null){this.browserDataName=browserDataName;}
    public int RecoveryCount=>recoveries;
    internal bool Recovering=>recovering;
    public string EffectiveProfile=>profile;
    public event Action<string>? Message;
    public static string Folder=>Path.Combine(AppContext.BaseDirectory,"assets","city");
    public Task Initialize(string profile,bool preview,string? state=null,int palette=0,bool explore=false)=>InitializeSource(Folder,"index.html?quality="+Uri.EscapeDataString(profile)+"&preview="+(preview?"1":"0")+"&palette="+palette+"&explore="+(explore?"1":"0"),state);
    internal Task InitializeLiving(int scene,bool manual=true)=>InitializeSource(Path.Combine(AppContext.BaseDirectory,"assets"),"living/index.html?scene="+scene+"&manual="+(manual?"1":"0"));
    internal Task InitializeAnime(int scene,bool manual=true)=>InitializeSource(Path.Combine(AppContext.BaseDirectory,"assets"),"anime/index.html?scene="+scene+"&manual="+(manual?"1":"0"));
    internal Task InitializeCityRender(bool bake=false)=>InitializeSource(Folder,"index.html?quality=Quality&preview=0&manual=1"+(bake?"&bake=1":""));
    async Task InitializeSource(string folder,string entry,string? state=null)
    {
        offline=entry.Contains("manual=1",StringComparison.Ordinal);
        entryUrl="https://vwp-city.local/"+entry;
        profile=entry.Contains("quality=",StringComparison.Ordinal)?entry.Split("quality=",2)[1].Split('&')[0]:"offline";
        lastState=state??"null";
        string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP",browserDataName??(offline?"living-render-browser":"city-browser"));
        string arguments="--autoplay-policy=no-user-gesture-required"+(offline?" --disable-background-timer-throttling --disable-renderer-backgrounding --disable-backgrounding-occluded-windows":"");
        var environment=await CoreWebView2Environment.CreateAsync(null,data,new CoreWebView2EnvironmentOptions(arguments));
        if(disposed)return;
        await Control.EnsureCoreWebView2Async(environment);if(disposed)return;
        var web=Control.CoreWebView2;web.Settings.AreDefaultContextMenusEnabled=false;web.Settings.AreDevToolsEnabled=false;web.Settings.AreBrowserAcceleratorKeysEnabled=false;web.Settings.IsStatusBarEnabled=false;
        web.SetVirtualHostNameToFolderMapping("vwp-city.local",folder,CoreWebView2HostResourceAccessKind.DenyCors);
        web.NavigationStarting+=(_,e)=>{if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out var url)||url.Scheme!="https"||url.Host!="vwp-city.local")e.Cancel=true;};
        web.NewWindowRequested+=(_,e)=>e.Handled=true;
        web.NavigationCompleted+=(_,e)=>{if(!e.IsSuccess&&e.WebErrorStatus!=CoreWebView2WebErrorStatus.OperationCanceled)Fail("Не удалось открыть локальную сцену города: "+e.WebErrorStatus);};
        web.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
        web.ProcessFailed+=(_,e)=>HandleProcessFailure(e.ProcessFailedKind,$"reason={e.Reason}; exit={e.ExitCode}; process={e.ProcessDescription}");
        web.WebMessageReceived+=async(_,e)=>
        {
            if(!e.Source.StartsWith("https://vwp-city.local/",StringComparison.Ordinal))return;
            string json=e.WebMessageAsJson;
            using var message=JsonDocument.Parse(json);
            string? type=message.RootElement.GetProperty("type").GetString();
            if(type=="state") {if(!recovering)lastState=message.RootElement.GetProperty("value").GetRawText();unresponsiveReports=0;return;}
            if(type=="ready")
            {
                unresponsiveReports=0;
                if(recovering)
                {
                    try{await web.ExecuteScriptAsync("window.city.restore("+lastState+");window.city.pause("+JsonSerializer.Serialize(paused)+")").WaitAsync(TimeSpan.FromSeconds(15));}
                    catch(Exception error){Fail("Не удалось восстановить позицию: "+error.Message);return;}
                }
                if(disposed)return;SetPaused(paused);ready.TrySetResult();recoveryReady?.TrySetResult();
            }
            if(type=="contextlost") {_ = RecoverRenderer("WebGL context lost");return;}
            if(type=="error") {Fail(message.RootElement.GetProperty("message").GetString()??"Ошибка 3D-сцены.");return;}
            Message?.Invoke(json);
        };
        await InstallStateScript();
        web.Navigate(entryUrl);
        try {await ready.Task.WaitAsync(TimeSpan.FromSeconds(90));}
        catch(Exception e){Log("initialization",e.Message);throw;}
    }
    async Task InstallStateScript()
    {
        var web=Control.CoreWebView2;
        using var validated=JsonDocument.Parse(lastState);
        if(stateScript is not null)web.RemoveScriptToExecuteOnDocumentCreated(stateScript);
        stateScript=await web.AddScriptToExecuteOnDocumentCreatedAsync("window.initialCityState="+validated.RootElement.GetRawText()+";addEventListener('error',e=>window.chrome.webview.postMessage({type:'error',message:e.message}));addEventListener('unhandledrejection',e=>window.chrome.webview.postMessage({type:'error',message:String(e.reason)}));");
    }
    internal void HandleProcessFailure(CoreWebView2ProcessFailedKind kind,string details="")
    {
        if(disposed)return;
        Log(kind.ToString(),details);
        // WebView2 recreates GPU and utility processes itself. A busy renderer
        // also reports this event without exiting, notably while compiling shaders.
        if(kind==CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
        {
            if(++unresponsiveReports<3)return;
            _ = RecoverRenderer(kind.ToString());return;
        }
        if(kind==CoreWebView2ProcessFailedKind.RenderProcessExited){_ = RecoverRenderer(kind.ToString());return;}
        if(kind==CoreWebView2ProcessFailedKind.BrowserProcessExited)Fail("Процесс WebView2 завершился. Открой город снова. Подробности: журнал VWP/city-runtime.log.");
    }
    async Task RecoverRenderer(string reason)
    {
        if(disposed||failed||recovering)return;
        if(offline||recoveries>=2){Fail("Не удалось восстановить 3D-сцену. Выбери режим «Экономия» и открой город снова. Подробности: журнал VWP/city-runtime.log.");return;}
        recovering=true;recoveries++;unresponsiveReports=0;
        var loaded=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);recoveryReady=loaded;
        try
        {
            // Leave ProcessFailed dispatch before navigating the affected renderer.
            await Task.Delay(250);if(disposed)return;
            if(entryUrl.Contains("quality=")){entryUrl=System.Text.RegularExpressions.Regex.Replace(entryUrl,"quality=[^&]*","quality=Eco");profile="Eco";}
            Log("recovery",reason+"; attempt="+recoveries);
            // Script registration can wait forever on a crashed renderer. Navigate
            // first; restore the cached position when the replacement reports ready.
            Control.CoreWebView2.Navigate(entryUrl);
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(90));
            if(!disposed)Message?.Invoke(JsonSerializer.Serialize(new{type="recovered",profile,attempt=recoveries}));
        }
        catch(Exception e){if(!disposed)Fail("Не удалось восстановить город: "+e.Message);}
        finally{recovering=false;recoveryReady=null;}
    }
    void Fail(string message)
    {
        if(disposed||failed)return;failed=true;Log("fatal",message);
        var exception=new InvalidOperationException(message);ready.TrySetException(exception);recoveryReady?.TrySetException(exception);
        Message?.Invoke(JsonSerializer.Serialize(new{type="error",message}));
    }
    void Log(string kind,string detail)
    {
        try
        {
            string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP");Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"city-runtime.log");
            if(File.Exists(path)&&new FileInfo(path).Length>1024*1024)File.Move(path,path+".previous",true);
            File.AppendAllText(path,JsonSerializer.Serialize(new{time=DateTimeOffset.UtcNow,kind,detail,profile,offline})+Environment.NewLine);
        }catch{/* Diagnostics must never interrupt playback. */}
    }
    public void SetPaused(bool value){paused=value;if(!disposed&&Control.CoreWebView2 is not null)try{Control.CoreWebView2.PostWebMessageAsJson("{\"type\":\"pause\",\"value\":"+(value?"true":"false")+"}");}catch(InvalidOperationException){}}
    public void SetState(string state){if(!disposed&&Control.CoreWebView2 is not null)Control.CoreWebView2.PostWebMessageAsJson("{\"type\":\"state\",\"value\":"+state+"}");}
    public async Task<string> State(){if(disposed||recovering||failed||Control.CoreWebView2 is null)return lastState;try{return await Control.CoreWebView2.ExecuteScriptAsync("window.city.state()").WaitAsync(TimeSpan.FromSeconds(2));}catch{return lastState;}}
    // Offline exports must not inherit the cloud worker's 1024px desktop.
    // Set a real render viewport, then capture that viewport without rescaling.
    internal async Task SetExportViewport(int width,int height)
    {
        if(Control.CoreWebView2 is null)throw new InvalidOperationException("Renderer is not initialized");
        await Control.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride",JsonSerializer.Serialize(new{width,height,deviceScaleFactor=1,mobile=false,screenWidth=width,screenHeight=height}));
    }
    internal async Task CaptureExport(string path,int width,int height)
    {
        if(Control.CoreWebView2 is null)throw new InvalidOperationException("Renderer is not initialized");
        string result=await Control.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.captureScreenshot",JsonSerializer.Serialize(new{format="png",fromSurface=true,captureBeyondViewport=true,clip=new{x=0,y=0,width,height,scale=1}}));
        using var image=JsonDocument.Parse(result);
        await File.WriteAllBytesAsync(path,Convert.FromBase64String(image.RootElement.GetProperty("data").GetString()!));
    }
    public async Task Capture(string path){if(Control.CoreWebView2 is null)return;using var file=File.Create(path);await Control.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);}
    public void Dispose(){if(disposed)return;disposed=true;ready.TrySetCanceled();recoveryReady?.TrySetCanceled();Control.Dispose();}
}
