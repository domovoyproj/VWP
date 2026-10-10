"""Vendor pinned open cursor packs as data only. INF/BAT installers are never executed."""
import io
import json
import struct
import urllib.request
import zipfile
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'assets'/'cursors'
roles=['Arrow','Help','AppStarting','Wait','Crosshair','IBeam','NWPen','No','SizeNS','SizeWE','SizeNWSE','SizeNESW','SizeAll','UpArrow','Hand','Pin','Person']
names=['pointer.cur','help.cur','work.ani','busy.ani','cross.cur','text.cur','handwriting.cur','unavailable.cur','vert.cur','horz.cur','dgn1.cur','dgn2.cur','move.cur','alternate.cur','link.cur','pin.cur','person.cur']
packs=[('bibata-ice','Bibata Modern Ice','ful1e5/Bibata_Cursor','v2.0.7','Bibata-Modern-Ice-Windows.zip','Bibata-Modern-Ice-Regular-Windows', 'GPL-3.0','#BAC8FF'),
       ('bibata-classic','Bibata Modern Classic','ful1e5/Bibata_Cursor','v2.0.7','Bibata-Modern-Classic-Windows.zip','Bibata-Modern-Classic-Regular-Windows','GPL-3.0','#818B9C'),
       ('capitaine-dark','Capitaine Dark','hervad/capitaine-cursors-w11-hidpi','v3.1.0','capitaine-dark-w11-hidpi-v3.1.0.zip','Capitaine Dark W11 HiDPI','LGPL-3.0-or-later','#D5D9E2')]
def fetch(url):
    with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'VWP'}),timeout=90) as response:return response.read()
def cursor_image(path):
    data=path.read_bytes();count=struct.unpack_from('<H',data,4)[0]
    entries=[data[6+i*16:22+i*16] for i in range(count)]
    entry=max(entries,key=lambda e:e[0] or 256)
    length,offset=struct.unpack_from('<II',entry,8);payload=data[offset:offset+length]
    if payload.startswith(b'\x89PNG'):return Image.open(io.BytesIO(payload)).convert('RGBA')
    return Image.open(io.BytesIO(struct.pack('<HHH',0,2,1)+entry[:12]+struct.pack('<I',22)+payload)).convert('RGBA')
def main():
    sources=OUT/'sources';sources.mkdir(parents=True,exist_ok=True)
    for ident,title,repo,tag,asset,subdir,license,accent in packs:
        folder=OUT/ident;folder.mkdir(parents=True,exist_ok=True)
        archive=zipfile.ZipFile(io.BytesIO(fetch(f'https://github.com/{repo}/releases/download/{tag}/{asset}')))
        mapping=dict(zip(roles,names))
        if ident=='capitaine-dark':mapping.update(AppStarting='working.ani',Crosshair='precision.cur')
        files={}
        for role,name in mapping.items():
            found=next(n for n in archive.namelist() if n.lower()==f'{subdir}/{name}'.lower())
            (folder/name).write_bytes(archive.read(found));files[role]=name
        image=cursor_image(folder/mapping['Arrow']);image.resize((96,96),Image.Resampling.LANCZOS).save(folder/'preview.png')
        sheet=Image.new('RGBA',(850,100))
        for i,role in enumerate(roles):
            if mapping[role].endswith('.cur'):
                image=cursor_image(folder/mapping[role]).resize((48,48),Image.Resampling.LANCZOS);sheet.alpha_composite(image,(i*50,25))
        sheet.save(folder/'sheet.png')
        (folder/'pack.json').write_text(json.dumps(dict(Id=ident,Name=title,Author='Abdulkaiz Khatri' if ident.startswith('bibata') else 'Keefer Rourke · hervad',License=license,Source=f'https://github.com/{repo}/tree/{tag}',SourceArchive='source.zip',Accent=accent,Description='Открытый набор · 17 ролей · анимированная загрузка'+(' · HiDPI' if ident=='capitaine-dark' else ' · 32–128 px'),Files=files),ensure_ascii=False,indent=2),encoding='utf-8')
        legal=fetch(f'https://raw.githubusercontent.com/{repo}/{tag}/'+('LICENSE' if ident.startswith('bibata') else 'COPYING'))
        (folder/'LICENSE.txt').write_text('\n'.join(line.rstrip() for line in legal.decode('utf-8').splitlines())+'\n',encoding='utf-8')
        source=sources/(repo.split('/')[-1]+'-'+tag+'.zip')
        if not source.exists():source.write_bytes(fetch(f'https://codeload.github.com/{repo}/zip/refs/tags/{tag}'))
        if ident=='capitaine-dark':
            tree=json.loads(fetch(f'https://api.github.com/repos/{repo}/git/trees/{tag}'))
            sha=next(e['sha'] for e in tree['tree'] if e['path']=='upstream')
            with zipfile.ZipFile(folder/'source.zip','w') as complete:
                complete.writestr('windows-port.zip',source.read_bytes())
                complete.writestr('upstream-artwork.zip',fetch(f'https://codeload.github.com/keeferrourke/capitaine-cursors/zip/{sha}'))
                complete.writestr('SOURCE.txt',f'Windows port: {repo} {tag}\nArtwork: keeferrourke/capitaine-cursors {sha}\n')
        else:(folder/'source.zip').write_bytes(source.read_bytes())
    print('Vendored 3 cursor packs with licenses and corresponding source archives.')
if __name__=='__main__':main()
