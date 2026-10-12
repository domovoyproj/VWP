using System;
using System.Drawing;
using System.Diagnostics;
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
    public static void Run(bool desktop=false,string profile="Auto")
    {
        Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
        string folder=Path.Combine(Environment.CurrentDirectory,"artifacts",profile=="Eco"?"city-check-eco":"city-check");Directory.CreateDirectory(folder);
        foreach(string name in new[]{"initial.png","animated.png","animation.json","performance.json","travel.png","metrics.json","cover.png","desktop-first.png","desktop-second.png","desktop-result.json"})File.Delete(Path.Combine(folder,name));
        using var form=new Forms.Form{ClientSize=new Size(1280,720),StartPosition=Forms.FormStartPosition.CenterScreen,TopMost=true,Text="VWP city verification"};
        using var city=new CitySurface();form.Controls.Add(city.Control);
        form.Shown+=async(_,_)=>
        {
            try
            {
                File.WriteAllText(Path.Combine(folder,"status.txt"),"RUNNING: native city verification");
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
                    using var host=new DesktopHost(form.Handle);
                    for(int palette=0;palette<3;palette++)foreach(string quality in new[]{"Eco","Balance","Quality","Auto"})
                    {
                        bool refused=false;try{await host.PlayCity(access,Forms.Screen.PrimaryScreen!,"none",quality,palette);}catch(InvalidOperationException){refused=true;}
                        Check(refused&&!host.Active,$"Desktop host locks palette {palette} in {quality} without a verified star");
                    }
                    fake.User=HttpStatusCode.OK;fake.Star=HttpStatusCode.NoContent;fake.HoldStar=true;
                    var checking=access.CheckAsync("test-read-only-token-01234567890");await fake.StarRequested.Task;
                    access.Disconnect();fake.ReleaseStar.SetResult();
                    Check(!await checking&&!access.Unlocked&&!File.Exists(Path.Combine(folder,"access.bin")),"Disconnect defeats a late successful verification");
                }
                File.AppendAllText(progress,"render\n");form.Show();form.Activate();
                await city.Initialize(profile,true);await Task.Delay(10000);
                await city.Capture(Path.Combine(folder,"initial.png"));
                using(var frame=new Bitmap(Path.Combine(folder,"initial.png")))
                {
                    int lit=0,colored=0;for(int y=0;y<frame.Height;y+=4)for(int x=0;x<frame.Width;x+=4){var c=frame.GetPixel(x,y);if(c.R+c.G+c.B>90)lit++;if(Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))>40)colored++;}
                    Check(lit>1500&&colored>1500,"Rendered city must contain visible detailed colored geometry");
                }
                using var a=JsonDocument.Parse(await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.metrics()"));int start=a.RootElement.GetProperty("frames").GetInt32();
                var timer=Stopwatch.StartNew();await Task.Delay(3000);using var b=JsonDocument.Parse(await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.metrics()"));
                int animatedFrames=b.RootElement.GetProperty("frames").GetInt32()-start;
                File.WriteAllText(Path.Combine(folder,"performance.json"),JsonSerializer.Serialize(new{width=1280,height=720,quality=profile,observedFrames=animatedFrames,seconds=timer.Elapsed.TotalSeconds,observedFps=animatedFrames/timer.Elapsed.TotalSeconds,details=b.RootElement.Clone()}));
                // CI may use Microsoft's software GPU. Prove changing rendered
                // pixels and an advancing clock separately from the FPS report.
                Check(animatedFrames>0,"City renderer advances on this adapter");
                await city.Capture(Path.Combine(folder,"animated.png"));
                using(var firstFrame=new Bitmap(Path.Combine(folder,"initial.png")))
                using(var nextFrame=new Bitmap(Path.Combine(folder,"animated.png")))
                {
                    Check(firstFrame.Size==nextFrame.Size,"Animation captures keep their viewport size");
                    int changed=0;
                    for(int y=0;y<firstFrame.Height;y+=8)for(int x=0;x<firstFrame.Width;x+=8)
                    {
                        var firstPixel=firstFrame.GetPixel(x,y);var nextPixel=nextFrame.GetPixel(x,y);
                        if(Math.Abs(firstPixel.R-nextPixel.R)+Math.Abs(firstPixel.G-nextPixel.G)+Math.Abs(firstPixel.B-nextPixel.B)>18)changed++;
                    }
                    Check(changed>400,"City renders changing geometry, rather than a still frame");
                    File.WriteAllText(Path.Combine(folder,"animation.json"),JsonSerializer.Serialize(new{changedSamples=changed,animatedFrames}));
                }
                Check(b.RootElement.GetProperty("actors").GetProperty("pedestrians").GetInt32()==(profile=="Eco"?6:14),"Rigged pedestrians are loaded");
                Check(b.RootElement.GetProperty("architecture").GetProperty("templates").GetInt32()<=48,"Geometry template cache stays bounded");
                double poseChange=0;
                // A single sample can land on the same phase of a looping walk.
                for(int sample=0;sample<4;sample++)
                {
                    await Task.Delay(150);using var motionPose=JsonDocument.Parse(await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.metrics().walkingPose"));
                    double difference=0;for(int i=0;i<4;i++)difference+=Math.Abs(a.RootElement.GetProperty("walkingPose")[i].GetDouble()-motionPose.RootElement[i].GetDouble());
                    poseChange=Math.Max(poseChange,difference);
                }
                Check(poseChange>.01,"Walking animation deforms the skeleton");
                city.SetPaused(true);await Task.Delay(300);string frozen=await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.metrics().frames");await Task.Delay(400);Check(frozen==await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.metrics().frames"),"Pause freezes renderer");city.SetPaused(false);
                await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.move(40000,-40000);window.city.interact()");await Task.Delay(900);
                using var moved=JsonDocument.Parse(await city.State());Check(Math.Abs(moved.RootElement.GetProperty("x").GetDouble())>39000,"Travel is not confined to the first city block");
                using var m=JsonDocument.Parse(await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.metrics()"));Check(m.RootElement.GetProperty("chunks").GetInt32()==(profile=="Eco"?25:49),"Infinite travel keeps a bounded chunk count");
                await city.Capture(Path.Combine(folder,"travel.png"));File.WriteAllText(Path.Combine(folder,"metrics.json"),m.RootElement.GetRawText());
                await city.Control.CoreWebView2.ExecuteScriptAsync("window.city.restore({x:5.55,y:2.3,z:22,palette:0});");await Task.Delay(500);
                string cover=Path.Combine(folder,"cover.png");await city.Control.CoreWebView2.ExecuteScriptAsync("document.body.classList.add('wallpaper')");await Task.Delay(150);await city.Capture(cover);
                if(desktop)
                {
                    city.Dispose();File.AppendAllText(progress,"desktop\n");
                    using var testAccess=new StarAccess(new HttpClient(new FakeGithub()),Path.Combine(folder,"desktop-access.bin"));
                    testAccess.Disconnect();Check(await testAccess.CheckAsync("test-read-only-token-01234567890"),"Test account unlocks through authenticated API responses");
                    using var actualHost=new DesktopHost();
                    try
                    {
                        await actualHost.PlayCity(testAccess,Forms.Screen.PrimaryScreen!,cover,"Auto");
                        await Task.Delay(1200);Check(actualHost.IsCity&&actualHost.Healthy,"City attaches to the real Explorer wallpaper host");
                        string? first=await actualHost.CitySnapshot();Check(first is not null,"Desktop city produces a frame");
                        File.Move(first!,Path.Combine(folder,"desktop-first.png"),true);
                        string initialState=await actualHost.CityState();await Task.Delay(1600);
                        string? second=await actualHost.CitySnapshot();Check(second is not null,"Desktop city produces a second frame");
                        File.Move(second!,Path.Combine(folder,"desktop-second.png"),true);
                        Check(initialState!=await actualHost.CityState(),"Desktop city continues animating after initialization");
                        using var firstImage=new Bitmap(Path.Combine(folder,"desktop-first.png"));using var secondImage=new Bitmap(Path.Combine(folder,"desktop-second.png"));
                        int changes=0;for(int y=0;y<firstImage.Height;y+=8)for(int x=0;x<firstImage.Width;x+=8){var pixelA=firstImage.GetPixel(x,y);var pixelB=secondImage.GetPixel(x,y);if(Math.Abs(pixelA.R-pixelB.R)+Math.Abs(pixelA.G-pixelB.G)+Math.Abs(pixelA.B-pixelB.B)>18)changes++;}
                        Check(changes>700,"Desktop WebView renders changing geometry");
                        File.WriteAllText(Path.Combine(folder,"desktop-result.json"),JsonSerializer.Serialize(new{healthy=actualHost.Healthy,width=firstImage.Width,height=firstImage.Height,changedSamples=changes,backdropError=actualHost.BackdropError}));
                    }
                    finally{actualHost.Stop();testAccess.Disconnect();}
                }
                File.WriteAllText(Path.Combine(folder,"status.txt"),"PASS: authenticated star gate, encrypted credentials, revocation, backend gate, visible animated city, pause and bounded 40km travel"+(desktop?", actual Explorer host initialization and changing frames":""));
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
