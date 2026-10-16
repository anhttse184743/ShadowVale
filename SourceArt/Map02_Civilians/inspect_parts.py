from pathlib import Path
import bpy,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/_Project/Art/Characters/Player/Player.fbx'))
rows=[]
for ob in [o for o in bpy.data.objects if o.type=='MESH']:
    n=len(ob.data.vertices);adj=[[] for _ in range(n)]
    for edge in ob.data.edges:
        a,b=edge.vertices;adj[a].append(b);adj[b].append(a)
    seen=set();parts=[]
    for i in range(n):
        if i in seen:continue
        stack=[i];seen.add(i);component=[]
        while stack:
            k=stack.pop();component.append(k)
            for j in adj[k]:
                if j not in seen:seen.add(j);stack.append(j)
        if len(component)<10:continue
        points=[ob.matrix_world@ob.data.vertices[k].co for k in component]
        mn=[min(p[a] for p in points) for a in range(3)];mx=[max(p[a] for p in points) for a in range(3)]
        weights={}
        for k in component:
            for g in ob.data.vertices[k].groups:weights[ob.vertex_groups[g.group].name]=weights.get(ob.vertex_groups[g.group].name,0)+g.weight
        parts.append({'count':len(component),'min':mn,'max':mx,'weights':sorted(weights.items(),key=lambda x:-x[1])[:3]})
    rows.append({'mesh':ob.name,'materials':[m.name for m in ob.data.materials],'parts':sorted(parts,key=lambda p:-p['count'])})
(ROOT/'Logs/Map02_Civilians/source-parts.json').write_text(json.dumps(rows,indent=2))
