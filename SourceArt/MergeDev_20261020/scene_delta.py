"""Inspect scene objects/transforms without saving either Unity scene."""
from pathlib import Path
import re,json,collections
main=Path('D:/DO_AN_GAME/ShadowVale')
dev=Path('C:/Users/Admin/.codex/worktrees/dev-preflight-20261020/ShadowVale')
scene='Assets/_Project/Scenes/Maps/Map 1.unity'
def parse(path):
    t=path.read_text(encoding='utf-8-sig');objects={};transforms={}
    for b in re.finditer(r'^--- !u!(1|4) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)',t,re.M|re.S):
        kind,key,body=b.groups()
        if kind=='1':
            n=re.search(r'^  m_Name: (.*)$',body,re.M);active=re.search(r'^  m_IsActive: (.*)$',body,re.M)
            objects[key]={'name':n.group(1) if n else '?','active':active.group(1) if active else '?'}
        else:
            go=re.search(r'^  m_GameObject: \{fileID: (-?\d+)\}',body,re.M);parent=re.search(r'^  m_Father: \{fileID: (-?\d+)\}',body,re.M)
            if go:
                val={'go':go.group(1),'parent':parent.group(1) if parent else '0'}
                for field in ('m_LocalPosition','m_LocalRotation','m_LocalScale'):
                    m=re.search(r'^  '+field+r': (.*)$',body,re.M);val[field]=m.group(1) if m else '?'
                transforms[key]=val
    def name(k):return objects.get(k,{}).get('name','prefab_'+k)
    def root(k):
        seen=set()
        while k in transforms and k not in seen:
            seen.add(k);p=transforms[k]['parent']
            if p=='0' or p not in transforms:return name(transforms[k]['go'])
            k=p
        return '?'
    return objects,transforms,root

a,ta,ra=parse(main/scene);b,tb,rb=parse(dev/scene)
rows=[]
for k in set(ta)|set(tb):
    x=ta.get(k);y=tb.get(k)
    if x!=y:
        ob=b.get(y['go']) if y else a.get(x['go'])
        rows.append({'transform_id':k,'name':(ob or {}).get('name','prefab'),'root':rb(k) if y else ra(k),
                     'change':'added' if not x else 'removed' if not y else 'modified','thinh':x,'dev':y})
counts=collections.Counter(row['root'] for row in rows)
out=main/'Docs/Reviews/MergeDev_20261020'
(out/'scene_changes.json').write_text(json.dumps({'thinh_objects':len(a),'dev_objects':len(b),'changed_transforms':len(rows),'by_root':dict(counts),'rows':rows},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'thinh_objects':len(a),'dev_objects':len(b),'changed_transforms':len(rows),'by_root':dict(counts)},ensure_ascii=False,indent=2))
