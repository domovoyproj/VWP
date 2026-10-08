using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace VWP;

// Coordinates refer to the original 1920 x 1080 composition. Static portraits
// are intentionally untouched: these are independently articulated scene actors.
public static class SceneActors
{
    static readonly Dictionary<string,SolidColorBrush> brushes=new();
    static readonly Dictionary<(string,double),Pen> pens=new();
    static readonly Brush trainFront=Gradient("#A39377","#55594F","#303937");
    static readonly Brush trainGlass=Gradient("#59706A","#2B4549","#182B30");
    static readonly Brush glass=Gradient("#10D5DFFF","#739CB4DC","#09E6C5EA");
    static Brush Gradient(string top,string middle,string bottom)
    {
        var b=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(1,1)};
        b.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(top),0));
        b.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(middle),.55));
        b.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(bottom),1));b.Freeze();return b;
    }
    public static string Description(int id)=>id switch
    {
        0=>"Птицы и движение городского транспорта",
        1=>"Прохожие на мосту и отражения в канале",
        2=>"Вращение луны, птицы и падающие листья",
        3=>"Парусник с кильватерным следом",
        4=>"Вращающиеся обломки и спутник",
        5=>"Прибытие поезда и пассажир на платформе",
        6=>"Прохожие под зонтом встречаются у раменной",
        7=>"Плавающие медузы и стая рыб",
        8=>"Лисёнок на тропе и течение ручья",
        9=>"Автомобили на мокрой дороге",
        10=>"Птицы между островами",
        11=>"Вращение двух планет и прибой",
        12=>"Прохожие под зонтами",
        13=>"Путники на дальней тропе и листья",
        14=>"Движение транспорта за окном и пар над чашкой",
        15=>"Идущий караван с движением ног",
        16=>"Вращающиеся стеклянные лепестки и капли",
        17=>"Кои плывут к корму и расходятся",
        _=>""
    };
    static Brush B(string color)
    {
        if(!brushes.TryGetValue(color,out var b)){b=new((Color)ColorConverter.ConvertFromString(color));b.Freeze();brushes[color]=b;}return b;
    }
    static Pen P(string color,double width)
    {
        width=Math.Max(.25,Math.Round(width*4)/4); // Bound the cache even for perspective-dependent widths.
        var key=(color,width);if(!pens.TryGetValue(key,out var p)){p=new(B(color),width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};p.Freeze();pens[key]=p;}return p;
    }
    static void Line(DrawingContext d,string c,double w,double x,double y,double xx,double yy)=>d.DrawLine(P(c,w),new(x,y),new(xx,yy));
    static void Ellipse(DrawingContext d,string c,double x,double y,double rx,double ry)=>d.DrawEllipse(B(c),null,new(x,y),rx,ry);
    static void Poly(DrawingContext d,string color,params Point[] points)
    {
        var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(points[0],true,true);for(int i=1;i<points.Length;i++)c.LineTo(points[i],true,false);}g.Freeze();d.DrawGeometry(B(color),null,g);
    }
    static double Frac(double n)=>n-Math.Floor(n);
    static double Ease(double n){n=Math.Clamp(n,0,1);return n*n*(3-2*n);}
    static double Fade(double n)=>Ease(n*12)*Ease((1-n)*12);

    public static void Draw(DrawingContext d,int id,double t,bool economy)
    {
        switch(id)
        {
            case 0: Birds(d,t,6,120,250,1080,90,.65);Traffic(d,t,350,688,650,13);break;
            case 1:
                WalkerRoute(d,t,34,860,451,1250,444,.20,false);WalkerRoute(d,t+12,43,1250,444,860,451,.18,false);
                Ripples(d,t,new Rect(50,755,850,265),"#3376B6D1",economy);break;
            case 2: Birds(d,t,4,50,330,1050,50,.5);Leaves(d,t,20,"#A6C85342");break;
            case 3:
                double boat=Frac(t/80);d.PushOpacity(Fade(boat));Boat(d,680+boat*620,773+Math.Sin(t*.7)*2,.65,t);d.Pop();
                Ripples(d,t,new Rect(690,850,1000,145),"#3352CCA9",economy);break;
            case 4: Space(d,t,economy);break;
            case 5: Train(d,t);break;
            case 6:
                Meet(d,t);Ripples(d,t,new Rect(760,825,380,190),"#449FC5CB",economy);break;
            case 7:
                for(int i=0;i<4;i++)Jellyfish(d,410+i*175+Math.Sin(t*.13+i)*50,280+Math.Sin(t*.24+i)*100,.5+i*.13,t+i);
                for(int i=0;i<9;i++)Fish(d,120+Frac(t/40+i*.057)*940,610+Math.Sin(t*.7+i)*30,.35,t+i,false);break;
            case 8:
                double fox=Frac(t/38);d.PushOpacity(Fade(fox));Fox(d,1190+fox*225,797-fox*170,.47,t);d.Pop();
                Ripples(d,t,new Rect(360,900,720,105),"#3364B6D4",economy);break;
            case 9:
                for(int i=0;i<3;i++){double a=Frac(t/19+i*.33);d.PushOpacity(Fade(a)*.65);Car(d,100+800*a,1080-260*a,1.1-.8*a,t+i);d.Pop();}break;
            case 10: Birds(d,t,9,100,420,1280,210,1);break;
            case 11: Ripples(d,t,new Rect(850,590,380,155),"#66D2A6EA",economy);break;
            case 12:
                WalkerRoute(d,t,40,780,678,1240,675,.35,true,-35);WalkerRoute(d,t+12,47,1240,675,780,678,.30,true,-35);break;
            case 13:
                WalkerRoute(d,t,40,950,677,1130,691,.18,false);Leaves(d,t,18,"#A6DA783C");break;
            case 14:
                Traffic(d,t,760,492,350,15);Steam(d,t,1500,705,48);break;
            case 15: Caravan(d,t);break;
            case 16: Glass(d,t,economy);break;
            case 17:
                for(int i=0;i<7;i++)
                {
                    double a=t*.14+i*.88;
                    double x=1100+Math.Cos(a)*(120+i*12),y=850+Math.Sin(a)*70;
                    d.PushTransform(new RotateTransform(Math.Atan2(Math.Cos(a)*70,-Math.Sin(a)*(120+i*12))*180/Math.PI,x,y));
                    d.PushOpacity(.55);Fish(d,x,y,.6,t+i,true);d.Pop();d.Pop();
                }
                Ripples(d,t,new Rect(620,840,550,135),"#337ABAD4",economy);break;
        }
    }

    // A planted foot traverses backwards relative to the pelvis during stance.
    // Forward body travel cancels that movement, rather than sliding a rigid sprite.
    public static Point Foot(double phase)
    {
        double p=Frac(phase);
        return p<.6?new(12-40*p,0):new(-12+24*Ease((p-.6)/.4),-13*Math.Sin((p-.6)/.4*Math.PI));
    }
    static void Leg(DrawingContext d,double phase,string color)
    {
        var f=Foot(phase);var hip=new Point(0,-43);var ankle=new Point(f.X,f.Y-3);
        var delta=ankle-hip;double len=delta.Length;
        var middle=hip+delta*.5;double bend=Math.Sqrt(Math.Max(0,24*24-len*len*.25));
        var knee=middle+new Vector(delta.Y,-delta.X)*(bend/Math.Max(1,len));
        Line(d,color,7,hip.X,hip.Y,knee.X,knee.Y);Line(d,color,6,knee.X,knee.Y,ankle.X,ankle.Y);
        Line(d,"#171B26",5,ankle.X-2,ankle.Y,ankle.X+6,ankle.Y);
    }
    static void Person(DrawingContext d,double x,double y,double scale,double phase,bool umbrella,bool facingLeft=false,double greeting=0)
    {
        d.PushTransform(new TranslateTransform(x,y));d.PushTransform(new ScaleTransform(facingLeft?-scale:scale,scale));
        Ellipse(d,"#44000000",0,1,17,3);
        Leg(d,phase+.5,"#252737");
        double bob=Math.Cos(phase*Math.PI*4)*1.1;
        double swing=Math.Sin(phase*Math.PI*2);
        Line(d,"#353444",6,0,-67+bob,-swing*11,-49+bob);
        Poly(d,"#414453",new(-9,-70+bob),new(8,-70+bob),new(12,-40),new(-11,-40));
        Line(d,"#A38B80",1.3,-8,-69+bob,-10,-42);
        Leg(d,phase,"#343442");
        Ellipse(d,"#A99082",2,-82+bob,6,8);Ellipse(d,"#24232E",0,-86+bob,7,6);
        Line(d,"#605A63",6,2,-66+bob,greeting>0?17:swing*11,-50-greeting*21+bob);
        if(greeting>0)Line(d,"#AD9481",4,17,-71+bob,23,-73-greeting*9+bob);
        if(umbrella)
        {
            Line(d,"#A9A1A1",1.7,11,-49,11,-113);
            Poly(d,"#682E42",new(-24,-98),new(-18,-111),new(-4,-119),new(11,-123),new(27,-119),new(40,-109),new(45,-98));
            Line(d,"#BD6876",1,-20,-101,10,-121);Line(d,"#BD6876",1,10,-121,40,-101);
        }
        d.Pop();d.Pop();
    }
    static void WalkerRoute(DrawingContext d,double t,double period,double x1,double y1,double x2,double y2,double scale,bool umbrella,double arch=0)
    {
        double p=Frac(t/period),x=x1+(x2-x1)*p,y=y1+(y2-y1)*p+arch*Math.Sin(p*Math.PI);
        d.PushOpacity(Fade(p));Person(d,x,y,scale,t*Math.Abs(x2-x1)/period/(40*scale),umbrella,x2<x1);d.Pop();
    }
    static void Meet(DrawingContext d,double t)
    {
        double p=Frac(t/36),walk=p<.36?p/.36:p<.61?1:1+(p-.61)/.39;
        double x=860+walk*110,y=705+walk*52,s=.43+walk*.10;
        double phase=(p<.36?t:p<.61?Math.Floor(t/36)*36+12.96:t-9)*.72;
        double hello=p>.38&&p<.59?Math.Sin(Ease((p-.38)/.21)*Math.PI):0;
        d.PushOpacity(Fade(p));Person(d,x,y,s,phase,true,false,hello);
        Person(d,1070,771,.56,hello*.08,false,true,hello);d.Pop();
    }
    static void Train(DrawingContext d,double t)
    {
        // Perspective rail endpoints measured from scene-5. The train stops,
        // then continues towards the camera; it never reverses to fake a loop.
        double cycle=t%54;
        double p=cycle<25?.015+.24*Ease(cycle/25):cycle<34?.255:.255+3*Math.Pow((cycle-34)/20,2);
        for(int car=3;car>=0;car--)
        {
            double q=p-car*.055;if(q<=0)continue;
            double x=1375-800*q,y=650+525*q,w=12+440*q,h=18+405*q;
            double bx=1375-800*Math.Max(.001,q-.05),by=650+525*Math.Max(.001,q-.05);
            double bw=12+440*Math.Max(.001,q-.05),bh=18+405*Math.Max(.001,q-.05);
            Poly(d,"#6F6451",new(x-w/2,y-h),new(x+w/2,y-h),new(bx+bw/2,by-bh),new(bx-bw/2,by-bh));
            Poly(d,"#3A3931",new(x+w/2,y-h),new(bx+bw/2,by-bh),new(bx+bw/2,by),new(x+w/2,y));
            Line(d,"#A18B66",1,x+w/2,y-h,bx+bw/2,by-bh);
            for(int seam=0;seam<3;seam++)Line(d,"#675A40",.6,x+w/2,y-h*(.2+seam*.22),bx+bw/2,by-bh*(.2+seam*.22));
            for(int j=0;j<4;j++)
            {
                double z=(j+.5)/4,wx=x+w/2+(bx+bw/2-x-w/2)*z,wy=y-h+(by-bh-y+h)*z;
                Line(d,"#1C2A2C",Math.Max(2,w*.037),wx,wy+h*.15,wx,wy+h*.43);
                Line(d,"#928365",Math.Max(1,w*.017),wx+.7,wy+h*.19,wx+.7,wy+h*.41);
            }
            if(car!=0)continue;
            d.DrawRoundedRectangle(trainFront,P("#252B29",1.2),new(x-w/2,y-h,w,h*.86),w*.065,w*.065);
            d.DrawRoundedRectangle(trainGlass,P("#333A35",1),new(x-w*.42,y-h*.84,w*.84,h*.35),w*.03,w*.03);
            Poly(d,"#224F635D",new(x-w*.39,y-h*.81),new(x+w*.36,y-h*.81),new(x-w*.23,y-h*.54));
            Line(d,"#B4A280",1,x-w*.4,y-h*.95,x+w*.38,y-h*.95);
            for(int detail=0;detail<5;detail++)Line(d,"#55594B",.7,x-w*.17,y-h*(.43-detail*.018),x+w*.17,y-h*(.43-detail*.018));
            d.DrawRectangle(B("#33392F"),null,new(x-w*.16,y-h*.95,w*.32,h*.06));
            Line(d,"#BDB090",Math.Max(.5,w*.006),x-w*.10,y-h*.915,x+w*.10,y-h*.915);
            Line(d,"#161F21",1,x-w*.29,y-h*.52,x-w*.17,y-h*.64);
            Line(d,"#161F21",1,x+w*.29,y-h*.52,x+w*.17,y-h*.64);
            Line(d,"#A66F43",Math.Max(1,h*.075),x-w*.45,y-h*.33,x+w*.45,y-h*.33);
            Line(d,"#B8B0A1",1,x,y-h*.83,x,y-h*.5);
            Ellipse(d,"#44FFE6B2",x-w*.32,y-h*.22,w*.08,h*.05);Ellipse(d,"#44FFE6B2",x+w*.32,y-h*.22,w*.08,h*.05);
            Ellipse(d,"#FFE6B2",x-w*.32,y-h*.22,w*.035,h*.022);Ellipse(d,"#FFE6B2",x+w*.32,y-h*.22,w*.035,h*.022);
            Line(d,"#181F21",Math.Max(1,w*.045),x,y-h*.13,x,y-h*.035);
            Line(d,"#252A28",Math.Max(2,h*.06),x-w*.43,y-h*.09,x+w*.43,y-h*.09);
        }
        double approach=Ease(Math.Min(cycle,24)/24);
        double wave=cycle>25&&cycle<32?Math.Sin((cycle-25)*1.8)*.22+.7:0;
        d.PushOpacity(Fade(cycle/54));Person(d,1434-approach*34,766-approach*29,.58,cycle<24?cycle*.47:0,false,true,wave);d.Pop();
    }
    static void Car(DrawingContext d,double x,double y,double scale,double t)
    {
        d.PushTransform(new TranslateTransform(x,y));d.PushTransform(new ScaleTransform(scale,scale));
        Ellipse(d,"#45000000",0,4,39,8);
        Poly(d,"#252B3C",new(-34,0),new(-38,-18),new(-21,-33),new(18,-33),new(34,-18),new(37,0));
        Poly(d,"#42586D",new(-23,-20),new(-16,-30),new(15,-30),new(24,-20));
        Line(d,"#BC466E",3,-29,-8,-13,-8);Line(d,"#BC466E",3,12,-8,28,-8);
        for(int i=-1;i<=1;i+=2){Ellipse(d,"#101624",i*29,0,5,8);Line(d,"#454958",1,i*29,0,i*29+Math.Cos(t*12)*3,Math.Sin(t*12)*5);}
        d.PushOpacity(.2);Line(d,"#D97E99",5,-22,12,-24,29);Line(d,"#D97E99",5,22,12,24,29);d.Pop();d.Pop();d.Pop();
    }
    static void Traffic(DrawingContext d,double t,double x,double y,double length,double period)
    {
        for(int i=0;i<5;i++){double p=Frac(t/period+i*.21);d.PushOpacity(Fade(p));Line(d,i%2==0?"#E7B9AB":"#A7CFE3",1.8,x+p*length,y+i%2*7,x+p*length+6,y+i%2*7);d.Pop();}
    }
    static void Birds(DrawingContext d,double t,int count,double x,double y,double length,double height,double scale)
    {
        for(int i=0;i<count;i++)
        {
            double p=Frac(t/(43+i*2)+i*.14),xx=x+p*length,yy=y+Math.Sin(p*4+i)*height;
            double wing=Math.Sin(t*(3+i*.17)+i)*7*scale;
            d.PushOpacity(Fade(p)*.65);Line(d,"#212636",1.8*scale,xx-9*scale,yy-wing,xx,yy);Line(d,"#212636",1.8*scale,xx,yy,xx+9*scale,yy-wing);d.Pop();
        }
    }
    static void Boat(DrawingContext d,double x,double y,double s,double t)
    {
        d.PushTransform(new TranslateTransform(x,y));d.PushTransform(new ScaleTransform(s,s));
        for(int i=0;i<6;i++)Line(d,"#3678BAAB",1,-30-i*12,-1+i*2,-65-i*15,3+i*2);
        Poly(d,"#313F43",new(-34,-5),new(37,-5),new(22,8),new(-22,8));
        Line(d,"#859A9D",2,0,-87,0,0);
        Poly(d,"#ACC6C4",new(2,-85),new(3+Math.Sin(t)*3,-15),new(36,-15));
        Poly(d,"#789CA3",new(-3,-76),new(-29,-17),new(-3,-17));d.Pop();d.Pop();
    }
    static void Ripples(DrawingContext d,double t,Rect area,string color,bool economy)
    {
        int count=economy?9:22;
        for(int i=0;i<count;i++)
        {
            double p=Frac(t*.10+i*.618),x=area.X+Frac(i*.3819)*area.Width,y=area.Y+p*area.Height;
            d.PushOpacity(Math.Sin(p*Math.PI)*.65);
            double radius=(10+p*45)*(1+i%3*.35);
            d.DrawEllipse(null,P(color,.8+p),new(x+Math.Sin(t*.3+i)*8,y),radius,1+p*2.5);d.Pop();
        }
    }
    static void Fish(DrawingContext d,double x,double y,double s,double t,bool koi)
    {
        d.PushTransform(new TranslateTransform(x,y));d.PushTransform(new ScaleTransform(s,s));
        double tail=Math.Sin(t*4)*8;
        Poly(d,koi?"#B7CCC9":"#728E9B",new(-16,0),new(-33,-10+tail),new(-29,tail),new(-33,10+tail));
        Ellipse(d,koi?"#CAD4D3":"#647F8D",0,0,19,6);
        if(koi){Ellipse(d,"#B36F4F",6,-1,6,4);Ellipse(d,"#555B60",-7,1,4,3);}
        Poly(d,"#7799B4B8",new(0,2),new(-6,12+tail*.25),new(8,3));Ellipse(d,"#283542",13,-1,1,1);d.Pop();d.Pop();
    }
    static void Jellyfish(DrawingContext d,double x,double y,double s,double t)
    {
        d.PushTransform(new TranslateTransform(x,y));d.PushTransform(new ScaleTransform(s,s));
        double pulse=1+Math.Sin(t*1.8)*.12;
        d.DrawEllipse(B("#387DCEDC"),P("#789CDBE8",1),new(0,0),30*pulse,19/pulse);
        for(int i=0;i<6;i++)
        {
            double px=-22+i*9;
            for(int j=0;j<7;j++){double yy=10+j*10;Line(d,"#668CCFDB",1,px+Math.Sin(t*1.5+j*.6+i)*6,yy,px+Math.Sin(t*1.5+(j+1)*.6+i)*6,yy+10);}
        }
        d.Pop();d.Pop();
    }
    static void Fox(DrawingContext d,double x,double y,double s,double t)
    {
        d.PushTransform(new TranslateTransform(x,y));d.PushTransform(new ScaleTransform(s,s));
        for(int i=0;i<4;i++){double a=Math.Sin(t*4+i*Math.PI*.7);double bx=-20+i*12;Line(d,i<2?"#66727B":"#A5A49B",4,bx,-24,bx+a*9,-10);Line(d,"#8D9495",3,bx+a*9,-10,bx-a*9,0);}
        Ellipse(d,"#C2BCAB",0,-28,26,11);
        Poly(d,"#B6B5A8",new(-23,-30),new(-54,-44-Math.Sin(t)*5),new(-42,-24),new(-24,-21));
        Ellipse(d,"#D6CAB5",25,-39,11,10);Poly(d,"#D6CAB5",new(17,-45),new(17,-60),new(26,-46));
        Poly(d,"#DBCEB7",new(27,-40),new(43,-35),new(29,-30));Ellipse(d,"#29343B",29,-42,1.5,1.5);d.Pop();d.Pop();
    }
    static void Caravan(DrawingContext d,double t)
    {
        for(int n=0;n<4;n++)
        {
            double p=Frac(t/100+n*.07);double x=300+p*840,y=720-p*112,s=.25+p*.10;
            d.PushOpacity(Fade(p));d.PushTransform(new TranslateTransform(x,y));d.PushTransform(new ScaleTransform(s,s));
            for(int j=0;j<4;j++){double phase=t*.8+j*.5;var f=Foot(phase);double legx=-22+j*15;Line(d,"#3D2D29",5,legx,-40,legx+f.X*.4,-20);Line(d,"#49372D",4,legx+f.X*.4,-20,legx+f.X,f.Y);}
            Ellipse(d,"#49352D",0,-45,35,17);Ellipse(d,"#49352D",-2,-60,15,13);
            Line(d,"#49352D",12,27,-49,38,-82);Ellipse(d,"#49352D",44,-84,13,7);
            Line(d,"#78604A",1,-20,-61,-8,-72);Line(d,"#78604A",1,33,-77,40,-89);
            Poly(d,"#4C3F3C",new(-14,-56),new(8,-56),new(15,-78),new(-5,-81));
            Ellipse(d,"#695048",2,-90,6,7);Line(d,"#52423D",4,5,-72,29,-68);
            d.Pop();d.Pop();d.Pop();
        }
    }
    static void Leaves(DrawingContext d,double t,int count,string color)
    {
        for(int i=0;i<count;i++){double p=Frac(t/27+i*.617),x=Frac(i*.38)*1250+Math.Sin(t*.4+i)*80,y=p*1080;
            d.PushOpacity(Fade(p));d.PushTransform(new RotateTransform(t*27+i*39,x,y));Ellipse(d,color,x,y,2+Math.Abs(Math.Sin(t+i))*3,1.8);d.Pop();d.Pop();}
    }
    static void Space(DrawingContext d,double t,bool economy)
    {
        for(int i=0;i<(economy?5:13);i++)
        {
            double p=Frac(t/(80+i)+i*.617),x=p*1100,y=130+Frac(i*.38)*730,s=3+i%4*3;
            d.PushOpacity(Fade(p)*.8);d.PushTransform(new RotateTransform(t*(2+i%3)+i*30,x,y));
            Poly(d,"#4E4860",new(x-s,y-s*.7),new(x+s*.6,y-s),new(x+s,y+s*.6),new(x-s*.4,y+s));d.Pop();d.Pop();
        }
        double a=Frac(t/100);d.PushOpacity(Fade(a));double xx=200+a*900,yy=260+a*250;
        d.PushTransform(new RotateTransform(t*2,xx,yy));
        d.DrawRectangle(B("#494D6C"),P("#7E809B",1),new(xx-36,yy-9,26,18));
        d.DrawRectangle(B("#494D6C"),P("#7E809B",1),new(xx+10,yy-9,26,18));
        Line(d,"#ACA4AD",3,xx-12,yy,xx+12,yy);Ellipse(d,"#B9AAB8",xx,yy,6,8);d.Pop();d.Pop();
    }
    static void Steam(DrawingContext d,double t,double x,double y,double height)
    {
        for(int i=0;i<9;i++){double p=Frac(t*.17+i*.11);d.PushOpacity(Math.Sin(p*Math.PI)*.1);Ellipse(d,"#D4C5BE",x+Math.Sin(t+p*5)*8,y-p*height,3+p*7,5+p*8);d.Pop();}
    }
    static void Glass(DrawingContext d,double t,bool economy)
    {
        for(int i=0;i<5;i++)
        {
            double a=t*.1+i*1.67,x=1020+Math.Cos(a)*220,y=600+Math.Sin(a*1.2)*120;
            d.PushOpacity(.35+.25*(Math.Sin(a)+1)/2);d.PushTransform(new RotateTransform(a*180/Math.PI,x,y));
            var petal=new StreamGeometry();using(var c=petal.Open())
            {c.BeginFigure(new(x-37,y),true,true);c.BezierTo(new(x-10,y-32),new(x+31,y-28),new(x+63,y-6),true,false);c.BezierTo(new(x+25,y+10),new(x-6,y+19),new(x-37,y),true,false);}
            petal.Freeze();d.DrawGeometry(glass,P("#668CA8CF",.7),petal);
            Line(d,"#88D1DCF3",.6,x-29,y,x+51,y-5);d.Pop();d.Pop();
        }
        for(int i=0;i<4;i++){double p=Frac(t*.3+i*.25);Ellipse(d,"#98B2CAE6",570+i*87,690+p*p*200,2,4);}
        Ripples(d,t,new Rect(360,900,790,100),"#448B9EBF",economy);
    }
}
