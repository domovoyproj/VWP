"""Decode every frame; verify motion, clean loops, unique artwork and bundle hashes."""
import hashlib
import json
from pathlib import Path
import zipfile
import numpy as np
import imageio_ffmpeg
from render_collections import SceneRenderer,ROOT,OUT,TAU,FPS,SECONDS

groups=json.loads((ROOT/'tools'/'collections-plan.json').read_text(encoding='utf-8'))
assert len(groups)==6 and all(len(g['entries'])==10 for g in groups)
scenes=[s for g in groups for s in g['entries']]
assert len({s['id'] for s in scenes})==60
source_hashes=set();report=[];failures=[]
for scene in scenes:
    sid=scene['id'];folder=ROOT/'assets'/scene['folder'];source=folder/f'{sid}.png'
    digest=hashlib.sha256(source.read_bytes()).hexdigest();assert digest not in source_hashes,'Duplicated artwork';source_hashes.add(digest)
    renderer=SceneRenderer(scene,(640,360))
    assert renderer.frame(0).tobytes()==renderer.frame(TAU).tobytes(),f'{sid}: nonperiodic effect'
    reader=imageio_ffmpeg.read_frames(str(folder/f'{sid}.mp4'),pix_fmt='rgb24',output_params=['-vf','scale=320:180'])
    meta=next(reader);assert meta['source_size']==(1920,1080) and meta['fps']==FPS and abs(meta['duration']-SECONDS)<.1,meta
    first=previous=last=None;motion=step=0.;changed=0;count=0
    for raw in reader:
        current=np.frombuffer(raw,dtype=np.uint8).astype(np.int16)
        assert current.mean()>2,f'{sid}: black frame'
        if first is None:first=current
        difference=np.abs(current-first)
        motion=max(motion,float(difference.mean()));changed=max(changed,int((difference.reshape(-1,3).max(axis=1)>5).sum()))
        if previous is not None:step=max(step,float(np.abs(current-previous).mean()))
        previous=last=current;count+=1
    assert count==FPS*SECONDS,f'{sid}: truncated video'
    seam=float(np.abs(last-first).mean())
    if not (motion>.015 and changed>12):failures.append(f'{sid}: insufficient visible motion ({motion}, {changed})')
    if not seam<max(.5,step*1.7):failures.append(f'{sid}: excessive loop step {seam:.4f}, adjacent {step:.4f}')
    record={'id':sid,'name':scene['name'],'motion':round(motion,4),'changedPixels':changed,'seam':round(seam,4),'maxStep':round(step,4),'frames':count}
    record['status']='FAIL' if any(f.startswith(str(sid)+':') for f in failures) else 'PASS'
    report.append(record);print(f"{sid}: {record['status']}, motion={motion:.3f}, pixels={changed}, seam={seam:.3f}",flush=True)
assert not failures,'\n'.join(failures)
for path in [OUT/'VWP-All-60.vwpbundle',*[OUT/f"{g['folder']}-10.vwpbundle" for g in groups]]:
    with zipfile.ZipFile(path) as archive:
        manifest=json.loads(archive.read('manifest.json'));expected=60 if path.name.startswith('VWP-All') else 10
        assert manifest['Format']==1 and len(manifest['Scenes'])==expected
        for name,digest in manifest['Hashes'].items():assert hashlib.sha256(archive.read(name)).hexdigest().upper()==digest
(OUT/'verification.json').write_text(json.dumps({'status':'PASS','scenes':report},ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS: 60 distinct artworks, 60 complete animated loops and seven bundle manifests/hashes.')
