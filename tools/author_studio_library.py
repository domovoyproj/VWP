"""Compile the individually directed library; a release must cover every source.

Run without arguments for strict, complete generation. --ids is only for local
iteration and deliberately never writes a partial application catalogue.
"""
import argparse
import hashlib
import json
from pathlib import Path
from library_directions import D

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'assets'
OUT = ASSETS / 'studio-library'

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def sources():
    rows = [dict(r, group='base', image=f"scene-{r['id']}.png") for r in read(ASSETS/'presets.json')]
    rows += [dict(r, group='cinematic', image=f"cinematic/{r['id']}.png") for r in read(ASSETS/'expansion.json')['spatial']]
    for group in ['living','anime','vibes','seasons','electric','horizons','winter','village-winter','village-summer','muses']:
        rows += [dict(r, group=group, image=f"{group}/{r['id']}.png") for r in read(ASSETS/group/'catalog.json')]
    descriptions={18:'Орбитальная оранжерея: вода, листва и прохладный свет.',19:'Вечерняя гавань: рябь и отражения огней.',20:'Тёплое кафе: дождь за стеклом и мягкий свет ламп.',21:'Альпийский виадук: снег и туман в долине.',22:'Ночной фестиваль: фонари, лепестки и отражения в реке.',23:'Ночная станция: холодный свет и отражения на платформе.',24:'Обсерватория: звёзды и медленное свечение туманности.',25:'Мастерская: свет панелей и пыль в лучах.',26:'Подводная станция: вода и пузырьки за стеклом.',27:'Баскетбольная площадка: дождь, прожекторы и мокрый асфальт.'}
    for row in rows:
        if not row.get('description'):row['description']=descriptions[row['id']]
    if len(rows)!=132 or len({r['id'] for r in rows})!=132:
        raise ValueError('Source library must contain exactly 132 unique compositions')
    return rows

def masks(recipe, channels=('r','g','b','a')):
    result=[]
    for channel,key in enumerate(channels):
        for shape in recipe.get(key,[]):
            result.append(dict(channel=channel, **shape))
    if len(result)>32:
        raise ValueError('Too many masks for a layer')
    return result

def transformed(shape,rect):
    x,y,w,h=rect
    return dict(shape,points=[[round(max(0,min(1,x+px*w)),6),round(max(0,min(1,y+py*h)),6)] for px,py in shape['points']])

def layer(id,name,source,recipe,rect,**extra):
    result=dict(id=id,name=name,source=source,rect=rect,opacity=1,depth=.035,
        motion=recipe.get('motion','still'),strength=recipe.get('strength',.65),
        surface=recipe.get('surface','water'),light=recipe.get('light','warm'),
        tint=recipe.get('tint','#B5D7F5'),surfaceStrength=recipe.get('surfaceStrength',.7),
        lightStrength=recipe.get('lightStrength',.65),seed=int(hashlib.sha256(id.encode()).hexdigest()[:4],16),
        waterline=recipe.get('waterline',.48),sun=recipe.get('sun',[.75,.4]),masks=masks(recipe))
    result.update(extra)
    return result

def compile_scene(row,posters=False):
    id=row['id']; recipe=D[id];rect=[-.01,-.01,1.02,1.02]
    image=row['image'];character=recipe.get('character')
    if row['group']=='living' or character:
        image=f'studio-library/art/{id}-background.png'
    if not (ASSETS/image).is_file():raise ValueError(f'{id}: missing background {image}')
    layers=[layer(f'{id}-background','Окружение',image,recipe,rect)]
    for index,overlay in enumerate(recipe.get('overlays',[])):
        layers.append(layer(f'{id}-material-{index}','Атмосфера · '+overlay['surface'],image,overlay,rect,
            opacity=overlay.get('opacity',.25),clipToSurface=True))
    if character:
        foreground=f'studio-library/art/{id}-character.png'
        if not (ASSETS/foreground).is_file():raise ValueError(f'{id}: missing alpha character {foreground}')
        alignment=read(OUT/'alignment.json')
        placement=next((r for r in alignment['characters'] if r['id']==id),None)
        if placement is None:raise ValueError(f'{id}: no verified character registration')
        if placement['characterSha256']!=hashlib.sha256((ASSETS/foreground).read_bytes()).hexdigest():raise ValueError(f'{id}: registration belongs to an older alpha layer')
        cx,cy,cw,ch=placement['rect']
        def local(shape):
            return dict(shape,points=[[round(max(0,min(1,(px-cx)/cw)),6),round(max(0,min(1,(py-cy)/ch)),6)] for px,py in shape['points']])
        movement=dict(b=[local(m) for m in character['hair']],a=[local(m) for m in character['breath']],motion='character',strength=character.get('strength',.55))
        clip=character.get('clip')
        if clip:movement['r']=[local(m) for m in clip]
        character_rect=[round(rect[0]+cx*rect[2],8),round(rect[1]+cy*rect[3],8),round(cw*rect[2],8),round(ch*rect[3],8)]
        layers.append(layer(f'{id}-character','Персонаж · волосы и дыхание',foreground,movement,character_rect,depth=.23,clipToSurface=bool(clip)))
    weather=dict(recipe.get('weather',dict(kind='none',count=0,region=[0,0,1,1],color='#DFEFFF')))
    weather['masks']=[dict(channel=0,**transformed(m,rect)) for m in weather.get('masks',[])]
    # Any weather meant to sit behind the subject must follow all background materials.
    if character and weather.get('afterLayer')==0:weather['afterLayer']=len(layers)-2
    cover=f'studio-library/posters/{id}.jpg' if posters else image
    if posters and not (ASSETS/cover).is_file():raise ValueError(f'{id}: missing native rendered poster')
    project=dict(schema=3,id=f'library-{id}',name=row['name'],description=row['description'],author='VWP',cover=cover,
        duration=24,layers=layers,weather=weather,settings=dict(exposure=0,bloom=.07,motion=1,wind=.65,parallax=.3,audioReactive=False,clock=False))
    if not any(recipe.get(k) for k in ['r','g','b','a','weather','overlays','character']):
        raise ValueError(f'{id}: no individually directed animation')
    target=OUT/'scenes'/f'{id}.vwpscene';target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps(project,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    entry=dict(id=id,name=row['name'],category=row['category'],description=row['description'],project=target.name,group=row['group'])
    if row.get('season'):entry['season']=row['season']
    proof=dict(id=id,source=row['image'],background=image,
        sourceSha256=hashlib.sha256((ASSETS/image).read_bytes()).hexdigest(),
        protectedPoints=[transformed(dict(points=[p]),rect)['points'][0] for p in recipe.get('fixed',[])],
        face=transformed(dict(points=[character['face']]),rect)['points'][0] if character else None,
        surfaces=sorted({l['surface'] for l in layers if any(m['channel']==0 for m in l['masks'])}),
        separateCharacter=bool(character),poster=cover)
    if not proof['protectedPoints']:raise ValueError(f'{id}: no protected visual reference')
    return entry,proof

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ids',help='Development subset; does not publish a catalogue')
    parser.add_argument('--posters',action='store_true',help='Require final native-rendered posters')
    args=parser.parse_args();rows=sources();expected={r['id'] for r in rows}
    if set(D)-expected:raise ValueError(f'Directions without a source: {sorted(set(D)-expected)}')
    if args.ids:
        selected={int(value) for value in args.ids.split(',')}
        if selected-expected:raise ValueError(f'Unknown scene IDs: {selected-expected}')
        rows=[r for r in rows if r['id'] in selected]
    elif expected-set(D):
        raise ValueError(f'Incomplete library: {len(D)}/132 directions. Missing {sorted(expected-set(D))}')
    entries=[];proof=[]
    for row in rows:
        entry,evidence=compile_scene(row,args.posters);entries.append(entry);proof.append(evidence)
    if not args.ids:
        (OUT/'catalog.json').write_text(json.dumps(entries,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
        (OUT/'directions.json').write_text(json.dumps(dict(schema=1,count=len(proof),scenes=proof),ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
        resources=set()
        for row in rows:
            project_path=OUT/'scenes'/f"{row['id']}.vwpscene";project=read(project_path)
            resources.add(project_path.relative_to(ASSETS).as_posix());resources.add(project['cover'])
            for item in project['layers']:
                resources.add(item['source']);resources.update(item.get('maskFiles',{}).values())
            if project['weather'].get('maskFile'):resources.add(project['weather']['maskFile'])
        resources.update('studio-library/'+name for name in ['catalog.json','directions.json','alignment.json','art/ARTWORK.json'])
        hashes={name:hashlib.sha256((ASSETS/name).read_bytes()).hexdigest() for name in sorted(resources)}
        (OUT/'integrity.json').write_text(json.dumps(dict(schema=1,sceneCount=132,renderedPosters=args.posters,files=hashes),ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    else:
        target=ROOT/'artifacts'/'library-development';target.mkdir(exist_ok=True)
        (target/'directions.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
    print(f'Compiled {len(entries)}/132 scenes; '+('development subset, catalogue untouched' if args.ids else 'complete catalogue written'))

if __name__=='__main__':main()
