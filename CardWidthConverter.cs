using System;
using System.Globalization;
using System.Windows.Data;
namespace VWP;
public sealed class CardWidthConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)
    {
        double width=Math.Max(240,(double)value-2);
        int columns=Math.Max(1,(int)(width/240));return Math.Floor(width/columns);
    }
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
