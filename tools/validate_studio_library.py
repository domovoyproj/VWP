"""Validate the complete GPU library and every pinned resource before shipping."""
import argparse,hashlib,json
from pathlib import Path
from PIL import Image

EXPECTED=set(range(28))|set(range(1001,1004))|set(range(1101,1116))|set(range(1201,1221))|set(range(1301,1307))|set(range(1401,1411))|set(range(1501,1511))|set(range(1601,1611))|set(range(1701,1711))|set(range(1801,1811))|set(range(1901,1911))
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def validate_library(source,rendered=True):
    source=Path(source).resolve();assets=source/'assets';library=assets/'studio-library'
    if not (library/'catalog.json').is_file():raise SystemExit('Complete Studio library catalogue missing')
    entries=read(library/'catalog.json')
    if len(entries)!=132 or {e['id'] for e in entries}!=EXPECTED:raise SystemExit('Library composition coverage mismatch')
    integrity=read(library/'integrity.json')
    if integrity['schema']!=1 or integrity['sceneCount']!=132 or (rendered and not integrity['renderedPosters']):raise SystemExit('Library integrity or native poster coverage missing')
    files=integrity['files']
    def pinned(name):
        path=(assets/name).resolve()
        if not path.is_relative_to(assets.resolve()) or name not in files or not path.is_file():raise SystemExit('Unpinned/missing library resource: '+name)
        return path
    for name,digest in files.items():
        with pinned(name).open('rb') as stream:actual=hashlib.file_digest(stream,'sha256').hexdigest()
        if actual!=digest:raise SystemExit('Library checksum mismatch: '+name)
    for entry in entries:
        if entry['project']!=f"{entry['id']}.vwpscene":raise SystemExit('Invalid library project filename')
        project=read(pinned('studio-library/scenes/'+entry['project']))
        if project['schema']!=3 or project['id']!=f"library-{entry['id']}" or not 1<=len(project['layers'])<=12:raise SystemExit('Invalid GPU project identity')
        cover=pinned(project['cover'])
        if rendered:
            if project['cover']!=f"studio-library/posters/{entry['id']}.jpg":raise SystemExit('Library uses a source still instead of a native poster')
            with Image.open(cover) as image:
                if image.size!=(1920,1080):raise SystemExit('Library poster is not native FHD')
        for layer in project['layers']:
            with Image.open(pinned(layer['source'])) as image:
                if image.width<1600 or image.height<900 or image.width>4096 or image.height>4096:raise SystemExit('Layer resolution outside quality budget')
                if layer['motion']=='character' and (image.mode!='RGBA' or image.getextrema()[3]!=(0,255)):raise SystemExit('Character has no genuine transparent alpha')
            for name in layer.get('maskFiles',{}).values():pinned(name)
        if project['weather'].get('maskFile'):pinned(project['weather']['maskFile'])
    alignment=read(pinned('studio-library/alignment.json'))
    if alignment.get('review') or len(alignment['characters'])!=31:raise SystemExit('Character registration review is incomplete')
    return len(files)
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--source',default=str(Path(__file__).resolve().parents[1]));parser.add_argument('--allow-source-covers',action='store_true');args=parser.parse_args()
    print(f'PASS: 132 scenes; {validate_library(args.source,not args.allow_source_covers)} SHA-256 verified resources; 31 registered alpha characters.')
