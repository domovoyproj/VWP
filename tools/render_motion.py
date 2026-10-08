"""Fifteen original, deterministic vector animations. Native-resolution H.264 loops.

No downloaded artwork, credentials or generative service is used. Coordinates are
in a 960x540 design space; drawing primitives scale to the requested pixel size.
"""
import argparse
from functools import lru_cache
import hashlib
import json
import math
from pathlib import Path
import random
import subprocess
import numpy as np
from PIL import Image, ImageDraw
import imageio_ffmpeg

ROOT=Path(__file__).resolve().parents[1]
TAU=math.tau
PALETTES=[('#101d37','#40506a'),('#071c32','#35656e'),('#253655','#e6a78f'),('#161c30','#46313d'),('#08152b','#263c60'),('#153e48','#487a78'),('#0b142e','#3b3760'),('#132c34','#376566'),('#191d42','#aa6684'),('#181d38','#4b3760'),('#253849','#739397'),('#283353','#927687'),('#54678a','#d7b1ac'),('#0d1f3a','#3e4d6a'),('#082239','#245b71')]

def rgb(value):return tuple(int(value[i:i+2],16) for i in (1,3,5))
def mix(a,b,u):return tuple(round(x+(y-x)*u) for x,y in zip(rgb(a),rgb(b)))
@lru_cache(maxsize=4)
def background(index,w,h):
    top,bottom=map(rgb,PALETTES[index]);t=np.linspace(0,1,h)[:,None,None]
    row=np.asarray(top)[None,None,:]*(1-t)+np.asarray(bottom)[None,None,:]*t
    return Image.fromarray(np.repeat(row.astype('uint8'),w,axis=1))

@lru_cache(maxsize=64)
def light_sprite(radius,color):
    axis=np.linspace(-1,1,radius*2+1,dtype=np.float32);xx,yy=np.meshgrid(axis,axis)
    alpha=(np.exp(-(xx*xx+yy*yy)*7)*150).astype('uint8')
    sprite=Image.new('RGB',(len(axis),len(axis)),color);return sprite,Image.fromarray(alpha)

class Canvas:
    def __init__(self,index,w,h):self.image=background(index,w,h).copy();self.d=ImageDraw.Draw(self.image);self.sx=w/960;self.sy=h/540
    def point(self,p):return p[0]*self.sx,p[1]*self.sy
    def poly(self,pts,c):self.d.polygon([self.point(p) for p in pts],fill=c)
    def line(self,pts,c,width=1):self.d.line([self.point(p) for p in pts],fill=c,width=max(1,round(width*self.sx)),joint='curve')
    def ellipse(self,x,y,rx,ry,c):self.d.ellipse(((x-rx)*self.sx,(y-ry)*self.sy,(x+rx)*self.sx,(y+ry)*self.sy),fill=c)
    def glow(self,x,y,r,c):
        radius=max(2,round(r*self.sx*3));sprite,mask=light_sprite(radius,c)
        self.image.paste(sprite,(round(x*self.sx-radius),round(y*self.sy-radius)),mask)
    def wave(self,y,amplitude,p,c,speed=1):
        pts=[(x,y+math.sin(x*.011+p*speed)*amplitude+math.sin(x*.025-p)*amplitude*.3) for x in range(-10,981,10)]
        self.poly(pts+[(980,550),(-10,550)],c)
    def mountain(self,y,c,p=0):self.poly([(0,550),(0,y)]+[(x,y-abs(math.sin(x*.013+p))*100) for x in range(0,981,20)]+[(980,550)],c)

def frame(scene,u,width=3840,height=2160):
    i=scene-100
    if not 0<=i<15:raise ValueError('Scene id must be 100..114')
    p=TAU*(u%1);c=Canvas(i,width,height);rng=random.Random(912+i)
    def stars(n=80):
        for j in range(n):
            x,y=rng.random()*960,rng.random()*380;r=.5+(j%3)*.3
            c.ellipse(x,y,r,r,mix('#516080','#d1d9e5',(.5+.5*math.sin(p+j))*.7))
    def fish(x,y,a,size,color,phase):
        def at(dx,dy):return x+math.cos(a)*dx-math.sin(a)*dy,y+math.sin(a)*dx+math.cos(a)*dy
        body=[at(math.cos(q*TAU/24)*size,math.sin(q*TAU/24)*size*.36) for q in range(24)]
        c.poly(body,color);wag=math.sin(phase)*size*.18
        c.poly([at(-size*.7,0),at(-size*1.6,-size*.55+wag),at(-size*1.4,size*.55+wag)],color)
        ex,ey=at(size*.55,-size*.1);c.ellipse(ex,ey,2,2,'#192c3d')
    if i==0:
        for j in range(70):c.glow(rng.random()*960,160+rng.random()*320,3+j%7,['#6db6b9','#e9b78e','#8a98c5'][j%3])
        for j in range(85):
            x=rng.random()*960;y=(rng.random()*650+u*650*(1+j%3))%650-50
            c.line([(x-2,y-18),(x,y),(x-1,y+28)],'#92b8c4',1.3);c.ellipse(x,y,2,4,'#c5d6da')
        c.poly([(0,500),(960,500),(960,540),(0,540)],'#1e2f46')
    elif i==1:
        stars()
        for band in range(5):
            pts=[(x,120+band*18+math.sin(x*.009+p+band*.3)*45) for x in range(0,981,8)]
            for off in range(12,0,-1):c.line([(x,y+off*3) for x,y in pts],mix('#23495b','#86c6b2',1-off/15),3)
        c.mountain(475,'#172e43');c.mountain(530,'#102536',1)
    elif i==2:
        c.glow(600,210,23,'#f6cfaa');c.ellipse(600,210,38,38,'#f5d2aa')
        for k in range(20):c.wave(255+k*16,3+k*.6,p+k*.2,mix('#637a96','#253b59',k/20),1+k%3)
        for j in range(50):
            y=270+j*5;x=600+math.sin(p+j*.6)*(10+j*.8)
            c.line([(x-5-j*.5,y),(x+5+j*.5,y)],mix('#657d91','#e8c0a3',.5+.4*math.sin(p+j)),1.3)
    elif i==3:
        c.glow(480,370,65,'#b57c62');c.ellipse(480,470,260,28,'#171e2b')
        for k in range(22):
            x=285+k*18;h=70+55*math.sin(p+k*.35)**2;pts=[(x-24,460),(x-9,390),(x+math.sin(p+k)*24,460-h),(x+18,400),(x+27,460)]
            c.poly(pts,['#bb745d','#dd9768','#efb67f'][k%3])
        for j in range(38):
            v=(rng.random()+u*(1+j%2))%1;x=330+rng.random()*300+math.sin(p+j)*35;y=460-v*330
            c.ellipse(x,y,1+2*(1-v),2+2*(1-v),'#e9b17c')
        for x in range(270,700,110):c.line([(x,460),(x+125,480)],'#51404a',18)
    elif i==4:
        for j in range(230):
            a=rng.random()*TAU;v=(rng.random()+u*(1+j%2))%1;r=20+v*v*640
            x,y=480+math.cos(a)*r,270+math.sin(a)*r*.66
            c.line([(x,y),(480+math.cos(a)*(r+3+v*35),270+math.sin(a)*(r+3+v*35)*.66)],mix('#314969','#d5dfe9',v),1+v)
    elif i==5:
        for j in range(22):
            x,y=rng.random()*960,rng.random()*540;c.ellipse(x,y,30+j%15,8+j%5,'#3c6769')
        for j in range(8):
            a=p+j*TAU/8;x=480+math.cos(a)*(230+j*10);y=280+math.sin(a)*(125+j*6)
            fish(x,y,math.atan2(math.cos(a)*(125+j*6),-math.sin(a)*(230+j*10)),25+j%3*5,['#e6d4b8','#c59179','#aebfc1'][j%3],p*4+j)
        for j in range(7):
            a=p+j;pts=[(480+math.cos(q*TAU/80)*((j+1)*35+math.sin(a)*4),270+math.sin(q*TAU/80)*(j+1)*15) for q in range(81)];c.line(pts,'#608e8d',.7)
    elif i==6:
        stars(180);c.ellipse(780,100,30,30,'#c5c5d4');c.mountain(500,'#252d48');c.mountain(540,'#171e35',2)
        for j in range(7):
            v=(u+j/7)%1;x=-200+v*1400;y=-100+j*30+v*450
            c.line([(x-100,y-32),(x,y)],'#8196b1',1);c.line([(x-25,y-8),(x,y)],'#dae0e3',2)
    elif i==7:
        c.ellipse(720,120,25,25,'#bfcbbd')
        for j in range(180):
            x=j*5.5;h=40+rng.random()*95;lean=math.sin(p+j*.23)*13
            c.line([(x,540),(x+lean*.5,540-h*.6),(x+lean,540-h)],['#1b3f46','#315953','#476d5d'][j%3],2)
        for j in range(55):
            x=rng.random()*960+math.sin(p+j)*20;y=150+rng.random()*300+math.cos(p+j*.6)*18
            c.glow(x,y,1.8+1.3*math.sin(p*2+j)**2,'#cbdba4')
    elif i==8:
        stars();c.ellipse(480,245,110,80,'#c48c9f')
        for j in range(10):c.line([(360,210+j*12),(600,210+j*12)],'#7c5c83',4)
        c.poly([(380,270),(580,270),(1030,540),(-70,540)],'#1b2948')
        for j in range(15):
            v=(j/15+u)%1;y=270+v*v*285;c.line([(380-v*490,y),(580+v*490,y)],'#947ca9',1.2)
        for x in range(-4,5):c.line([(480+x*20,270),(480+x*210,540)],'#61799f',1.4)
        for j in range(18):
            v=(j/18+u)%1;y=280+v*v*300
            for side in (-1,1):c.line([(480+side*(125+v*410),y),(480+side*(125+v*410),y-15-v*75)],'#aacbd2',1.4+v*3)
    elif i==9:
        for j in range(17):
            a=p+j*2.4;x=480+math.sin(a)*(140+j*8);y=270+math.cos(p*(1+j%2)+j)*180;r=20+j%5*15
            for k in range(7,0,-1):c.ellipse(x,y,r*k/7,r*1.6*k/7,mix('#393953',['#b7a6c9','#aabed3','#dbb7ba'][j%3],1-k/9))
    elif i==10:
        for j in range(27):
            x=j*38-20;h=90+rng.random()*170
            for n in range(3):c.poly([(x,340-h+n*45),(x-35-n*14,430+n*20),(x+35+n*14,430+n*20)],['#456477','#3c5c6b','#345262'][n])
        c.wave(480,12,0,'#a5bdc3',0)
        for j in range(160):
            x=(rng.random()*1100+u*1100)%1100-70;y=(rng.random()*640+u*640*(1+j%3))%640-50
            c.ellipse(x,y,.7+j%3*.6,.7+j%3*.6,'#dce5e5')
    elif i==11:
        c.ellipse(700,150,45,45,'#c2b6c7');c.mountain(350,'#4a4567');
        for k in range(13):c.wave(365+k*15,4,p+k*.6,mix('#6e708f','#343957',k/13))
        c.line([(0,80),(190,120),(350,45),(450,25)],'#3b324b',15)
        for j in range(95):
            x=(rng.random()*1100+u*1100)%1100-70;y=(rng.random()*650+u*650)%650-50
            a=p*2+j;pts=[(x+math.cos(a+k*TAU/5)*(3+(k%2)*3),y+math.sin(a+k*TAU/5)*4) for k in range(5)];c.poly(pts,['#cea6c2','#e0b8ca','#aa8caf'][j%3])
    elif i==12:
        c.ellipse(750,100,40,40,'#ead0b9')
        for layer in range(4):
            for j in range(9):
                x=((j*150+u*1350*(1+layer%2))%1350)-190;y=300+layer*55+math.sin(p+j)*6
                c.ellipse(x,y,125,35,['#b4a5bb','#c6b5c4','#d3c4cf','#ded3d7'][layer])
        for j in range(3):
            x=220+j*260+math.sin(p+j)*35;y=170+j*35+math.cos(p+j)*15;col=['#c1909f','#8ca8b5','#d2b396'][j]
            c.ellipse(x,y,37,47,col);c.line([(x-23,y+35),(x-10,y+70),(x+10,y+70),(x+23,y+35)],'#645d72',1);c.poly([(x-13,y+68),(x+13,y+68),(x+10,y+80),(x-10,y+80)],'#967d78')
    elif i==13:
        stars(130);c.ellipse(480,270,110,110,'#7e8db2')
        for j in range(12):
            a=p+j*.6;x=480+math.sin(a)*95;y=185+j*14;r=6+math.cos(a)*4
            if math.cos(a)>0:c.ellipse(x,y,r*2,r,'#b1bac9')
        for j in range(4):
            a=p*(1+j%2)+j*1.5;rx=170+j*55;ry=55+j*30
            pts=[(480+math.cos(q*TAU/100)*rx,270+math.sin(q*TAU/100)*ry) for q in range(101)];c.line(pts,'#506783',1)
            x,y=480+math.cos(a)*rx,270+math.sin(a)*ry;c.ellipse(x,y,8+j*2,8+j*2,['#d3b7a4','#99bfbe','#c5b5cb','#c1c5b5'][j])
    elif i==14:
        for j in range(9):
            x=80+j%5*185+math.sin(p+j)*22;y=130+j//5*205+math.cos(p+j)*35;r=25+j%3*7;pulse=1+.12*math.sin(p*3+j)
            for k in range(6):
                pts=[(x+(k-2.5)*r*.22+math.sin(p*3+j+q*.13)*q*.08,y+q) for q in range(5,115,4)];c.line(pts,'#8db7c5',1.3)
            c.ellipse(x,y,r*pulse,r*.6,mix('#517c94','#bed0d8',.55+.15*math.sin(p*3+j)))
        for j in range(50):
            x=rng.random()*960;y=(rng.random()*600-u*600)%600-30;c.ellipse(x,y,1,1,'#73a8b9')
    return c.image

def encode(scene,output,width,height,fps,seconds):
    output.mkdir(parents=True,exist_ok=True);ffmpeg=imageio_ffmpeg.get_ffmpeg_exe();target=output/f'{scene}.mp4';temp=output/f'{scene}.tmp.mp4'
    cmd=[ffmpeg,'-v','error','-y','-f','rawvideo','-pix_fmt','rgb24','-s',f'{width}x{height}','-r',str(fps),'-i','-','-an','-c:v','libx264','-preset','fast','-crf','20','-threads','2','-pix_fmt','yuv420p','-movflags','+faststart',str(temp)]
    proc=subprocess.Popen(cmd,stdin=subprocess.PIPE)
    try:
        for index in range(fps*seconds):proc.stdin.write(frame(scene,index/(fps*seconds),width,height).tobytes())
        proc.stdin.close()
        if proc.wait()!=0:raise RuntimeError('FFmpeg encoding failed')
    except BaseException:
        proc.kill();proc.wait();raise
    temp.replace(target);cover=frame(scene,.25,width,height);cover.save(output/f'{scene}.cover.jpg',quality=94)
    cover.resize((1280,720),Image.Resampling.LANCZOS).save(output/f'{scene}.jpg',quality=92)
    subprocess.run([ffmpeg,'-v','error','-y','-i',str(target),'-vf','scale=1280:720,fps=30','-an','-c:v','libx264','-preset','fast','-crf','23','-threads','2','-movflags','+faststart',str(output/f'{scene}.preview.mp4')],check=True)
    result=subprocess.run([ffmpeg,'-v','error','-threads','2','-i',str(target),'-f','framemd5','-'],capture_output=True,check=True,text=True)
    hashes=[line.split(',')[-1].strip() for line in result.stdout.splitlines() if line and not line.startswith('#')]
    assert len(hashes)==fps*seconds and all(a!=b for a,b in zip(hashes,hashes[1:])),f'Frozen/missing frames: {scene}'
    # Do not wrap u before this check: that would hide discontinuous translations.
    # A subpixel rasterization tolerance covers floating-point endpoints of diagonal lines.
    endpoint_delta=float(np.abs(np.asarray(frame(scene,0,960,540),dtype=np.int16)-np.asarray(frame(scene,1,960,540),dtype=np.int16)).mean())
    assert endpoint_delta<.025,f'Loop endpoint is not periodic: {scene}, delta {endpoint_delta}'
    report={'id':scene,'width':width,'height':height,'fps':fps,'seconds':seconds,'frames':len(hashes),'uniqueAdjacentFrames':True,'periodic':True,'endpointMeanDifference':endpoint_delta,'sha256':hashlib.sha256(target.read_bytes()).hexdigest()}
    (output/f'{scene}.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report),flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--id',type=int,required=True);parser.add_argument('--output',type=Path,default=ROOT/'assets/motion');parser.add_argument('--width',type=int,default=3840);parser.add_argument('--height',type=int,default=2160);parser.add_argument('--fps',type=int,default=60);parser.add_argument('--seconds',type=int,default=12);parser.add_argument('--poster',action='store_true');args=parser.parse_args()
    if args.poster:args.output.mkdir(parents=True,exist_ok=True);frame(args.id,.25,args.width,args.height).save(args.output/f'{args.id}.jpg',quality=94)
    else:encode(args.id,args.output,args.width,args.height,args.fps,args.seconds)
