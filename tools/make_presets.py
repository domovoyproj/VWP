"""Generate original looping anime-inspired scenery, with no downloaded footage."""
import math
import random
import subprocess
from functools import lru_cache
from pathlib import Path
from PIL import Image, ImageDraw
import imageio_ffmpeg

ROOT = Path(__file__).resolve().parents[1] / 'assets'
THEMES = [('Sakura Midnight', (14, 16, 42), (248, 119, 184)),
          ('Neon Tokyo', (8, 18, 35), (54, 221, 242)),
          ('Crimson Moon', (24, 9, 30), (243, 79, 113))]

@lru_cache(maxsize=3)
def artwork(index):
    path=ROOT/f'scene-{index}.png'
    return Image.open(path).convert('RGB').resize((1280,720),Image.Resampling.LANCZOS) if path.exists() else None

def frame(index, t):
    name, bg, accent = THEMES[index]
    base=artwork(index)
    if base is not None:
        im=base.copy()
        d=ImageDraw.Draw(im,'RGBA')
        rng=random.Random(index+82)
        for _ in range(32):
            phase=rng.random()*math.tau
            x=(rng.random()*1280+math.sin(t*math.tau+phase)*55)%1280
            y=(rng.random()*720+math.cos(t*math.tau+phase)*30)%720
            size=rng.randrange(2,5)
            d.ellipse((x,y,x+size*2,y+size),fill=(*accent,100))
        return im
    im = Image.new('RGB', (960, 540), bg)
    d = ImageDraw.Draw(im)
    for y in range(540):
        k = y / 540
        d.line((0,y,960,y), fill=tuple(int(v*(1-k*.5)) for v in bg))
    d.ellipse((650,55,830,235), fill=accent)
    d.ellipse((625,37,797,208), fill=bg)
    rng = random.Random(42)
    for x in range(0,960,40):
        h = rng.randrange(70,210)
        d.rectangle((x,540-h,x+36,540), fill=(5,7,18))
        for yy in range(550-h,520,18):
            for xx in range(x+5,x+30,12):
                if rng.random()>.4:
                    d.rectangle((xx,yy,xx+3,yy+5), fill=accent)
    # Foreground silhouette: long hair, scarf and coat.
    d.ellipse((421,240,476,301), fill=(4,5,13))
    d.polygon([(426,273),(410,356),(392,409),(430,378),(443,320),(474,383),(504,408),(484,336),(472,275)],fill=(4,5,13))
    d.polygon([(441,301),(463,301),(491,438),(407,438)],fill=(4,5,13))
    d.polygon([(432,432),(447,432),(439,530),(422,530)],fill=(4,5,13))
    d.polygon([(455,432),(470,432),(482,530),(465,530)],fill=(4,5,13))
    wave=math.sin(t*math.tau)*12
    d.polygon([(448,307),(465,309),(540,291+wave),(572,306+wave),(489,322)],fill=accent)
    for _ in range(65):
        phase=rng.random()*math.tau
        x=(rng.random()*960+math.sin(t*math.tau+phase)*75)%960
        y=(rng.random()*540+math.cos(t*math.tau+phase)*45)%540
        size=rng.randrange(2,6)
        d.ellipse((x,y,x+size*2,y+size),fill=accent)
    return im

def main():
    ROOT.mkdir(exist_ok=True)
    for idx,(name,*_) in enumerate(THEMES):
        frame(idx,0).save(ROOT/f'{idx}.jpg')
        width,height=frame(idx,0).size
        cmd=[imageio_ffmpeg.get_ffmpeg_exe(),'-y','-f','rawvideo','-pix_fmt','rgb24','-s',f'{width}x{height}','-r','24','-i','-','-an','-c:v','libx264','-pix_fmt','yuv420p','-crf','20','-movflags','+faststart',str(ROOT/f'{idx}.mp4')]
        p=subprocess.Popen(cmd,stdin=subprocess.PIPE,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
        for n in range(144):
            p.stdin.write(frame(idx,n/144).tobytes())
        p.stdin.close()
        errors=p.stderr.read()
        if p.wait():
            raise RuntimeError(errors.decode())
        print(name)

if __name__=='__main__':
    main()
