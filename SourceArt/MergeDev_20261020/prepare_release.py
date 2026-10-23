"""Freeze a reviewed staged tree after all candidate failures have passing reruns."""
from pathlib import Path
import subprocess,json,xml.etree.ElementTree as ET,shutil,hashlib
main=Path(r'D:/DO_AN_GAME/ShadowVale')
root=Path(r'C:/Users/Admin/.codex/worktrees/merge-dev-thinh-20261020/ShadowVale')
report=main/'Docs/Reviews/MergeDev_20261020'
assert not (root/'Temp/UnityLockfile').exists(),'Wait for Unity to exit.'
def git(*args):return subprocess.check_output(['git','-c','core.autocrlf=false','-c','core.safecrlf=false',*args],cwd=root)
assert not git('diff','--name-only','--diff-filter=U').strip()
latest={}
def run_time(path):
 r=ET.parse(path).getroot();return r.get('end-time') or r.get('start-time') or ''
for p in sorted(report.glob('candidate-*.xml'),key=run_time):
 for c in ET.parse(p).getroot().iter('test-case'):latest[c.get('fullname')]=(c.get('result'),p.name)
fail={n:v for n,v in latest.items() if v[0]!='Passed'}
assert latest and not fail,fail
for required in ['MosinStealthTests','C3BedAnimationTests','BossIntegrationTests','Map01OperationsTests','Map02ArrivalTests','WeaponAnimationIntegrationTests','OpeningCutsceneTests','K54NormalRestorationTests','AimToggleIntegrationTests']:
 assert any(required+'.' in n for n in latest),required

# Existing tracked files include the real dev merge and the three-way preserved local work.
git('add','-u')
manifest=json.loads((main/'Temp/MergeDev_20261020_Before/manifest.json').read_text(encoding='utf-8'))
assets=[]
for f in manifest['files']:
 rel=f['path']
 if not rel.startswith('Assets/') or f.get('deleted'):continue
 if rel.startswith('Assets/_Recovery') or rel.startswith('Assets/_Project/Map01/Tests/Generated'):continue
 if rel.startswith('Assets/_Project/Art/Weapon_Integration_20261018'):continue
 if not f.get('tracked'):assets.append(rel)
if assets:git('add','--',*assets)

# This is input to a source-contact regression, not generated screenshots or player saves.
witness='Docs/Reviews/C3_BedKnife_Unity_20261020/source_witness.json'
assert (root/witness).is_file();git('add','--',witness)
for folder in ['SourceArt/MergeDev_20261020','Docs/Reviews/MergeDev_20261020']:
 for p in (main/folder).glob('*'):
  if p.suffix not in ['.py','.md','.json','.xml']:continue
  if p.name in ['release_tree.json','apply_result.json']:continue
  rel=p.relative_to(main);dst=root/rel;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dst)
  git('add','--',rel.as_posix())

tree=git('write-tree').decode().strip()
paths=[p for p in git('diff','--cached','--name-only','-z','HEAD').decode().split('\0') if p]
rows=[]
for rel in paths:
 p=root/rel
 rows.append({'path':rel,'deleted':not p.exists(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None,'bytes':p.stat().st_size if p.exists() else 0})
data={'head':manifest['head'],'dev':manifest['dev'],'tree':tree,'candidate':str(root),'files':rows,'validatedCases':len(latest),'failures':fail,'newAssetsIncluded':len(assets)}
(report/'release_tree.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'tree':tree,'files':len(rows),'validatedCases':len(latest),'newAssetsIncluded':len(assets),'bytes':sum(r['bytes'] for r in rows)},indent=2))
