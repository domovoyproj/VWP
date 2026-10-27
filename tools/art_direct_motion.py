"""Explicit motion placement derived from the generated compositions."""
import json
from pathlib import Path
path=Path(__file__).with_name('collections-plan.json')
groups=json.loads(path.read_text(encoding='utf-8'))
patches={
 1503:{'waterRect':[0,.62,1,1]},
 1601:{'chimneys':[[.946,.269]]},
 1605:{'waterRect':[0,.59,.61,1]},
 1606:{'chimneys':[[.104,.376]]},
 1607:{'waterRect':[.14,.70,.85,1]},
 1701:{'chimneys':[[.175,.135]]},
 1702:{'chimneys':[[.826,.115]]},
 1703:{'waterRect':[.37,.68,.94,1],'chimneys':[[.932,.175]]},
 1704:{'chimneys':[[.281,.152]]},
 1706:{'chimneys':[[.039,.072]]},
 1708:{'chimneys':[[.221,.49],[.506,.635],[.867,.686]]},
 1709:{'chimneys':[[.753,.295]]},
 1802:{'waterRect':[0,.61,1,1]},
 1808:{'waterRect':[0,.57,1,1]},
 1903:{'waterRect':[0,.66,.49,1]},
 1910:{'waterRect':[0,.76,.43,1]}}
for group in groups:
    for scene in group['entries']:
        scene.update(patches.get(scene['id'],{}))
path.write_text(json.dumps(groups,ensure_ascii=False,indent=2),encoding='utf-8')
print('Placed water ripples and chimney plumes to match the artwork.')
