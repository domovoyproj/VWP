"""Restore SHA-256 pinned official media and exact-source verified sixty-scene masters."""
import hashlib,json,shutil,subprocess,tempfile,urllib.request,zipfile
from pathlib import Path,PurePosixPath
import imageio_ffmpeg

ROOT=Path(__file__).resolve().parents[1]
config=json.loads((ROOT/'tools/release-media.json').read_text(encoding='utf-8'))
groups=json.loads((ROOT/'tools/collections-plan.json').read_text(encoding='utf-8'))
legacy=['assets/presets.json','assets/expansion.json','assets/scene-*.png','assets/layers','assets/living','assets/anime','assets/cinematic','assets/vibes','assets/seasons','assets/cursors']
subprocess.run(['git','diff','--exit-code',config['baseline']['ref'],'--',*legacy],cwd=ROOT,check=True,stdout=subprocess.DEVNULL)
subprocess.run(['git','diff','--exit-code',config['collection']['source'],'--','tools/render_collections.py','tools/collections-plan.json',*[f"assets/{g['folder']}" for g in groups]],cwd=ROOT,check=True,stdout=subprocess.DEVNULL)

def digest(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()

def github(path):
    return json.loads(subprocess.check_output(['gh','api','repos/domovoyproj/VWP/'+path],text=True))

cache_root=ROOT/'artifacts';cache_root.mkdir(exist_ok=True)
with tempfile.TemporaryDirectory(prefix='vwp-release-media-',dir=cache_root) as temporary:
    cache=Path(temporary);baseline=cache/'baseline.zip'
    print('Downloading official 0.9.1 media baseline...',flush=True)
    with urllib.request.urlopen(config['baseline']['url'],timeout=180) as source,baseline.open('wb') as target:shutil.copyfileobj(source,target,1024*1024)
    if digest(baseline)!=config['baseline']['sha256']:raise SystemExit('Official baseline checksum mismatch')
    restored=0
    with zipfile.ZipFile(baseline) as archive:
        for item in archive.infolist():
            path=PurePosixPath(item.filename)
            if item.is_dir() or path.parts[:2]!=('VWP','assets'):continue
            if '..' in path.parts or ':' in item.filename or item.external_attr>>16&0xF000==0xA000:raise SystemExit('Invalid baseline path')
            relative=Path(*path.parts[1:]);destination=ROOT/relative
            if destination.exists():continue
            if relative.parts[1:2] in [('motion',),('spatial',)]:continue
            if item.file_size>512*1024*1024:raise SystemExit('Unexpected baseline asset size')
            destination.parent.mkdir(parents=True,exist_ok=True)
            with archive.open(item) as source,destination.open('wb') as target:shutil.copyfileobj(source,target)
            restored+=1
    baseline.unlink();print(f'Restored {restored} unchanged official assets; SHA-256 matched.',flush=True)
    pinned=config['collection'];run=github(f"actions/runs/{pinned['run']}");artifact=github(f"actions/artifacts/{pinned['artifact']}")
    if run['head_sha']!=pinned['source'] or run['conclusion']!='success':raise SystemExit('Collection source workflow is not successful')
    if artifact['expired'] or artifact['name']!=pinned['name'] or artifact['workflow_run']['id']!=pinned['run'] or artifact['digest']!='sha256:'+pinned['sha256']:raise SystemExit('Collection artifact provenance mismatch')
    package=cache/'sixty.zip'
    with package.open('wb') as target:subprocess.run(['gh','api',f"repos/domovoyproj/VWP/actions/artifacts/{pinned['artifact']}/zip"],stdout=target,check=True)
    if digest(package)!=pinned['sha256']:raise SystemExit('Collection artifact checksum mismatch')
    bundle=cache/'sixty.vwpbundle'
    with zipfile.ZipFile(package) as archive:
        names=[name for name in archive.namelist() if name.endswith('VWP-All-60.vwpbundle')]
        if len(names)!=1:raise SystemExit('Missing unique sixty-scene bundle')
        with archive.open(names[0]) as source,bundle.open('wb') as target:shutil.copyfileobj(source,target)
    package.unlink()
    scenes={str(s['id']):s for g in groups for s in g['entries']}
    with zipfile.ZipFile(bundle) as archive:
        manifest=json.loads(archive.read('manifest.json'))
        if len(manifest['Scenes'])!=60 or {s['Id'] for s in manifest['Scenes']}!=set(scenes):raise SystemExit('Incomplete verified collection')
        for entry in manifest['Scenes']:
            scene=scenes[entry['Id']];folder=ROOT/'assets'/scene['folder']
            for key,extension in [('Video','mp4'),('Cover','poster.png')]:
                relative=entry[key];payload=archive.read(relative)
                if hashlib.sha256(payload).hexdigest().upper()!=manifest['Hashes'][relative]:raise SystemExit('Scene content checksum mismatch')
                (folder/f"{scene['id']}.{extension}").write_bytes(payload)
            subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-hide_banner','-loglevel','error','-y','-i',str(folder/f"{scene['id']}.mp4"),'-vf','scale=960:540,fps=30','-an','-c:v','libx264','-threads','2','-crf','23','-pix_fmt','yuv420p','-movflags','+faststart',str(folder/f"{scene['id']}.preview.mp4")],check=True)
    print('Restored all 60 verified masters/covers and rebuilt lightweight previews.',flush=True)
