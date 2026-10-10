using System.Windows;
using System.Windows.Media;
namespace VWP;
public static class RoundClip
{
    public static readonly DependencyProperty RadiusProperty=DependencyProperty.RegisterAttached("Radius",typeof(double),typeof(RoundClip),new PropertyMetadata(0d,Changed));
    public static void SetRadius(DependencyObject target,double radius)=>target.SetValue(RadiusProperty,radius);
    public static double GetRadius(DependencyObject target)=>(double)target.GetValue(RadiusProperty);
    static void Changed(DependencyObject target,DependencyPropertyChangedEventArgs e)
    {
        if(target is not FrameworkElement element)return;
        element.SizeChanged-=SizeChanged;element.SizeChanged+=SizeChanged;Clip(element);
    }
    static void SizeChanged(object sender,SizeChangedEventArgs e)=>Clip((FrameworkElement)sender);
    static void Clip(FrameworkElement element)=>element.Clip=new RectangleGeometry(new Rect(0,0,element.ActualWidth,element.ActualHeight),GetRadius(element),GetRadius(element));
}
