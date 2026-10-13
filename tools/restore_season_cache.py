"""Reuse exact-source seasonal render artifacts, verifying each pinned ZIP digest."""
import argparse,hashlib,json,os,shutil,subprocess,tempfile,zipfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
SOURCE='3acb43714a86a4b2bd4228ea8c9f28de1219e881'
RUN=38323580442
CACHE={
 1301:(11782195093,'518ad4722e58c13ab0210f995ff1a0270e507420a8041cec105fb4cbb9c7054f'),
 1302:(11781082803,'478b258520eab21d2d4fc94b6b783a97e15fc3bb3a0ca4e63ca091ae476ccb1f'),
 1303:(11781174923,'64318794815ab7b11167d24c35bb659b2bef450ef1c4fab129ede70b8b93b9d1'),
 1304:(11782065426,'0fcb0a6f2c756a881e711172dacb5271a942fe3ef48a13f6bdc74b7ff0357c0b'),
 1305:(11782076023,'c67da4d37a2d5b19569b31e53cc9378b4ca27cb9b142cef93c05c207673ca35c'),
 1306:(11780973369,'7990b300b5b4e98ab76f11e67fd8346c9c8589aa247b87f3fd0ef633ebed20f3')
}
parser=argparse.ArgumentParser();parser.add_argument('--scene',type=int,required=True,choices=CACHE);args=parser.parse_args()
def output(cached):
    if path:=os.environ.get('GITHUB_ENV'):
        with open(path,'a',encoding='utf-8') as f:f.write('SEASON_CACHED='+str(cached).lower()+'\n')
diff=subprocess.run(['git','diff','--quiet',SOURCE,'--','assets/seasons','CitySurface.cs','tools/live-check/LivingRender.cs'],cwd=ROOT)
if diff.returncode!=0:
    output(False);print('Seasonal renderer inputs changed; fresh render required.');raise SystemExit(0)
repository='repos/domovoyproj/VWP'
run=json.loads(subprocess.check_output(['gh','api',f'{repository}/actions/runs/{RUN}']))
if run['head_sha']!=SOURCE or run['conclusion']!='success':raise SystemExit('Cache source run is not verified.')
ident,digest=CACHE[args.scene]
artifact=json.loads(subprocess.check_output(['gh','api',f'{repository}/actions/artifacts/{ident}']))
if artifact['expired']:
    output(False);print('Render artifact expired; fresh render required.');raise SystemExit(0)
if artifact['name']!=f'VWP-Season-{args.scene}' or artifact['digest']!='sha256:'+digest or artifact['workflow_run']['id']!=RUN:raise SystemExit('Unexpected render cache identity.')
with tempfile.TemporaryDirectory(prefix='vwp-season-cache-') as folder:
    archive=Path(folder)/'source.zip'
    with archive.open('wb') as f:subprocess.run(['gh','api',f'{repository}/actions/artifacts/{ident}/zip'],stdout=f,check=True)
    with archive.open('rb') as f:actual=hashlib.file_digest(f,'sha256').hexdigest()
    if actual!=digest:raise SystemExit('Render cache SHA256 mismatch.')
    expected={f'{args.scene}.{ext}' for ext in ('mp4','poster.png','preview.mp4')}
    with zipfile.ZipFile(archive) as z:
        if set(z.namelist())!=expected:raise SystemExit('Unexpected render cache contents.')
        for name in expected:
            with z.open(name) as source,(ROOT/'assets/seasons'/name).open('wb') as target:shutil.copyfileobj(source,target)
output(True);print(f'{args.scene}: restored exact-source, SHA256 verified master, poster and preview.')
