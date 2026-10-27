"""Render deterministic, seamless lightning overlays on generated anime artwork.

Usage: python tools/render_electric.py [--height 1080] [--fps 30] [--seconds 8]
The portrait stays still; only electric arcs, sparks and their glow move.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import subprocess
import zipfile
from PIL import Image, ImageDraw, ImageFilter
import imageio_ffmpeg

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'assets' / 'electric'
TAU = math.tau


def frame(base, scene_id, phase):
    """All motion is periodic: frame(0) == frame(2*pi). No random state per frame."""
    w, h = base.size
    scale = w / 960
    glow = Image.new('RGBA', base.size)
    core = Image.new('RGBA', base.size)
    gd, cd = ImageDraw.Draw(glow), ImageDraw.Draw(core)
    rng = random.Random(scene_id)
    # Arcs surround the portrait; the central face and clock area remain clear.
    anchors = [(.05,.24),(.20,.50),(.08,.78),(.27,.88),(.39,.71),
               (.84,.19),(.93,.36),(.89,.63),(.93,.84),(.58,.91),(.76,.89)]
    for index, (ax, ay) in enumerate(anchors):
        seed = rng.uniform(0, TAU)
        angle = rng.uniform(0, TAU)
        length = rng.uniform(.07, .17) * w
        pulse = (.5 + .5 * math.sin(phase * (2 + index % 3) + seed)) ** 5
        alpha = int(28 + 220 * pulse)
        points = []
        for j in range(23):
            u = j / 22
            bend = math.sin(u * 21 + phase * 3 + seed) * 6 * scale
            bend += math.sin(u * 49 - phase * 5 + seed) * 3 * scale
            x = ax*w + math.cos(angle)*length*(u-.5) - math.sin(angle)*bend
            y = ay*h + math.sin(angle)*length*(u-.5) + math.cos(angle)*bend
            points.append((x,y))
        gd.line(points, fill=(40,195,255,alpha), width=max(3,round(8*scale)))
        cd.line(points, fill=(135,239,255,alpha), width=max(2,round(3*scale)))
        cd.line(points, fill=(245,255,255,alpha), width=max(1,round(scale)))
        branch = [points[13]]
        for j in range(1,8):
            branch.append((points[13][0]+j*5*scale,points[13][1]-j*4*scale+math.sin(j*2+phase*4+seed)*4*scale))
        cd.line(branch, fill=(170,245,255,alpha//2), width=max(1,round(scale)))
    for i in range(32):
        x = rng.random()*w
        y = rng.random()*h
        if .36*w < x < .81*w and y < .7*h:
            continue
        offset = rng.random()*TAU
        x += math.sin(phase+offset)*9*scale
        y += math.cos(phase+offset)*15*scale
        a = int(30+100*(.5+.5*math.sin(phase*2+offset))**3)
        r = scale*(.6+rng.random())
        cd.ellipse((x-r,y-r,x+r,y+r),fill=(185,245,255,a))
    result = Image.alpha_composite(base,glow.filter(ImageFilter.GaussianBlur(5*scale)))
    return Image.alpha_composite(result,core).convert('RGB')


def render(scene, args):
    scene_id = scene['id']
    size = (args.height*16//9,args.height)
    base = Image.open(FOLDER/f'{scene_id}.png').convert('RGBA').resize(size,Image.Resampling.LANCZOS)
    output = FOLDER/f'{scene_id}.mp4'
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    command = [ffmpeg,'-hide_banner','-loglevel','error','-y','-f','rawvideo',
               '-pix_fmt','rgb24','-s',f'{size[0]}x{size[1]}','-r',str(args.fps),
               '-i','-','-an','-c:v','libx264','-preset','fast','-crf','18',
               '-pix_fmt','yuv420p','-movflags','+faststart',str(output)]
    total = round(args.seconds*args.fps)
    with subprocess.Popen(command,stdin=subprocess.PIPE) as encoder:
        try:
            for i in range(total):
                image = frame(base,scene_id,TAU*i/total)
                if i == total//8:
                    image.resize((960,540),Image.Resampling.LANCZOS).save(FOLDER/f'{scene_id}.poster.png')
                encoder.stdin.write(image.tobytes())
            encoder.stdin.close()
            if encoder.wait():
                raise RuntimeError(f'Encoding failed: {scene_id}')
        except BaseException:
            encoder.kill()
            raise
    subprocess.run([ffmpeg,'-hide_banner','-loglevel','error','-y','-i',str(output),
                    '-vf','scale=960:540,fps=30','-an','-c:v','libx264','-crf','23',
                    '-pix_fmt','yuv420p','-movflags','+faststart',str(FOLDER/f'{scene_id}.preview.mp4')],check=True)
    print(f'{scene_id}: {size[0]}x{size[1]}, {args.fps} fps, {args.seconds}s',flush=True)


def package(catalog):
    output = ROOT/'artifacts'/'Electric-Killua.vwpbundle'
    output.parent.mkdir(exist_ok=True)
    manifest = {'Format':1,'Name':'Electric Killua','Author':'VWP / AI-assisted fan art',
                'Scenes':[],'Hashes':{},'Settings':{'IntervalMinutes':15}}
    with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED) as archive:
        for scene in catalog:
            sid = str(scene['id'])
            entry = {'Id':sid,'Name':scene['name']}
            for key, suffix in [('Video','.mp4'),('Cover','.poster.png')]:
                path = FOLDER/f'{sid}{suffix}'
                name = f'scenes/{sid}/{path.name}'
                archive.write(path,name)
                manifest['Hashes'][name] = hashlib.sha256(path.read_bytes()).hexdigest().upper()
                entry[key] = name
            manifest['Scenes'].append(entry)
        archive.writestr('manifest.json',json.dumps(manifest,ensure_ascii=False))
    print(output,flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--height',type=int,default=1080,choices=[360,720,1080])
    parser.add_argument('--fps',type=int,default=30,choices=[15,30,60])
    parser.add_argument('--seconds',type=int,default=8,choices=[4,8,12])
    args = parser.parse_args()
    # Legacy two-scene sampler; the full collection uses render_collections.py.
    catalog = [scene for scene in json.loads((FOLDER/'catalog.json').read_text(encoding='utf-8')) if scene['id'] in (1401,1402)]
    for scene in catalog:
        render(scene,args)
    package(catalog)
