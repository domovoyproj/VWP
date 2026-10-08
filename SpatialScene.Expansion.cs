using System;
using System.Windows.Media.Media3D;
namespace VWP;

public sealed partial class SpatialScene
{
    void BuildExpansion(int id)
    {
        switch(id){case 18:OrbitalGarden();break;case 19:Harbor();break;case 20:Cafe();break;case 21:Alpine();break;case 22:Festival();break;case 23:Metro();break;case 24:Observatory();break;case 25:Workshop();break;case 26:Research();break;case 27:Basketball();break;}
    }
    void OrbitalGarden()
    {
        Floor("#547679");Planet(-5,5,-8,3,"#567CA0","#B8C4B7",5);
        foreach(double x in new[]{-6.0,-2,2,6}){world.Box("#C1C6C3",x,.3,-1,2.5,.6,5);for(int k=0;k<4;k++){var leaf=Node(x,1,-3+k);leaf.Sphere("#7DB5A0",0,0,0,.5,.75,.3);int j=k;motions.Add(t=>leaf.Roll.Angle=Math.Sin(t+j)*5);}}
        var person=Person("#D0C7AE",0,3,1.25);var can=new SpatialNode(person.RightHand.Model,0,-.2,.1);can.Sphere("#C9987E",0,0,0,.18,.2,.18);
        motions.Add(t=>{person.Pose(t);person.Root.Yaw.Angle=-40+Math.Sin(t*.4)*25;person.RightArm.Pitch.Angle=-70;can.Roll.Angle=20+Math.Sin(t*.9)*15;});
        var drone=Node();drone.Sphere("#ADC3D0",0,0,0,.5,.15,.4);drone.Sphere("#EDD09B",0,-.1,.3,.1,.1,.1,true);
        motions.Add(t=>drone.Move(Math.Sin(t*.35)*5,3+Math.Sin(t*.8)*.25,Math.Cos(t*.35)*2));Particles("#BBD7D3",20,false);
    }
    void Harbor()
    {
        Water("#527F99");world.Box("#A3A69C",-5,.3,0,6,.6,12);City();
        var ferry=Node();ferry.Sphere("#68839D",0,.3,0,2.8,.6,1);ferry.Box("#DBDCD0",0,1.1,0,3.2,1,.95);for(int j=0;j<6;j++)ferry.Box("#7295A6",-1.25+j*.5,1.2,.5,.3,.4,.03);ferry.Box("#C9AA89",-1,1.9,0,.4,.6,.4);
        motions.Add(t=>{ferry.Move(2+Math.Sin(t*.18)*2,.05+Math.Sin(t)*.08,Math.Cos(t*.18)*3);ferry.Yaw.Angle=Math.Sin(t*.18)*15;});
        world.Box("#E0B487",-5,2.8,-2,.25,5.6,.25);var crane=Node(-5,5.4,-2);crane.Box("#D4AD88",1.2,0,0,3,.2,.2);var crate=new SpatialNode(crane.Model,2,-2,0);crate.Box("#AE7E68",0,0,0,.85,.85,.85);crate.Box("#BFC4C3",0,1,0,.025,2,.025);motions.Add(t=>{crane.Yaw.Angle=Math.Sin(t*.3)*55;crate.Position.OffsetY=-1.8+Math.Sin(t*.6)*.55;});Walk(Person("#F0C688",-6,2),-5,2,1.5,18);
    }
    void Cafe()
    {
        Floor("#A38A7D");world.Box("#5A5365",0,2.2,-4,15,4.4,.2);world.Box("#CCA27B",0,1.2,-1,6,.3,1.4);
        var barista=Person("#C4C8BE",-1,-2,1.15);var guest=Person("#B58E9D",1,1,1.15);guest.Root.Yaw.Angle=180;
        var pot=new SpatialNode(barista.RightHand.Model,0,-.2,0);pot.Sphere("#BFC5C7",0,0,0,.13,.18,.13);world.Sphere("#E3D0B7",-.2,1.45,-.9,.14,.16,.14);
        motions.Add(t=>{barista.Pose(t);guest.Pose(t);barista.RightArm.Pitch.Angle=-75+Math.Sin(t*.8)*10;barista.RightHand.Pitch.Angle=-40;pot.Roll.Angle=30+Math.Sin(t)*15;guest.LeftArm.Pitch.Angle=-25-Math.Sin(t*.6)*20;});
        world.Sphere("#E2D2B6",3,3,-3.8,.7,.7,.06);var hand=Node(3,3,-3.7);hand.Box("#505165",0,.25,0,.05,.5,.03);motions.Add(t=>hand.Roll.Angle=-t*18);
        for(int i=0;i<3;i++){world.Box("#796B74",-5+i*5,.8,3,1.3,.12,1.3);world.Box("#65717B",-5+i*5,.4,3,.15,.8,.15);}Viewport.Camera=new PerspectiveCamera(new(10,7,15),new(-10,-5,-15),new(0,1,0),44);
    }
    void Alpine()
    {
        Floor("#859DA6",-1);Mountains("#B6C6CC");for(int i=0;i<5;i++)Pine(-7+i*3.5,-4,1.7);
        world.Box("#AAB2B6",0,1.5,0,18,.5,2);for(int i=0;i<7;i++)world.Box("#A3AFB4",-8+i*2.7,.4,0,.55,2.2,.8);
        var train=Train(0);train.Position.OffsetY=1.9;motions.Add(t=>train.Position.OffsetX=-18+Cycle(t,22)*35);
        world.Box("#657785",0,6,1,18,.05,.05);var cable=Node();cable.Box("#CCB6A2",0,0,0,1,.8,.8);cable.Box("#79A0B4",0,.1,.41,.7,.4,.03);cable.Box("#576D7F",0,.75,0,.07,1,.07);motions.Add(t=>cable.Move(Math.Sin(t*.2)*7,4.8,1));Particles("#E6EDF0",25,true);
    }
    void Festival()
    {
        Water("#354C71");world.Box("#8F7C87",0,.1,3,17,.3,3);House(-5,-5,"#BB8195");House(1,-5,"#A690AA");Tree(6,-4,"#C293B8");
        for(int i=0;i<16;i++){int j=i;var lantern=Node();lantern.Sphere(i%2==0?"#E9AD87":"#D7B2C9",0,0,0,.22,.3,.22,true);motions.Add(t=>lantern.Move(-7+j%8*2,2+(j/8)*2+Math.Sin(t*.5+j)*.5,-3+j%3));}
        var boat=Node();boat.Sphere("#897086",0,0,0,1.4,.25,.5);var sailor=new SpatialPerson(boat.Model,"#C6B2A0","#383F55",0,.1,0,.6);motions.Add(t=>{boat.Move(Math.Sin(t*.3)*5,.05,0);boat.Roll.Angle=Math.Sin(t)*3;sailor.Pose(t);sailor.RightArm.Pitch.Angle=-60+Math.Sin(t*2)*25;});Walk(Person("#BEB4D3",0,3,1.1),0,3,4,26);
    }
    void Metro()
    {
        Floor("#526774");world.Box("#638392",0,2,-5,19,4,.3);for(int i=0;i<6;i++){world.Box("#CADBDD",-7+i*3,3.6,-4.75,1.5,.1,.06,true);world.Box("#415966",-7+i*3,1.7,-4.5,.3,3.4,.4);}Rail(-1.5);
        world.Box("#9AADB4",0,.15,2,18,.3,4);world.Box("#D4C09C",0,.31,.3,18,.03,.18);
        var train=Train(-1.5);var a=Person("#AFCCCD",-2,2,1.15);var b=Person("#D7B09D",3,2,1.15);
        motions.Add(t=>{double p=t%24;train.Position.OffsetX=p<8?-16+15*Smooth(p/8):p<16?-1:-1+23*Smooth((p-16)/8);double move=Math.Sin(t*Math.PI/12);a.Root.Move(-2+move,0,2-Math.Abs(move)*.5);a.Root.Yaw.Angle=120;a.Pose(t,Math.Abs(move));b.Pose(t);b.RightArm.Pitch.Angle=-45;});
    }
    void Observatory()
    {
        Floor("#586077");Mountains("#3C465C");world.Sphere("#A6ACBC",-4,1.5,-3,2,1.5,2);world.Box("#C1BCC1",-4,.8,-3,4,1.6,4);
        var telescope=Node(1,1,1);telescope.Box("#7E8E9D",0,-.5,0,.25,1,.25);var barrel=new SpatialNode(telescope.Model,0,.6,0);barrel.Sphere("#BABDC6",0,.2,0,.45,.45,1.8);barrel.Sphere("#6EA2C5",0,.2,1.7,.35,.35,.12,true);
        motions.Add(t=>{telescope.Yaw.Angle=20+Math.Sin(t*.2)*40;barrel.Pitch.Angle=-20+Math.Sin(t*.3)*12;});var astronomer=Person("#B8A6B8",2,2,1.2);motions.Add(t=>{astronomer.Pose(t);astronomer.LeftArm.Pitch.Angle=-60;astronomer.Head.Yaw.Angle=-35+Math.Sin(t*.4)*20;});Planet(-5,6,-9,1.5,"#C9C2C9","#9EABBE",10);Particles("#D8DCE8",30,false);
    }
    void Workshop()
    {
        Floor("#4D626B");world.Box("#3A505E",0,2,-4,16,4,.4);world.Box("#78908D",0,.7,0,14,.4,2);
        for(int i=0;i<9;i++){int j=i;var block=Node();block.Box("#B99B79",0,0,0,.65,.55,.7);motions.Add(t=>block.Move(-6+Cycle(t+j,9)*12,1.15,0));}
        var baseNode=Node(0,1,-2);baseNode.Box("#E0BC85",0,0,0,.8,.8,.8);var shoulder=new SpatialNode(baseNode.Model,0,.4,0);shoulder.Box("#D9B582",0,.9,0,.35,1.8,.35);var elbow=new SpatialNode(shoulder.Model,0,1.8,0);elbow.Box("#B7BEC1",0,.7,0,.25,1.4,.25);elbow.Sphere("#C7D3CB",0,1.5,0,.35,.15,.3);
        motions.Add(t=>{baseNode.Yaw.Angle=Math.Sin(t*.6)*45;shoulder.Pitch.Angle=25+Math.Sin(t*.8)*25;elbow.Pitch.Angle=50+Math.Cos(t*.8)*25;});var technician=Person("#8DB1B8",4,2,1.2);motions.Add(t=>{technician.Pose(t);technician.LeftArm.Pitch.Angle=-60;});
    }
    void Research()
    {
        Floor("#467886");world.Sphere("#A6BBC4",-3,.8,-2,2.5,1.2,1.8);world.Box("#48697E",-3,.8,-.2,2,.7,.05);for(int i=0;i<10;i++){double x=-7+i*1.5;world.Sphere(i%2==0?"#AC9CB6":"#92B8AD",x,.4,-4,.25,.7,.25);}
        var sub=Node();sub.Sphere("#D2B798",0,0,0,1.5,.65,.65);sub.Sphere("#7FB3C9",1.2,0,0,.45,.4,.4);var rotor=new SpatialNode(sub.Model,-1.5,0,0);rotor.Box("#8DA7AD",0,0,0,.1,1.2,.13);motions.Add(t=>{sub.Move(3+Math.Sin(t*.4)*2,2+Math.Sin(t*.6)*.3,0);rotor.Pitch.Angle=t*320;});
        var diver=Person("#809CC0",0,2,1.1);motions.Add(t=>{diver.Pose(t,.5);diver.Root.Move(Math.Sin(t*.25)*2,2.5+Math.Cos(t*.4)*.3,2);diver.Root.Roll.Angle=-70;diver.LeftArm.Pitch.Angle=Math.Sin(t*2)*60;});Fish(6,false);Particles("#A4D3DC",24,false);
    }
    void Basketball()
    {
        Floor("#967A89");City();world.Box("#BA9695",0,.02,1,12,.04,8);foreach(double x in new[]{-5.7,5.7})world.Box("#E6CBB6",x,.05,1,.05,.025,7.5);world.Box("#E6CBB6",0,.05,1,.04,.025,7.5);
        world.Box("#7B8C9B",4,1.8,-2,.13,3.6,.13);world.Box("#B4C7CA",4,3.5,-2,1.7,1,.08);world.Box("#D6AD90",4,3.3,-1.6,.65,.05,.5);
        var a=Person("#CC9F85",-3,2,1.3);var b=Person("#8DAFBB",2,2,1.3);a.Root.Yaw.Angle=90;b.Root.Yaw.Angle=-90;var ball=Node();ball.Sphere("#D8A77C",0,0,0,.23,.23,.23);ball.Box("#896663",0,0,.225,.025,.35,.025);
        motions.Add(t=>{double q=Cycle(t,8);a.Pose(t);b.Pose(t);double u=q<.5?q*2:(q-.5)*2;ball.Move(q<.5?-3+u*5:2-u*5,1.3+Math.Sin(u*Math.PI)*1.8,2);ball.Roll.Angle=t*150;a.RightArm.Pitch.Angle=-50-45*Math.Max(0,Math.Sin(t*Math.PI/4));b.LeftArm.Pitch.Angle=-50+45*Math.Min(0,Math.Sin(t*Math.PI/4));});
    }
}
