"""Remove the bolt gesture and fit magazine-only reload to the existing AK base timer.
Runtime integration must follow ReloadProgress / actual magazine duration after skills.
"""
import bpy,json,math
from pathlib import Path
from mathutils import Matrix,Vector
here=Path(__file__).resolve().parent
out=here.parents[2]/'Logs/AnimationPreview/NamCrawl/ReloadMagazineOnlyPreview';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(here/'Nam_Prone_Reload_Mixamo_Review_v2.blend'))
scene=bpy.data.scenes['Nam Mixamo prone reload review'];bpy.context.window.scene=scene
rig=scene.objects['Nam_Crawl_Review'];gun=scene.objects['AK47_Review']
old=scene.objects['Reload old magazine'];new=scene.objects['Reload replacement magazine']
objects=[rig,gun,old,new]
mags=[o for o in scene.objects if o.type=='MESH' and 'magazine' in o.name.lower()]
def capture(at):
    scene.frame_set(int(at),subframe=at-int(at));bpy.context.view_layer.update()
    return ({b.name:b.matrix_basis.copy() for b in rig.pose.bones},[o.matrix_world.copy() for o in objects],[o.hide_render for o in mags])
def mix(a,b,u):
    return Matrix.LocRotScale(a.translation.lerp(b.translation,u),a.to_quaternion().slerp(b.to_quaternion(),u),a.to_scale().lerp(b.to_scale(),u))
seated=capture(72);ready=capture(192);samples=[]
for f in range(73):
    if f<=60:samples.append(capture(f*72/60))
    else:
        u=(f-60)/12;u=u*u*(3-2*u)
        samples.append(({n:mix(seated[0][n],ready[0][n],u) for n in seated[0]},[mix(a,b,u) for a,b in zip(seated[1],ready[1])],ready[2]))
for o in objects:o.animation_data_clear();o.rotation_mode='QUATERNION'
for o in mags:o.animation_data_clear()
for f,(bones,worlds,visibility) in enumerate(samples):
    scene.frame_set(f)
    for b in rig.pose.bones:
        b.matrix_basis=bones[b.name];b.keyframe_insert('location',frame=f,group=b.name);b.keyframe_insert('rotation_quaternion',frame=f,group=b.name)
    for o,world in zip(objects,worlds):
        o.matrix_world=world;o.keyframe_insert('location',frame=f);o.keyframe_insert('rotation_quaternion',frame=f)
    for o,hidden in zip(mags,visibility):o.hide_render=hidden;o.keyframe_insert('hide_render',frame=f)
for o in objects:
    if o.animation_data and o.animation_data.action:
        for layer in o.animation_data.action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:key.interpolation='LINEAR'
rig.animation_data.action.name='Nam_Prone_Reload_MagazineOnly'
scene.name='Nam prone magazine-only reload';scene.frame_start=0;scene.frame_end=72;scene.render.fps=30
scene.frame_set(0)
note=bpy.data.texts.new('Magazine-only timing')
note.write('Mixamo Prone Reloading derived preview. Source frames 0..72 only: magazine exchange; bolt/receiver gesture 73..165 excluded. Smooth return to approved ready pose. 2.4-second AK base duration from Player prefab / WeaponTuning. Gameplay must sample by PlayerCombat.ReloadProgress, so Shooting ReloadMultiplier and other real duration modifiers affect every stance equally. No new per-stance skill or reload timer. Preview only; gameplay not changed.\n')
bpy.ops.wm.save_as_mainfile(filepath=str(here/'Nam_Prone_Reload_MagazineOnly_v3.blend'))
cam=scene.camera
for i in range(36):
    scene.frame_set(i*2)
    for close in [False,True]:
        cam.data.ortho_scale=1.65 if close else 2.8
        cam.location=(-2.5,-1.6,1.15) if close else (-2.4,-2.7,2.1)
        target=Vector((-.15,-.65,.25) if close else (0,-.13,.2))
        cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(out/f'{"close" if close else "frame"}-{i:03}.png');bpy.ops.render.render(write_still=True)
(out/'timing.json').write_text(json.dumps({'base_seconds':2.4,'keys':73,'source_range':[0,72],'bolt_gesture_removed':True,'recovery_seconds':.4,'runtime_timing_contract':'PlayerCombat.ReloadProgress (includes Shooting ReloadMultiplier)','gameplay_changed':False},indent=2))
print('MAGAZINE_ONLY_RELOAD_PREVIEW_COMPLETE',flush=True)
