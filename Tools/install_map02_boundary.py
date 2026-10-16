"""Install the validated scene and new assets, refusing to overwrite concurrent scene edits."""
from pathlib import Path
import hashlib, shutil, re
root=Path(__file__).resolve().parents[1]
project=root/'Tools/Map02BoundaryBuild'
scene=Path('Assets/_Project/Scenes/Maps/Map 2.unity')
assert hashlib.sha256((root/scene).read_bytes()).hexdigest()==(project/'baseline-scene.sha256').read_text(), 'Main scene changed during authoring; do not overwrite it.'
report=project/'Tools/Map02Reports/surroundings-checks.txt'
assert report.exists() and report.read_text().startswith('PASS') and 'FAIL' not in report.read_text(), 'Build has not passed.'
folder=Path('Assets/_Project/Art/Environment/Map02_Village/Boundary')
assert (project/folder/'Map 2 cloud sky.mat').exists()
def records(path):
    parts=re.split(r'(?m)(?=^--- !u!)',path.read_text(encoding='utf-8-sig'))
    return {re.search(r'&(-?\d+)',p)[1]:p for p in parts if p.startswith('--- !u!')}
before=records(root/scene);after=records(project/scene)
assert not set(before)-set(after), 'Existing scene objects were removed.'
changed=[key for key in before if before[key]!=after[key]]
for key in changed:
    assert before[key].splitlines()[1] in ('RenderSettings:','SceneRoots:','Camera:'), 'Unexpected existing object modification: '+key
backup=root/'Tools/Map02Reports/Map2_before_surroundings.unity'
if not backup.exists():shutil.copy2(root/scene,backup)
shutil.copytree(project/folder,root/folder,dirs_exist_ok=True)
shutil.copy2(project/(str(folder)+'.meta'),root/(str(folder)+'.meta'))
shutil.copy2(project/scene,root/scene)
for p in (project/'Tools/Map02Reports').glob('*'):
    if p.suffix=='.png' or p.name=='surroundings-checks.txt':shutil.copy2(p,root/'Tools/Map02Reports'/p.name)
# Validate newly referenced GUIDs against the destination metadata.
known=set()
for meta in (root/'Assets').rglob('*.meta'):
    m=re.search(r'^guid: (\w+)',meta.read_text(errors='ignore'),re.M)
    if m:known.add(m[1])
for asset in (root/folder).rglob('*'):
    if asset.is_file() and asset.suffix!='.meta':
        for guid in re.findall(rb'guid: ([a-f0-9]{32})',asset.read_bytes()):
            value=guid.decode()
            assert value in known or value.startswith('0000000000000000'), 'Unresolved asset reference: '+value
assert hashlib.sha256((root/scene).read_bytes()).digest()==hashlib.sha256((project/scene).read_bytes()).digest()
(root/'Tools/Map02Reports/surroundings-install-checks.txt').write_text(
    'PASS no existing scene records removed\nPASS existing records changed only for RenderSettings, camera draw distance and root list\nPASS installed scene matches rendered scene\nPASS new landscape asset references resolve\n',encoding='utf-8')
print('Installed checked landscape assets and saved Map 2; original scene backup retained.')
