using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VWP;
using Forms=System.Windows.Forms;
static class CityCheck
{
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);}
    public static void Run()
    {
        Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
        string folder=Path.Combine(Environment.CurrentDirectory,"artifacts","city-check");Directory.CreateDirectory(folder);
        using var form=new Forms.Form{ClientSize=new Size(1280,720),StartPosition=Forms.FormStartPosition.CenterScreen,TopMost=true,Text="VWP city verification"};
        using var city=new CitySurface();form.Controls.Add(city.Control);
        form.Shown+=async(_,_)=>
        {
            try
            {
                string progress=Path.Combine(folder,"progress.txt");File.WriteAllText(progress,"authentication\n");
                var fake=new FakeGithub();using(var access=new StarAccess(new HttpClient(fake),Path.Combine(folder,"access.bin")))
                {
                    access.Disconnect();Check(!access.Unlocked,"Starts locked");
                    fake.Star=HttpStatusCode.NotFound;Check(!await access.CheckAsync("test-read-only-token-01234567890"),"Unstarred account remains locked");
                    fake.Star=HttpStatusCode.NoContent;Check(await access.CheckAsync(),"Own authenticated star unlocks");
                    Check(access.Login=="verified-user"&&access.Unlocked,"Account comes from GitHub /user");
                    Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(folder,"access.bin"))).Contains("test-read-only"),"Credential is encrypted");
                    fake.Star=HttpStatusCode.NotFound;Check(!await access.CheckAsync()&&!access.Unlocked,"Removing star locks access");
                    fake.Star=HttpStatusCode.Forbidden;Check(!await access.CheckAsync()&&!access.Unlocked,"Missing permissions fail closed");
                    fake.Star=HttpStatusCode.NoContent;Check(await access.CheckAsync("test-read-only-token-01234567890"),"Reconnect works");
                    fake.User=HttpStatusCode.Unauthorized;Check(!await access.CheckAsync()&&!access.Unlocked,"Revoked token locks access");
                    using var host=new DesktopHost(form.Handle);bool refused=false;try{await host.PlayCity(access,Forms.Screen.PrimaryScreen!,"none","Quality");}catch(InvalidOperationException){refused=true;}
                    Check(refused&&!host.Active,"Desktop host enforces star entitlement");
                    fake.User=HttpStatusCode.OK;fake.Star=HttpStatusCode.NoContent;fake.HoldStar=true;
                    var checking=access.CheckAsync("test-read-only-token-01234567890");await fake.StarRequested.Task;
                    access.Disconnect();fake.ReleaseStar.SetResult();
                    Check(!await checking&&!access.Unlocked&&!File.Exists(Path.Combine(folder,"access.bin")),"Disconnect defeats a late successful verification");
                }
                File.AppendAllText(progress,"render\n");form.Show();form.Activate();
                await city.Initialize("Quality",true);await Task.Delay(1200);
                await city.Capture(Path.Combine(folder,"initial.png"));
                using(var frame=new Bitmap(Path.Combine(folder,"initial.png")))
                {
                    int lit=0,colored=0;for(int y=0;y<frame.Height;y+=4)for(int x=0;x<frame.Width;x+=4){var c=frame.GetPixel(x,y);if(c.R+c.G+c.B>90)lit++;if(Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))>40)colored++;}
                    Check(lit>1500&&colored>1500,"Rendered city must contain visible detailed colored geometry");
                }
                using var a=JsonDocument.Parse(await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.metrics()"));int start=a.RootElement.GetProperty("frames").GetInt32();
                await Task.Delay(800);using var b=JsonDocument.Parse(await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.metrics()"));Check(b.RootElement.GetProperty("frames").GetInt32()>start+3,"City is animated");
                city.SetPaused(true);await Task.Delay(300);string frozen=await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.metrics().frames");await Task.Delay(400);Check(frozen==await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.metrics().frames"),"Pause freezes renderer");city.SetPaused(false);
                await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.move(40000,-40000);window.city.interact()");await Task.Delay(900);
                using var moved=JsonDocument.Parse(await city.State());Check(Math.Abs(moved.RootElement.GetProperty("x").GetDouble())>39000,"Travel is not confined to the first city block");
                using var m=JsonDocument.Parse(await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.metrics()"));Check(m.RootElement.GetProperty("chunks").GetInt32()==49,"Infinite travel keeps a bounded chunk count");
                await city.Capture(Path.Combine(folder,"travel.png"));File.WriteAllText(Path.Combine(folder,"metrics.json"),m.RootElement.GetRawText());
                await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.restore({x:0,y:4.3,z:22,palette:0});");await Task.Delay(500);
                string cover=Path.Combine(folder,"cover.png");await city.Control.CoreWebView2.ExecuteScriptAsync("document.body.classList.add('wallpaper')");await Task.Delay(150);await city.Capture(cover);
                File.WriteAllText(Path.Combine(folder,"status.txt"),"PASS: authenticated star gate, encrypted credentials, revocation, backend gate, visible animated city, pause and bounded 40km travel");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(folder,"status.txt"),e.ToString());Environment.ExitCode=1;}
            finally{form.Close();}
        };
        Forms.Application.Run(form);
    }
    sealed class FakeGithub:HttpMessageHandler
    {
        public HttpStatusCode User=HttpStatusCode.OK,Star=HttpStatusCode.NoContent;
        public bool HoldStar;
        public readonly TaskCompletionSource StarRequested=new(),ReleaseStar=new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,System.Threading.CancellationToken cancel)
        {
            Check(request.Method==HttpMethod.Get,"Verification must never write a star");Check(request.RequestUri!.Host=="api.github.com","Token stays on GitHub API");
            bool user=request.RequestUri.AbsolutePath=="/user";if(!user&&HoldStar){StarRequested.TrySetResult();await ReleaseStar.Task.WaitAsync(cancel);}return new HttpResponseMessage(user?User:Star){Content=new StringContent(user?"{\"login\":\"verified-user\",\"id\":123}":"")};
        }
    }
}
