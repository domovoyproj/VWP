"""Restore only unchanged media from official 0.9.0; all removed overlays render afresh."""
import hashlib,json,shutil,subprocess,tempfile,urllib.request,zipfile
from pathlib import Path

root=Path(__file__).resolve().parents[1];baseline='3ce1599f928d4aec14912f3d66a53b7bac78da42'
url='https://github.com/domovoyproj/VWP/releases/download/v0.9.0/VWP-0.9.0-win-x64.zip'
digest='50f8adf765ec00ba2f255d760aa039a2c011df6f6e7cf4388f6736bb9a39bc3d'
inputs=['assets/presets.json','assets/scene-*.png','assets/layers','assets/anime','assets/seasons','assets/vibes']
exclude=[':(exclude)**/*.md',':(exclude)**/*.prompt.txt',':(exclude)assets/vibes/vibes.js',':(exclude)assets/vibes/catalog.json',':(exclude)assets/vibes/characters/**']
if subprocess.run(['git','diff','--quiet',baseline,'--',*inputs,*exclude],cwd=root).returncode:raise SystemExit('Protected source artwork changed; unchanged masters cannot be reused.')
if hashlib.sha256((root/'assets/vibes/vibes.js').read_bytes()).hexdigest()!='68d812120883d93b0a8ee3e00cbdf0dca52d8b05ef39b940fd0edb3865c8f052':raise SystemExit('Unreviewed Vibe renderer change; render all Vibe movies instead.')
before={e['id']:e for e in json.loads(subprocess.check_output(['git','show',baseline+':assets/vibes/catalog.json'],cwd=root))}
after=json.loads((root/'assets/vibes/catalog.json').read_text(encoding='utf-8'))
assert len(after)==20
for entry in after:
    if entry['id'] not in (1204,1206):assert entry==before[entry['id']]
expected=[f'assets/{i}{suffix}' for i in range(18) for suffix in ('.mp4','.preview.mp4','.cover.jpg')]
expected += [f'assets/anime/{i}.mp4' for i in range(1101,1116)]
expected += [f'assets/seasons/{i}.{ext}' for i in range(1301,1307) for ext in ('mp4','poster.png','preview.mp4')]
expected += [f'assets/vibes/{i}.{ext}' for i in range(1201,1221) if i not in (1204,1206) for ext in ('mp4','poster.png','preview.mp4')]
with tempfile.TemporaryDirectory(prefix='vwp-clean-media-') as folder:
    archive=Path(folder)/'source.zip';checksum=hashlib.sha256()
    with urllib.request.urlopen(url,timeout=120) as response,archive.open('wb') as output:
        while chunk:=response.read(1024*1024):checksum.update(chunk);output.write(chunk)
    if checksum.hexdigest()!=digest:raise SystemExit('Official 0.9.0 portable checksum mismatch.')
    with zipfile.ZipFile(archive) as z:
        for relative in expected:
            info=z.getinfo('VWP/'+relative)
            if info.is_dir() or info.file_size<1000:raise SystemExit('Invalid baseline media: '+relative)
            target=root/relative;target.parent.mkdir(parents=True,exist_ok=True)
            with z.open(info) as source,target.open('wb') as output:shutil.copyfileobj(source,output)
print(f'Restored {len(expected)} unchanged media files; all 15 affected movies are rendered afresh.')
