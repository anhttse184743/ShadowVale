from pathlib import Path
import bpy
from mathutils import Vector
out=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(out/'BunkerLayout_Proposal.blend'))
for ob in bpy.data.collections['01 Crafting workbench'].objects: ob.location.y += .03
for ob in bpy.data.collections['02 Mission board'].objects: ob.location += Vector((1.9,-.56,0))
bpy.context.view_layer.update()
bench=bpy.data.collections['01 Crafting workbench']
tops=[o for o in bench.objects if o.name.startswith('Separate solid top plank')]
edge=max((o.matrix_world@Vector(c)).y for o in tops for c in o.bound_box)
assert abs(2.89-edge-.03)<.00001,(edge,2.89-edge)
scene=bpy.context.scene;cam=scene.camera
scene.cycles.samples=16
for name,pos,target,lens in [('01_layout',(-7.9,-10.8,7.3),(-.8,.65,.7),45),('02_workbench',(-6.5,-2.8,3.7),(-3.05,2.0,.8),48),('03_meeting',(4.5,-5.7,3.3),(1.15,-.2,.72),52)]:
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=lens
    scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'BunkerLayout_Proposal.blend'))
print('PASS tabletop-wall gap =',2.89-edge,'metres')
