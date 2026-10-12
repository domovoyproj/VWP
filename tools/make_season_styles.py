"""Six original seasonal Windows themes and multiresolution cursor schemes."""
import json, math
from pathlib import Path
from PIL import Image, ImageDraw
from make_cursors import ROLES, SIZES, drawing, cursor, animated

ROOT=Path(__file__).resolve().parents[1]
OUTLINES=[
 [(4,3),(5,26),(11,21),(16,29),(20,27),(15,19),(25,17),(18,11)],
 [(4,3),(6,25),(12,20),(19,29),(23,26),(15,17),(26,14),(17,12)],
 [(4,3),(7,26),(12,20),(18,29),(22,26),(15,17),(25,15),(18,10)],
 [(4,3),(4,26),(10,21),(16,29),(21,26),(14,18),(25,17),(22,11)],
 [(4,3),(7,25),(12,20),(20,28),(23,25),(16,16),(26,13),(17,11)],
 [(4,3),(5,26),(11,21),(17,29),(22,26),(15,18),(26,15),(19,11)]
]

def pointer(role,size,fill,accent,order,phase=0):
    family='petal' if order<3 else 'crystal'
    if role not in ('Arrow','Help','AppStarting'):
        return drawing(role,size,fill,'#172432',accent,phase,family)
    scale=size*4/32;image=Image.new('RGBA',(size*4,size*4));d=ImageDraw.Draw(image)
    outline=[(x*scale,y*scale) for x,y in OUTLINES[order]]
    d.polygon(outline,fill=fill);d.line(outline+[outline[0]],fill='#172432',width=round(1.35*scale),joint='curve')
    if order<3:
        # A readable maple/fern inlay that never covers the tip/hotspot.
        leaf=[(11,10),(12,13),(15,12),(14,15),(16,17),(12,17),(11,20),(10,17),(7,17),(9,15),(7,12),(10,13)]
        d.polygon([(x*scale,y*scale) for x,y in leaf],fill=accent)
        d.line([(11*scale,13*scale),(11*scale,20*scale)],fill='#172432',width=max(1,round(.5*scale)))
    else:
        for n in range(3):
            a=n*math.pi/3;dx=math.cos(a)*4*scale;dy=math.sin(a)*4*scale
            d.line([(11*scale-dx,15*scale-dy),(11*scale+dx,15*scale+dy)],fill=accent,width=round(1.1*scale))
        d.ellipse((9.5*scale,13.5*scale,12.5*scale,16.5*scale),fill='#FFFFFF')
    image=image.resize((size,size),Image.Resampling.LANCZOS)
    if role!='Arrow':
        indicator=drawing(role,size,fill,'#172432',accent,phase,family)[0]
        mask=Image.new('L',(size,size));md=ImageDraw.Draw(mask)
        if role=='Help':md.rectangle((round(18*size/32),0,size,round(14*size/32)),fill=255)
        else:md.rectangle((round(17*size/32),round(17*size/32),size,size),fill=255)
        image=Image.composite(Image.alpha_composite(image,indicator),image,mask)
    return image,(round(4*size/32),round(3*size/32))

def main():
    entries=json.loads((ROOT/'assets/seasons/catalog.json').read_text(encoding='utf-8'))
    assert len(entries)==6
    path=ROOT/'assets/themes/themes.json'
    themes=[t for t in json.loads(path.read_text(encoding='utf-8')) if not t['Id'].startswith('season-')]
    palettes=json.loads((ROOT/'assets/themes/palettes.json').read_text(encoding='utf-8'))
    for order,e in enumerate(entries):
        ident='vwp-season-'+e['style'];folder=ROOT/'assets/cursors'/ident;folder.mkdir(parents=True,exist_ok=True)
        rgb=tuple(bytes.fromhex(e['accent'][1:]));fill='#'+''.join(f'{round(c*.25+255*.75):02X}' for c in rgb);files={}
        for role in ROLES:
            name=role+('.ani' if role in ('Wait','AppStarting') else '.cur')
            frames=[cursor([pointer(role,size,fill,e['accent'],order,phase/24) for size in SIZES]) for phase in range(24 if name.endswith('.ani') else 1)]
            (folder/name).write_bytes(animated(frames) if name.endswith('.ani') else frames[0]);files[role]=name
        pointer('Arrow',96,fill,e['accent'],order)[0].save(folder/'preview.png')
        sheet=Image.new('RGBA',(850,100))
        for n,role in enumerate(ROLES):sheet.alpha_composite(pointer(role,48,fill,e['accent'],order)[0],(n*50,25))
        sheet.save(folder/'sheet.png')
        pack=dict(Id=ident,Name=e['cursor'],Author='VWP',License='CC0-1.0',Source='https://github.com/domovoyproj/VWP',Accent=e['accent'],Order=80+order,Description=e['season']+' · 17 ролей · 24–96 px · анимированная загрузка',Files=files)
        (folder/'pack.json').write_text(json.dumps(pack,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        (folder/'LICENSE.txt').write_text('Original VWP cursor artwork, dedicated under CC0 1.0.\nhttps://creativecommons.org/publicdomain/zero/1.0/\nSource: tools/make_season_styles.py and tools/make_cursors.py\n',encoding='utf-8')
        palette='Season-'+e['style']
        if e['mode']=='Light':palettes[palette]=['#'+''.join(f'{round(c*factor+255*(1-factor)):02X}' for c in rgb) for factor in (.08,.13,.025,.20)]
        else:palettes[palette]=['#'+''.join(f'{round(c*factor+base):02X}' for c in rgb) for factor,base in ((.045,10),(.055,14),(.065,18),(.09,24))]
        themes.append(dict(Id='season-'+e['style'],Name=e['name'],Description=e['description']+' Тема и курсоры '+e['cursor']+'.',Author='VWP',License='VWP original artwork · AI-assisted',Source='https://github.com/domovoyproj/VWP',Mode=e['mode'],Accent=e['accent'],Palette=palette,Background='../seasons/'+str(e['id'])+'.poster.png',CursorId=ident))
    path.write_text(json.dumps(themes,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (ROOT/'assets/themes/palettes.json').write_text(json.dumps(palettes,indent=2)+'\n',encoding='utf-8')
    print('Generated 6 seasonal themes and 6 complete native cursor packs')

if __name__=='__main__':main()
