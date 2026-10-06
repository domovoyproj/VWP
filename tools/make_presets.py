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
import cv2
import numpy as np
from make_previews import encode_preview

cv2.setNumThreads(2)

ROOT = Path(__file__).resolve().parents[1] / 'assets'
PRESETS = json.loads((ROOT / 'presets.json').read_text(encoding='utf-8-sig'))
WIDTH, HEIGHT, FPS, SECONDS = 3840, 2160, 60, 6
SCALE = WIDTH / 1280

@lru_cache(maxsize=2)
def artwork(index):
    with Image.open(ROOT / f'scene-{index}.png') as source:
        return np.asarray(source.convert('RGB').resize((WIDTH,HEIGHT),Image.Resampling.LANCZOS))

@lru_cache(maxsize=2)
def light(index,accent):
    layer=Image.new('RGBA',(WIDTH,HEIGHT))
    draw=ImageDraw.Draw(layer)
    x=900 if index%2==0 else 370
    draw.ellipse(tuple(v*SCALE for v in (x-350,-150,x+350,400)),fill=(*ImageColor.getrgb(accent),45))
    rgba=np.asarray(layer.filter(ImageFilter.GaussianBlur(95*SCALE)))
    return np.ascontiguousarray(rgba[:,:,:3]),rgba[:,:,3].astype(np.float32)/255

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
    # Compose camera and local displacement into one high-resolution resampling pass.
    zoom=1.035+.006*(1-math.cos(phase))
    width,height=1280/zoom,720/zoom
    left=(1280-width)/2+6*math.sin(phase)
    top=(720-height)/2+3*math.sin(phase)
    x,y=np.meshgrid(np.linspace(left,left+width,129,dtype=np.float32),np.linspace(top,top+height,73,dtype=np.float32))
    depth=(y/720)**2
    dx=math.sin(phase)*2.5*depth
    dy=math.cos(phase)*1.2*depth
    if region:
        cx,cy,rx,ry=region
        influence=np.exp(-((x/1280-cx)/rx)**2-((y/720-cy)/ry)**2)
        dx+=np.sin(phase+y/130)*2.4*influence
        dy+=np.cos(phase+x/260)*.9*influence
    map_x=cv2.resize(dx,(WIDTH,HEIGHT),interpolation=cv2.INTER_CUBIC)*SCALE
    map_y=cv2.resize(dy,(WIDTH,HEIGHT),interpolation=cv2.INTER_CUBIC)*SCALE
    map_x+=(np.arange(WIDTH,dtype=np.float32)/zoom+left*SCALE)[None,:]
    map_y+=(np.arange(HEIGHT,dtype=np.float32)/zoom+top*SCALE)[:,None]
    im=cv2.remap(source,map_x,map_y,cv2.INTER_CUBIC,borderMode=cv2.BORDER_REFLECT_101)
    glow,alpha=light(preset['id'],preset['accent'])
    weight=alpha*(.68+.25*math.sin(phase))
    return Image.fromarray(cv2.blendLinear(im,glow,1-weight,weight))

class ScaledDraw:
    """Draw particle geometry directly at output resolution, in logical coordinates."""
    def __init__(self,image):
        self.draw=ImageDraw.Draw(image,'RGBA')
    def line(self,coordinates,**options):
        self.draw.line(tuple(v*SCALE for v in coordinates),**self.options(options))
    def ellipse(self,coordinates,**options):
        self.draw.ellipse(tuple(v*SCALE for v in coordinates),**self.options(options))
    def options(self,options):
        if 'width' in options:options['width']=max(1,round(options['width']*SCALE))
        return options

def frame(preset,t):
    t=t%1
    im=living_art(preset,t)
    draw=ScaledDraw(im)
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

def encode(preset,encoder='libx264'):
    index=preset['id']
    temporary=ROOT/f'{index}.tmp.mp4'
    codec=['-c:v','h264_qsv','-preset','slow','-global_quality','16'] if encoder=='h264_qsv' else ['-c:v','libx264','-preset','veryfast','-crf','16']
    cmd=[imageio_ffmpeg.get_ffmpeg_exe(),'-y','-hide_banner','-loglevel','error','-f','rawvideo','-pix_fmt','rgb24','-s',f'{WIDTH}x{HEIGHT}','-r',str(FPS),'-i','-','-an',*codec,'-threads','2','-vf','scale=out_color_matrix=bt709','-pix_fmt','yuv420p','-color_primaries','bt709','-color_trc','bt709','-colorspace','bt709','-movflags','+faststart',str(temporary)]
    process=subprocess.Popen(cmd,stdin=subprocess.PIPE,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
    try:
        for n in range(FPS*SECONDS):
            process.stdin.write(frame(preset,n/(FPS*SECONDS)).tobytes())
            if n%60==0:print(f"{index}: rendered {n}/{FPS*SECONDS}",flush=True)
    finally:
        process.stdin.close()
    errors=process.stderr.read()
    if process.wait():raise RuntimeError(errors.decode())
    temporary.replace(ROOT/f'{index}.mp4')
    cover=frame(preset,0)
    cover.save(ROOT/f'{index}.cover.jpg',quality=96,subsampling=0)
    cover.resize((1280,720),Image.Resampling.LANCZOS).save(ROOT/f'{index}.jpg',quality=95)
    encode_preview(index)
    print(f"{index}: {preset['name']}",flush=True)

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--ids',nargs='+',type=int)
    parser.add_argument('--jobs',type=int,default=1)
    parser.add_argument('--encoder',choices=['libx264','h264_qsv'],default='libx264')
    args=parser.parse_args()
    chosen=[p for p in PRESETS if args.ids is None or p['id'] in args.ids]
    if args.jobs>1:
        with ProcessPoolExecutor(max_workers=args.jobs) as pool:list(pool.map(encode,chosen,[args.encoder]*len(chosen)))
    else:
        for preset in chosen:encode(preset,args.encoder)

if __name__=='__main__':main()
