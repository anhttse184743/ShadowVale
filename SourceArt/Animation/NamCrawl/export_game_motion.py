"""Export approved full-body poses in original Nam model space, no gameplay root travel."""
import bpy,json
from mathutils import Matrix,Vector
from pathlib import Path
here=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(here/'Nam_Crawl_Mixamo_Review_v4.blend'))
def flat(m):return {'m':[float(m[r][c]) for r in range(4) for c in range(4)]}
scene=bpy.data.scenes['Mixamo unarmed'];bpy.context.window.scene=scene
rig=next(o for o in scene.objects if o.type=='ARMATURE' and not o.name.startswith('SOURCE'))
names=[b.name for b in rig.data.bones]
# Rest coordinates are from the untouched Player.fbx imported into Blender.
inspection=here.parents[2]/'Logs/AnimationPreview/NamCrawl/mixamo-source-inspection.json'
rest_world=Matrix(json.loads(inspection.read_text())['Nam']['matrix'])
rest=[flat(rest_world@rig.data.bones[n].matrix_local) for n in names]
clips=[]
for kind in ['unarmed','rifle','aim','knife','grenade','stone']:
    scene=bpy.data.scenes['Mixamo '+kind];bpy.context.window.scene=scene
    actor=next(o for o in scene.objects if o.type=='ARMATURE' and not o.name.startswith('SOURCE'))
    prop=next((o for o in scene.objects if o.parent==actor and o.parent_type=='BONE'),None)
    grip=next((o for o in prop.children_recursive if o.type=='MESH' and o.name.startswith('bl_grip_low')),None) if prop else None
    frames=[]
    for frame in range(73):
        scene.frame_set(frame)
        row={'bones':[flat(actor.matrix_world@actor.pose.bones[n].matrix) for n in names]}
        if grip and kind in ['rifle','aim']:
            row['grip']=list(sum((grip.matrix_world@Vector(v) for v in grip.bound_box),Vector())/8)
            row['forward']=list(prop.matrix_world.to_quaternion()@Vector((0,-1,0)))
            row['up']=list(prop.matrix_world.to_quaternion()@Vector((0,0,1)))
        frames.append(row)
    clips.append({'name':kind,'frames':frames})
out={'fps':30,'names':names,'rest':rest,'clips':clips}
(here/'game-motion.json').write_text(json.dumps(out,separators=(',',':')))
print('GAME_MOTION_EXPORTED',len(names),len(clips),flush=True)
