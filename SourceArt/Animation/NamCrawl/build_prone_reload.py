"""Artist review: existing genuine Mixamo Prone Idle plus local rifle-reload reference.
No new Mixamo Prone Reloading download; no Unity asset/controller changes.
"""
import ast
from pathlib import Path
builder=Path(__file__).with_name('build_mixamo_preview.py')
tree=ast.parse(builder.read_text())
stop=next(i for i,n in enumerate(tree.body) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='validation' for t in n.targets))
exec(compile(ast.Module(body=tree.body[:stop],type_ignores=[]),str(builder),'exec'),globals())
OUT=ROOT/'Logs/AnimationPreview/NamCrawl/ReloadPreview';OUT.mkdir(parents=True,exist_ok=True)
before=set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_Reload.fbx'))
imported=set(bpy.data.objects)-before
reload_source=next(o for o in imported if o.type=='ARMATURE')
reload_source.name='REFERENCE_Local_Standing_Reload'
for ob in imported:ob.hide_render=True
reload_action=reload_source.animation_data.action
reload_action.name='REFERENCE_Local_Standing_Reload';reload_action.use_fake_user=True
start,end=reload_action.frame_range
scene.frame_set(int(start));bpy.context.view_layer.update()
reload_first={p.name:p.matrix_basis.to_quaternion().copy() for p in reload_source.pose.bones}
pose_mixamo(0,'aim')
rest_gun=gun.matrix_world.copy();rest_gun.translation.z-=.05
gun.parent=None;gun.matrix_world=rest_gun
bpy.context.view_layer.update()
base_right=(gun.matrix_world.inverted()@(rig.matrix_world@rig.pose.bones['RightHand'].matrix)).copy()
base_left=(gun.matrix_world.inverted()@(rig.matrix_world@rig.pose.bones['LeftHand'].matrix)).copy()
mag_parts=[o for o in guns if 'magazine' in o.name.lower()]
assert len(mag_parts)>=3
mag_home=sum((sum((o.matrix_world@Vector(c) for c in o.bound_box),Vector())/8 for o in mag_parts),Vector())/len(mag_parts)
mag_local=gun.matrix_world.inverted()@mag_home
old=bpy.data.objects.new('Reload old magazine',None);scene.collection.objects.link(old)
old.location=mag_home
bpy.context.view_layer.update()
for ob in mag_parts:
    world=ob.matrix_world.copy();ob.parent=old;ob.matrix_world=world
    bpy.context.view_layer.update()
new=bpy.data.objects.new('Reload replacement magazine',None);scene.collection.objects.link(new)
new.location=mag_home
bpy.context.view_layer.update()
new_parts=[]
for ob in mag_parts:
    copy=ob.copy();copy.data=ob.data;scene.collection.objects.link(copy)
    copy.parent=new;copy.matrix_parent_inverse=ob.matrix_parent_inverse.copy();copy.matrix_basis=ob.matrix_basis.copy()
    copy.name='Replacement '+ob.name;new_parts.append(copy)
def ease(a,b,t):return smooth((t-a)/(b-a))
def path(points,t):
    for (a,p),(b,q) in zip(points,points[1:]):
        if t<=b:return Vector(p).lerp(Vector(q),ease(a,b,t))
    return Vector(points[-1][1])
def wrist_orientation(side,q):
    bone=rig.pose.bones[side+'Hand'];world=rig.matrix_world@bone.matrix
    bone.matrix=rig.matrix_world.inverted()@Matrix.LocRotScale(world.translation,q,world.to_scale())
    bpy.context.view_layer.update()
def apply_frame(frame):
    t=frame/30
    # Retain the downloaded idle's restrained breathing and planted lower body.
    pose_mixamo((frame%72)/72,'aim')
    source_time=start+(end-start)*min(1,t/4.2)
    scene.frame_set(int(source_time),subframe=source_time-int(source_time));bpy.context.view_layer.update()
    delta=reload_first['LeftShoulder'].inverted()@reload_source.pose.bones['LeftShoulder'].matrix_basis.to_quaternion()
    shoulder=rig.pose.bones['LeftShoulder'];shoulder.rotation_quaternion=shoulder.rotation_quaternion@Quaternion().slerp(delta,.28)
    bpy.context.view_layer.update()
    envelope=ease(0,.45,t)*(1-ease(3.7,4.2,t))
    gun.matrix_world=rest_gun.copy()
    gun.location+=Vector((.025,.045,.008))*envelope
    gun.rotation_mode='QUATERNION';gun.rotation_quaternion=rest_gun.to_quaternion()@Quaternion((1,0,0),math.radians(-5)*envelope)
    bpy.context.view_layer.update()
    right=gun.matrix_world@base_right
    solve('RightArm','RightForeArm','RightHand',right.translation,pos('RightForeArm'))
    wrist_orientation('Right',right.to_quaternion())
    home=gun.matrix_world@mag_local
    out=home+Vector((-.01,.025,-.085))
    pouch=pos('Hips')+Vector((-.40,-.25,.005));pouch.z=max(.24,pouch.z)
    oldpos=path([(0,home),(.8,home),(1.15,out),(1.8,pouch),(4.2,pouch)],t)
    newpos=path([(0,pouch),(2.05,pouch),(2.65,out),(3.05,home),(4.2,home)],t)
    old.location=oldpos;new.location=newpos
    old.rotation_mode=new.rotation_mode='QUATERNION'
    old.rotation_quaternion=new.rotation_quaternion=gun.rotation_quaternion.copy()
    # End-pivot the magazine as it comes free; keep its bottom clear of the floor.
    old.rotation_quaternion=old.rotation_quaternion@Quaternion((1,0,0),math.radians(-12)*ease(.8,1.2,t))
    for ob in mag_parts:ob.hide_render=t>=1.95
    for ob in new_parts:ob.hide_render=t<1.95
    grip=lambda center:center+Vector((-.065,.005,.045))
    support=(gun.matrix_world@base_left).translation
    left=path([(0,support),(.35,support),(.8,grip(home)),(1.15,grip(out)),(1.8,grip(pouch)),
               (2.05,grip(pouch)),(2.65,grip(out)),(3.05,grip(home)),(3.3,grip(home)),
               (3.55,gun.location+Vector((.055,-.11,.02))),
               (3.75,gun.location+Vector((.055,-.025,.02))),(4.2,support)],t)
    hint=pos('LeftForeArm').copy();hint.x-=.025*envelope
    solve('LeftArm','LeftForeArm','LeftHand',left,hint)
    baseline=(gun.matrix_world@base_left).to_quaternion()
    palm('Left',(0,0,-1),(1,0,0));closed=(rig.matrix_world@rig.pose.bones['LeftHand'].matrix).to_quaternion()
    grip_weight=ease(.35,.8,t)*(1-ease(3.3,4.2,t))
    wrist_orientation('Left',baseline.slerp(closed,grip_weight))
    reset_fingers('Left')
    long=(pos('LeftHandMiddle1')-pos('LeftHand')).normalized()
    across=(pos('LeftHandIndex1')-pos('LeftHandPinky1')).normalized()
    hand_normals['Left']=long.cross(across).normalized();fingers('Left',.92)
    # The right hand always retains the rifle. Thumb contact is retained from the approved aim pose.
    bpy.context.view_layer.update()
    shown=new_parts if t>=1.95 else mag_parts
    actual=sum((sum((o.matrix_world@Vector(c) for c in o.bound_box),Vector())/8 for o in shown),Vector())/len(shown)
    expected=new.location if t>=1.95 else old.location
    assert (actual-expected).length<.001,('magazine transform error',t,list(actual),list(expected))
    return {'time':t,'head':list(pos('Head')),'hip':list(pos('Hips')),'left_hand':list(pos('LeftHand')),'old_mag':list(old.location),'new_mag':list(new.location)}

samples=[];poses=[]
for frame in range(127):
    sample=apply_frame(frame);samples.append(sample)
    poses.append((snapshot(),rig.matrix_world.copy(),gun.matrix_world.copy(),old.matrix_world.copy(),new.matrix_world.copy(),frame/30>=1.95))
action=bpy.data.actions.new('Nam_Prone_Reload_Review');action.use_fake_user=True
rig.animation_data_create();rig.animation_data.action=action
for ob in [gun,old,new]:ob.animation_data_clear();ob.rotation_mode='QUATERNION'
for frame,(local,world,g,om,nm,replacement) in enumerate(poses):
    scene.frame_set(frame);rig.matrix_world=world
    for bone in rig.pose.bones:
        bone.matrix_basis=local[bone.name]
        bone.keyframe_insert('location',frame=frame,group=bone.name);bone.keyframe_insert('rotation_quaternion',frame=frame,group=bone.name)
    rig.keyframe_insert('location',frame=frame);rig.keyframe_insert('rotation_quaternion',frame=frame)
    for ob,m in [(gun,g),(old,om),(new,nm)]:
        ob.matrix_world=m;ob.keyframe_insert('location',frame=frame);ob.keyframe_insert('rotation_quaternion',frame=frame)
    for ob in mag_parts+new_parts:
        ob.hide_render=replacement if ob in mag_parts else not replacement
        ob.keyframe_insert('hide_render',frame=frame)
scene.frame_start=0;scene.frame_end=126;scene.render.fps=30
scene.name='Nam prone reload review'
for ob in [rig,gun,old,new]:
    if ob.animation_data and ob.animation_data.action:
        for layer in ob.animation_data.action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for fc in bag.fcurves:
                        for key in fc.keyframe_points:key.interpolation='LINEAR'
for i in range(64):
    scene.frame_set(min(126,i*2));camera()
    scene.render.filepath=str(OUT/f'frame-{i:03}.png');bpy.ops.render.render(write_still=True)
    if i in [0,12,24,32,43,49,56,63]:
        camera(True);scene.render.filepath=str(OUT/f'side-{i:03}.png');bpy.ops.render.render(write_still=True)
camera();scene.frame_set(0)
note=bpy.data.texts.new('Prone reload provenance')
note.write('Preview only. Base: genuine Mixamo Prone Idle already downloaded; shoulder reference: existing project Nam_Rifle_Reload.fbx; magazine/arm contacts authored in Blender. No new Mixamo Prone Reloading download. User asked to review before gameplay integration.\n')
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Nam_Prone_Reload_Review_v1.blend'))
(OUT/'validation.json').write_text(json.dumps({'preview_only':True,'new_mixamo_reload_downloaded':False,'frames':127,'seconds':4.2,'samples':samples},indent=2))
print('PRONE_RELOAD_REVIEW_COMPLETE',flush=True)
