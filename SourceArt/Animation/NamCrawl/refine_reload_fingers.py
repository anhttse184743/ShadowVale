"""Refine the baked free/support hand without repeating motion retargeting."""
import bpy,ast,math
from pathlib import Path
from mathutils import Matrix,Vector,Quaternion
here=Path(__file__).resolve().parent
tree=ast.parse((here/'build_crawl.py').read_text())
helpers=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ['pos','rotate_bone','fingers']]
exec(compile(ast.Module(body=helpers,type_ignores=[]),'reload finger helpers','exec'),globals())
bpy.ops.wm.open_mainfile(filepath=str(here/'Nam_Prone_Reload_Review_v1.blend'))
scene=bpy.data.scenes['Nam prone reload review'];bpy.context.window.scene=scene
rig=scene.objects['Nam_Crawl_Review'];hand_normals={}
for frame in range(127):
    scene.frame_set(frame)
    for bone in rig.pose.bones:
        if bone.name.startswith('LeftHand') and bone.name!='LeftHand':bone.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    long=(pos('LeftHandMiddle1')-pos('LeftHand')).normalized()
    across=(pos('LeftHandIndex1')-pos('LeftHandPinky1')).normalized()
    hand_normals['Left']=long.cross(across).normalized();fingers('Left',.92)
    for bone in rig.pose.bones:
        if bone.name.startswith('LeftHand') and bone.name!='LeftHand':bone.keyframe_insert('rotation_quaternion',frame=frame,group=bone.name)
scene.frame_set(0)
bpy.ops.wm.save_as_mainfile(filepath=str(here/'Nam_Prone_Reload_Review_v1.blend'))
print('RELOAD_FINGERS_REFINED',flush=True)
