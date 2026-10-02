"""Copy only Map 2 dependencies into an isolated Unity authoring project."""
from pathlib import Path
import re, shutil, json, hashlib, uuid

root=Path(__file__).resolve().parents[1]
target=root/'Tools/Map02BoundaryBuild'
target.mkdir(exist_ok=True)
(target/'.gitignore').write_text('*\n!.gitignore\n',encoding='utf-8')
index={}
for meta in (root/'Assets').rglob('*.meta'):
    match=re.search(r'^guid: (\w+)',meta.read_text(errors='ignore'),re.M)
    if match:index[match[1]]=meta.with_suffix('')
scene=root/'Assets/_Project/Scenes/Maps/Map 2.unity'
source=root/'Assets/_Project/Map01/Editor/Map02Surroundings.cs'
if not source.with_suffix('.cs.meta').exists():
    source.with_suffix('.cs.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
queue=[scene,source,root/'Assets/_Project/Art/Environment/Map01_Realism/TropicalSky.shader',root/'Assets/_Project/Art/Environment/Map01_Optimized/Materials/MapPalette.mat']
for pattern in ('*.cs','*.asmdef','*.asmref'):
    queue.extend((root/'Assets').rglob(pattern))
for p in (root/'ProjectSettings').iterdir():
    if p.is_file():
        dest=target/'ProjectSettings'/p.name;dest.parent.mkdir(exist_ok=True);shutil.copy2(p,dest)
        for g in re.findall(rb'guid: ([a-f0-9]{32})',p.read_bytes()):
            if g.decode() in index:queue.append(index[g.decode()])
seen=set();size=0
while queue:
    p=queue.pop()
    if p in seen or not p.is_file():continue
    seen.add(p);rel=p.relative_to(root);dest=target/rel;dest.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(p,dest);size+=p.stat().st_size
    meta=Path(str(p)+'.meta')
    if meta.exists():shutil.copy2(meta,Path(str(dest)+'.meta'))
    data=p.read_bytes()
    for g in set(re.findall(rb'guid: ([a-f0-9]{32})',data)):
        dep=index.get(g.decode())
        if dep:queue.append(dep)
    # Shader includes can also refer directly to project files.
    if p.suffix in ('.shader','.hlsl'):
        for inc in re.findall(rb'#include "(Assets/[^"\r\n]+)"',data):queue.append(root/inc.decode())
(target/'Packages').mkdir(exist_ok=True)
shutil.copy2(root/'Packages/manifest.json',target/'Packages/manifest.json')
(target/'baseline-scene.sha256').write_text(hashlib.sha256(scene.read_bytes()).hexdigest())
print(f'Copied {len(seen)} dependencies, {size/1024/1024:.1f} MiB; original scene hash recorded.')
