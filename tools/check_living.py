"""Validate the three approved originals' animated videos before packaging."""
import argparse
from pathlib import Path
import imageio_ffmpeg

parser = argparse.ArgumentParser()
parser.add_argument('--previews', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
size, fps = ((1280,720),30) if args.previews else ((3840,2160),60)
for scene in range(1001,1004):
    video = root/'artifacts'/'living-check'/f'{scene}-720p.mp4' if args.previews else root/'assets'/'living'/f'{scene}.mp4'
    if not video.is_file() or video.stat().st_size<100_000:
        raise SystemExit(f'Missing animated original: {video}')
    samples=[]
    for seconds in (0,5):
        reader=imageio_ffmpeg.read_frames(str(video),pix_fmt='rgb24',input_params=['-ss',str(seconds)],output_params=['-frames:v','1'])
        info=next(reader);samples.append(next(reader));reader.close()
        if tuple(info['size'])!=size or abs(info['fps']-fps)>.01:
            raise SystemExit(f'Wrong video format: {video}: {info}')
    changed=sum(abs(a-b)>14 for a,b in zip(samples[0],samples[1]))
    if changed<size[0]*size[1]*.005:
        raise SystemExit(f'Static animated original: {video}: {changed} changed channels')
    print(f'{scene}: {size[0]}x{size[1]}, {fps} FPS, {changed} changed color channels')
