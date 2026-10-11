using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
namespace VWP;
public sealed class ThumbnailImageConverter:IValueConverter
{
    readonly Dictionary<string,BitmapImage> cache=new(StringComparer.OrdinalIgnoreCase);
    readonly Queue<string> order=new();
    public object? Convert(object value,Type targetType,object parameter,CultureInfo culture)
    {
        if(value is not string path||!File.Exists(path))return null;
        try
        {
            var info=new FileInfo(path);string key=Path.GetFullPath(path)+":"+info.Length+":"+info.LastWriteTimeUtc.Ticks;
            if(cache.TryGetValue(key,out var existing))return existing;
            var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=480;image.UriSource=new Uri(Path.GetFullPath(path));image.EndInit();image.Freeze();
            while(order.Count>=96)cache.Remove(order.Dequeue());cache[key]=image;order.Enqueue(key);return image;
        }
        catch{return null;}
    }
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
