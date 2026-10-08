using System;
namespace VWP;
public sealed class SceneLayers
{
    public string? Background {get;set;}
    public string? Foreground {get;set;}
    public string Accent {get;set;}="#A698EE";
    public string Effect {get;set;}="Stars";
    public int? MotionId {get;set;}
    [System.Text.Json.Serialization.JsonIgnore]
    public string? Video {get;set;}
}
public sealed record PerformanceProfile(string Key,string Name,int Fps,int MaxHeight,int Particles)
{
    public static PerformanceProfile Resolve(string? key)=>key switch{
        "Eco"=>new("Eco","Экономия",15,720,18),
        "Quality"=>new("Quality","Качество",60,2160,80),
        _=>new("Balance","Баланс",30,1080,40)};
}
