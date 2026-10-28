"""Retarget the genuine downloaded Prone Reloading take to Nam; review only."""
import ast
from pathlib import Path
file=Path(__file__).with_name('build_prone_reload.py')
source=file.read_text().replace("ROOT/'Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_Reload.fbx'","HERE/'MixamoSource/Prone_Reloading_XBot.fbx'")
source=source.replace('REFERENCE_Local_Standing_Reload','SOURCE_Mixamo_Prone_Reloading')
tree=ast.parse(source);stop=next(i for i,n in enumerate(tree.body) if isinstance(n,ast.FunctionDef) and n.name=='apply_frame')
exec(compile(ast.Module(body=tree.body[:stop],type_ignores=[]),str(file),'exec'),globals())
OUT=ROOT/'Logs/AnimationPreview/NamCrawl/ReloadMixamoPreview';OUT.mkdir(parents=True,exist_ok=True)
source_map={p.name.split(':')[-1]:p for p in reload_source.pose.bones}
def sp(n):return reload_source.matrix_world@source_map[n].head
def sq(n):return (reload_source.matrix_world@source_map[n].matrix).to_quaternion()
scene.frame_set(int(start));bpy.context.view_layer.update()
first_source={n:sp(n).copy() for n in source_map}
first_q={n:sq(n).copy() for n in source_map}
pose_mixamo(0,'aim')
base_pose=snapshot();base_world=rig.matrix_world.copy()
base_positions={n:pos(n).copy() for n in first_source if n in rig.pose.bones}
base_rotations={n:(rig.matrix_world@rig.pose.bones[n].matrix).to_quaternion() for n in first_source if n in rig.pose.bones}
src_axis=first_source['LeftArm']-first_source['RightArm'];src_axis.z=0
dst_axis=base_positions['LeftArm']-base_positions['RightArm'];dst_axis.z=0
align=src_axis.normalized().rotation_difference(dst_axis.normalized())
ratio=(base_positions['LeftHand']-base_positions['LeftForeArm']).length/(first_source['LeftHand']-first_source['LeftForeArm']).length
right_base=base_positions['RightHand'].copy()
left_correction=base_positions['LeftHand']-right_base-align@((first_source['LeftHand']-first_source['RightHand'])*ratio)
old_offsets={o.name:o.matrix_local.copy() for o in mag_parts}

def set_hand(side,point,rotation,hint):
    solve(side+'Arm',side+'ForeArm',side+'Hand',point,hint)
    wrist_orientation(side,rotation)
def curl(side,amount):
    reset_fingers(side)
    long=(pos(side+'HandMiddle1')-pos(side+'Hand')).normalized()
    across=(pos(side+'HandIndex1')-pos(side+'HandPinky1')).normalized()
    normal=long.cross(across).normalized()
    if side=='Right':normal=-normal
    hand_normals[side]=normal
    for finger in ['Index','Middle','Ring','Pinky']:
        direction=(pos(side+'Hand'+finger+'2')-pos(side+'Hand'+finger+'1')).normalized()
        axis=direction.cross(normal).normalized()
        for segment,angle in [(1,70),(2,95),(3,70)]:
            rotate_bone(side+'Hand'+finger+str(segment),angle,axis)
    solve(side+'HandThumb1',side+'HandThumb2',side+'HandThumb3',pos(side+'HandIndex2')+normal*.012,
          pos(side+'HandThumb1')+Vector((.05 if side=='Left' else -.05,0,.025)))
def apply_frame(frame):
    phase=frame/(end-start);source_time=start+frame
    scene.frame_set(int(source_time));bpy.context.view_layer.update()
    points={n:sp(n).copy() for n in source_map}
    rotations={n:sq(n).copy() for n in source_map}
    rig.animation_data_clear();rig.matrix_world=base_world.copy()
    for b in rig.pose.bones:b.matrix_basis=base_pose[b.name]
    bpy.context.view_layer.update()
    # Small captured torso shifts; retain the approved legs and low body contact.
    for n in ['Spine','Spine1','Spine2','Neck','Head','LeftShoulder','RightShoulder']:
        b=rig.pose.bones[n];world=rig.matrix_world@b.matrix
        delta=align@(rotations[n]@first_q[n].inverted())@align.inverted()
        q=Quaternion().slerp(delta,.22 if n.startswith('Spine') else .45)@world.to_quaternion()
        b.matrix=rig.matrix_world.inverted()@Matrix.LocRotScale(world.translation,q,world.to_scale())
        bpy.context.view_layer.update()
    relative=(points['RightHand']-points['Spine2'])-(first_source['RightHand']-first_source['Spine2'])
    right=right_base+align@(relative*ratio*.4)
    right_q=Quaternion().slerp(align@(rotations['RightHand']@first_q['RightHand'].inverted())@align.inverted(),.25)@base_rotations['RightHand']
    # A rigid rifle stays in the actual right hand, with its magazine pointing down.
    set_hand('Right',right,right_q,base_positions['RightForeArm'])
    gun.matrix_world=(rig.matrix_world@rig.pose.bones['RightHand'].matrix)@base_right.inverted()
    bpy.context.view_layer.update()
    home=gun.matrix_world@mag_local
    hand_target=right+align@((points['LeftHand']-points['RightHand'])*ratio)+left_correction
    hand_target.z=max(.16,hand_target.z)
    hint=base_positions['LeftForeArm']+align@((points['LeftForeArm']-first_source['LeftForeArm'])*ratio*.65)
    hint.z=max(.055,hint.z)
    delta=align@(rotations['LeftHand']@first_q['LeftHand'].inverted())@align.inverted()
    hand_q=delta@base_rotations['LeftHand']
    # Capture the magazine contact before extraction, then keep the prop in the hand.
    set_hand('Left',hand_target,hand_q,hint)
    if frame==18:
        apply_frame.contact=(Matrix.LocRotScale(pos('LeftHand'),hand_q,Vector((1,1,1))).inverted()@Matrix.LocRotScale(home,gun.rotation_quaternion,Vector((1,1,1))))
    curl('Left',.92);curl('Right',.92)
    for o in mag_parts:o.hide_render=frame>=43
    for o in new_parts:o.hide_render=frame<46
    attached=Matrix.LocRotScale(pos('LeftHand'),(rig.matrix_world@rig.pose.bones['LeftHand'].matrix).to_quaternion(),Vector((1,1,1)))@apply_frame.contact if frame>=18 else Matrix.LocRotScale(home,gun.rotation_quaternion,Vector((1,1,1)))
    home_matrix=Matrix.LocRotScale(home,gun.rotation_quaternion,Vector((1,1,1)))
    old.matrix_world=home_matrix if frame<18 else attached
    new.matrix_world=attached
    if frame>=60:
        u=smooth((frame-60)/12)
        new.matrix_world=Matrix.LocRotScale(attached.translation.lerp(home,u),attached.to_quaternion().slerp(home_matrix.to_quaternion(),u),Vector((1,1,1)))
        if frame<=72:
            contact_hand=new.matrix_world@apply_frame.contact.inverted()
            set_hand('Left',contact_hand.translation,contact_hand.to_quaternion(),hint)
    if frame>=72:new.matrix_world=home_matrix
    # End the source's bolt/receiver gesture naturally on the original support.
    if frame>=166:
        u=smooth((frame-166)/26)
        support=gun.matrix_world@base_left
        set_hand('Left',pos('LeftHand').lerp(support.translation,u),hand_q.slerp(support.to_quaternion(),u),hint)
    bpy.context.view_layer.update()
    return {'frame':frame,'left_hand':list(pos('LeftHand')),'hip':list(pos('Hips')),'head':list(pos('Head')),'old_mag':list(old.location),'new_mag':list(new.location),'skin_low':lowest_skin()}

COUNT=int(end-start);samples=[];poses=[]
for frame in range(COUNT+1):
    samples.append(apply_frame(frame))
    poses.append((snapshot(),rig.matrix_world.copy(),gun.matrix_world.copy(),old.matrix_world.copy(),new.matrix_world.copy()))
action=bpy.data.actions.new('Nam_Mixamo_Prone_Reloading');action.use_fake_user=True
rig.animation_data_create();rig.animation_data.action=action
for o in [gun,old,new]:o.animation_data_clear();o.rotation_mode='QUATERNION'
for frame,(local,world,g,om,nm) in enumerate(poses):
    scene.frame_set(frame);rig.matrix_world=world
    for b in rig.pose.bones:
        b.matrix_basis=local[b.name];b.keyframe_insert('location',frame=frame,group=b.name);b.keyframe_insert('rotation_quaternion',frame=frame,group=b.name)
    rig.keyframe_insert('location',frame=frame);rig.keyframe_insert('rotation_quaternion',frame=frame)
    for o,m in [(gun,g),(old,om),(new,nm)]:
        o.matrix_world=m;o.keyframe_insert('location',frame=frame);o.keyframe_insert('rotation_quaternion',frame=frame)
    for o in mag_parts+new_parts:
        o.hide_render=frame>=43 if o in mag_parts else frame<46;o.keyframe_insert('hide_render',frame=frame)
scene.name='Nam Mixamo prone reload review';scene.frame_start=0;scene.frame_end=COUNT;scene.render.fps=30
scene.frame_set(0);camera()
note=bpy.data.texts.new('Genuine Prone Reloading source')
note.write('Actual Mixamo Prone Reloading X Bot, downloaded 2026-10-23, 193 keys at 30 fps / 6.4 seconds. Both hands follow source wrist paths and orientations corrected for Nam proportions; approved legs held; rifle seated to right hand; magazine geometry follows left hand during extraction/replacement. Review only, no Unity gameplay changes.\n')
path=HERE/'Nam_Prone_Reload_Mixamo_Review_v2.blend'
try:bpy.ops.wm.save_as_mainfile(filepath=str(path))
except RuntimeError:
    pending=Path(str(path)+'@');content=pending.read_bytes()
    assert content[:7]==b'BLENDER' or content[:4]==bytes.fromhex('28b52ffd');path.write_bytes(content)
(OUT/'validation.json').write_text(json.dumps({'actual_mixamo_reload':True,'frames':COUNT+1,'seconds':COUNT/30,'source':str(HERE/'MixamoSource/Prone_Reloading_XBot.fbx'),'samples':samples},indent=2))
for i in ([0,12,21,27,36,48,60,72,84,96] if '--stills' in sys.argv else range(97)):
    scene.frame_set(min(COUNT,i*2));camera()
    scene.render.filepath=str(OUT/f'frame-{i:03}.png');bpy.ops.render.render(write_still=True)
    cam.data.ortho_scale=1.65;cam.location=(-2.5,-1.6,1.15)
    cam.rotation_euler=(Vector((-.15,-.65,.25))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(OUT/f'close-{i:03}.png');bpy.ops.render.render(write_still=True)
print('ACTUAL_MIXAMO_RELOAD_REVIEW_COMPLETE',flush=True)
