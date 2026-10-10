"""Reuse unchanged, verified 4K masters from the official 0.5.2 release."""
import hashlib
import urllib.request
import zipfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
URL='https://github.com/domovoyproj/VWP/releases/download/v0.5.2/VWP-0.5.2-win-x64.zip'
SHA256='e9971c6d358b119fa65c887b782e4026f2e4b014dac8fd8c8b792aa0f13d7f88'

def main():
    targets=[ROOT/'assets'/'cinematic'/f'{ident}.mp4' for ident in range(18,28)]
    if all(path.is_file() for path in targets):
        print('All cinematic masters are already present; run check_cinematic.py to validate them.')
        return
    folder=ROOT/'artifacts';folder.mkdir(exist_ok=True)
    archive=folder/'cinematic-source-0.5.2.zip'
    try:
        digest=hashlib.sha256()
        with urllib.request.urlopen(urllib.request.Request(URL,headers={'User-Agent':'VWP-Studio'}),timeout=90) as response, archive.open('wb') as output:
            while chunk:=response.read(4*1024*1024):digest.update(chunk);output.write(chunk)
        if digest.hexdigest()!=SHA256:raise RuntimeError('Official source archive SHA-256 mismatch')
        with zipfile.ZipFile(archive) as source:
            for target in targets:
                # Extract only fixed known media names; never extract the whole external archive.
                name='VWP/assets/cinematic/'+target.name
                target.parent.mkdir(parents=True,exist_ok=True)
                with source.open(name) as content,target.with_suffix('.tmp.mp4').open('wb') as output:
                    while chunk:=content.read(4*1024*1024):output.write(chunk)
                target.with_suffix('.tmp.mp4').replace(target)
        print('Restored 10 cinematic masters from official 0.5.2; archive SHA-256 verified.')
    finally:
        archive.unlink(missing_ok=True)

if __name__=='__main__':main()
