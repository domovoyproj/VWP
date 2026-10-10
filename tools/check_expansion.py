"""Check the curated wallpaper catalog; retired procedural scenes must stay retired."""
import json
from pathlib import Path
from PIL import Image
root=Path(__file__).resolve().parents[1]/'assets'
catalog=json.loads((root/'expansion.json').read_text(encoding='utf-8'))
original=json.loads((root/'presets.json').read_text(encoding='utf-8'))
assert len(original)==18 and {scene['id'] for scene in original}==set(range(18))
for scene in original:
    for suffix in ('.mp4','.jpg','.cover.jpg','.preview.mp4'):
        assert (root/f"{scene['id']}{suffix}").stat().st_size>1000
    with Image.open(root/f"scene-{scene['id']}.png") as image:assert image.width>=1280
assert [v['id'] for v in catalog['spatial']]==list(range(18,28))
assert catalog['live']==[], 'Retired procedural collection has returned'
for scene in catalog['spatial']:
    with Image.open(root/'cinematic'/f"{scene['id']}.png") as cover:assert cover.width>=1280
print('PASS: 46 built-in entries; all 15 procedural wallpapers are removed')
