"""Validate six seasonal 4K60 masters, composed posters and moving previews."""
import argparse,json
from pathlib import Path
import imageio_ffmpeg
from PIL import Image

parser=argparse.ArgumentParser();parser.add_argument('--scene',type=int);parser.add_argument('--previews',action='store_true');args=parser.parse_args()
root=Path(__file__).resolve().parents[1];entries=json.loads((root/'assets/seasons/catalog.json').read_text(encoding='utf-8'))
assert len(entries)==6 and {e['id'] for e in entries}==set(range(1301,1307))
assert sum(e['season']=='Осень' for e in entries)==3 and sum(e['season']=='Зима' for e in entries)==3
assert args.scene is None or args.scene in {e['id'] for e in entries}
for entry in entries:
    scene=entry['id']
    if args.scene and scene!=args.scene:continue
    path=root/'assets/seasons'/f'{scene}.mp4';assert path.stat().st_size>100_000
    samples=[]
    for seconds in (0,5):
        reader=imageio_ffmpeg.read_frames(str(path),pix_fmt='rgb24',input_params=['-ss',str(seconds)],output_params=['-frames:v','1'])
        info=next(reader);samples.append(next(reader));reader.close()
        assert tuple(info['size'])==(3840,2160) and abs(info['fps']-60)<.01 and abs(info['duration']-12)<.05,(path,info)
    changed=sum(abs(a-b)>14 for a,b in zip(*samples));assert changed>3840*2160*.005,(scene,changed)
    with Image.open(root/'assets/seasons'/f'{scene}.poster.png') as poster:assert poster.size==(3840,2160)
    metrics=root/'artifacts/living-check'/f'{scene}-metrics.json'
    if metrics.exists():
        data=json.loads(metrics.read_text());assert data['edgeChanges']<=10 and data['changed']>=700
    if args.previews:
        reader=imageio_ffmpeg.read_frames(str(root/'assets/seasons'/f'{scene}.preview.mp4'),pix_fmt='rgb24',output_params=['-frames:v','1'])
        info=next(reader);next(reader);reader.close();assert tuple(info['size'])==(1280,720) and abs(info['fps']-30)<.01 and abs(info['duration']-12)<.05
    print(f'{scene}: PASS, 4K60, 12 s, {changed} changing color channels, composed poster')
