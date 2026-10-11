using System;
namespace VWP;
public static class PerformancePolicy
{
    public static void Apply(MonitorPreferences config,string mode)
    {
        if(mode is not ("Auto" or "Eco" or "Balance" or "Quality"))throw new ArgumentException("Unknown performance mode",nameof(mode));
        config.Performance=mode=="Auto"?"Balance":mode;
        config.CityPerformance=mode;
    }
    public static string Mode(MonitorPreferences config)=>config.CityPerformance=="Auto"&&config.Performance=="Balance"?"Auto":config.CityPerformance==config.Performance?config.Performance:"Custom";
}
