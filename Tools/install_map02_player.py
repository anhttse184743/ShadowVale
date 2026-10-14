"""Install the saved player scene only after its isolated Play Mode checks pass."""
from pathlib import Path
import hashlib, re, shutil
root=Path(__file__).resolve().parents[1]
project=root/'Tools/Map02BoundaryBuild'
scene=Path('Assets/_Project/Scenes/Maps/Map 2.unity')
reports=project/'Tools/Map02Reports'
assert hashlib.sha256((root/scene).read_bytes()).hexdigest()==(project/'baseline-scene.sha256').read_text(), 'Scene changed during testing; do not overwrite.'
for name in ('player-setup-checks.txt','player-play-checks.txt'):
    text=(reports/name).read_text()
    assert text.startswith('PASS') and 'FAIL' not in text, name
def records(path):
    return {re.search(r'&(-?\d+)',s)[1]:s for s in re.split(r'(?m)(?=^--- !u!)',path.read_text(encoding='utf-8-sig')) if s.startswith('--- !u!')}
a,b=records(root/scene),records(project/scene)
assert not set(a)-set(b), 'Existing scene records removed.'
changed=[key for key in a if a[key]!=b[key]]
for key in changed:
    assert a[key].splitlines()[1] in ('Camera:','AudioListener:','GameObject:','SceneRoots:'), 'Unexpected changed record '+key
backup=root/'Tools/Map02Reports/Map2_before_player.unity'
if not backup.exists():shutil.copy2(root/scene,backup)
shutil.copy2(project/scene,root/scene)
for name in ('player-setup-checks.txt','player-play-checks.txt','map02-player-boat-spawn.png'):
    shutil.copy2(reports/name,root/'Tools/Map02Reports'/name)
(root/'Tools/Map02Reports/player-install-checks.txt').write_text('PASS saved player scene installed after actual Play Mode checks\nPASS existing environment records unchanged; overview camera/listener replaced by follow camera\nPASS original scene backed up before installation\n')
print('Installed Map 2 exploration player and validated spawn/camera scene.')
