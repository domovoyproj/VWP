using System;
using System.Collections.Generic;
using System.Linq;

namespace VWP;

public sealed class MonitorPreferences
{
    public string? Scene { get; set; }
    public int Volume { get; set; }
    public string Fit { get; set; } = "Fill";
    public double FocusX { get; set; } = .5;
    public double FocusY { get; set; } = .5;
    public bool UserPaused { get; set; }
    public bool Interactive {get;set;}
    public bool SceneAnimation {get;set;}=true;
    public bool MusicReactive {get;set;}
    public double Depth {get;set;}=.65;
    public string Performance {get;set;}="Quality";
    public string PlaylistName { get; set; } = "Мой плейлист";
    public List<string> Playlist { get; set; } = new();
    public bool PlaylistEnabled { get; set; }
    public bool Shuffle { get; set; }
    public int IntervalMinutes { get; set; } = 15;
    public string ScheduleStart { get; set; } = "00:00";
    public string ScheduleEnd { get; set; } = "00:00";
    public MonitorPreferences ForScene(bool spatial){var copy=(MonitorPreferences)MemberwiseClone();copy.SceneAnimation=spatial;return copy;}
}

public static class PlaybackRules
{
    public static string Key(Wallpaper scene) => scene.PresetId is int id ? (scene.IsSpatial?"spatial:":"preset:") + id : scene.Path;
    public static bool InSchedule(DateTime now, string start, string end)
    {
        if (!TimeSpan.TryParseExact(start, @"hh\:mm", null, out var from) || !TimeSpan.TryParseExact(end, @"hh\:mm", null, out var to)) return false;
        var time = now.TimeOfDay;
        return from == to || (from < to ? time >= from && time < to : time >= from || time < to);
    }
    public static string? Next(IReadOnlyList<string> playlist, string? current, bool shuffle, Random random, int direction = 1)
    {
        if (playlist.Count == 0) return null;
        var alternatives = playlist.Where(key => key != current).ToArray();
        if (shuffle && alternatives.Length > 0) return alternatives[random.Next(alternatives.Length)];
        int index = playlist.ToList().IndexOf(current ?? "");
        return playlist[(index < 0 ? (direction > 0 ? 0 : playlist.Count - 1) : (index + direction + playlist.Count) % playlist.Count)];
    }
    public static System.Drawing.Rectangle Frame(int sourceWidth, int sourceHeight, int width, int height, string fit, double focusX, double focusY)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0) return new(0, 0, width, height);
        double scale = fit == "Fit" ? Math.Min((double)width / sourceWidth, (double)height / sourceHeight) : Math.Max((double)width / sourceWidth, (double)height / sourceHeight);
        int w = (int)Math.Ceiling(sourceWidth * scale), h = (int)Math.Ceiling(sourceHeight * scale);
        return new((int)((width - w) * Math.Clamp(focusX, 0, 1)), (int)((height - h) * Math.Clamp(focusY, 0, 1)), w, h);
    }
}
