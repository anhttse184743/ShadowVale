import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/_Project/Art/Characters/Player/Player.fbx'))
bpy.context.view_layer.update()
report=[]
for o in bpy.data.objects:
 if o.type!='MESH':continue
 groups={g.index for g in o.vertex_groups if g.name in ['Head','Head_end']}
 pts=[o.matrix_world@v.co for v in o.data.vertices if any(g.group in groups and g.weight>.5 for g in v.groups)]
 if pts:
  lo=[min(p[i] for p in pts) for i in range(3)];hi=[max(p[i] for p in pts) for i in range(3)]
  report.append({'name':o.name,'count':len(pts),'min':lo,'max':hi,'size':[hi[i]-lo[i] for i in range(3)]})
(root/'SourceArt/BunkerLayoutReview/head_measurements.json').write_text(json.dumps(report,indent=2))
print(report)
