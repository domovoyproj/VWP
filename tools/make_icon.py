"""Render VWP's original vector mark into a Windows multi-resolution ICO."""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]/'assets'

def main():
    size=1024
    background=Image.new('RGBA',(size,size))
    draw=ImageDraw.Draw(background)
    for y in range(size):
        t=y/(size-1)
        draw.line((0,y,size,y),fill=(int(37-16*t),int(39-17*t),int(57-23*t),255))
    rounded=Image.new('L',(size,size));ImageDraw.Draw(rounded).rounded_rectangle((0,0,1023,1023),radius=210,fill=255)
    background.putalpha(rounded)
    mask=Image.new('L',(size,size));ImageDraw.Draw(mask).polygon([(228,237),(378,237),(514,592),(650,237),(799,237),(592,787),(437,787)],fill=255)
    ribbon=Image.new('RGBA',(size,size));ink=ImageDraw.Draw(ribbon)
    for y in range(size):
        t=min(1,max(0,(y-230)/560))
        ink.line((0,y,size,y),fill=(int(244-85*t),int(240-95*t),int(255-17*t),255))
    background.paste(ribbon,(0,0),mask)
    ImageDraw.Draw(background).polygon([(471,316),(564,370),(471,424)],fill=(244,240,255,255))
    background.resize((256,256),Image.Resampling.LANCZOS).save(ROOT/'app.png')
    background.save(ROOT/'app.ico',format='ICO',sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
    svg='''<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024"><defs><linearGradient id="tile" x2="0" y2="1"><stop stop-color="#252739"/><stop offset="1" stop-color="#151622"/></linearGradient><linearGradient id="ribbon" x2="0" y2="1"><stop stop-color="#F4F0FF"/><stop offset="1" stop-color="#9F91EE"/></linearGradient></defs><rect width="1024" height="1024" rx="210" fill="url(#tile)"/><path d="M228 237H378L514 592L650 237H799L592 787H437Z" fill="url(#ribbon)"/><path d="M471 316L564 370L471 424Z" fill="#F4F0FF"/></svg>'''
    (ROOT/'app.svg').write_text(svg,encoding='utf-8')
    print('app.svg, app.png, app.ico (7 icon sizes)')

if __name__=='__main__':main()
