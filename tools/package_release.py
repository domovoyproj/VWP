"""Package the self-contained x64 build; preserve its native libraries and runtime."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--version', default='0.3.1')
    parser.add_argument('--build', default='dist/Release-0.3.1')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    source = (root / args.build).resolve()
    if not (source / 'VWP.exe').is_file():
        raise SystemExit('Build VWP.exe before packaging.')
    output = root / 'dist' / f'VWP-{args.version}-win-x64.zip'
    count = 0
    with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted(source.rglob('*')):
            if not path.is_file():
                continue
            relative = path.relative_to(source)
            if relative.parts[0] == 'verification' or path.suffix in ('.pdb', '.log'):
                continue
            if relative.parts[:2] in (('libvlc', 'win-x86'), ('libvlc', 'win-arm64')):
                continue
            archive.write(path, Path('VWP') / relative)
            count += 1
        archive.write(root / 'README.md', 'VWP/README.md')
    checksum = hashlib.sha256(output.read_bytes()).hexdigest()
    output.with_suffix(output.suffix + '.sha256').write_text(f'{checksum}  {output.name}\n', encoding='utf-8')
    with zipfile.ZipFile(output) as archive:
        bad = archive.testzip()
        if bad:
            raise SystemExit(f'Archive check failed: {bad}')
        if 'VWP/VWP.exe' not in archive.namelist():
            raise SystemExit('VWP.exe is missing from archive.')
        required = [f'VWP/assets/scene-{scene_id}.png' for scene_id in range(18)]
        required += [f'VWP/assets/cinematic/{scene_id}.mp4' for scene_id in range(18, 28)]
        required += [f'VWP/assets/living/{scene_id}.{ext}' for scene_id in range(1001,1004) for ext in ('mp4','png')]
        required += ['VWP/assets/living/catalog.json','VWP/assets/city/index.html','VWP/assets/city/city.js','VWP/assets/city/vendor/three.core.js','VWP/assets/city/vendor/BufferGeometryUtils.js','VWP/assets/city/vendor/LICENSE.txt']
        required += ['VWP/assets/city/'+name for name in ('city.css','cover.png','vendor/three.module.js','vendor/Reflector.js','textures/apartment.png','textures/townhouse.png','textures/shop.png','textures/tree.png','textures/commuter.png','textures/office-color.jpg')]
        manifest = 'VWP/assets/city/resources.json'
        if manifest not in archive.namelist():
            raise SystemExit('City resource manifest is missing from archive.')
        required += ['VWP/assets/city/' + name for name in json.loads(archive.read(manifest))['files']]
        absent = [name for name in required if name not in archive.namelist()]
        if absent:
            raise SystemExit(f'Wallpaper media missing from archive: {absent[0]}')
        names=archive.namelist()
        if any(name.startswith(('VWP/assets/motion/','VWP/assets/spatial/')) for name in names):
            raise SystemExit('Retired procedural or low-poly assets are present in the package.')
        for name in ['themes/themes.json','themes/obsidian.png','themes/glacier.png','themes/pearl.png']:
            if 'VWP/assets/'+name not in names:raise SystemExit('Personalization asset missing: '+name)
        if len([name for name in names if name.startswith('VWP/assets/cursors/') and name.endswith('/pack.json')])!=14:
            raise SystemExit('Cursor catalog is incomplete.')
    print(f'{output.name}: {count} files, {output.stat().st_size:,} bytes, ZIP CRC verified')
    print(f'SHA256: {checksum}')

if __name__ == '__main__':
    main()
