"""Build fifteen coordinated original themes and native Windows cursor packs."""
import json
from pathlib import Path
from PIL import Image
from make_cursors import ROLES, SIZES, drawing, cursor, animated

ROOT=Path(__file__).resolve().parents[1]
entries=json.loads((ROOT/'assets/anime/catalog.json').read_text(encoding='utf-8'))
assert len(entries)==15
themes_path=ROOT/'assets/themes/themes.json'
themes=[t for t in json.loads(themes_path.read_text(encoding='utf-8')) if not t['Id'].startswith('atelier-')]
palettes={}
for order,entry in enumerate(entries):
    ident='vwp-atelier-'+entry['style']
    folder=ROOT/'assets/cursors'/ident;folder.mkdir(parents=True,exist_ok=True)
    accent=entry['accent'];rgb=tuple(bytes.fromhex(accent[1:]))
    fill='#'+''.join(f'{round(c*.42+255*.58):02X}' for c in rgb)
    edge='#182130';shape=entry['shape']
    files={}
    for role in ROLES:
        name=role+('.ani' if role in ('Wait','AppStarting') else '.cur')
        frames=[cursor([drawing(role,size,fill,edge,accent,phase/24,shape) for size in SIZES]) for phase in range(24 if name.endswith('.ani') else 1)]
        (folder/name).write_bytes(animated(frames) if name.endswith('.ani') else frames[0]);files[role]=name
    drawing('Arrow',96,fill,edge,accent,shape=shape)[0].save(folder/'preview.png')
    sheet=Image.new('RGBA',(850,100))
    for index,role in enumerate(ROLES):sheet.alpha_composite(drawing(role,48,fill,edge,accent,shape=shape)[0],(index*50,25))
    sheet.save(folder/'sheet.png')
    pack=dict(Id=ident,Name=entry['cursor'],Author='VWP',License='CC0-1.0',Source='https://github.com/domovoyproj/VWP',Accent=accent,Order=30+order,Description=entry['name']+' · 17 ролей · 24–96 px · анимированная загрузка',Files=files)
    (folder/'pack.json').write_text(json.dumps(pack,ensure_ascii=False,indent=2),encoding='utf-8')
    (folder/'LICENSE.txt').write_text('Original VWP cursor artwork, dedicated to the public domain under CC0 1.0.\nhttps://creativecommons.org/publicdomain/zero/1.0/\nSource: tools/make_anime_styles.py and tools/make_cursors.py\n',encoding='utf-8')
    palette='Atelier-'+entry['style']
    palettes[palette]=['#'+''.join(f'{round(c*factor+base):02X}' for c in rgb) for factor,base in ((.045,10),(.055,14),(.065,18),(.09,24))]
    themes.append(dict(Id='atelier-'+entry['style'],Name=entry['name'],Description=entry['description']+' Согласованная палитра и курсоры '+entry['cursor']+'.',Author='VWP',License='VWP original artwork',Source='https://github.com/domovoyproj/VWP',Mode='Light' if entry['id'] in (1107,1110,1115) else 'Dark',Accent=accent,Palette=palette,Background='../anime/'+str(entry['id'])+'.png',CursorId=ident))
themes_path.write_text(json.dumps(themes,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(ROOT/'assets/themes/palettes.json').write_text(json.dumps(palettes,indent=2)+'\n',encoding='utf-8')
print('Generated 15 matching themes and 15 complete original cursor packs')
