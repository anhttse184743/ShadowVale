import bpy,json
from pathlib import Path
here=Path(__file__).resolve().parent;root=here.parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_Reload.fbx'))
out={'objects':[{'name':o.name,'type':o.type,'parent':o.parent.name if o.parent else None} for o in bpy.context.scene.objects]}
for actor in [o for o in bpy.context.scene.objects if o.type=='ARMATURE']:
    out[actor.name]={'matrix':[list(row) for row in actor.matrix_world],'bones':[p.name for p in actor.pose.bones], 'action':actor.animation_data.action.name if actor.animation_data else None,'frames':list(actor.animation_data.action.frame_range) if actor.animation_data else None}
(here/'reload-source-inspection.json').write_text(json.dumps(out,indent=2))
print('RELOAD_SOURCE_INSPECTED',flush=True)
