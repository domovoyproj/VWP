using System;
using System.Globalization;
using System.Windows.Data;
namespace VWP;
public sealed class CardImageHeightConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>Math.Clamp(92+((double)value-640)*.22,90,142);
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
