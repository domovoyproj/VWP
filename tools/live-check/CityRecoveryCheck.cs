using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using VWP;
using Forms=System.Windows.Forms;

static class CityRecoveryCheck
{
    public static void Run()
    {
        Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
        string folder=Path.Combine(Environment.CurrentDirectory,"artifacts","city-recovery-check");Directory.CreateDirectory(folder);
        var checks=new List<string>();
        void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);File.WriteAllText(Path.Combine(folder,"checks.json"),JsonSerializer.Serialize(checks));}
        using var form=new Forms.Form{ClientSize=new Size(800,450),Text="VWP · recovery verification"};
        using var city=new CitySurface("city-recovery-check-browser-"+Environment.ProcessId);form.Controls.Add(city.Control);
        var failures=new List<string>();city.Message+=json=>{using var message=JsonDocument.Parse(json);if(message.RootElement.GetProperty("type").GetString()=="error")failures.Add(json);};
        form.Shown+=async(_,_)=>
        {
            try
            {
                File.WriteAllText(Path.Combine(folder,"status.txt"),"RUNNING");
                await city.Initialize("Eco",true);await Task.Delay(1200);
                int before=await Frames(city);
                city.HandleProcessFailure(CoreWebView2ProcessFailedKind.GpuProcessExited,"verification: nonfatal notification");
                city.HandleProcessFailure(CoreWebView2ProcessFailedKind.UtilityProcessExited,"verification: nonfatal notification");
                city.HandleProcessFailure(CoreWebView2ProcessFailedKind.RenderProcessUnresponsive,"verification: transient busy renderer");
                await Task.Delay(1200);
                Check(failures.Count==0&&city.RecoveryCount==0&&await Frames(city)>before,"GPU/utility exit and a transient busy report preserve animation");
                await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.restore({x:45.5,y:2,z:22,palette:2});window.city.pause(true)");city.SetPaused(true);
                await Task.Delay(3500);string saved=await city.State();
                // Crash only this isolated test renderer through the native DevTools API.
                try{await city.Control.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.crash","{}").WaitAsync(TimeSpan.FromSeconds(2));}catch{}
                for(int i=0;i<180;i++)
                {
                    await Task.Delay(500);
                    if(city.RecoveryCount>0&&city.EffectiveProfile=="Eco")
                    {
                        try{using var metrics=JsonDocument.Parse(await city.Control.CoreWebView2.ExecuteScriptAsync("window.city?.metrics()??null").WaitAsync(TimeSpan.FromSeconds(2)));if(metrics.RootElement.ValueKind==JsonValueKind.Object&&metrics.RootElement.GetProperty("frames").GetInt32()>3)break;}catch{}
                    }
                }
                Check(city.RecoveryCount==1&&failures.Count==0,"Actual renderer crash recovers once without a terminal error");
                using var state=JsonDocument.Parse(await city.State());using var original=JsonDocument.Parse(saved);
                Check(Math.Abs(state.RootElement.GetProperty("x").GetDouble()-original.RootElement.GetProperty("x").GetDouble())<.01&&state.RootElement.GetProperty("palette").GetInt32()==2,"Recovery retains position and selected district");
                before=await Frames(city);await Task.Delay(800);Check(await Frames(city)==before,"Recovery retains pause");
                city.SetPaused(false);await Task.Delay(1500);Check(await Frames(city)>before,"Recovered renderer resumes moving frames");
                await city.Capture(Path.Combine(folder,"recovered.png"));
                using var frame=new Bitmap(Path.Combine(folder,"recovered.png"));int lit=0;for(int y=0;y<frame.Height;y+=8)for(int x=0;x<frame.Width;x+=8){var c=frame.GetPixel(x,y);if(c.R+c.G+c.B>90)lit++;}
                Check(lit>300,"Recovered renderer displays city geometry");
                File.WriteAllText(Path.Combine(folder,"status.txt"),"PASS: "+checks.Count+" native recovery checks, including an actual renderer crash");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(folder,"status.txt"),e.ToString());Environment.ExitCode=1;}
            finally{form.Close();}
        };
        Forms.Application.Run(form);
    }
    static async Task<int> Frames(CitySurface city){using var metrics=JsonDocument.Parse(await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.metrics()"));return metrics.RootElement.GetProperty("frames").GetInt32();}
}
