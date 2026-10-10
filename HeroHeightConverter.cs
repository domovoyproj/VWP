using System;
using System.Globalization;
using System.Windows.Data;
namespace VWP;
public sealed class HeroHeightConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>Math.Clamp(175+((double)value-640)*.4,165,240);
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
