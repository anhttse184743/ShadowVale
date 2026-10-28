"""Render baked review scenes without repeating source retargeting."""
import bpy
from pathlib import Path
from mathutils import Vector
here=Path(__file__).resolve().parent
out=here.parents[2]/'Logs/AnimationPreview/NamCrawl/MixamoPreview'
bpy.ops.wm.open_mainfile(filepath=str(here/'Nam_Crawl_Mixamo_Review_v4.blend'))
for kind in ['unarmed','rifle','aim','knife','grenade','stone','transition']:
    scene=bpy.data.scenes['Mixamo '+kind];bpy.context.window.scene=scene
    cam=scene.camera
    def camera(side=False):
        cam.data.ortho_scale=3.1 if side else 2.8
        cam.location=(-3,.1,.85) if side else (-2.4,-2.7,2.1)
        cam.rotation_euler=(Vector((0,-.13,.2))-cam.location).to_track_quat('-Z','Y').to_euler()
    for i in range(72 if kind=='transition' else 36):
        scene.frame_set(i*2);camera()
        scene.render.filepath=str(out/kind/f'frame-{i:03}.png');bpy.ops.render.render(write_still=True)
        if kind=='transition':
            camera(True);scene.render.filepath=str(out/kind/f'side-{i:03}.png');bpy.ops.render.render(write_still=True)
    print('RENDERED_SAVED',kind,flush=True)
print('SAVED_PREVIEWS_COMPLETE',flush=True)
