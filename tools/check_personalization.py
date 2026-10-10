"""Validate packaged Windows theme and cursor data before release."""
import json
import re
import struct
from pathlib import Path
from PIL import Image
from make_cursors import ROLES
root=Path(__file__).resolve().parents[1]/'assets'
themes=json.loads((root/'themes'/'themes.json').read_text(encoding='utf-8'))
assert len(themes)==13
for theme in themes:
    assert re.fullmatch(r'#[0-9A-Fa-f]{6}',theme['Accent']) and theme['Mode'] in ('Light','Dark')
    with Image.open(root/'themes'/theme['Background']) as picture:assert picture.width>=1280 and picture.height>=720
packs=list((root/'cursors').glob('*/pack.json'));assert len(packs)==14
assert len({theme['Id'] for theme in themes})==13
assert all(theme['CursorId'] in {json.loads(p.read_text(encoding='utf-8'))['Id'] for p in packs} for theme in themes)
for manifest in packs:
    pack=json.loads(manifest.read_text(encoding='utf-8'));assert set(pack['Files'])==set(ROLES)
    assert (manifest.parent/'LICENSE.txt').stat().st_size>30
    if pack['Author']!='VWP':assert (manifest.parent/'source.zip').stat().st_size>1000
    for role,name in pack['Files'].items():
        path=(manifest.parent/name).resolve();assert path.parent==manifest.parent.resolve()
        data=path.read_bytes();assert len(data)<8*1024*1024
        if path.suffix=='.cur':
            zero,kind,count=struct.unpack_from('<HHH',data);assert (zero,kind)==(0,2) and 0<count<100
            for i in range(count):
                w,h,_,_,x,y,length,offset=struct.unpack_from('<BBBBHHII',data,6+i*16)
                assert x<(w or 256) and y<(h or 256) and offset+length<=len(data)
        else:assert data[:4]==b'RIFF' and data[8:12]==b'ACON' and struct.unpack_from('<I',data,4)[0]+8==len(data)
print('PASS: 13 themes, 14 packs, 238 native cursor files, licenses and corresponding sources')
