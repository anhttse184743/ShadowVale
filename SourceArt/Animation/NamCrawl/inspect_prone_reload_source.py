import bpy,json
from pathlib import Path
here=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(here/'MixamoSource/Prone_Reloading_XBot.fbx'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');action=rig.animation_data.action
start,end=action.frame_range
rows=[]
for i in range(19):
    t=start+(end-start)*i/18;bpy.context.scene.frame_set(int(t),subframe=t-int(t))
    rows.append({'frame':t,'positions':{p.name.split(':')[-1]:list(rig.matrix_world@p.head) for p in rig.pose.bones if p.name.split(':')[-1] in ['Hips','Head','Spine2','LeftHand','RightHand','LeftForeArm','RightForeArm']}})
out={'fps':bpy.context.scene.render.fps,'start':start,'end':end,'matrix':[list(r) for r in rig.matrix_world],'samples':rows}
(here/'prone-reload-source-inspection.json').write_text(json.dumps(out,indent=2))
print(json.dumps(out),flush=True)
