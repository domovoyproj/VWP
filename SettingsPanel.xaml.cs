using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
namespace VWP;
public partial class SettingsPanel : UserControl
{
    public double Aspect {get;set;}=16.0/9;
    public SettingsPanel(){InitializeComponent();CropCanvas.SizeChanged+=(_,_)=>RenderCrop();}
    void CropChanged(object sender,SelectionChangedEventArgs e)=>RenderCrop();
    void FocusChanged(object sender,RoutedPropertyChangedEventArgs<double> e)=>RenderCrop();
    public void RenderCrop()
    {
        if(CropImage?.Source is not BitmapSource image || CropCanvas is null || FocusX is null || FocusY is null)return;
        double width=250,height=width/Aspect;
        if(height>140){height=140;width=height*Aspect;}
        CropCanvas.Width=width;CropCanvas.Height=height;
        CropCanvas.Clip=new System.Windows.Media.RectangleGeometry(new Rect(0,0,width,height),10,10);
        var frame=PlaybackRules.Frame(image.PixelWidth,image.PixelHeight,(int)width,(int)height,FitMode.SelectedIndex==1?"Fit":"Fill",FocusX.Value,FocusY.Value);
        CropImage.Width=frame.Width;CropImage.Height=frame.Height;Canvas.SetLeft(CropImage,frame.X);Canvas.SetTop(CropImage,frame.Y);
    }
}
