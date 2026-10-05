"""Create seamless particle loops from the original artwork in assets/presets.json."""
import argparse
from functools import lru_cache
import json
import math
from pathlib import Path
import random
import subprocess
from concurrent.futures import ProcessPoolExecutor
from PIL import Image, ImageDraw, ImageColor, ImageFilter
import imageio_ffmpeg

ROOT = Path(__file__).resolve().parents[1] / 'assets'
PRESETS = json.loads((ROOT / 'presets.json').read_text(encoding='utf-8-sig'))

@lru_cache(maxsize=18)
def artwork(index):
    with Image.open(ROOT / f'scene-{index}.png') as source:
        return source.convert('RGB').resize((1280,720),Image.Resampling.LANCZOS)

@lru_cache(maxsize=18)
def light(index,accent):
    layer=Image.new('RGBA',(1280,720))
    draw=ImageDraw.Draw(layer)
    x=900 if index%2==0 else 370
    draw.ellipse((x-350,-150,x+350,400),fill=(*ImageColor.getrgb(accent),45))
    return layer.filter(ImageFilter.GaussianBlur(95))

# Localized motion stays away from faces: hair tips, foliage, fabric or crystal petals.
SWAY={0:(.86,.59,.13,.24),1:(.84,.65,.13,.25),2:(.87,.57,.12,.32),
      6:(.07,.31,.12,.30),7:(.84,.43,.15,.31),8:(.15,.30,.17,.30),
      9:(.91,.12,.09,.18),10:(.90,.14,.09,.22),11:(.91,.57,.08,.28),
      12:(.13,.31,.12,.24),13:(.88,.59,.10,.30),14:(.06,.33,.06,.23),
      16:(.78,.45,.15,.27),17:(.79,.16,.11,.20)}

def living_art(preset,t):
    phase=t*math.tau
    source=artwork(preset['id'])
    region=SWAY.get(preset['id'])
    def displaced(x,y):
        depth=(y/720)**2
        dx=math.sin(phase)*2.5*depth
        dy=math.cos(phase)*1.2*depth
        if region:
            cx,cy,rx,ry=region
            influence=math.exp(-((x/1280-cx)/rx)**2-((y/720-cy)/ry)**2)
            dx+=math.sin(phase+y/130)*2.4*influence
            dy+=math.cos(phase+x/260)*.9*influence
        return x+dx,y+dy
    mesh=[]
    for y in range(0,720,90):
        for x in range(0,1280,160):
            corners=[displaced(x,y),displaced(x,y+90),displaced(x+160,y+90),displaced(x+160,y)]
            mesh.append(((x,y,x+160,y+90),tuple(v for point in corners for v in point)))
    warped=source.transform(source.size,Image.Transform.MESH,mesh,Image.Resampling.BICUBIC)
    # Slow orbital camera, with overscan so no exposed border enters the frame.
    zoom=1.035+.006*(1-math.cos(phase))
    width,height=1280/zoom,720/zoom
    left=(1280-width)/2+6*math.sin(phase)
    top=(720-height)/2+3*math.sin(phase)
    im=warped.transform((1280,720),Image.Transform.EXTENT,(left,top,left+width,top+height),Image.Resampling.BICUBIC).convert('RGBA')
    glow=light(preset['id'],preset['accent']).copy()
    glow.putalpha(glow.getchannel('A').point(lambda a:int(a*(.68+.25*math.sin(phase)))))
    return Image.alpha_composite(im,glow).convert('RGB')

def frame(preset,t):
    t=t%1
    im=living_art(preset,t)
    draw=ImageDraw.Draw(im,'RGBA')
    rng=random.Random(preset['id']+82)
    color=ImageColor.getrgb(preset['accent'])
    motion=preset['motion']
    count=85 if motion in ('rain','snow') else 40
    for _ in range(count):
        phase=rng.random()*math.tau
        base_x=rng.random()*1280
        base_y=rng.random()*720
        size=rng.randrange(1,4)
        x=base_x+math.sin(t*math.tau+phase)*20
        if motion=='rain':
            y=(base_y+t*720)%720
            draw.line((x,y,x-5,y+22),fill=(*color,75),width=1)
        elif motion=='snow':
            y=(base_y+t*720)%720
            draw.ellipse((x,y,x+size,y+size),fill=(235,242,255,140))
        elif motion=='bubbles':
            y=(base_y-t*720)%720
            draw.ellipse((x,y,x+size*3,y+size*3),outline=(*color,100),width=1)
        elif motion=='embers':
            y=(base_y-t*720)%720
            alpha=int(95+65*math.sin(t*math.tau+phase))
            draw.ellipse((x,y,x+size,y+size),fill=(*color,alpha))
        elif motion in ('stars','fireflies'):
            y=base_y+math.cos(t*math.tau+phase)*10
            alpha=int(90+80*math.sin(t*math.tau+phase))
            draw.ellipse((x-size,y-size,x+size,y+size),fill=(*color,alpha))
        else:
            x=base_x+math.sin(t*math.tau+phase)*45
            y=base_y+math.cos(t*math.tau+phase)*25
            draw.ellipse((x,y,x+size*(2 if motion=='petals' else 1),y+size),fill=(*color,110))
    return im

def encode(preset):
    index=preset['id']
    temporary=ROOT/f'{index}.tmp.mp4'
    cmd=[imageio_ffmpeg.get_ffmpeg_exe(),'-y','-f','rawvideo','-pix_fmt','rgb24','-s','1280x720','-r','24','-i','-','-an','-c:v','libx264','-threads','2','-pix_fmt','yuv420p','-crf','20','-movflags','+faststart',str(temporary)]
    process=subprocess.Popen(cmd,stdin=subprocess.PIPE,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
    try:
        for n in range(144):process.stdin.write(frame(preset,n/144).tobytes())
    finally:
        process.stdin.close()
    errors=process.stderr.read()
    if process.wait():raise RuntimeError(errors.decode())
    temporary.replace(ROOT/f'{index}.mp4')
    frame(preset,0).save(ROOT/f'{index}.jpg',quality=92)
    print(f"{index}: {preset['name']}",flush=True)

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--ids',nargs='+',type=int)
    parser.add_argument('--jobs',type=int,default=1)
    args=parser.parse_args()
    chosen=[p for p in PRESETS if args.ids is None or p['id'] in args.ids]
    if args.jobs>1:
        with ProcessPoolExecutor(max_workers=args.jobs) as pool:list(pool.map(encode,chosen))
    else:
        for preset in chosen:encode(preset)

if __name__=='__main__':main()
