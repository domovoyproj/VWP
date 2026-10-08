using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
namespace VWP;

// Shared, frozen meshes keep the scene affordable when several monitors are active.
internal static class SpatialGeometry
{
    static readonly Dictionary<string,Material> materials=new();
    public static readonly MeshGeometry3D Cube=MakeCube();
    public static readonly MeshGeometry3D Ball=Round(20,12,false);
    public static readonly MeshGeometry3D Cone=Round(12,1,true);
    public static Material Paint(string hex,bool luminous=false)
    {
        string key=hex+luminous;if(materials.TryGetValue(key,out var cached))return cached;
        var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));brush.Freeze();
        var group=new MaterialGroup();
        if(luminous)group.Children.Add(new EmissiveMaterial(brush));
        else group.Children.Add(new DiffuseMaterial(brush));
        if(!luminous)group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(22,220,230,255)),24));
        group.Freeze();materials[key]=group;return group;
    }
    public static GeometryModel3D Shape(Model3DGroup parent,MeshGeometry3D mesh,string color,double x,double y,double z,double sx,double sy,double sz,bool glow=false)
    {
        var model=new GeometryModel3D(mesh,Paint(color,glow));
        var transform=new Transform3DGroup();transform.Children.Add(new ScaleTransform3D(sx,sy,sz));transform.Children.Add(new TranslateTransform3D(x,y,z));transform.Freeze();
        model.Transform=transform;model.Freeze();parent.Children.Add(model);return model;
    }
    static MeshGeometry3D MakeCube()
    {
        var m=new MeshGeometry3D();
        double[] p={-.5,-.5,-.5, .5,-.5,-.5, .5,.5,-.5, -.5,.5,-.5, -.5,-.5,.5, .5,-.5,.5, .5,.5,.5, -.5,.5,.5};
        for(int i=0;i<p.Length;i+=3)m.Positions.Add(new(p[i],p[i+1],p[i+2]));
        int[] f={0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,3,7,6,3,6,2,0,1,5,0,5,4};
        foreach(int i in f)m.TriangleIndices.Add(i);m.Freeze();return m;
    }
    static MeshGeometry3D Round(int longitude,int latitude,bool cone)
    {
        var m=new MeshGeometry3D();
        for(int j=0;j<=latitude;j++)for(int i=0;i<=longitude;i++)
        {
            double a=i*2*Math.PI/longitude,b=j*Math.PI/latitude;
            double radius=cone?1-j/(double)latitude:Math.Sin(b);
            m.Positions.Add(new(radius*Math.Cos(a),cone?j/(double)latitude:Math.Cos(b),radius*Math.Sin(a)));
            m.TextureCoordinates.Add(new Point(i/(double)longitude,j/(double)latitude));
        }
        for(int j=0;j<latitude;j++)for(int i=0;i<longitude;i++)
        {
            int a=j*(longitude+1)+i,b=a+longitude+1;
            // Sphere latitude runs north to south; cone runs bottom to top.
            if(cone){Add(a,b,a+1);Add(a+1,b,b+1);}else{Add(a,a+1,b);Add(a+1,b+1,b);}
        }
        m.Freeze();return m;
        void Add(int a,int b,int c){m.TriangleIndices.Add(a);m.TriangleIndices.Add(b);m.TriangleIndices.Add(c);}
    }
}

internal sealed class SpatialNode
{
    public Model3DGroup Model {get;}=new();
    public TranslateTransform3D Position {get;}=new();
    public AxisAngleRotation3D Yaw {get;}=new(new(0,1,0),0);
    public AxisAngleRotation3D Pitch {get;}=new(new(1,0,0),0);
    public AxisAngleRotation3D Roll {get;}=new(new(0,0,1),0);
    public SpatialNode(Model3DGroup parent,double x=0,double y=0,double z=0,double scale=1)
    {
        var t=new Transform3DGroup();t.Children.Add(new ScaleTransform3D(scale,scale,scale));t.Children.Add(new RotateTransform3D(Pitch));t.Children.Add(new RotateTransform3D(Roll));t.Children.Add(new RotateTransform3D(Yaw));t.Children.Add(Position);Model.Transform=t;Move(x,y,z);parent.Children.Add(Model);
    }
    public void Move(double x,double y,double z){Position.OffsetX=x;Position.OffsetY=y;Position.OffsetZ=z;}
    public void Box(string c,double x,double y,double z,double sx,double sy,double sz,bool glow=false)=>SpatialGeometry.Shape(Model,SpatialGeometry.Cube,c,x,y,z,sx,sy,sz,glow);
    public void Sphere(string c,double x,double y,double z,double sx,double sy,double sz,bool glow=false)=>SpatialGeometry.Shape(Model,SpatialGeometry.Ball,c,x,y,z,sx,sy,sz,glow);
    public void Cone(string c,double x,double y,double z,double sx,double sy,double sz)=>SpatialGeometry.Shape(Model,SpatialGeometry.Cone,c,x,y,z,sx,sy,sz);
}

// Every limb rotates about a joint rather than translating a flat silhouette.
internal sealed class SpatialPerson
{
    public SpatialNode Root {get;}
    public SpatialNode Head {get;}
    public SpatialNode LeftArm {get;}
    public SpatialNode RightArm {get;}
    public SpatialNode LeftHand {get;}
    public SpatialNode RightHand {get;}
    readonly SpatialNode leftLeg,rightLeg,leftShin,rightShin;
    public SpatialPerson(Model3DGroup parent,string coat,string hair,double x,double y,double z,double scale=1)
    {
        Root=new(parent,x,y,z,scale);const string skin="#E6B69D",pants="#273045";
        Root.Sphere(coat,0,1.21,0,.31,.43,.19);Root.Box(coat,0,.99,0,.53,.30,.34);
        Head=new(Root.Model,0,1.65,0);Head.Sphere(skin,0,.18,0,.22,.27,.21);Head.Sphere(hair,0,.32,-.04,.235,.18,.23);
        Head.Sphere(hair,0,.18,-.13,.24,.26,.13);Head.Sphere("#202235",-.077,.2,.195,.022,.03,.016);Head.Sphere("#202235",.077,.2,.195,.022,.03,.016);
        LeftArm=Arm(-.33);RightArm=Arm(.33);LeftHand=Forearm(LeftArm);RightHand=Forearm(RightArm);
        leftLeg=Leg(-.15);rightLeg=Leg(.15);leftShin=Shin(leftLeg);rightShin=Shin(rightLeg);
        SpatialNode Arm(double side){var n=new SpatialNode(Root.Model,side,1.48,0);n.Sphere(coat,0,-.17,0,.11,.22,.12);return n;}
        SpatialNode Forearm(SpatialNode p){var n=new SpatialNode(p.Model,0,-.34,0);n.Sphere(coat,0,-.13,0,.09,.18,.1);n.Sphere(skin,0,-.31,0,.085,.105,.075);return n;}
        SpatialNode Leg(double side){var n=new SpatialNode(Root.Model,side,.95,0);n.Sphere(pants,0,-.21,0,.13,.26,.14);return n;}
        SpatialNode Shin(SpatialNode p){var n=new SpatialNode(p.Model,0,-.43,0);n.Sphere(pants,0,-.2,0,.1,.23,.105);n.Box("#E6DDD4",0,-.4,.065,.22,.13,.36);return n;}
    }
    public void Pose(double time,double walking=0,bool seated=false)
    {
        double s=Math.Sin(time*5.2)*walking;
        leftLeg.Pitch.Angle=seated?-82:s*30;rightLeg.Pitch.Angle=seated?-82:-s*30;
        leftShin.Pitch.Angle=seated?85:Math.Max(0,-s)*48;rightShin.Pitch.Angle=seated?85:Math.Max(0,s)*48;
        LeftArm.Pitch.Angle=-s*26;RightArm.Pitch.Angle=s*26;LeftHand.Pitch.Angle=-12;RightHand.Pitch.Angle=-12;
        Head.Yaw.Angle=Math.Sin(time*.65)*9;Head.Pitch.Angle=Math.Sin(time*.9)*3;
        Root.Roll.Angle=Math.Sin(time*2.6)*walking*2;
    }
}
