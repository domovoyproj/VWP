"""Render 60 individually art-directed desktop scenes and six importable bundles.

Only environment effects move: snow, rain, water, mist, energy, sparks and light.
No character or animal body animation is claimed. Every motion is periodic.
"""
import argparse
from concurrent.futures import ProcessPoolExecutor
import hashlib
import json
import math
from pathlib import Path
import random
import shutil
import subprocess
import time
import zipfile
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import imageio_ffmpeg

ROOT=Path(__file__).resolve().parents[1]
PLAN=ROOT/'tools'/'collections-plan.json'
OUT=ROOT.parent/'VWP-60-Live-Wallpapers'
TAU=math.tau
FPS=30
SECONDS=8
SIZE=(1920,1080)


def prepare():
    groups=json.loads(PLAN.read_text(encoding='utf-8'))
    for group in groups:
        folder=ROOT/'assets'/group['folder'];folder.mkdir(parents=True,exist_ok=True)
        (folder/'catalog.json').write_text(json.dumps(group['entries'],ensure_ascii=False,indent=2),encoding='utf-8')
    return groups


class SceneRenderer:
    def __init__(self,scene,size=SIZE):
        self.scene=scene;self.size=size
        self.base=Image.open(ROOT/'assets'/scene['folder']/f"{scene['id']}.png").convert('RGBA').resize(size,Image.Resampling.LANCZOS)
        self.w,self.h=960,540
        self.color=tuple(bytes.fromhex(scene['accent'].lstrip('#')))
        rng=random.Random(scene['id'])
        self.particles=[(rng.random(),rng.random(),rng.random(),rng.uniform(0,TAU),rng.random()) for _ in range(220)]
        self.bolts=[(rng.uniform(0,TAU),rng.uniform(0,TAU),rng.uniform(70,150)) for _ in range(11)]

    def deform(self,phase):
        scene=self.scene;effect=scene['effect'];line=scene.get('waterline',.58)
        if effect not in ('water','abstract','bubbles','aurora') and not scene.get('waterline'):
            return self.base
        w,h=self.size
        box=scene.get('waterRect',[0,line,1,1]) if (effect=='water' or scene.get('waterline')) else ([0,0,1,.40] if effect=='aurora' else [0,0,1,1])
        left,top,right,bottom=map(int,(box[0]*w,box[1]*h,box[2]*w,box[3]*h))
        mesh=[]
        for y in range(top,bottom,48):
            y2=min(bottom,y+48)
            for x in range(left,right,96):
                x2=min(right,x+96)
                def point(px,py):
                    fade=max(0,min(1,(py-top)/max(1,bottom-top)))
                    fade*=min(1,(px-left)/max(1,w*.05),(right-px)/max(1,w*.05))
                    strength=3 if effect=='water' else 1.4
                    dx=math.sin(py/31+phase*2)*strength*fade
                    dy=math.sin(px/84+phase)*1.4*fade
                    return px+dx,py+dy
                mesh.append(((x,y,x2,y2),point(x,y)+point(x,y2)+point(x2,y2)+point(x2,y)))
        warped=self.base.transform(self.size,Image.Transform.MESH,mesh,Image.Resampling.BICUBIC)
        result=self.base.copy();result.paste(warped.crop((left,top,right,bottom)),(left,top))
        return result

    def frame(self,phase):
        phase=phase%TAU
        s=self.scene;effect=s['effect'];w,h=self.w,self.h;color=self.color
        sharp=Image.new('RGBA',(w,h));soft=Image.new('RGBA',(w,h))
        d=ImageDraw.Draw(sharp);g=ImageDraw.Draw(soft)
        if effect in ('lightning','energy'):
            anchors=[(.06,.22),(.21,.43),(.08,.77),(.27,.88),(.39,.72),(.86,.18),(.94,.40),(.90,.66),(.94,.87),(.57,.92),(.77,.90)]
            for i,((ax,ay),(seed,angle,length)) in enumerate(zip(anchors,self.bolts)):
                pulse=(.5+.5*math.sin(phase*(2+i%3)+seed))**4
                a=int(24+210*pulse);points=[]
                for j in range(29):
                    u=j/28;bend=math.sin(u*23+phase*3+seed)*5+math.sin(u*51-phase*5+seed)*2.6
                    points.append((ax*w+math.cos(angle)*length*(u-.5)-math.sin(angle)*bend,ay*h+math.sin(angle)*length*(u-.5)+math.cos(angle)*bend))
                g.line(points,fill=color+(a,),width=8)
                d.line(points,fill=color+(a,),width=3)
                d.line(points,fill=(239,253,255,a),width=1)
                p=points[17];d.line([p,(p[0]+12,p[1]-14),(p[0]+21,p[1]-13),(p[0]+28,p[1]-29)],fill=color+(a//2,),width=1)
            if effect=='energy':
                for i in range(3):
                    cx,cy=w*.33,h*.59;rx=105+15*math.sin(phase+i);ry=42+i*15
                    pts=[(cx+rx*math.cos(j/100*TAU),cy+ry*math.sin(j/100*TAU)) for j in range(101)]
                    g.line(pts,fill=color+(25,),width=5);d.line(pts,fill=color+(45,),width=1)
        if effect=='aurora':
            for band in range(3):
                points=[]
                for x in range(-20,w+21,6):
                    y=h*.16+band*23+math.sin(x/120+phase+band)*27+math.sin(x/69-phase*2)*9
                    points.append((x,y))
                g.line(points,fill=color+(45,),width=30)
        if effect in ('mist','abstract','bubbles'):
            for i in range(5):
                cx=w*(.15+i*.21)+math.sin(phase+i)*25;cy=h*(.70 if effect=='mist' else .35)+math.cos(phase+i)*12
                g.ellipse((cx-165,cy-28,cx+165,cy+28),fill=color+(7 if effect=='mist' else 5,))
        for i,(px,py,depth,seed,extra) in enumerate(self.particles):
            if effect in ('snow','aurora'):
                y=((py+(phase/TAU)*(1+i%3))%1)*(h+24)-12
                x=px*w+math.sin(phase*(1+i%2)+seed)*(8+depth*14)
                if s['id']==1902 and x>w*.51:continue
                r=.55+depth*1.8;a=int(48+depth*130)
                target=g if depth>.87 else d
                target.ellipse((x-r,y-r,x+r,y+r),fill=(235,247,255,a))
            elif effect=='rain':
                # A full integer number of screen traversals makes an exact loop.
                y=((py+phase/TAU*(6+i%3))%1)*(h+45)-22
                x=px*w+math.sin(phase+seed)*2;length=5+depth*17
                # In portraits/interiors rain belongs in the background on the left.
                if (s['folder']=='muses' or s['id']==1509) and x>w*.51:continue
                d.line((x,y,x-2-depth*2,y+length),fill=(190,216,239,int(22+depth*46)),width=1)
            elif effect in ('petals','leaves','wind','embers','pollen','dust','fireflies','stars','bubbles'):
                limit={'stars':95,'dust':60,'pollen':70,'fireflies':40,'bubbles':36,'wind':36,'embers':68,'petals':42,'leaves':30}[effect]
                if i>=limit:continue
                # Portrait eyes and face remain readable.
                if s['folder'] in ('electric','muses') and px>.42 and .18<py<.68:continue
                if effect=='stars':
                    x=px*w;y=py*h*.47;r=.4+depth*.75;a=int(25+120*(.5+.5*math.sin(phase*(1+i%2)+seed))**3)
                elif effect=='bubbles':
                    x=px*w+math.sin(phase+seed)*9;y=((py-phase/TAU)%1)*(h+20)-10;r=1+depth*3;a=50
                elif effect in ('petals','leaves'):
                    x=px*w+math.sin(phase+seed)*45;y=((py+phase/TAU)%1)*(h+30)-15;r=1.5+depth*3;a=int(65+depth*80)
                else:
                    x=px*w+math.sin(phase+seed)*(18 if effect=='wind' else 8)
                    y=py*h+math.cos(phase+seed)*(12 if effect=='embers' else 5)
                    r=.5+depth*(1.3 if effect=='fireflies' else .8)
                    a=int((35+depth*80)*(.4+.6*(.5+.5*math.sin(phase*2+seed))))
                if effect=='bubbles':d.ellipse((x-r,y-r,x+r,y+r),outline=(180,240,250,a),width=1)
                elif effect in ('petals','leaves'):
                    tilt=math.sin(phase*2+seed);d.ellipse((x-r,y-r*.45,x+r,y+r*.45),fill=color+(a,))
                else:d.ellipse((x-r,y-r,x+r,y+r),fill=color+(a,))
                if effect in ('fireflies','embers'):g.ellipse((x-3*r,y-3*r,x+3*r,y+3*r),fill=color+(a//3,))
        for chimney in s.get('chimneys',[]):
            cx,cy=chimney[0]*w,chimney[1]*h
            for i in range(16):
                progress=(i/16+phase/TAU)%1
                y=cy-progress*55;x=cx+math.sin(progress*4+phase)*progress*9
                radius=2+progress*10;alpha=int(9*math.sin(math.pi*progress))
                g.ellipse((x-radius,y-radius*.55,x+radius,y+radius*.55),fill=(224,229,236,alpha))
        base=self.deform(phase)
        overlay=Image.alpha_composite(soft.filter(ImageFilter.GaussianBlur(4)),sharp)
        overlay=overlay.resize(self.size,Image.Resampling.BILINEAR)
        return Image.alpha_composite(base,overlay).convert('RGB')


def render(scene):
    folder=ROOT/'assets'/scene['folder'];sid=scene['id'];target=folder/f'{sid}.mp4'
    temporary=folder/f'{sid}.tmp.mp4';renderer=SceneRenderer(scene)
    command=[imageio_ffmpeg.get_ffmpeg_exe(),'-hide_banner','-loglevel','error','-y','-f','rawvideo','-pix_fmt','rgb24','-s','1920x1080','-r',str(FPS),'-i','-','-an','-c:v','libx264','-preset','fast','-crf',str(scene.get('crf',18)),'-threads','2','-pix_fmt','yuv420p','-movflags','+faststart',str(temporary)]
    with subprocess.Popen(command,stdin=subprocess.PIPE) as encoder:
        try:
            for i in range(FPS*SECONDS):
                frame=renderer.frame(TAU*i/(FPS*SECONDS))
                if i==FPS:frame.resize((960,540),Image.Resampling.LANCZOS).save(folder/f'{sid}.poster.png')
                encoder.stdin.write(frame.tobytes())
            encoder.stdin.close()
            if encoder.wait():raise RuntimeError(f'Encoding failed: {sid}')
        except BaseException:
            encoder.kill();raise
    temporary.replace(target)
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-hide_banner','-loglevel','error','-y','-i',str(target),'-vf','scale=960:540,fps=30','-an','-c:v','libx264','-threads','2','-crf','23','-pix_fmt','yuv420p','-movflags','+faststart',str(folder/f'{sid}.preview.mp4')],check=True)
    return sid


def package(groups):
    OUT.mkdir(exist_ok=True)
    def bundle(path,scenes,name):
        manifest={'Format':1,'Name':name,'Author':'VWP / AI-assisted artwork','Scenes':[],'Hashes':{},'Settings':{'IntervalMinutes':15}}
        with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED) as archive:
            for scene in scenes:
                sid=str(scene['id']);folder=ROOT/'assets'/scene['folder'];entry={'Id':sid,'Name':scene['name']}
                for key,suffix in [('Video','.mp4'),('Cover','.poster.png')]:
                    source=folder/f'{sid}{suffix}';relative=f'scenes/{sid}/{source.name}'
                    archive.write(source,relative);manifest['Hashes'][relative]=hashlib.sha256(source.read_bytes()).hexdigest().upper();entry[key]=relative
                manifest['Scenes'].append(entry)
            archive.writestr('manifest.json',json.dumps(manifest,ensure_ascii=False))
    allscenes=[]
    for group in groups:
        scenes=group['entries'];allscenes.extend(scenes);destination=OUT/group['folder'];destination.mkdir(exist_ok=True)
        for scene in scenes:shutil.copy2(ROOT/'assets'/group['folder']/f"{scene['id']}.mp4",destination/f"{scene['id']}.mp4")
        bundle(OUT/f"{group['folder']}-10.vwpbundle",scenes,group['category']+' · 10')
        sheet=Image.new('RGB',(1600,1000),(17,21,29));draw=ImageDraw.Draw(sheet)
        try:font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
        except OSError:font=ImageFont.load_default()
        for i,scene in enumerate(scenes):
            x=(i%2)*800;y=(i//2)*200;thumb=Image.open(ROOT/'assets'/group['folder']/f"{scene['id']}.poster.png");thumb.thumbnail((320,180));sheet.paste(thumb,(x,y));draw.text((x+333,y+30),scene['name'],font=font,fill='white');draw.text((x+333,y+65),scene['effect'],font=font,fill='#A6B9CA')
        sheet.save(OUT/f"{group['folder']}-overview.jpg",quality=92)
    bundle(OUT/'VWP-All-60.vwpbundle',allscenes,'VWP · 60 live wallpapers')
    manifest={'count':len(allscenes),'resolution':SIZE,'fps':FPS,'seconds':SECONDS,'scenes':allscenes}
    (OUT/'collection.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print('PACKAGED '+str(OUT),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--watch',action='store_true');parser.add_argument('--workers',type=int,default=2);parser.add_argument('--ids',type=int,nargs='*');parser.add_argument('--package-only',action='store_true');parser.add_argument('--force',action='store_true');args=parser.parse_args()
    groups=prepare();scenes=[s for g in groups for s in g['entries']]
    if args.package_only:package(groups);raise SystemExit()
    pending=[s for s in scenes if not args.ids or s['id'] in args.ids];done=set();running={}
    with ProcessPoolExecutor(max_workers=args.workers) as pool:
        while len(done)<len(pending):
            for future,scene in list(running.items()):
                if future.done():
                    sid=future.result();done.add(sid);del running[future];print(f'RENDERED {sid} ({len(done)}/{len(pending)})',flush=True)
            for scene in pending:
                sid=scene['id']
                if sid in done or any(x['id']==sid for x in running.values()):continue
                source=ROOT/'assets'/scene['folder']/f'{sid}.png'
                if not source.exists():
                    if not args.watch:raise FileNotFoundError(source)
                    continue
                if not args.force and all((source.parent/f'{sid}.{suffix}').exists() for suffix in ('mp4','preview.mp4','poster.png')):
                    done.add(sid);continue
                if len(running)>=args.workers:break
                running[pool.submit(render,scene)]=scene
            if len(done)<len(pending):time.sleep(1)
    if len(pending)==60:package(groups)
