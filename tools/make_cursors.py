"""Generate original multiresolution Windows cursor packs; no external renderer needed."""
import io
import json
import math
import struct
from pathlib import Path
from PIL import Image, ImageDraw
from gaming_styles import GAMING_STYLES

ROOT = Path(__file__).resolve().parents[1] / 'assets' / 'cursors'
ROLES = ['Arrow','Help','AppStarting','Wait','Crosshair','IBeam','NWPen','No','SizeNS','SizeWE','SizeNWSE','SizeNESW','SizeAll','UpArrow','Hand','Pin','Person']
SIZES = [24,32,48,64,96]

def drawing(role, size, fill, edge, accent, phase=0, shape='classic'):
    scale = size*4/32
    image = Image.new('RGBA', (size*4,size*4))
    d = ImageDraw.Draw(image)
    def points(p): return [(x*scale,y*scale) for x,y in p]
    def polygon(p, color=fill):
        p=points(p);d.polygon(p,fill=color);d.line(p+[p[0]],fill=edge,width=max(1,round(1.35*scale)),joint='curve')
    def line(p, color=fill, width=2):
        p=points(p);d.line(p,fill=edge,width=round((width+2)*scale),joint='curve');d.line(p,fill=color,width=round(width*scale),joint='curve')
    def circle(box,color):d.ellipse(tuple(v*scale for v in box),fill=color)
    hot=(16,16)
    if role in ('Arrow','Help','AppStarting'):
        arrows={
            'classic':[(4,3),(4,26),(10,21),(15,30),(19,28),(14,19),(23,18)],
            'petal':[(4,3),(6,25),(12,21),(17,30),(21,28),(15,18),(25,15),(14,11)],
            'neon':[(4,3),(4,27),(10,21),(16,29),(20,26),(14,18),(26,17)],
            'blade':[(4,3),(8,22),(12,18),(22,29),(25,26),(15,15),(21,11)],
            'comet':[(4,3),(8,26),(14,19),(23,24),(25,20),(16,14),(26,10)],
            'fin':[(4,3),(4,27),(11,20),(17,30),(22,27),(15,18),(26,17),(19,11)],
            'tech':[(4,3),(4,25),(10,19),(17,28),(21,24),(15,18),(25,18),(25,14)],
            'crystal':[(4,3),(6,26),(12,19),(19,28),(22,25),(16,16),(27,14)],
            'oni':[(4,3),(5,27),(12,20),(18,30),(23,26),(16,18),(27,17),(17,10)]}
        polygon(arrows[shape])
        if shape=='neon':line([(7,10),(7,20),(10,17)],'#F877B8',.7)
        if shape in ('blade','oni'):line([(7,8),(13,15),(21,25)],accent,.6)
        if shape=='crystal':line([(7,8),(12,15),(23,14)],'#70E8C3',.6)
        if shape=='tech':line([(9,11),(13,14)],accent,.8)
        if shape=='petal':circle((9,11,12,15),accent)
        if shape in ('comet','fin'):circle((10,12,13,15),accent)
        hot=(4,3)
        if role=='Help':
            circle((19,2,30,13),edge);circle((20,3,29,12),accent)
            d.text((23*scale,2.5*scale),'?',fill=edge,stroke_width=0,font_size=round(8*scale))
    if role in ('Wait','AppStarting'):
        cx,cy,r = (16,16,10) if role=='Wait' else (24,25,6)
        count=5 if shape=='petal' else 6 if shape in ('crystal','tech') else 12
        for i in range(count):
            a=(i/count*2*math.pi)+phase*2*math.pi
            alpha=round(60+195*i/max(1,count-1))
            color=tuple(bytes.fromhex(accent.lstrip('#')))+(alpha,)
            x,y=cx+r*math.cos(a),cy+r*math.sin(a)
            if shape=='petal':
                d.ellipse(tuple(v*scale for v in (x-2,y-3,x+2,y+3)),fill=color)
            elif shape=='crystal':
                line([(cx+(r-4)*math.cos(a),cy+(r-4)*math.sin(a)),(x,y)],color,.8)
            elif shape in ('neon','tech','blade','oni'):
                a2=a+.22;line([(x,y),(cx+r*math.cos(a2),cy+r*math.sin(a2))],color,1.1)
            elif shape=='comet':
                y=cy+r*.65*math.sin(a);circle((x-1.4,y-1.4,x+1.4,y+1.4),color)
            else:circle((x-1.8,y-1.8,x+1.8,y+1.8),color)
        if shape=='comet':circle((cx-3,cy-3,cx+3,cy+3),fill)
    elif role=='Hand':
        polygon([(10,15),(10,5),(11,3),(13,3),(15,5),(15,14),(17,12),(20,13),(23,14),(26,17),(25,27),(22,30),(14,30),(7,22),(5,18),(6,16),(8,17),(10,19)])
        line([(16,16),(16,22)],fill,.7);line([(20,17),(20,23)],fill,.7);hot=(12,3)
    elif role=='IBeam':
        line([(11,5),(16,5),(21,5)]);line([(16,5),(16,27)]);line([(11,27),(21,27)])
    elif role=='Crosshair':
        if shape in ('neon','tech','blade','oni'):
            for p in [[(6,12),(6,6),(12,6)],[(20,6),(26,6),(26,12)],[(26,20),(26,26),(20,26)],[(12,26),(6,26),(6,20)]]:line(p,accent,.9)
            circle((15,15,17,17),accent)
        else:line([(16,3),(16,29)],accent,1);line([(3,16),(29,16)],accent,1)
    elif role=='NWPen':
        polygon([(6,26),(9,18),(22,4),(28,10),(14,24)]);hot=(6,26)
    elif role=='No':
        d.ellipse(tuple(v*scale for v in (5,5,27,27)),outline=edge,width=round(5*scale))
        d.ellipse(tuple(v*scale for v in (6,6,26,26)),outline=accent,width=round(3*scale));line([(9,9),(23,23)],accent,2)
    elif role in ('SizeNS','SizeWE','SizeNWSE','SizeNESW','SizeAll','UpArrow'):
        polygon([(16,3),(10,10),(14,10),(14,22),(10,22),(16,29),(22,22),(18,22),(18,10),(22,10)])
        if role in ('SizeWE','SizeNWSE','SizeNESW'):image=image.rotate({'SizeWE':90,'SizeNWSE':45,'SizeNESW':-45}[role],Image.Resampling.BICUBIC)
        if role=='SizeAll':image=Image.alpha_composite(image,image.rotate(90))
        if role=='UpArrow':hot=(16,3)
    elif role=='Pin':
        polygon([(16,30),(6,17),(5,11),(8,5),(16,2),(24,5),(27,11),(26,17)]);circle((12,8,20,16),edge);hot=(16,30)
    elif role=='Person':
        circle((10,3,22,15),edge);circle((11,4,21,14),fill)
        polygon([(5,29),(6,23),(11,18),(21,18),(26,23),(27,29)]);hot=(16,16)
    image=image.resize((size,size),Image.Resampling.LANCZOS)
    return image,tuple(round(v*size/32) for v in hot)

def cursor(images):
    payloads=[]
    for im,hot in images:
        w,h=im.size
        # DIB: bottom-up premultiplied-free BGRA plus the 1-bit transparency mask.
        pixels=b''.join(im.crop((0,y,w,y+1)).tobytes('raw','BGRA') for y in range(h-1,-1,-1))
        stride=((w+31)//32)*4
        mask=bytearray(stride*h)
        for y in range(h):
            for x in range(w):
                if im.getpixel((x,h-1-y))[3]==0:mask[y*stride+x//8]|=0x80>>(x%8)
        dib=struct.pack('<IiiHHIIiiII',40,w,h*2,1,32,0,len(pixels),0,0,0,0)+pixels+mask
        payloads.append((w,h,hot,dib))
    result=bytearray(struct.pack('<HHH',0,2,len(images)));offset=6+16*len(images)
    for w,h,hot,dib in payloads:
        result+=struct.pack('<BBBBHHII',w,h,0,0,*hot,len(dib),offset);offset+=len(dib)
    return bytes(result)+b''.join(p[3] for p in payloads)

def chunk(tag,data):return tag+struct.pack('<I',len(data))+data+(b'\0' if len(data)%2 else b'')
def animated(frames):
    header=struct.pack('<9I',36,len(frames),len(frames),0,0,32,1,3,1)
    body=b'ACON'+chunk(b'anih',header)+chunk(b'LIST',b'fram'+b''.join(chunk(b'icon',frame) for frame in frames))
    return b'RIFF'+struct.pack('<I',len(body))+body

def main():
    packs=[('vwp-'+s['id'],s['cursor'],s['fill'],s['edge'],s['accent'],s['shape'],i) for i,s in enumerate(GAMING_STYLES)]
    packs += [
        ('vwp-pearl','VWP Pearl','#FAFAFD','#1B2334','#7388EB','classic',20),
        ('vwp-obsidian','VWP Obsidian','#22242B','#EAD4A9','#E0B980','classic',21),
        ('vwp-rose','VWP Rose','#F8CDD9','#452B43','#EF759C','classic',22)]
    for ident,name,fill,edge,accent,shape,order in packs:
        folder=ROOT/ident;folder.mkdir(parents=True,exist_ok=True)
        files={}
        for role in ROLES:
            namefile=role+('.ani' if role in ('Wait','AppStarting') else '.cur')
            frames=[cursor([drawing(role,size,fill,edge,accent,i/24,shape) for size in SIZES]) for i in range(24 if namefile.endswith('.ani') else 1)]
            (folder/namefile).write_bytes(animated(frames) if namefile.endswith('.ani') else frames[0]);files[role]=namefile
        drawing('Arrow',96,fill,edge,accent,shape=shape)[0].save(folder/'preview.png')
        sheet=Image.new('RGBA',(850,100))
        for i,role in enumerate(ROLES):sheet.alpha_composite(drawing(role,48,fill,edge,accent,shape=shape)[0],(i*50,25))
        sheet.save(folder/'sheet.png')
        (folder/'pack.json').write_text(json.dumps(dict(Id=ident,Name=name,Author='VWP',License='CC0-1.0',Source='https://github.com/domovoyproj/VWP',Accent=accent,Order=order,Description=('Аниме / gaming' if shape!='classic' else 'Оригинальный набор')+' · 17 ролей · 24–96 px · анимированная загрузка',Files=files),ensure_ascii=False,indent=2),encoding='utf-8')
        (folder/'LICENSE.txt').write_text('Original VWP cursor artwork, dedicated to the public domain under CC0 1.0.\nhttps://creativecommons.org/publicdomain/zero/1.0/\nSource: tools/make_cursors.py\n',encoding='utf-8')
    print(f'Generated {len(packs)} original packs, 17 roles each, 5 resolutions, animated busy cursors.')
if __name__=='__main__':main()
