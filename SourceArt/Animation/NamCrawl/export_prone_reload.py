"""Append the approved magazine-only take to the existing shared crawl export."""
import bpy,json
from pathlib import Path
from mathutils import Vector,Matrix
here=Path(__file__).resolve().parent
path=here/'game-motion.json';data=json.loads(path.read_text())
bpy.ops.wm.open_mainfile(filepath=str(here/'Nam_Prone_Reload_MagazineOnly_v3.blend'))
scene=bpy.data.scenes['Nam prone magazine-only reload'];bpy.context.window.scene=scene
rig=scene.objects['Nam_Crawl_Review'];gun=scene.objects['AK47_Review']
grip=next(o for o in gun.children_recursive if o.type=='MESH' and o.name.startswith('bl_grip_low'))
old=scene.objects['Reload old magazine'];new=scene.objects['Reload replacement magazine']
def flat(m):return {'m':[float(m[r][c]) for r in range(4) for c in range(4)]}
def center(o):return sum((o.matrix_world@Vector(v) for v in o.bound_box),Vector())/8
frames=[]
for f in range(73):
    scene.frame_set(f);bpy.context.view_layer.update()
    # Container origins coincide with the authored magazine center.
    frames.append({'bones':[flat(rig.matrix_world@rig.pose.bones[n].matrix) for n in data['names']],
        'grip':list(center(grip)), 'forward':list(gun.matrix_world.to_quaternion()@Vector((0,-1,0))),
        'up':list(gun.matrix_world.to_quaternion()@Vector((0,0,1))),
        'oldMagazine':flat(old.matrix_world),'newMagazine':flat(new.matrix_world),
        'oldVisible':any(not o.hide_render for o in old.children_recursive if o.type=='MESH'),
        'newVisible':any(not o.hide_render for o in new.children_recursive if o.type=='MESH')})
data['clips']=[c for c in data['clips'] if c['name']!='reload']+[{'name':'reload','frames':frames}]
path.write_text(json.dumps(data,separators=(',',':')))
print('PRONE_RELOAD_EXPORTED',len(frames),[(i,frames[i]['oldVisible'],frames[i]['newVisible']) for i in [0,30,36,40,60,72]],flush=True)
