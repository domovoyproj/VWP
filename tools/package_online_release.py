"""Create hash-verified portable volumes and an online install manifest below GitHub's asset limit."""
import argparse,hashlib,json,zipfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
LIMIT=1_500_000_000

def validate(source):
    required=['VWP.exe','VWP.dll','VWP.runtimeconfig.json','coreclr.dll','hostfxr.dll','tools/ffmpeg.exe','libvlc/win-x64/libvlc.dll','assets/presets.json','assets/themes/themes.json','assets/themes/palettes.json']
    for sid in range(18):required += [f'assets/{sid}{ext}' for ext in ('.mp4','.jpg','.cover.jpg','.preview.mp4')]+[f'assets/scene-{sid}.png']
    for sid in range(18,28):required += [f'assets/cinematic/{sid}.{ext}' for ext in ('png','mp4')]
    for folder,ids in [('living',range(1001,1004)),('anime',range(1101,1116)),('vibes',range(1201,1221)),('seasons',range(1301,1307))]:
        required += [f'assets/{folder}/catalog.json']
        for sid in ids:required += [f'assets/{folder}/{sid}.{ext}' for ext in (('png','mp4') if folder in ('living','anime') else ('png','mp4','poster.png','preview.mp4'))]
    for group in json.loads((ROOT/'tools/collections-plan.json').read_text(encoding='utf-8')):
        required += [f"assets/{group['folder']}/catalog.json"]
        for scene in group['entries']:required += [f"assets/{group['folder']}/{scene['id']}.{ext}" for ext in ('png','mp4','poster.png','preview.mp4')]
    city=source/'assets/city/resources.json'
    if not city.is_file():raise SystemExit('City resource manifest missing')
    required += ['assets/city/'+name for name in json.loads(city.read_text(encoding='utf-8-sig'))['files']]
    packs=list((source/'assets/cursors').glob('*/pack.json'))
    if len(packs)!=55:raise SystemExit('Incomplete cursor library')
    for pack in packs:
        data=json.loads(pack.read_text(encoding='utf-8-sig'));folder=pack.parent.relative_to(source)
        required += [str(folder/name) for name in data['Files'].values()]+[str(folder/'LICENSE.txt'),str(folder/'preview.png')]
    absent=[name for name in required if not (source/name).is_file()]
    if absent:raise SystemExit('Incomplete release: '+absent[0])
    if any((source/path).exists() for path in ('assets/motion','assets/spatial','assets/vibes/characters')):raise SystemExit('Retired scenery in release')
    return len(required)

def package(source,output,version,limit=LIMIT):
    files=[]
    for path in sorted(source.rglob('*')):
        if not path.is_file():continue
        relative=path.relative_to(source)
        if relative.parts[0]=='verification' or path.suffix in ('.pdb','.log'):continue
        if relative.parts[:2] in (('libvlc','win-x86'),('libvlc','win-arm64')):continue
        if path.stat().st_size>=limit:raise ValueError('Single file exceeds portable volume limit: '+str(relative))
        files.append((path,relative))
    parts=[];batch=[];size=0
    for item in files:
        length=item[0].stat().st_size
        if batch and size+length>limit:parts.append(batch);batch=[];size=0
        batch.append(item);size+=length
    if batch:parts.append(batch)
    output.mkdir(parents=True,exist_ok=True);manifest={'version':version,'parts':[]}
    for index,items in enumerate(parts,1):
        name=f'VWP-{version}-Portable-{index}.zip';path=output/name
        with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
            for file,relative in items:archive.write(file,relative.as_posix())
        with path.open('rb') as stream:digest=hashlib.file_digest(stream,'sha256').hexdigest()
        if path.stat().st_size>=2_000_000_000:raise ValueError('GitHub asset size limit exceeded')
        with zipfile.ZipFile(path) as archive:
            if archive.testzip():raise ValueError('Portable volume CRC failed')
        (output/(name+'.sha256')).write_text(digest+'  '+name+'\n',encoding='ascii')
        manifest['parts'].append({'name':name,'sha256':digest,'size':path.stat().st_size,'uncompressedSize':sum(file.stat().st_size for file,_ in items),'files':len(items)})
        print(f'{name}: {path.stat().st_size:,} bytes, CRC and SHA-256 verified',flush=True)
    (output/'install-parts.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    lines=['; Generated from verified portable archives.']
    for part in manifest['parts']:
        lines.append('Source: "{#ReleaseBaseURL}/'+part['name']+'"; DestDir: "{app}"; DestName: "'+part['name']+'"; ExternalSize: '+str(part['uncompressedSize'])+'; Hash: "'+part['sha256']+'"; Flags: external download extractarchive ignoreversion recursesubdirs createallsubdirs')
    (output/'installer-parts.iss').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    return manifest

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--version',required=True);parser.add_argument('--build',required=True);parser.add_argument('--validate-only',action='store_true');args=parser.parse_args()
    source=(ROOT/args.build).resolve();count=validate(source);print(f'Validated {count} required release files and 55 cursor packs.',flush=True)
    if args.validate_only:raise SystemExit()
    # Every volume extracts directly into the same destination, for both Setup and portable use.
    readme=f'VWP {args.version}\n\nУСТАНОВКА: скачайте VWP-{args.version}-Setup.exe. Установщик сам скачает и проверит все пакеты; нужен интернет.\n\nPORTABLE: скачайте ВСЕ файлы VWP-{args.version}-Portable-*.zip и распакуйте их в одну общую папку. Затем запустите VWP.exe.\n\nВключены .NET x64, все 151 записи обоев, 55 наборов курсоров и редактор живых сцен.\nНастройки пользователя сохраняются в %LOCALAPPDATA%/VWP.\n'
    (source/'README-INSTALL.txt').write_text(readme,encoding='utf-8-sig')
    (ROOT/'dist/README-INSTALL.txt').write_text(readme,encoding='utf-8-sig')
    package(source,ROOT/'dist',args.version)
