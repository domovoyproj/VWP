using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace VWP;

// A separate bitmap surface can fade reliably over a hardware-decoded VLC child.
internal sealed class DesktopTransition : IDisposable
{
    readonly Bitmap image;
    IntPtr window,bitmap;
    byte opacity=255;
    public bool Valid=>window!=IntPtr.Zero&&IsWindow(window);
    public DesktopTransition(IntPtr parent,Rectangle bounds,string picture)
    {
        image=new Bitmap(bounds.Width,bounds.Height);
        try
        {
            using(var source=Image.FromFile(picture))using(var graphics=Graphics.FromImage(image))graphics.DrawImage(source,0,0,image.Width,image.Height);
            var point=new Point{X=bounds.X,Y=bounds.Y};MapWindowPoints(IntPtr.Zero,parent,ref point,1);
            window=CreateWindowEx(0x080800A0,"STATIC","VWP Transition",0x40000000|0x10000000|0x04000000|0x4E,point.X,point.Y,bounds.Width,bounds.Height,parent,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
            if(window==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            bitmap=image.GetHbitmap();SendMessage(window,0x172,IntPtr.Zero,bitmap);
            SetOpacity(255);BringToFront();UpdateWindow(window);DwmFlush();
        }
        catch{Dispose();throw;}
    }
    public void BringToFront()=>SetWindowPos(window,IntPtr.Zero,0,0,0,0,0x53);
    public void SetProgress(double progress)
    {
        progress=Math.Clamp(progress,0,1);
        double eased=progress*progress*(3-2*progress);
        SetOpacity((byte)Math.Round(255*(1-eased)));
    }
    void SetOpacity(byte value)
    {
        if(!SetLayeredWindowAttributes(window,0,value,2))throw new Win32Exception(Marshal.GetLastWin32Error());
        opacity=value;
    }
    // Preserve the visible blend when another scene is selected during a dissolve.
    public void ComposeOver(string underlay)
    {
        using var result=new Bitmap(image.Width,image.Height);
        using(var source=Image.FromFile(underlay))using(var graphics=Graphics.FromImage(result))
        {
            graphics.DrawImage(source,0,0,result.Width,result.Height);
            using var attributes=new ImageAttributes();attributes.SetColorMatrix(new ColorMatrix{Matrix33=opacity/255f});
            graphics.DrawImage(image,new Rectangle(0,0,result.Width,result.Height),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);
        }
        result.Save(underlay,ImageFormat.Png);
    }
    public void Dispose(){if(window!=IntPtr.Zero){DestroyWindow(window);window=IntPtr.Zero;}if(bitmap!=IntPtr.Zero){DeleteObject(bitmap);bitmap=IntPtr.Zero;}image.Dispose();}
    [StructLayout(LayoutKind.Sequential)] struct Point{public int X,Y;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateWindowEx(uint ex,string cls,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] static extern int MapWindowPoints(IntPtr from,IntPtr to,ref Point point,uint count);
    [DllImport("user32.dll",SetLastError=true)] static extern bool SetLayeredWindowAttributes(IntPtr window,uint key,byte alpha,uint flags);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr bitmap);
    [DllImport("user32.dll")] static extern bool UpdateWindow(IntPtr window);
    [DllImport("dwmapi.dll")] static extern int DwmFlush();
}
