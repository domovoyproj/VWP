"""Package the self-contained x64 build; preserve its native libraries and runtime."""
import argparse
import hashlib
from pathlib import Path
import zipfile

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--version', default='0.3.0')
    parser.add_argument('--build', default='dist/Release-0.3.0')
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
            if relative.parts[0] == 'assets' and path.name.startswith('scene-') and path.suffix == '.png':
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
    print(f'{output.name}: {count} files, {output.stat().st_size:,} bytes, ZIP CRC verified')
    print(f'SHA256: {checksum}')

if __name__ == '__main__':
    main()
