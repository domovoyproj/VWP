"""Exercise portable volume reconstruction, installer hashes and payload limits."""
import hashlib,json,random,tempfile,zipfile
from pathlib import Path
from package_online_release import package,validate

cache=Path(__file__).resolve().parents[1]/'artifacts';cache.mkdir(exist_ok=True)
with tempfile.TemporaryDirectory(prefix='vwp-volume-check-',dir=cache) as temporary:
    root=Path(temporary);source=root/'source';source.mkdir();rng=random.Random(10)
    expected={}
    for index in range(7):
        relative=f'assets/scene-{index}.bin';target=source/relative;target.parent.mkdir(parents=True,exist_ok=True)
        expected[relative]=rng.randbytes(600);target.write_bytes(expected[relative])
    result=package(source,root/'output','0.10.0',1400)
    assert len(result['parts'])==4
    restored={}
    for part in result['parts']:
        archive=root/'output'/part['name']
        assert hashlib.sha256(archive.read_bytes()).hexdigest()==part['sha256']
        with zipfile.ZipFile(archive) as z:
            assert sum(i.file_size for i in z.infolist())==part['uncompressedSize']
            for name in z.namelist():assert name not in restored;restored[name]=z.read(name)
    assert restored==expected
    text=(root/'output/installer-parts.iss').read_text()
    assert all(part['sha256'] in text and part['name'] in text for part in result['parts'])
    (source/'too-large.bin').write_bytes(bytes(1500))
    try:package(source,root/'rejected','0.10.0',1400)
    except ValueError:pass
    else:raise AssertionError('Oversized individual file was accepted')
    try:validate(root/'missing')
    except SystemExit:pass
    else:raise AssertionError('Incomplete application was accepted')
print('PASS: lossless portable reconstruction, volume limits, CRC, SHA-256, installer hash manifest and incomplete-layout rejection.')
