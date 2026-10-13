"""Generate twenty coordinated original desktop themes and native cursor schemes."""
import json
from pathlib import Path
from PIL import Image, ImageDraw
from make_cursors import ROLES, SIZES, drawing, cursor, animated

ROOT=Path(__file__).resolve().parents[1]
# Each pointer has its own outline, with a stable, precise upper-left hotspot.
OUTLINES=[
 [(4,3),(5,26),(11,21),(16,29),(20,27),(15,19),(25,17),(19,13)],
 [(4,3),(7,25),(12,19),(20,29),(23,26),(15,16),(27,13),(17,12)],
 [(4,3),(4,25),(10,20),(15,29),(20,26),(14,18),(25,16),(20,10)],
 [(4,3),(6,27),(11,20),(18,29),(22,27),(15,17),(23,15),(17,10)],
 [(4,3),(5,25),(11,20),(17,29),(21,26),(14,17),(26,17),(20,12)],
 [(4,3),(7,25),(12,21),(18,30),(22,27),(15,18),(26,14),(15,11)],
 [(4,3),(4,26),(10,22),(16,30),(21,27),(14,18),(24,17),(21,12)],
 [(4,3),(5,24),(11,19),(20,29),(24,25),(16,17),(26,16),(18,10)],
 [(4,3),(4,27),(9,22),(17,30),(21,26),(14,19),(25,18),(25,14)],
 [(4,3),(4,25),(9,25),(12,20),(19,29),(23,25),(16,17),(25,17),(25,13)],
 [(4,3),(6,24),(11,20),(16,29),(21,27),(15,18),(24,15),(17,12)],
 [(4,3),(5,27),(12,19),(20,28),(23,25),(16,15),(27,12),(16,11)],
 [(4,3),(7,26),(13,20),(22,27),(25,23),(17,16),(25,12),(15,10)],
 [(4,3),(4,26),(11,20),(17,29),(22,26),(15,17),(26,17),(22,11)],
 [(4,3),(6,25),(12,21),(18,29),(22,26),(16,18),(26,15),(15,10)],
 [(4,3),(8,26),(12,20),(19,29),(23,25),(15,17),(27,15),(16,12)],
 [(4,3),(4,26),(11,19),(18,28),(22,24),(15,16),(26,16),(20,10)],
 [(4,3),(5,25),(12,20),(20,29),(24,25),(16,16),(27,13),(16,10)],
 [(4,3),(8,25),(12,19),(22,29),(25,26),(16,15),(23,12),(16,10)],
 [(4,3),(5,26),(12,21),(17,30),(22,27),(15,18),(26,15),(17,11)]
]
FAMILIES=['classic','comet','petal','blade','fin','comet','petal','tech','neon','tech','petal','fin','comet','comet','petal','neon','tech','crystal','blade','petal']

def styled(role,size,fill,edge,accent,order,phase=0):
    family=FAMILIES[order]
    if role not in ('Arrow','Help','AppStarting'):return drawing(role,size,fill,edge,accent,phase,family)
    image=Image.new('RGBA',(size*4,size*4));d=ImageDraw.Draw(image);scale=size*4/32
    outline=[(x*scale,y*scale) for x,y in OUTLINES[order]]
    d.polygon(outline,fill=fill);d.line(outline+[outline[0]],fill=edge,width=round(1.35*scale),joint='curve')
    # Motifs follow the theme: inlay, star, petal, needle, leaf, kite, lantern,
    # chrome, cassette, arcade, fern, feather, dune, orbit, flower, ribbon,
    # concrete, glass, quill and lotus. These remain readable at 24 px.
    motif=order%5
    if motif==0:d.line([(8*scale,11*scale),(8*scale,18*scale),(12*scale,15*scale)],fill=accent,width=round(1.3*scale))
    elif motif==1:d.polygon([(11*scale,10*scale),(12*scale,13*scale),(15*scale,14*scale),(12*scale,15*scale),(11*scale,18*scale),(10*scale,15*scale),(7*scale,14*scale),(10*scale,13*scale)],fill=accent)
    elif motif==2:d.ellipse((9*scale,11*scale,13*scale,17*scale),fill=accent)
    elif motif==3:d.line([(8*scale,9*scale),(16*scale,22*scale)],fill=accent,width=round(1.1*scale))
    else:d.arc((7*scale,9*scale,16*scale,18*scale),10,285,fill=accent,width=round(1.2*scale))
    image=image.resize((size,size),Image.Resampling.LANCZOS)
    if role!='Arrow':
        # Reuse the native role indicator, keeping only the separate badge area.
        indicator=drawing(role,size,fill,edge,accent,phase,family)[0]
        mask=Image.new('L',(size,size));md=ImageDraw.Draw(mask)
        md.rectangle((round(18*size/32),0,size,round(14*size/32)),fill=255) if role=='Help' else md.rectangle((round(17*size/32),round(17*size/32),size,size),fill=255)
        image=Image.composite(Image.alpha_composite(image,indicator),image,mask)
    return image,(round(4*size/32),round(3*size/32))

def main():
    entries=json.loads((ROOT/'assets/vibes/catalog.json').read_text(encoding='utf-8'));assert len(entries)==20
    path=ROOT/'assets/themes/themes.json'
    themes=[t for t in json.loads(path.read_text(encoding='utf-8')) if not t['Id'].startswith('vibe-')]
    palettes=json.loads((ROOT/'assets/themes/palettes.json').read_text(encoding='utf-8'))
    for order,e in enumerate(entries):
        ident='vwp-vibe-'+e['style'];folder=ROOT/'assets/cursors'/ident;folder.mkdir(parents=True,exist_ok=True)
        rgb=tuple(bytes.fromhex(e['accent'][1:]));fill='#'+''.join(f'{round(c*.32+255*.68):02X}' for c in rgb);edge='#1A2030';files={}
        for role in ROLES:
            name=role+('.ani' if role in ('Wait','AppStarting') else '.cur')
            frames=[cursor([styled(role,size,fill,edge,e['accent'],order,phase/24) for size in SIZES]) for phase in range(24 if name.endswith('.ani') else 1)]
            (folder/name).write_bytes(animated(frames) if name.endswith('.ani') else frames[0]);files[role]=name
        styled('Arrow',96,fill,edge,e['accent'],order)[0].save(folder/'preview.png')
        sheet=Image.new('RGBA',(850,100))
        for n,role in enumerate(ROLES):sheet.alpha_composite(styled(role,48,fill,edge,e['accent'],order)[0],(n*50,25))
        sheet.save(folder/'sheet.png')
        pack=dict(Id=ident,Name=e['cursor'],Author='VWP',License='CC0-1.0',Source='https://github.com/domovoyproj/VWP',Accent=e['accent'],Order=50+order,Description=e['name']+' · 17 ролей · 24–96 px · анимированная загрузка',Files=files)
        (folder/'pack.json').write_text(json.dumps(pack,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        (folder/'LICENSE.txt').write_text('Original VWP cursor artwork, dedicated under CC0 1.0.\nhttps://creativecommons.org/publicdomain/zero/1.0/\nSource: tools/make_vibe_styles.py and tools/make_cursors.py\n',encoding='utf-8')
        palette='Vibe-'+e['style']
        if e['mode']=='Light':palettes[palette]=['#'+''.join(f'{round(c*factor+255*(1-factor)):02X}' for c in rgb) for factor in (.08,.13,.025,.20)]
        else:palettes[palette]=['#'+''.join(f'{round(c*factor+base):02X}' for c in rgb) for factor,base in ((.045,10),(.055,14),(.065,18),(.09,24))]
        themes.append(dict(Id='vibe-'+e['style'],Name=e['name'],Description=e['description']+' Согласованные цвета и курсоры '+e['cursor']+'.',Author='VWP',License='VWP original artwork',Source='https://github.com/domovoyproj/VWP',Mode=e['mode'],Accent=e['accent'],Palette=palette,Background='../vibes/'+str(e['id'])+'.poster.png',CursorId=ident))
    path.write_text(json.dumps(themes,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (ROOT/'assets/themes/palettes.json').write_text(json.dumps(palettes,indent=2)+'\n',encoding='utf-8')
    print('Generated 20 coordinated themes and 20 complete native cursor packs')
if __name__=='__main__':main()
