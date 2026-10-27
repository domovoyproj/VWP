using System;
using System.Globalization;
using System.Windows.Data;
namespace VWP;
public sealed class CardImageHeightConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>Math.Max(90,(double)value*9/16);
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
