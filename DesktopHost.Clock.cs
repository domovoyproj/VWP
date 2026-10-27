using System;
using System.Globalization;
using System.Windows.Threading;
using LibVLCSharp.Shared;
namespace VWP;
public sealed partial class DesktopHost
{
    DispatcherTimer? clockTimer;
    internal bool ClockEnabled=>clockTimer?.IsEnabled==true && player.MarqueeInt(VideoMarqueeOption.Enable)==1
        && player.MarqueeInt(VideoMarqueeOption.Position)==4;
    internal string? ClockCaption=>player.MarqueeString(VideoMarqueeOption.Text);
    internal bool CaptureClockFrame(string path)=>player.TakeSnapshot(0,path,960,540);
    internal static string ClockText(DateTime localTime) => localTime.ToString("dddd",CultureInfo.InvariantCulture).ToUpperInvariant()
        + "\n" + localTime.ToString("dd MMMM, yyyy",CultureInfo.InvariantCulture).ToUpperInvariant()
        + "\n" + localTime.ToString("HH:mm",CultureInfo.InvariantCulture);
    public void ShowClock(bool enabled)
    {
        clockTimer?.Stop();clockTimer=null;
        player.SetMarqueeInt(VideoMarqueeOption.Enable,0);
        if(!enabled||currentMedia is null)return;
        clockTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
        clockTimer.Tick+=(_,_)=>UpdateClock();
        UpdateClock();clockTimer.Start();
    }
    void UpdateClock()
    {
        // Marquee is composited by VLC, so it stays above its native video surface.
        // Refresh also restores it after looping or rebuilding the desktop host.
        uint width=0,height=0;player.Size(0,ref width,ref height);
        // Marquee uses libVLC's alignment bitmask (4 = top), not the Position enum.
        player.SetMarqueeInt(VideoMarqueeOption.Position,4);
        player.SetMarqueeInt(VideoMarqueeOption.Size,Math.Max(16,(int)height/38));
        player.SetMarqueeInt(VideoMarqueeOption.Y,Math.Max(12,(int)height/24));
        player.SetMarqueeInt(VideoMarqueeOption.Color,0x477CB7);
        player.SetMarqueeInt(VideoMarqueeOption.Opacity,240);
        player.SetMarqueeInt(VideoMarqueeOption.Timeout,0);
        player.SetMarqueeString(VideoMarqueeOption.Text,ClockText(DateTime.Now));
        player.SetMarqueeInt(VideoMarqueeOption.Enable,1);
    }
}
