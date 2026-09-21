import bpy, os
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=os.path.abspath('outputs/ShadowVale_Map01.blend'))
s=bpy.context.scene;c=bpy.data.objects['CAM_03 • Abandoned base'];c.location=(58,12,110);c.rotation_euler=(Vector((46,31,2))-c.location).to_track_quat('-Z','Y').to_euler();c.data.ortho_scale=47
bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath('outputs/ShadowVale_Map01.blend'))
s.camera=c;s.render.resolution_x=1400;s.render.resolution_y=1200;s.cycles.samples=16;s.render.filepath=os.path.abspath('outputs/Map01_AbandonedBase.png');bpy.ops.render.render(write_still=True)
