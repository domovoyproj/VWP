"""Art-directed masks measured against the six original seasonal plates."""
import json
from pathlib import Path

path=Path(__file__).resolve().parents[1]/'assets/seasons/catalog.json'
entries=json.loads(path.read_text(encoding='utf-8'))
settings={
 1301:dict(waterRect=[.15,.61,1,1],waterEdges=[.15,.43,1,1],sky=.26,glow=[.96,.39]),
 1302:dict(background='1302-clean.png',weatherRect=[0,0,.60,.83],panes=[[0,.07,.235,.69],[.276,.02,.483,.66],[.511,.065,.596,.61]],steam=[.744,.721,.031,.14],glow=[.919,.616]),
 1303:dict(waterRect=[0,.51,1,1],waterEdges=[0,0,.84,.46],sky=.17,steam=[.46,.53,.36,.055],glow=[.872,.361]),
 1304:dict(sky=.15,glow=[.896,.41]),
 1305:dict(waterRect=[0,.56,1,.86],waterEdges=[0,.23,1,.96],sky=.24,steam=[.49,.57,.37,.12],glow=[.912,.324]),
 1306:dict(weatherRect=[0,0,.61,.82],panes=[[0,0,.141,.817],[.176,0,.521,.671],[.545,.041,.61,.586]],steam=[.846,.47,.019,.11],glow=[.663,.254])
}
for e in entries:e.update(settings[e['id']])
path.write_text(json.dumps(entries,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Six seasonal water/window/steam masks configured')
