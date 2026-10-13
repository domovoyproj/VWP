"""Reuse all unchanged 0.8.0 media after checking source inputs and ZIP SHA256."""
import hashlib,shutil,subprocess,tempfile,urllib.request,zipfile
from pathlib import Path

root=Path(__file__).resolve().parents[1]
baseline='acdf24a53fc0caa50e5518b14311995f91f80de9'
url='https://github.com/domovoyproj/VWP/releases/download/v0.8.0/VWP-0.8.0-win-x64.zip'
expected_hash='86937f5ea6bc6124f5d8415ca45024289271ecce8a3b6056ca35ddbd490288e2'
inputs=['assets/anime','assets/living','assets/cinematic','assets/vibes','assets/presets.json','assets/expansion.json']
diff=subprocess.run(['git','diff','--quiet',baseline,'--',*inputs],cwd=root)
if diff.returncode!=0:raise SystemExit('Baseline artwork/rendering inputs changed; render fresh masters.')
expected=[f'assets/{i}{suffix}' for i in range(18) for suffix in ('.mp4','.preview.mp4','.cover.jpg')]
expected += [f'assets/cinematic/{i}.mp4' for i in range(18,27)] # Rooftop Basketball is rendered afresh.
expected += [f'assets/living/{i}.{ext}' for i in range(1001,1004) for ext in ('mp4','png')]
expected += [f'assets/anime/{i}.mp4' for i in range(1101,1116)]
expected += [f'assets/vibes/{i}.{ext}' for i in range(1201,1221) for ext in ('mp4','poster.png','preview.mp4')]
with tempfile.TemporaryDirectory(prefix='vwp-season-baseline-') as folder:
    archive=Path(folder)/'source.zip';checksum=hashlib.sha256()
    with urllib.request.urlopen(url,timeout=120) as response,archive.open('wb') as output:
        while chunk:=response.read(1024*1024):checksum.update(chunk);output.write(chunk)
    if checksum.hexdigest()!=expected_hash:raise SystemExit('Official 0.8.0 portable checksum mismatch.')
    with zipfile.ZipFile(archive) as source:
        for relative in expected:
            entry=source.getinfo('VWP/'+relative)
            if entry.is_dir() or entry.file_size<1000:raise SystemExit('Invalid baseline media: '+relative)
            destination=root/relative;destination.parent.mkdir(parents=True,exist_ok=True)
            with source.open(entry) as original,destination.open('wb') as output:shutil.copyfileobj(original,output)
print(f'Restored {len(expected)} unchanged media files from SHA-256 verified official 0.8.0.')
