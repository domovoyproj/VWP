"""Check every declared video, thumbnail, artwork and its full FFmpeg decode."""
import json
from pathlib import Path
import subprocess
import imageio_ffmpeg
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]/'assets'
presets=json.loads((ROOT/'presets.json').read_text(encoding='utf-8-sig'))
assert len(presets)==18, f'Expected 18 scenes, got {len(presets)}'
assert len({p['id'] for p in presets})==18
for preset in presets:
    index=preset['id']
    for extension in ('.jpg','.mp4'):
        assert (ROOT/f'{index}{extension}').is_file(),f'Missing {index}{extension}'
    with Image.open(ROOT/f'{index}.jpg') as image:
        assert image.size==(1280,720)
        image.verify()
    with Image.open(ROOT/f'scene-{index}.png') as image:image.verify()
    check=subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-v','error','-i',str(ROOT/f'{index}.mp4'),'-f','null','-'],capture_output=True)
    assert check.returncode==0,check.stderr.decode()
    print(f"OK {index}: {preset['name']}",flush=True)
with Image.open(ROOT/'app.ico') as icon:assert len(icon.ico.sizes())==7
print('18 videos fully decoded, all thumbnails/artworks and 7 icon sizes verified.')
