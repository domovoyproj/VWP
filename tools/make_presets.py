"""Create seamless particle loops from the original artwork in assets/presets.json."""
import argparse
from functools import lru_cache
import json
import math
from pathlib import Path
import random
import subprocess
from PIL import Image, ImageDraw, ImageColor
import imageio_ffmpeg

ROOT = Path(__file__).resolve().parents[1] / 'assets'
PRESETS = json.loads((ROOT / 'presets.json').read_text(encoding='utf-8-sig'))

@lru_cache(maxsize=18)
def artwork(index):
    with Image.open(ROOT / f'scene-{index}.png') as source:
        return source.convert('RGB').resize((1280,720),Image.Resampling.LANCZOS)

def frame(preset,t):
    im=artwork(preset['id']).copy()
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
    cmd=[imageio_ffmpeg.get_ffmpeg_exe(),'-y','-f','rawvideo','-pix_fmt','rgb24','-s','1280x720','-r','24','-i','-','-an','-c:v','libx264','-pix_fmt','yuv420p','-crf','20','-movflags','+faststart',str(temporary)]
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
    args=parser.parse_args()
    chosen=[p for p in PRESETS if args.ids is None or p['id'] in args.ids]
    for preset in chosen:encode(preset)

if __name__=='__main__':main()
