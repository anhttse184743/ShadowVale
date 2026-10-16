import bpy,json
from pathlib import Path
bpy.ops.wm.read_factory_settings(use_empty=True)
root=Path(__file__).resolve().parents[2]
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/_Project/Art/Characters/NPCs/hung.fbx'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
out={'rig':rig.name,'matrix':[list(r) for r in rig.matrix_world],'bones':[{ 'name':b.name,'head':list(rig.matrix_world@b.head_local),'tail':list(rig.matrix_world@b.tail_local),'parent':b.parent.name if b.parent else None} for b in rig.data.bones],'meshes':[{ 'name':o.name,'verts':len(o.data.vertices),'materials':[m.name for m in o.data.materials]} for o in bpy.data.objects if o.type=='MESH']}
(root/'Logs/Map02_Civilians/rig-survey.json').write_text(json.dumps(out,indent=2),encoding='utf8')
print('RIG_SURVEY_COMPLETE',len(rig.data.bones))
