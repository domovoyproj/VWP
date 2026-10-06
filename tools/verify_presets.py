"""Check every declared video, thumbnail, artwork and its full FFmpeg decode."""
import json
import argparse
from pathlib import Path
import subprocess
import imageio_ffmpeg
import cv2
from PIL import Image
from make_presets import frame,WIDTH,HEIGHT,FPS,SECONDS

ROOT=Path(__file__).resolve().parents[1]/'assets'
presets=json.loads((ROOT/'presets.json').read_text(encoding='utf-8-sig'))
assert len(presets)==18, f'Expected 18 scenes, got {len(presets)}'
assert len({p['id'] for p in presets})==18
parser=argparse.ArgumentParser()
parser.add_argument('--ids',type=int,nargs='+')
args=parser.parse_args()
if args.ids is not None:presets=[p for p in presets if p['id'] in args.ids]
for preset in presets:
    index=preset['id']
    for extension in ('.jpg','.mp4','.cover.jpg','.preview.mp4'):
        assert (ROOT/f'{index}{extension}').is_file(),f'Missing {index}{extension}'
    with Image.open(ROOT/f'{index}.jpg') as image:
        assert image.size==(1280,720)
        image.verify()
    with Image.open(ROOT/f'scene-{index}.png') as image:image.verify()
    with Image.open(ROOT/f'{index}.cover.jpg') as image:
        assert image.size==(WIDTH,HEIGHT)
        image.verify()
    video=cv2.VideoCapture(str(ROOT/f'{index}.mp4'))
    try:
        assert video.isOpened(),f'Cannot open {index}'
        assert (int(video.get(cv2.CAP_PROP_FRAME_WIDTH)),int(video.get(cv2.CAP_PROP_FRAME_HEIGHT)))==(WIDTH,HEIGHT),f'Not 4K: {index}'
        assert abs(video.get(cv2.CAP_PROP_FPS)-FPS)<.001,f'Not 60 FPS: {index}'
        assert int(video.get(cv2.CAP_PROP_FRAME_COUNT))==FPS*SECONDS,f'Wrong frame count: {index}'
    finally:video.release()
    preview=cv2.VideoCapture(str(ROOT/f'{index}.preview.mp4'))
    try:
        assert preview.isOpened(),f'Cannot open preview {index}'
        assert (int(preview.get(cv2.CAP_PROP_FRAME_WIDTH)),int(preview.get(cv2.CAP_PROP_FRAME_HEIGHT)))==(1280,720)
        assert abs(preview.get(cv2.CAP_PROP_FPS)-30)<.001
        assert int(preview.get(cv2.CAP_PROP_FRAME_COUNT))==30*SECONDS
        preview.set(cv2.CAP_PROP_POS_FRAMES,30*SECONDS-1)
        ok,_=preview.read()
        assert ok,f'Preview last frame cannot be decoded: {index}'
    finally:preview.release()
    check=subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-v','error','-threads','2','-i',str(ROOT/f'{index}.mp4'),'-map','0:v:0','-f','framemd5','-'],capture_output=True)
    assert check.returncode==0,check.stderr.decode()
    hashes=[line.split(',')[-1].strip() for line in check.stdout.decode().splitlines() if line and not line.startswith('#')]
    assert len(hashes)==FPS*SECONDS,f'Missing decoded frames: {index}'
    assert all(a!=b for a,b in zip(hashes,hashes[1:])),f'Duplicated adjacent frames: {index}'
    first=frame(preset,0).tobytes()
    assert first==frame(preset,1).tobytes(),f'Loop is not periodic: {index}'
    assert first!=frame(preset,.5).tobytes(),f'Missing motion: {index}'
    print(f"OK {index}: {preset['name']}",flush=True)
with Image.open(ROOT/'app.ico') as icon:assert len(icon.ico.sizes())==7
print(f'{len(presets)} UHD 3840x2160 / 60 FPS videos: 360 decoded frames each, no adjacent duplicates, periodic motion, 4K covers and thumbnails verified.')
