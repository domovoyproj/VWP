using System;
namespace VWP;
public sealed class SceneLayers
{
    public string? Background {get;set;}
    public string? Foreground {get;set;}
    public string Accent {get;set;}="#A698EE";
    public string Effect {get;set;}="Stars";
    public int? MotionId {get;set;}
    public bool Realtime {get;set;}
    public double Speed {get;set;}=1;
    public double Density {get;set;}=1;
    public double ParticleSize {get;set;}=1;
    public double Wind {get;set;}=.15;
    public double Parallax {get;set;}=.5;
    public double RegionTop {get;set;}
    public double RegionBottom {get;set;}=1;
    public bool AudioReactive {get;set;}
    public bool Clock {get;set;}
    public SceneLayers Copy()=>System.Text.Json.JsonSerializer.Deserialize<SceneLayers>(System.Text.Json.JsonSerializer.Serialize(this))!;
    public bool ValidProperties()=>double.IsFinite(Speed)&&Speed>=.1&&Speed<=3
        && double.IsFinite(Density)&&Density>=0&&Density<=2
        && double.IsFinite(ParticleSize)&&ParticleSize>=.25&&ParticleSize<=3
        && double.IsFinite(Wind)&&Wind>=-1&&Wind<=1
        && double.IsFinite(Parallax)&&Parallax>=0&&Parallax<=1
        && double.IsFinite(RegionTop)&&double.IsFinite(RegionBottom)&&RegionTop>=0&&RegionBottom<=1&&RegionTop<RegionBottom;
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
