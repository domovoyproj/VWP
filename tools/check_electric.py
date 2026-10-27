"""Verify decoded video movement, loop boundary, format and portable bundle hashes."""
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile
import numpy as np
from PIL import Image
import imageio_ffmpeg
from render_electric import frame

root=Path(__file__).resolve().parents[1]
folder=root/'assets'/'electric'
catalog=[scene for scene in json.loads((folder/'catalog.json').read_text(encoding='utf-8')) if scene['id'] in (1401,1402)]
assert len(catalog)==2 and len({s['id'] for s in catalog})==2
for scene in catalog:
    sid=scene['id']
    base=Image.open(folder/f'{sid}.png').convert('RGBA').resize((640,360))
    a=np.asarray(frame(base,sid,0)).astype(float)
    b=np.asarray(frame(base,sid,np.pi*2)).astype(float)
    assert np.max(np.abs(a-b))<=1, f'{sid}: nonperiodic renderer'
    reader=imageio_ffmpeg.read_frames(str(folder/f'{sid}.mp4'),pix_fmt='rgb24',output_params=['-vf','scale=320:180'])
    meta=next(reader)
    assert meta['source_size']==(1920,1080),meta
    assert meta['fps']==30 and abs(meta['duration']-8)<.1,meta
    samples=[np.frombuffer(buf,dtype=np.uint8).astype(float) for buf in reader]
    assert len(samples)==240,len(samples)
    motion=max(np.mean(np.abs(samples[i]-samples[0])) for i in (30,60,90,120))
    seam=np.mean(np.abs(samples[-1]-samples[0]))
    adjacent=max(np.mean(np.abs(samples[i]-samples[i-1])) for i in range(1,len(samples)))
    assert motion>.08,f'{sid}: static video'
    assert seam<max(.5,adjacent*1.5),f'{sid}: hard loop cut'
    print(f'{sid}: PASS, motion={motion:.3f}, seam={seam:.3f}, max step={adjacent:.3f}')
with zipfile.ZipFile(root/'artifacts'/'Electric-Killua.vwpbundle') as bundle:
    manifest=json.loads(bundle.read('manifest.json'))
    assert len(manifest['Scenes'])==2
    for name,digest in manifest['Hashes'].items():
        assert hashlib.sha256(bundle.read(name)).hexdigest().upper()==digest
print('PASS: both 1080p/30 loops and all bundle hashes')
