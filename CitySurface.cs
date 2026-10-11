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
    public event Action<string>? Message;
    public static string Folder=>Path.Combine(AppContext.BaseDirectory,"assets","city");
    public Task Initialize(string profile,bool preview,string? state=null,int palette=0,bool explore=false)=>InitializeSource(Folder,"index.html?quality="+Uri.EscapeDataString(profile)+"&preview="+(preview?"1":"0")+"&palette="+palette+"&explore="+(explore?"1":"0"),state);
    internal Task InitializeLiving(int scene,bool manual=true)=>InitializeSource(Path.Combine(AppContext.BaseDirectory,"assets"),"living/index.html?scene="+scene+"&manual="+(manual?"1":"0"));
    internal Task InitializeCityRender(bool bake=false)=>InitializeSource(Folder,"index.html?quality=Quality&preview=0&manual=1"+(bake?"&bake=1":""));
    async Task InitializeSource(string folder,string entry,string? state=null)
    {
        bool offline=entry.Contains("manual=1",StringComparison.Ordinal);
        string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VWP",offline?"living-render-browser":"city-browser");
        string arguments="--autoplay-policy=no-user-gesture-required"+(offline?" --disable-background-timer-throttling --disable-renderer-backgrounding --disable-backgrounding-occluded-windows":"");
        var environment=await CoreWebView2Environment.CreateAsync(null,data,new CoreWebView2EnvironmentOptions(arguments));
        if(disposed)return;
        await Control.EnsureCoreWebView2Async(environment);if(disposed)return;
        var web=Control.CoreWebView2;web.Settings.AreDefaultContextMenusEnabled=false;web.Settings.AreDevToolsEnabled=false;web.Settings.AreBrowserAcceleratorKeysEnabled=false;web.Settings.IsStatusBarEnabled=false;
        web.SetVirtualHostNameToFolderMapping("vwp-city.local",folder,CoreWebView2HostResourceAccessKind.DenyCors);
        web.NavigationStarting+=(_,e)=>{if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out var url)||url.Scheme!="https"||url.Host!="vwp-city.local")e.Cancel=true;};
        web.NewWindowRequested+=(_,e)=>e.Handled=true;
        web.NavigationCompleted+=(_,e)=>{if(!e.IsSuccess)ready.TrySetException(new InvalidOperationException("Не удалось открыть локальную сцену города: "+e.WebErrorStatus));};
        web.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
        web.ProcessFailed+=(_,_)=>{ready.TrySetException(new InvalidOperationException("Видеодвижок города остановился."));Message?.Invoke("{\"type\":\"error\",\"message\":\"Видеодвижок города остановился\"}");};
        web.WebMessageReceived+=(_,e)=>
        {
            if(!e.Source.StartsWith("https://vwp-city.local/",StringComparison.Ordinal))return;
            string json=e.WebMessageAsJson;
            using var message=JsonDocument.Parse(json);
            string? type=message.RootElement.GetProperty("type").GetString();
            if(type=="ready")ready.TrySetResult();
            if(type=="error")ready.TrySetException(new InvalidOperationException(message.RootElement.GetProperty("message").GetString()));
            Message?.Invoke(json);
        };
        using var validated=JsonDocument.Parse(state??"null");
        await web.AddScriptToExecuteOnDocumentCreatedAsync("window.initialCityState="+validated.RootElement.GetRawText()+";addEventListener('error',e=>window.chrome.webview.postMessage({type:'error',message:e.message}));addEventListener('unhandledrejection',e=>window.chrome.webview.postMessage({type:'error',message:String(e.reason)}));");
        web.Navigate("https://vwp-city.local/"+entry);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(offline?90:45));
    }
    public void SetPaused(bool value){if(!disposed&&Control.CoreWebView2 is not null)Control.CoreWebView2.PostWebMessageAsJson("{\"type\":\"pause\",\"value\":"+(value?"true":"false")+"}");}
    public void SetState(string state){if(!disposed&&Control.CoreWebView2 is not null)Control.CoreWebView2.PostWebMessageAsJson("{\"type\":\"state\",\"value\":"+state+"}");}
    public async Task<string> State()=>Control.CoreWebView2 is null?"null":await Control.CoreWebView2.ExecuteScriptAsync("window.city.state()");
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
    public void Dispose(){disposed=true;ready.TrySetCanceled();Control.Dispose();}
}
