"""Check the saved Blender animation independently from the authoring script."""
import bpy, json, math
from pathlib import Path
OUT=Path(__file__).resolve().parent/'Animation_v4'
parameters=json.loads((OUT/'validation.json').read_text());last=parameters['loop_frames']+1
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Nam_Tam_ShoulderAssist.blend'))
scene=bpy.context.scene;result={}
for name in ('Nam','Tam'):
    rig=bpy.data.objects[name];assert rig.animation_data.action is not None
    poses=[]
    for frame in (1,last):
        scene.frame_set(frame);bpy.context.view_layer.update()
        poses.append({p.name:p.matrix.copy() for p in rig.pose.bones})
    max_position=max((poses[0][n].translation-poses[1][n].translation).length for n in poses[0])
    max_rotation=max(poses[0][n].to_quaternion().rotation_difference(poses[1][n].to_quaternion()).angle for n in poses[0])
    assert max_position<.0001,(name,max_position)
    assert max_rotation<.001,(name,max_rotation)
    result[name]={'bones':len(rig.pose.bones),'loop_position_error_m':max_position,'loop_rotation_error_rad':max_rotation}
    for frame in range(1,last+1):
        scene.frame_set(frame)
        assert all(math.isfinite(v) for p in rig.pose.bones for row in p.matrix for v in row)
# Reimport the actual FBX exports and confirm they carry keyed motion.
for name in ('Nam','Tam'):
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(OUT/(name+'_ShoulderAssist_Walk.fbx')))
    imported=next(o for o in set(bpy.data.objects)-before if o.type=='ARMATURE')
    assert imported.animation_data and imported.animation_data.action
    start,end=imported.animation_data.action.frame_range
    assert end-start>=parameters['loop_frames'],(name,start,end)
    result[name]['fbx_animation_frame_range']=[start,end]
(OUT/'independent_verification.json').write_text(json.dumps(result,indent=2))
print('SHOULDER_ASSIST_VERIFY_PASS',json.dumps(result))
