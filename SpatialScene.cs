using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
namespace VWP;

/// <summary>Procedural, articulated dioramas. No video, bitmap deformation or network service.</summary>
public sealed partial class SpatialScene : Grid,IDisposable
{
    public const int SceneCount=28;
    readonly SpatialNode world;
    readonly List<Action<double>> motions=new();
    readonly Random random;
    bool disposed;
    public int SceneId {get;}
    public int MovingGroups=>motions.Count;
    public double Time {get;private set;}
    public Viewport3D Viewport {get;}=new(){ClipToBounds=true,IsHitTestVisible=false};
    public static readonly string[] Descriptions={
        "3D · Прогулка под сакурой, городской поезд",
        "3D · Пешеходы у канала, ночной экспресс",
        "3D · Путница у храма, вращение алой луны",
        "3D · Парусник на волнах, северное сияние",
        "3D · Движение астронавта и орбитальных обломков",
        "3D · Поезд прибывает, пассажир идёт к вагону",
        "3D · Повар подаёт рамен посетителю",
        "3D · Пловец, рыбы и пульсирующие медузы",
        "3D · Лиса гуляет среди ворот и ручьёв",
        "3D · Автомобиль проходит ночную трассу",
        "3D · Путники на мосту между парящими островами",
        "3D · Девушка у моря, две вращающиеся планеты",
        "3D · Прогулка вдвоём под зонтом и снегом",
        "3D · Ронин проходит храмовый двор",
        "3D · Художник рисует и поднимает чашку",
        "3D · Караван верблюдов пересекает пустыню",
        "3D · Объёмный цветок раскрывает лепестки",
        "3D · Кормление кои в лунном саду",
        "3D · Садовник поливает орбитальную оранжерею",
        "3D · Паром отходит от причала, кран переносит груз",
        "3D · Бариста готовит кофе, посетители беседуют",
        "3D · Поезд пересекает альпийский виадук",
        "3D · Лодки и фонари на ночном фестивале",
        "3D · Поезд метро прибывает на платформу",
        "3D · Астроном поворачивает телескоп",
        "3D · Манипулятор собирает робота на конвейере",
        "3D · Подводная станция, батискаф и дайвер",
        "3D · Два игрока передают и бросают мяч"};
    public SpatialScene(int id)
    {
        if(id<0||id>=SceneCount)throw new ArgumentOutOfRangeException(nameof(id));SceneId=id;random=new Random(740+id);
        string[] sky={"#292D58","#101D3D","#2F173C","#103B50","#111C3E","#B56E69","#222C45","#124F68","#264B47","#111B38","#758CB0","#383153","#586E8A","#5D343C","#283454","#B67565","#152D46","#243D54"};
        var color=(Color)ColorConverter.ConvertFromString(id<18?sky[id]:new[]{"#182D46","#4E7B88","#503D4D","#658799","#242E51","#283E4B","#182438","#243845","#123D5D","#705B7C"}[id-18]);
        Background=new LinearGradientBrush(color,Color.FromRgb((byte)Math.Min(255,color.R+35),(byte)Math.Min(255,color.G+24),(byte)Math.Min(255,color.B+33)),90);
        Children.Add(Viewport);
        var root=new Model3DGroup();root.Children.Add(new AmbientLight(Color.FromRgb(115,119,142)));root.Children.Add(new DirectionalLight(Color.FromRgb(255,228,209),new(-.5,-1,-.5)));root.Children.Add(new DirectionalLight(Color.FromRgb(95,134,210),new(1,-.3,1)));
        world=new(root);Viewport.Children.Add(new ModelVisual3D{Content=root});
        Viewport.Camera=new PerspectiveCamera(new(12,9,17),new(-12,-6.5,-17),new(0,1,0),45);
        switch(id)
        {
            case 0:Sakura();break;case 1:Tokyo();break;case 2:Crimson();break;case 3:Aurora();break;
            case 4:Astral();break;case 5:Station();break;case 6:Ramen();break;case 7:Ocean();break;
            case 8:Shrine();break;case 9:Racer();break;case 10:Clouds();break;case 11:Horizon();break;
            case 12:Winter();break;case 13:Ronin();break;case 14:Atelier();break;case 15:Desert();break;
            case 16:Bloom();break;case 17:Garden();break;
            default:BuildExpansion(id);break;
        }
        DressScene();
        if(id is 2 or 11)Viewport.Camera=new PerspectiveCamera(new(12,10,20),new(-12,-6,-20),new(0,1,0),48);
        if(id is 6 or 14)Viewport.Camera=new PerspectiveCamera(new(8,6,11),new(-8,-4.2,-11),new(0,1,0),43);
        if(id==16)Viewport.Camera=new PerspectiveCamera(new(7,6,10),new(-7,-4.4,-10),new(0,1,0),42);
        Update(0);
    }
    public void Update(double time){if(disposed)return;Time=time;foreach(var motion in motions)motion(time);}
    public void Dispose(){disposed=true;motions.Clear();Viewport.Children.Clear();}
    static double Cycle(double t,double duration)=>t/duration-Math.Floor(t/duration);
    static double Smooth(double t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
    SpatialNode Node(double x=0,double y=0,double z=0,double scale=1)=>new(world.Model,x,y,z,scale);
    void Floor(string color="#394651",double y=-.25)=>world.Box(color,0,y,0,18,.5,14);
    void Shadow(double x,double z,double size=1)=>world.Sphere("#283443",x,.015,z,size,.018,size*.55);
    void DressScene()
    {
        // Ground details frame the moving subjects without adding per-frame work.
        if(SceneId is 0 or 1 or 5 or 6 or 9 or 12 or 13)
        {
            for(int i=0;i<4;i++)
            {
                double x=-7+i*4.6;var lamp=Node(x,0,4.5);lamp.Box("#435061",0,1.5,0,.07,3,.07);lamp.Box("#566175",0,3,0,.45,.12,.45);lamp.Sphere("#F3C996",0,2.8,0,.18,.18,.18,true);
                lamp.Box("#495A65",.7,.22,0,.65,.44,.6);lamp.Sphere(SceneId==12?"#D1DAD9":"#7F9B90",.7,.55,0,.45,.4,.4);
            }
        }
        if(SceneId is 2 or 8 or 13 or 17)
        {
            for(int i=0;i<20;i++){double x=-7+random.NextDouble()*14,z=-3+random.NextDouble()*8;if(Math.Abs(x)<3)continue;world.Sphere("#82928B",x,.12,z,.3,.18,.23);world.Cone("#829F90",x+.3,.02,z,.06,.45,.06);}
            foreach(double x in new[]{-3.2,3.2}){world.Box("#86898A",x,.55,-1,.4,1.1,.4);world.Box("#E6C398",x,1.1,-1,.3,.3,.3,true);world.Box("#686D78",x,1.35,-1,.6,.15,.6);}
        }
        if(SceneId is 3 or 7 or 11)
        {
            for(int i=0;i<15;i++){double x=-8+random.NextDouble()*16,z=-5+random.NextDouble()*10;if(Math.Abs(x)<4)continue;world.Sphere("#62778B",x,.02,z,.45+random.NextDouble()*.5,.25,.4);}
        }
        if(SceneId==14)
        {
            world.Box("#B78D8C",0,.02,1,6,.03,4);world.Box("#637185",3,1,-1,1.4,2,.65);world.Box("#C9AD92",3,2.1,-1,1.5,.14,.8);
            world.Box("#E1C7A0",-1.5,1.6,0,.05,.7,.05);world.Cone("#EAC8A0",-1.5,1.8,0,.32,.38,.32);
        }
    }
    void Mountains(string color="#37465E")
    {for(int i=0;i<9;i++){var n=Node(-12+i*3,0,-9);n.Cone(color,0,0,0,2.8,3+random.NextDouble()*4,2.4);}}
    void City()
    {
        for(int i=0;i<10;i++){double x=-10+i*2.2,h=2+random.NextDouble()*5;world.Box(i%2==0?"#273451":"#35435E",x,h/2,-6,1.7,h,1.5);
            for(int k=0;k<4;k++)for(int j=0;j<3;j++)world.Box(k%3==0?"#E0AC83":"#8AAFC3",x-.55+j*.52,.7+k*(h-.5)/4,-5.23,.18,.28,.025,true);}
    }
    void Tree(double x,double z,string color="#EEAFC9",double scale=1)
    {
        var n=Node(x,0,z,scale);n.Box("#5E4855",0,1.4,0,.24,2.8,.26);
        for(int i=0;i<7;i++){double a=i*2.4;n.Sphere(color,Math.Cos(a)*.75,2.6+(i%3)*.3,Math.Sin(a)*.65,.85,.52,.8);}
    }
    void Pine(double x,double z,double scale=1)
    {var n=Node(x,0,z,scale);n.Box("#5C4950",0,.8,0,.2,1.6,.2);n.Cone("#436A68",0,.5,0,.85,2, .85);n.Cone("#72908D",0,1.3,0,.6,1.5,.6);}
    void Torii(double x,double z,double scale=1)
    {var n=Node(x,0,z,scale);foreach(double side in new[]{-1.3,1.3})n.Box("#BE665D",side,1.5,0,.26,3,.28);n.Box("#D38676",0,2.7,0,3.5,.22,.36);n.Box("#39434B",0,3,0,3.9,.25,.48);}
    void House(double x,double z,string color="#C18F77",double scale=1)
    {
        var n=Node(x,0,z,scale);n.Box(color,0,1,0,2.7,2,2);
        n.Box("#354052",0,2.12,0,3.2,.25,2.5);n.Box("#425268",0,2.4,0,2.7,.3,2.1);n.Box("#4C5B70",0,2.65,0,2,.2,1.6);
        n.Box("#EDBD82",-.65,1,.99,.7,.85,.05,true);n.Box("#EDBD82",.65,1,.99,.7,.85,.05,true);n.Box("#584B4E",0,.65,1.03,.25,1.3,.08);
    }
    void Rail(double z,double length=16)
    {
        for(int i=0;i<20;i++)world.Box("#65505B",-length/2+i*length/19,.1,z,.22,.13,1.2);
        foreach(double dz in new[]{-.42,.42})world.Box("#ABB2BE",0,.2,z+dz,length,.08,.06);
    }
    SpatialNode Train(double z)
    {
        var train=Node(0,.3,z);
        for(int c=0;c<3;c++){
            double x=c*2.65;train.Box("#E0D4C7",x,.75,0,2.5,1.2,.95);train.Box("#537E85",x,.45,.49,2.5,.2,.04);train.Box("#4E5C72",x,1.4,0,2.5,.16,1.05);
            for(int j=0;j<4;j++)train.Box("#BDE1DF",x-.87+j*.57,.94,.49,.39,.42,.035,true);
            foreach(double axle in new[]{-.8,.8})foreach(double side in new[]{-.44,.44}){var wheel=new SpatialNode(train.Model,x+axle,.12,side);wheel.Sphere("#263244",0,0,0,.24,.24,.10);wheel.Box("#A9B3C0",0,0,.11,.035,.35,.03);motions.Add(t=>wheel.Roll.Angle=-t*170);}}
        return train;
    }
    SpatialPerson Person(string color,double x,double z,double scale=1)=>new(world.Model,color,"#353247",x,0,z,scale);
    void Walk(SpatialPerson p,double center,double z,double distance,double duration,double offset=0)
    {
        motions.Add(t=>{double a=2*Math.PI*(t+offset)/duration;p.Root.Move(center+Math.Sin(a)*distance,0,z+Math.Cos(a)*.6);p.Root.Yaw.Angle=Math.Atan2(Math.Cos(a)*distance,-Math.Sin(a)*.6)*180/Math.PI;p.Pose(t+offset,Math.Abs(Math.Cos(a))*.7+.3);});
    }
    void Water(string color="#487E94",double y=-.05)
    {
        var mesh=new MeshGeometry3D();const int columns=25,rows=19;
        for(int z=0;z<rows;z++)for(int x=0;x<columns;x++)mesh.Positions.Add(new(-9+x*.75,y,-7+z*.78));
        for(int z=0;z<rows-1;z++)for(int x=0;x<columns-1;x++){int a=z*columns+x;foreach(int k in new[]{a,a+columns,a+1,a+1,a+columns,a+columns+1})mesh.TriangleIndices.Add(k);}
        world.Model.Children.Add(new GeometryModel3D(mesh,SpatialGeometry.Paint(color)));
        motions.Add(t=>{var positions=new Point3DCollection(columns*rows);for(int z=0;z<rows;z++)for(int x=0;x<columns;x++)positions.Add(new(-9+x*.75,y+Math.Sin(x*.6+t*1.2)*.035+Math.Sin(z*.8+t)*.035,-7+z*.78));mesh.Positions=positions;});
        for(int i=0;i<22;i++){double x=-8+random.NextDouble()*16,z=-6+random.NextDouble()*12;var wave=Node(x,y,z);wave.Sphere(i%3==0?"#93B4C1":"#608BA4",0,0,0,.35+random.NextDouble(),.025,.07);int k=i;motions.Add(t=>{wave.Position.OffsetY=y+Math.Sin(t*1.8+k)*.07;wave.Position.OffsetX=x+Math.Sin(t*.7+k)*.6;});}
    }
    void Particles(string color,int count,bool falling)
    {
        for(int i=0;i<count;i++){double x=-8+random.NextDouble()*16,z=-6+random.NextDouble()*12,p=random.NextDouble();var n=Node();n.Sphere(color,0,0,0,.045,.035,.05,true);motions.Add(t=>n.Move(x+Math.Sin(t*.5+p*20)*.4,falling?7*(1-Cycle(t+p*15,15)):2+Math.Sin(t+p*20)*1.5,z));}
    }
    SpatialNode Planet(double x,double y,double z,double radius,string color,string land,double speed=6)
    {
        var p=Node(x,y,z);var texture=new DrawingGroup();
        using(var dc=texture.Open())
        {
            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),null,new Rect(0,0,512,256));
            var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(land));
            for(int i=0;i<16;i++)
            {
                var shape=new StreamGeometry();using(var g=shape.Open()){double px=random.NextDouble()*512,py=25+random.NextDouble()*205;g.BeginFigure(new Point(px,py),true,true);for(int j=0;j<10;j++){double a=j*Math.PI/5,r=12+random.NextDouble()*22;g.LineTo(new Point(px+Math.Cos(a)*r*1.7,py+Math.Sin(a)*r*.65),true,false);}}shape.Freeze();dc.DrawGeometry(brush,null,shape);
            }
        }
        texture.Freeze();var material=new DiffuseMaterial(new DrawingBrush(texture){ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,1,1),TileMode=TileMode.Tile});material.Freeze();
        var globe=new GeometryModel3D(SpatialGeometry.Ball,material){Transform=new ScaleTransform3D(radius,radius,radius)};globe.Freeze();p.Model.Children.Add(globe);
        motions.Add(t=>p.Yaw.Angle=t*speed);return p;
    }
    void Sakura()
    {
        Floor("#767188");City();Rail(-3);var train=Train(-3);motions.Add(t=>train.Position.OffsetX=-18+Cycle(t,22)*30);
        for(int i=0;i<13;i++)world.Box("#D1B4BA",-8+i*1.3,.7,-1,.07,1.4,.07);world.Box("#EAD1CB",0,1.4,-1,17,.08,.1);
        Tree(-5,2);Tree(5,-.5);var p=Person("#E6D5CF",0,2,1.25);Walk(p,0,2,2.5,22);Particles("#F5BCCD",30,true);
        world.Box("#514757",-4,.45,3,2,.15,.65);
    }
    void Tokyo()
    {
        Water("#253E5A");City();world.Box("#596775",0,.1,2,17,.3,3);Rail(-2);var train=Train(-2);motions.Add(t=>train.Position.OffsetX=15-Cycle(t,18)*34);
        for(int i=0;i<5;i++){var sign=Node(-7+i*3,0,-4.8);sign.Box(i%2==0?"#E785B2":"#70D2D6",0,2,0,.6,2.2,.09,true);}
        Walk(Person("#D6A291",0,2,1.15),0,2,4,26);Walk(Person("#A4BFD1",3,3),1,3,4,30,12);
    }
    void Crimson()
    {
        Floor("#5B4057");Mountains("#3B304F");Torii(0,-2,1.5);House(0,-6,"#9E665E",1.2);Planet(-4,6,-8,2.2,"#B9586D","#DC8891",9);
        for(int i=0;i<6;i++)world.Box("#82616C",0,.1+i*.1,-1+i*.5,4,.2,.48);
        var p=Person("#E4BDAC",0,3,1.25);Walk(p,0,3,2.3,24);Particles("#E9AA8E",22,false);
    }
    void Aurora()
    {
        Water();Mountains("#405E76");var boat=Node();boat.Sphere("#836668",0,.2,0,1.8,.4,.65);boat.Box("#D7D8CA",0,1.7,0,.09,3,.09);
        var sail=new SpatialNode(boat.Model,0,1.9,0);sail.Box("#ECE3D0",.6,0,0,1.15,1.9,.045);var sailor=new SpatialPerson(boat.Model,"#DBA477","#313B4A",-.6,.35,0,.65);
        motions.Add(t=>{boat.Move(Math.Sin(t*.14)*3,.12+Math.Sin(t*1.2)*.13,0);boat.Roll.Angle=Math.Sin(t)*4;sail.Yaw.Angle=Math.Sin(t*.7)*13;sailor.Pose(t);sailor.RightArm.Pitch.Angle=-65;});
        var ribbon=new MeshGeometry3D();for(int i=0;i<40;i++){ribbon.Positions.Add(new());ribbon.Positions.Add(new());if(i<39)foreach(int k in new[]{i*2,i*2+1,i*2+2,i*2+1,i*2+3,i*2+2})ribbon.TriangleIndices.Add(k);}
        var glow=SpatialGeometry.Paint("#79BAAB",true);world.Model.Children.Add(new GeometryModel3D(ribbon,glow){BackMaterial=glow});
        motions.Add(t=>{var points=new Point3DCollection();for(int i=0;i<40;i++){double h=5.7+Math.Sin(i*.22+t*.35)*.6;points.Add(new(-10+i*.52,h,-8+Math.Sin(i*.3+t*.2)));points.Add(new(-10+i*.52,h+1.1+Math.Sin(i*.2+t*.5)*.3,-8+Math.Sin(i*.3+t*.2)));}ribbon.Positions=points;});
    }
    void Astral()
    {
        Planet(-3,2,-5,3.5,"#547CA6","#97ADC7",7);var astronaut=Person("#E3DBD1",2,1,1.5);astronaut.Head.Sphere("#AACBD9",0,.2,.04,.29,.3,.28);astronaut.Head.Box("#3E607E",0,.22,.28,.38,.2,.04);astronaut.Root.Box("#AEB5C4",0,1.3,-.28,.5,.65,.25);
        motions.Add(t=>{astronaut.Root.Move(2+Math.Sin(t*.3),2+Math.Sin(t*.5)*.5,2);astronaut.Pose(t,.35);astronaut.Root.Roll.Angle=Math.Sin(t*.3)*20;astronaut.LeftArm.Roll.Angle=45+Math.Sin(t)*15;astronaut.RightArm.Roll.Angle=-45;});
        for(int i=0;i<16;i++){int k=i;var rock=Node();rock.Sphere("#77829A",0,0,0,.15+i%3*.1,.18,.23);motions.Add(t=>{double a=t*.1+k*.5;rock.Move(Math.Cos(a)*6,2+Math.Sin(a*.7)*3,Math.Sin(a)*5);rock.Roll.Angle=t*12+k*20;});}Particles("#D1DDF4",35,false);
    }
    void Station()
    {
        Floor("#A79588");Mountains("#9A7E87");Rail(-1);House(-5,-5,"#D3B995");Tree(6,-4,"#B7A171");
        world.Box("#D9C6AE",0,.13,2.8,16,.26,4);world.Box("#F2D8A4",0,.28,.85,16,.04,.14,true);
        var train=Train(-1);var p=Person("#607D90",-3,2,1.2);world.Box("#705A53",-3,.65,2.5,2.2,.16,.65);
        foreach(double x in new[]{-3.8,-2.2})world.Box("#495462",x,.3,2.5,.1,.6,.5);
        motions.Add(t=>{double a=t%28;train.Position.OffsetX=a<9?-17+16*Smooth(a/9):a<19?-1:-1+22*Smooth((a-19)/9);
            double walk=a<10?0:a<16?Smooth((a-10)/6):a<20?1:1-Smooth((a-20)/8);
            p.Root.Move(-3+walk*4,a<8?-.35:-.35+.35*Smooth((a-8)/2),2.6-walk*1.2);p.Root.Yaw.Angle=a<20?110:-70;p.Pose(t,a>=10&&a<16||a>=20?1:0,a<8);});
    }
    void Ramen()
    {
        Floor("#56616F");House(0,-3,"#AF7768",1.8);world.Box("#8E5C55",0,1.2,0,5,.18,1.1);
        for(int i=0;i<5;i++){var lantern=Node(-3+i*1.5,3,0);lantern.Sphere("#F1B77E",0,0,0,.24,.38,.24,true);int j=i;motions.Add(t=>lantern.Roll.Angle=Math.Sin(t+j)*5);}
        var cook=Person("#E1DAD0",-.8,-.7,1.05);cook.Head.Box("#F1EADC",0,.5,0,.5,.2,.4);var guest=Person("#97B3C2",.8,1.2,1.05);guest.Root.Yaw.Angle=180;
        var bowl=Node(0,1.34,0);bowl.Sphere("#E9CC9C",0,0,0,.25,.13,.25);
        motions.Add(t=>{double a=(1-Math.Cos(t*.8))/2;cook.Pose(t);guest.Pose(t);cook.RightArm.Pitch.Angle=-30-a*55;cook.RightHand.Pitch.Angle=-40;guest.LeftArm.Pitch.Angle=-80+a*30;guest.LeftHand.Pitch.Angle=-35;bowl.Position.OffsetZ=-.3+a*.8;bowl.Position.OffsetX=-.4+a*.9;});Walk(Person("#D2A3A0",0,3),0,3,5,30);
    }
    void Ocean()
    {
        Floor("#487D85");for(int i=0;i<6;i++){double x=-7+i*2.7;world.Box("#6C9D9E",x,1.6,-4,.55,3.2,.55);world.Box("#88B1AF",x,3.25,-4,.9,.2,.9);}world.Box("#85AAAA",0,3.5,-4,15,.35,.7);
        var swimmer=Person("#ACC6DB",0,1,1.35);motions.Add(t=>{swimmer.Root.Move(Math.Sin(t*.25)*3,2+Math.Sin(t*.7)*.2,1);swimmer.Pose(t,.45);swimmer.Root.Roll.Angle=-75;swimmer.LeftArm.Pitch.Angle=t*110;swimmer.RightArm.Pitch.Angle=t*110+180;});
        for(int i=0;i<6;i++){int k=i;var jelly=Node(-6+i*2,3,-1);jelly.Sphere("#B5CBDB",0,0,0,.4,.25,.4,true);var tails=new List<SpatialNode>();for(int j=0;j<5;j++){var tail=new SpatialNode(jelly.Model,-.25+j*.12,-.2,0);tail.Sphere("#84B6CB",0,-.4,0,.02,.5,.02,true);tails.Add(tail);}motions.Add(t=>{jelly.Position.OffsetY=2.7+Math.Sin(t+k)*.6;foreach(var tail in tails)tail.Roll.Angle=Math.Sin(t*2+k)*18;});}
        Fish(8,false);Particles("#8BC8D4",25,false);
    }
    void Shrine()
    {
        Floor("#58766A");Mountains("#385E58");Torii(0,-3,1.4);for(int i=0;i<8;i++)Pine(-7+i*2,-5,1.5);world.Box("#B2B6A2",0,.05,1,3,.1,9);
        var fox=Animal("#D5A17B",false);motions.Add(t=>{double a=t*.3;fox.root.Move(Math.Sin(a)*3,0,1+Math.Cos(a));fox.root.Yaw.Angle=Math.Atan2(Math.Cos(a)*3,-Math.Sin(a))*180/Math.PI;for(int i=0;i<4;i++)fox.legs[i].Pitch.Angle=Math.Sin(t*5+i%2*Math.PI)*28;fox.tail.Roll.Angle=Math.Sin(t*2)*20;});Particles("#D5D8A4",20,false);
    }
    void Racer()
    {
        Floor("#273647");City();world.Box("#586579",0,.03,1,18,.06,5);
        for(int i=0;i<12;i++)world.Box("#E4C9B9",-8+i*1.5,.08,1,.7,.025,.08,true);
        var car=Node();car.Box("#B9ADCC",0,.52,0,2.7,.5,1.3);car.Box("#9BAAC5",-.2,.94,0,1.5,.4,1.16);car.Box("#314C68",-.05,1.13,0,1.1,.08,1.08);car.Box("#D8E5E2",1.37,.58,0,.045,.16,1,true);
        for(int i=0;i<4;i++){var wheel=new SpatialNode(car.Model,i<2?-.85:.85,.3,i%2==0?-.65:.65);wheel.Sphere("#252B38",0,0,0,.3,.3,.14);wheel.Box("#A3B4C7",0,0,i%2==0?-.15:.15,.05,.44,.035);motions.Add(t=>wheel.Roll.Angle=-t*260);}
        motions.Add(t=>{double a=t*.22;car.Move(Math.Sin(a)*5,.02,1+Math.Cos(a)*1.4);car.Yaw.Angle=Math.Atan2(Math.Sin(a)*1.4,Math.Cos(a)*5)*180/Math.PI;});
    }
    void Clouds()
    {
        for(int i=0;i<3;i++){double x=-6+i*6;var island=Node(x,0,-1);island.Sphere("#9FA1B2",0,-.7,0,2.6,1.2,2);island.Sphere("#B6C4AE",0,0,0,2.6,.3,2);House(x,-2,"#E1D7CC",.8);}
        world.Box("#BEB1A7",0,.08,1,14,.18,1.3);for(int i=0;i<18;i++)world.Box("#E4D6C9",-7+i*.82,.55,1.65,.05,.95,.05);world.Box("#DFD4C9",0,1.04,1.65,14,.07,.06);
        Walk(Person("#D5A6AD",0,1,.9),0,1,5.5,35);Walk(Person("#849EBB",3,1,.8),0,1,5.5,35,3);
        for(int i=0;i<8;i++){int k=i;var bird=Node();var wing=new SpatialNode(bird.Model);wing.Box("#F2EBDA",0,0,0,.55,.035,.12);motions.Add(t=>{bird.Move(Math.Sin(t*.3+k)*7,3+Math.Cos(t*.5+k),Math.Cos(t*.3+k)*3);wing.Roll.Angle=Math.Sin(t*7+k)*25;});}
    }
    void Horizon()
    {
        Water("#746C91");world.Sphere("#6A5B71",3,0,2,3,.55,2);Planet(-3,6,-8,3.2,"#9582AF","#C1A3C2",8);Planet(3,5,-8,1.6,"#BD9EBC","#E0C1D0",-12);
        var p=Person("#47465F",3,2,1.45);p.Root.Yaw.Angle=-65;motions.Add(t=>{p.Pose(t,0,true);p.Root.Position.OffsetY=.2;p.RightArm.Pitch.Angle=-25-Math.Sin(t*.65)*25;p.RightHand.Pitch.Angle=-35;p.Head.Yaw.Angle=-20+Math.Sin(t*.4)*25;});
    }
    void Winter()
    {
        Floor("#D0D9DF");Mountains("#879BAD");for(int i=0;i<4;i++){House(-6+i*4,-4,"#A6A5AE");world.Box("#E7E7E3",-6+i*4,2.8,-4,2.6,.18,2.2);}Pine(-7,1,1.5);Pine(7,-1,1.5);
        var a=Person("#A57485",0,2);var b=Person("#64788F",1,2);var umbrella=new SpatialNode(a.Root.Model,0,2.2,0);umbrella.Cone("#D29CA7",0,0,0,.9,.35,.9);umbrella.Box("#C1B7B8",0,-.35,0,.035,.7,.035);
        motions.Add(t=>{double x=Math.Sin(t*.18)*4;a.Root.Move(x,0,2);b.Root.Move(x+.8,0,2.15);a.Root.Yaw.Angle=b.Root.Yaw.Angle=Math.Cos(t*.18)>0?90:-90;a.Pose(t,1);b.Pose(t+.4,1);a.RightArm.Pitch.Angle=-100;a.RightHand.Pitch.Angle=-35;});Particles("#F5F0E8",60,true);
    }
    void Ronin()
    {
        Floor("#8D7977");House(0,-4,"#A57769",1.4);Torii(-5,-1);Tree(6,-3,"#C79A78",1.4);
        var p=Person("#59586B",0,2,1.4);p.Root.Box("#C8B7A2",-.42,.78,0,.07,.9,.08);Walk(p,0,2,3,24);motions.Add(t=>{p.LeftArm.Pitch.Angle=-35;p.LeftHand.Pitch.Angle=-55;p.Head.Yaw.Angle=Math.Sin(t*.5)*20;});Particles("#DBA078",26,true);
    }
    void Atelier()
    {
        Floor("#A28E8C");world.Box("#787D96",0,2.7,-3.5,13,5.4,.2);world.Box("#3D526D",0,3,-3.35,7,3.2,.05);for(int i=0;i<3;i++)world.Box("#BEC1C6",-3+i*3,3,-3.28,.08,3.3,.06);world.Box("#BEC1C6",0,3,-3.25,7,.08,.05);
        world.Box("#C8A992",0,1.2,0,4,.16,1.6);foreach(double x in new[]{-1.7,1.7})world.Box("#6D6571",x,.6,0,.12,1.2,1.2);world.Box("#EFE1C7",.4,1.29,.25,1.2,.03,.8);
        var p=Person("#B0B8BC",0,1.3,1.2);p.Root.Yaw.Angle=180;var pencil=new SpatialNode(p.RightHand.Model,0,-.3,0);pencil.Box("#E3B47C",0,0,0,.025,.32,.025);var cup=new SpatialNode(p.LeftHand.Model,0,-.25,0);cup.Sphere("#DAB3A3",0,0,0,.11,.15,.11);
        motions.Add(t=>{double drink=Smooth((Math.Sin(t*.45)-.3)/.5);p.Pose(t,0,true);p.Root.Position.OffsetY=-.15;p.RightArm.Pitch.Angle=-70+Math.Sin(t*3)*5;p.RightHand.Pitch.Angle=-35+Math.Sin(t*2)*8;p.LeftArm.Pitch.Angle=-40-drink*45;p.LeftHand.Pitch.Angle=-30-drink*70;p.Head.Pitch.Angle=12-drink*18;});
        world.Box("#B4A08C",-4,1.5,-2,1.7,3,.5);for(int i=0;i<9;i++)world.Box(i%2==0?"#A4B6B9":"#C4A19D",-4.6+i%3*.5,.6+i/3*.8,-1.68,.28,.6,.3);Tree(5,-1,"#8CA69E",.8);
    }
    (SpatialNode root,SpatialNode[] legs,SpatialNode tail) Animal(string color,bool camel)
    {
        var n=Node();n.Sphere(color,0,camel?1.15:.65,0,camel?.55:.25,camel?.45:.25,camel?.95:.6);var legs=new SpatialNode[4];
        for(int i=0;i<4;i++){legs[i]=new(n.Model,i%2==0?-.3:.3,camel?1:.55,i<2?-.45:.4);legs[i].Sphere(color,0,camel?-.5:-.25,0,.075,camel?.55:.28,.08);}
        n.Sphere(color,0,camel?1.8:.85,.6,camel?.17:.2,camel?.7:.2,.2);n.Sphere(color,0,camel?2.3:.88,camel?.8:.82,.2,.18,.3);
        n.Sphere("#273341",-.12,camel?2.36:.95,camel?.98:.96,.03,.03,.03);n.Sphere("#273341",.12,camel?2.36:.95,camel?.98:.96,.03,.03,.03);
        var tail=new SpatialNode(n.Model,0,camel?1.3:.65,-.6);tail.Sphere(camel?color:"#E2C1A1",0,-.1,-.3,camel?.04:.18,camel?.4:.17,camel?.06:.45);
        if(camel)n.Sphere(color,0,1.55,-.15,.4,.4,.45);else{n.Cone(color,-.13,1,.69,.09,.23,.09);n.Cone(color,.13,1,.69,.09,.23,.09);}return(n,legs,tail);
    }
    void Desert()
    {
        Floor("#C7A287");for(int i=0;i<8;i++)world.Sphere(i%2==0?"#D2AF91":"#B9917E",-9+i*2.8,-.1,-5,3.5,1.3,3);
        for(int j=0;j<3;j++){int k=j;var camel=Animal("#B98A65",true);var rider=new SpatialPerson(camel.root.Model,k%2==0?"#C8C8BE":"#768C9C","#4B424B",0,1.3,-.15,.65);
            motions.Add(t=>{double a=t*.13;camel.root.Move(Math.Sin(a)*4+(k-1)*2.6,.03,1+Math.Cos(a)*.7);camel.root.Yaw.Angle=Math.Cos(a)>0?90:-90;for(int i=0;i<4;i++)camel.legs[i].Pitch.Angle=Math.Sin(t*3+k+i%2*Math.PI)*22;camel.tail.Roll.Angle=Math.Sin(t*2)*10;rider.Pose(t,0,true);rider.Root.Roll.Angle=Math.Sin(t*3+k)*4;});}
    }
    void Bloom()
    {
        Water("#35556E");var flower=Node(0,1.8,0);flower.Sphere("#F1CCA8",0,0,0,.55,.45,.55,true);
        for(int i=0;i<14;i++){double a=i*360.0/14;var pivot=new SpatialNode(flower.Model);pivot.Yaw.Angle=a;var petal=new SpatialNode(pivot.Model);petal.Sphere(i%2==0?"#A8BED4":"#C4ADCA",0,.2,1.25,.58,.2,1.1);int k=i;motions.Add(t=>petal.Pitch.Angle=-20-25*(.5+.5*Math.Sin(t*.55+k*.12)));}
        motions.Add(t=>{flower.Yaw.Angle=t*9;flower.Position.OffsetY=1.8+Math.Sin(t*.7)*.2;});Particles("#DFD3DD",35,false);
    }
    void Fish(int count,bool koi)
    {
        for(int i=0;i<count;i++){int k=i;var fish=Node();fish.Sphere(koi?(i%2==0?"#D5A483":"#DED9C6"):"#A7C6C4",0,0,0,.16,.16,.38);var tail=new SpatialNode(fish.Model,0,0,-.35);tail.Sphere("#A9B6BC",0,0,-.15,.22,.035,.2);
            motions.Add(t=>{double a=t*.45+k;fish.Move(Math.Cos(a)*(koi?2:5),koi?.04:1.5+Math.Sin(a)*.8,Math.Sin(a)*(koi?1.5:3));fish.Yaw.Angle=-a*180/Math.PI;tail.Yaw.Angle=Math.Sin(t*7+k)*30;});}
    }
    void Garden()
    {
        Water("#456476");world.Box("#9A9A8F",4,.1,0,5,.3,12);Tree(6,-3,"#AD9CB9",1.4);Tree(-6,-4,"#B0A2BC",1.2);Torii(2,-5,.9);Planet(-5,6,-8,1.1,"#D0CCD7","#A8ACBC",8);Fish(8,true);
        var p=Person("#ADB7CC",2.5,1,1.25);p.Root.Yaw.Angle=-80;motions.Add(t=>{p.Pose(t);p.RightArm.Pitch.Angle=-55-Math.Sin(t*1.2)*25;p.RightHand.Pitch.Angle=-35;p.Head.Pitch.Angle=15;});
        for(int i=0;i<6;i++){int k=i;var food=Node();food.Sphere("#E6CC9F",0,0,0,.035,.035,.035);motions.Add(t=>{double a=Cycle(t+k*.22,2.8);food.Move(2.2-a*2.4,1.1*(1-a)+Math.Sin(a*Math.PI)*.5,.8-a*.3);});}
    }
}

