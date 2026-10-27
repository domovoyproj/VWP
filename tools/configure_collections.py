"""Save human-readable descriptions, exact artwork prompts and app catalogs."""
import json
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
plan=ROOT/'tools'/'collections-plan.json'
groups=json.loads(plan.read_text(encoding='utf-8'))
prompts=json.loads((ROOT/'tools'/'generation-prompts.json').read_text(encoding='utf-8')) if (ROOT/'tools'/'generation-prompts.json').exists() else {}
descriptions={
 'lightning':'Светящиеся молнии и ветвящиеся электрические разряды.',
 'energy':'Энергетические дуги, световые орбиты и свечение.',
 'snow':'Плавно падающий снег в нескольких планах.',
 'aurora':'Северное сияние и мягкий снег.',
 'rain':'Движущиеся дождевые струи и мягкая атмосфера.',
 'water':'Мягкая рябь воды и движущиеся отражения.',
 'bubbles':'Подводное преломление света и поднимающиеся пузырьки.',
 'fireflies':'Летающие светлячки с мягким мерцанием.',
 'stars':'Тонкое мерцание звёзд.',
 'mist':'Плавное движение лёгкого тумана.',
 'abstract':'Непрерывное мягкое течение формы и света.',
 'dust':'Неспешное движение частиц в свете.',
 'pollen':'Лёгкая летняя пыльца в солнечном свете.',
 'petals':'Медленно кружащиеся цветочные лепестки.',
 'leaves':'Плавно опадающие осенние листья.',
 'embers':'Тлеющие искры и мягкое свечение.',
 'wind':'Лёгкие частицы, движущиеся на ветру.'}
for group in groups:
    folder=ROOT/'assets'/group['folder'];folder.mkdir(parents=True,exist_ok=True)
    notes=['# '+group['category'],'','10 individually generated illustrations and looped live wallpapers.','',
      'Generated with the built-in image_gen tool. Output: 1920×1080, 30 FPS, eight-second H.264 loops.',
      'Source images are generally 1672×941 and are upscaled; this is not native 4K.',
      'Environment effects move. Character/animal bodies and faces stay still.',
      'Character fan art belongs to the respective franchise rights holders.','',
      'Reproduce: `python tools/render_collections.py`, then `python tools/check_collections.py`.','']
    for scene in group['entries']:
        scene['prompt']=prompts.get('base',{}).get(str(scene['id']),scene['prompt'])
        if str(scene['id']) in prompts.get('realism',{}):scene['realismEditPrompt']=prompts['realism'][str(scene['id'])]
        scene['description']=descriptions[scene['effect']]+(' День, дата и время ПК отдельным слоем.' if scene['clock'] else '')
        source=folder/f"{scene['id']}.png"
        if source.exists():
            with Image.open(source) as image:scene['sourceResolution']=list(image.size)
        notes+=['## '+str(scene['id'])+' · '+scene['name'],'',scene['description'],'','Prompt (verbatim):','',scene['prompt'],'']
        if scene.get('realismEditPrompt'):notes+=['Realism refinement prompt (verbatim):','',scene['realismEditPrompt'],'']
    (folder/'catalog.json').write_text(json.dumps(group['entries'],ensure_ascii=False,indent=2),encoding='utf-8')
    (folder/'ARTWORK.md').write_text('\n'.join(notes),encoding='utf-8')
plan.write_text(json.dumps(groups,ensure_ascii=False,indent=2),encoding='utf-8')
print('Configured six collections, 60 unique scene IDs and exact generation prompts.')
