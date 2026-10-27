using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VWP;
using Forms=System.Windows.Forms;

internal static class StudioRecoveryCheck
{
    public static void Run()
    {
        string folder=Path.GetFullPath("artifacts/studio-recovery");Directory.CreateDirectory(folder);
        using var form=new Forms.Form{ClientSize=new System.Drawing.Size(960,540),Text="VWP · scene recovery"};using var surface=new StudioSurface();form.Controls.Add(surface.Control);
        var errors=new List<string>();surface.Message+=json=>{using var data=JsonDocument.Parse(json);if(data.RootElement.GetProperty("type").GetString()=="error")errors.Add(json);};
        form.Shown+=async(_,_)=>
        {
            try
            {
                await surface.Initialize(StudioProject.Load(Path.GetFullPath("assets/studio-originals/silver-shore/scene.vwpscene")),"Balance",true);
                await surface.Seek(7.25);surface.SetPaused(true);await Task.Delay(2500);
                try{await surface.Control.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.crash","{}").WaitAsync(TimeSpan.FromSeconds(2));}catch{}
                for(int attempt=0;attempt<120;attempt++){await Task.Delay(500);if(surface.RecoveryCount==1&&!surface.Recovering&&surface.Ready)break;}
                if(surface.RecoveryCount!=1||surface.Recovering||!surface.Ready||errors.Count>0)throw new Exception("Native renderer crash did not recover: "+string.Join(";",errors));
                var stats=await surface.ReadStatistics();if(!stats.Paused||Math.Abs(stats.Time-7.25)>.03)throw new Exception("Recovery lost pause or animation time.");
                await surface.Capture(Path.Combine(folder,"restored.png"));surface.SetPaused(false);await Task.Delay(800);if((await surface.ReadStatistics()).Time<=stats.Time+.1)throw new Exception("Recovered scene failed to resume.");
                File.WriteAllText(Path.Combine(folder,"status.txt"),"PASS: real native renderer crash; project/masks/time retained; pause retained; animation resumes.");
            }
            catch(Exception error){File.WriteAllText(Path.Combine(folder,"status.txt"),error.ToString());Environment.ExitCode=1;}
            finally{form.Close();}
        };
        Forms.Application.Run(form);
    }
}
