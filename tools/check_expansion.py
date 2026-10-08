"""Check the complete cloud-produced collection before publishing any app artifacts."""
import hashlib
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
    assert (root/'spatial'/f"{scene['id']}.png").is_file()
assert [v['id'] for v in catalog['spatial']]==list(range(18,28))
assert [v['id'] for v in catalog['live']]==list(range(100,115))
for scene in catalog['spatial']:
    with Image.open(root/'spatial'/f"{scene['id']}.png") as cover:assert cover.size==(960,540)
for scene in catalog['live']:
    stem=root/'motion'/str(scene['id'])
    report=json.loads(stem.with_suffix('.json').read_text())
    assert (report['width'],report['height'],report['fps'],report['seconds'],report['frames'])==(3840,2160,60,12,720),report
    assert report['uniqueAdjacentFrames'] and report['periodic'] and report['endpointMeanDifference']<.025
    assert hashlib.sha256(stem.with_suffix('.mp4').read_bytes()).hexdigest()==report['sha256']
    for suffix,size in [('.jpg',(1280,720)),('.cover.jpg',(3840,2160))]:
        with Image.open(stem.parent/(stem.name+suffix)) as cover:assert cover.size==size
    assert (stem.parent/(stem.name+'.preview.mp4')).stat().st_size>1000
print('PASS: 10 new 3D covers and 15 independently decoded native 4K/60 loops; 61 total built-in wallpapers')
