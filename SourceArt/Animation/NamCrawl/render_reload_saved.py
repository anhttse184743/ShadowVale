import bpy,sys
from pathlib import Path
from mathutils import Vector
here=Path(__file__).resolve().parent;out=here.parents[2]/'Logs/AnimationPreview/NamCrawl/ReloadPreview'
bpy.ops.wm.open_mainfile(filepath=str(here/'Nam_Prone_Reload_Review_v1.blend'))
scene=bpy.data.scenes['Nam prone reload review'];bpy.context.window.scene=scene;cam=scene.camera
for i in range(64):
    scene.frame_set(min(126,i*2));close='--close' in sys.argv
    cam.data.ortho_scale=1.6 if close else 2.8;cam.location=(-2.5,-1.6,1.15) if close else (-2.4,-2.7,2.1)
    cam.rotation_euler=(Vector((-.15,-.65,.25) if close else (0,-.13,.20))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(out/f'{"close" if close else "frame"}-{i:03}.png');bpy.ops.render.render(write_still=True)
print('RELOAD_SAVED_RENDERED',flush=True)
