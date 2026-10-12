"""Restore unchanged, checksum-pinned original masters without rerendering them."""
import hashlib
import shutil
import subprocess
import tempfile
import urllib.request
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
baseline = '99e67aead9ad72f5ff3ca0cf05eff904710fbb48'
url = 'https://github.com/domovoyproj/VWP/releases/download/v0.7.0/VWP-0.7.0-win-x64.zip'
expected_hash = '73cab2fecb109bdbf866f4daeeee89864acabcce6aa392e67f9fea1d272a0eee'
inputs = ['assets/anime', 'assets/living', 'assets/cinematic', 'assets/presets.json', 'assets/expansion.json']
if subprocess.run(['git', 'diff', '--quiet', baseline, '--', *inputs], cwd=root).returncode:
    raise SystemExit('Artwork or video renderer changed: masters must be rendered afresh.')
expected = [f'assets/{i}{suffix}' for i in range(18) for suffix in ('.mp4', '.preview.mp4', '.cover.jpg')]
expected += [f'assets/cinematic/{i}.mp4' for i in range(18,28)]
expected += [f'assets/living/{i}.{extension}' for i in range(1001,1004) for extension in ('mp4','png')]
expected += [f'assets/anime/{i}.mp4' for i in range(1101,1116)]
with tempfile.TemporaryDirectory(prefix='vwp-hotfix-media-') as folder:
    archive = Path(folder) / 'source.zip'
    checksum = hashlib.sha256()
    with urllib.request.urlopen(url, timeout=120) as response, archive.open('wb') as output:
        while chunk := response.read(1024*1024):
            checksum.update(chunk)
            output.write(chunk)
    if checksum.hexdigest() != expected_hash:
        raise SystemExit('Official 0.7.0 portable checksum mismatch.')
    with zipfile.ZipFile(archive) as source:
        for relative in expected:
            entry = source.getinfo('VWP/'+relative)
            if entry.is_dir() or entry.file_size < 1000:
                raise SystemExit(f'Invalid media entry: {relative}')
            destination = root / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            with source.open(entry) as original, destination.open('wb') as output:
                shutil.copyfileobj(original, output)
print(f'Restored {len(expected)} media files from SHA-256 verified official 0.7.0; artwork inputs unchanged.')
