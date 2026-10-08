using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace VWP;

public sealed class PlanetMotionEffect : ShaderEffect
{
    static string Folder=>Path.Combine(AppContext.BaseDirectory,"assets","live");
    static readonly Lazy<PixelShader> shader=new(()=>{var s=new PixelShader{UriSource=new Uri(Path.Combine(Folder,"Planets.ps"))};s.Freeze();return s;});
    public static bool Available(int id)=>id is 2 or 11 or 17 && RenderCapability.Tier>>16>=2 && RenderCapability.IsPixelShaderVersionSupported(3,0) && File.Exists(Path.Combine(Folder,"Planets.ps")) && File.Exists(Path.Combine(Folder,$"{id}.objects.png"));
    public static readonly DependencyProperty InputProperty=RegisterPixelShaderSamplerProperty(nameof(Input),typeof(PlanetMotionEffect),0);
    public Brush Input {get=>(Brush)GetValue(InputProperty);set=>SetValue(InputProperty,value);}
    public static readonly DependencyProperty MaskProperty=RegisterPixelShaderSamplerProperty(nameof(Mask),typeof(PlanetMotionEffect),1);
    public Brush Mask {get=>(Brush)GetValue(MaskProperty);set=>SetValue(MaskProperty,value);}
    public static readonly DependencyProperty SurfaceProperty=RegisterPixelShaderSamplerProperty(nameof(Surface),typeof(PlanetMotionEffect),2);
    public Brush Surface {get=>(Brush)GetValue(SurfaceProperty);set=>SetValue(SurfaceProperty,value);}
    public static readonly DependencyProperty TimeProperty=DependencyProperty.Register(nameof(Time),typeof(double),typeof(PlanetMotionEffect),new UIPropertyMetadata(0.0,PixelShaderConstantCallback(0)));
    public double Time {get=>(double)GetValue(TimeProperty);set=>SetValue(TimeProperty,value);}
    public static readonly DependencyProperty PlanetAProperty=DependencyProperty.Register(nameof(PlanetA),typeof(Point4D),typeof(PlanetMotionEffect),new UIPropertyMetadata(new Point4D(),PixelShaderConstantCallback(1)));
    public Point4D PlanetA {get=>(Point4D)GetValue(PlanetAProperty);set=>SetValue(PlanetAProperty,value);}
    public static readonly DependencyProperty PlanetBProperty=DependencyProperty.Register(nameof(PlanetB),typeof(Point4D),typeof(PlanetMotionEffect),new UIPropertyMetadata(new Point4D(),PixelShaderConstantCallback(2)));
    public Point4D PlanetB {get=>(Point4D)GetValue(PlanetBProperty);set=>SetValue(PlanetBProperty,value);}
    public static readonly DependencyProperty FrameProperty=DependencyProperty.Register(nameof(Frame),typeof(Point4D),typeof(PlanetMotionEffect),new UIPropertyMetadata(new Point4D(0,0,1,1),PixelShaderConstantCallback(3)));
    public Point4D Frame {get=>(Point4D)GetValue(FrameProperty);set=>SetValue(FrameProperty,value);}
    static Brush Texture(string name){var b=new ImageBrush(InteractiveVisual.Load(Path.Combine(Folder,name),1080)){Stretch=Stretch.Fill};b.Freeze();return b;}
    public PlanetMotionEffect(int id)
    {
        PixelShader=shader.Value;Mask=Texture($"{id}.objects.png");Surface=Texture("surface.png");
        PlanetA=id switch {11=>new(.658,.244,.168,.298),2=>new(.5335,.2614,.057,.101),_=>new(.6483,.0967,.023,.041)};
        if(id==11)PlanetB=new(.763,.263,.105,.187);
        foreach(var p in new[]{InputProperty,MaskProperty,SurfaceProperty,TimeProperty,PlanetAProperty,PlanetBProperty,FrameProperty})UpdateShaderValue(p);
    }
}
