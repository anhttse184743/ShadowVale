import bpy,json
from pathlib import Path
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r'G:\game\Capstone\ShadowVale\Assets\_Project\Art\Characters\NPCs\chỉ huy mình.fbx')
a=next(o for o in bpy.data.objects if o.type=='ARMATURE');mesh=next(o for o in bpy.data.objects if o.type=='MESH');data={}
for name in ['LeftHand','RightHand']:
 b=a.data.bones[name];group=mesh.vertex_groups.get(name);points=[]
 for v in mesh.data.vertices:
  w=sum(g.weight for g in v.groups if g.group==group.index)
  if w>.7:points.append(list(b.matrix_local.inverted()@a.matrix_world.inverted()@mesh.matrix_world@v.co))
 data[name]={'head':list(b.head_local),'tail':list(b.tail_local),'points':points,'matrix':[list(x) for x in b.matrix_local]}
Path(r'C:\Users\dangk\Documents\Codex\2026-09-20\du\output\CommanderRower_20261026\hands.json').write_text(json.dumps(data))
print([(k,len(v['points'])) for k,v in data.items()])
