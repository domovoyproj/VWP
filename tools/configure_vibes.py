"""Composition coordinates reviewed against the original clean artwork plates."""
import json
from pathlib import Path

path=Path(__file__).resolve().parents[1]/'assets/vibes/catalog.json'
entries=json.loads(path.read_text(encoding='utf-8'))
water={1202:[.22,.54,.61,.65],1203:[.17,.65,1,1],1206:[.03,.49,.34,.61],1207:[.05,.59,1,1],1208:[0,.57,.40,1],1211:[.47,.55,1,1],1212:[0,.53,.53,1],1214:[.24,.61,1,1],1215:[.77,.89,1,1],1216:[0,.77,.46,.86],1217:[0,.75,1,1],1220:[.13,.77,1,1]}
for e in entries:
    if e['id'] in water:e['waterRect']=water[e['id']]
    if e['id']==1204:
        e['disc']=[.695,.696,.155,.046]
        e['character']={'file':'record-girl.png','x':.13,'y':.555,'height':.54,'eyes':[.489,.103,.536,.111],'eyeRadius':[.019,.008]}
        e['description']='Дождь за окном, вращающийся винил и девушка в наушниках: дыхание, моргание и движение волос.'
    if e['id']==1206:
        e['character']={'file':'rooftop-girl.png','x':.84,'y':.685,'height':.53,'eyes':[.515,.100,.539,.107],'eyeRadius':[.018,.008]}
        e['description']='Тёплый закат над морем и девушка на террасе: ветер в волосах, дыхание и небольшие движения рук.'
    if e['id']==1214:e['planet']={'x':.705,'y':.30,'radius':.19}
path.write_text(json.dumps(entries,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
