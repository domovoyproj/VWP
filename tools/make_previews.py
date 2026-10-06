"""Create lightweight launcher previews while desktop playback uses UHD masters."""
import argparse
import json
from pathlib import Path
import subprocess
import time
import imageio_ffmpeg

ROOT=Path(__file__).resolve().parents[1]/'assets'

def encode_preview(index):
    source=ROOT/f'{index}.mp4'
    target=ROOT/f'{index}.preview.mp4'
    if target.exists() and target.stat().st_mtime>=source.stat().st_mtime:return
    temporary=ROOT/f'{index}.preview.tmp.mp4'
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-hide_banner','-loglevel','error','-threads','2','-i',str(source),'-an','-vf','fps=30,scale=1280:720:flags=lanczos','-c:v','libx264','-threads','2','-preset','veryfast','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',str(temporary)],check=True)
    temporary.replace(target)
    print(f'Preview ready: {index}',flush=True)

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--wait',action='store_true',help='Wait for each UHD poster to appear during a first render')
    args=parser.parse_args()
    pending={p['id'] for p in json.loads((ROOT/'presets.json').read_text(encoding='utf-8-sig'))}
    deadline=time.monotonic()+7200
    while pending:
        for index in sorted(pending.copy()):
            if args.wait and not (ROOT/f'{index}.cover.jpg').exists():continue
            encode_preview(index)
            pending.remove(index)
        if pending:
            if time.monotonic()>deadline:raise TimeoutError('UHD scenes did not finish rendering')
            time.sleep(5)

if __name__=='__main__':main()
