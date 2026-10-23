"""Retarget genuine downloaded Mixamo motions onto Nam's unchanged skeleton.
Artist preview only. No upload, Unity controller or gameplay edits.
"""
import ast
from pathlib import Path
import numpy as np
_author=Path(__file__).with_name('build_crawl.py')
_tree=ast.parse(_author.read_text())
_stop=next(i for i,n in enumerate(_tree.body) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='validation' for t in n.targets))
exec(compile(ast.Module(body=_tree.body[:_stop],type_ignores=[]),str(_author),'exec'),globals())
OUT=ROOT/'Logs/AnimationPreview/NamCrawl/MixamoPreview';OUT.mkdir(parents=True,exist_ok=True)
REFERENCE_FIT=True # Use the elbow-plane floor constraint, not the procedural pose.
scene.render.fps=30
NKEY=72;NPREVIEW=36
target_world=rig.matrix_world.copy()
target_rest={b.name:(target_world@b.matrix_local).to_quaternion() for b in rig.data.bones}
original_rest={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
sources={}
for key,file in [('rifle','Prone_Forward_InPlace_XBot.fbx'),('aim','Prone_Idle_XBot.fbx'),('unarmed','Crawl_Forward_XBot.fbx')]:
    before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(HERE/'MixamoSource'/file))
    objects=set(bpy.data.objects)-before
    source=next(o for o in objects if o.type=='ARMATURE');source.name='SOURCE_Mixamo_'+key
    action=source.animation_data.action;action.name='SOURCE_'+file.removesuffix('.fbx');action.use_fake_user=True
    start,end=action.frame_range
    mapping={p.name.split(':')[-1]:p for p in source.pose.bones}
    restq={name:(source.matrix_world@p.bone.matrix_local).to_quaternion() for name,p in mapping.items()}
    source_height=(source.matrix_world@mapping['Hips'].bone.head_local).z
    height=(target_world@rig.data.bones['Hips'].head_local).z/source_height
    scene.frame_set(int(start));first=source.matrix_world@mapping['Hips'].head
    scene.frame_set(int(end));last=source.matrix_world@mapping['Hips'].head
    for ob in objects:ob.hide_render=True
    sources[key]=(source,mapping,restq,float(start),float(end),height,first.copy(),last.copy())

def retarget(phase,key):
    source,mapping,restq,start,end,ratio,first,last=sources[key]
    rig.animation_data_clear();rig.matrix_world=target_world.copy()
    t=start+(end-start)*phase
    scene.frame_set(int(t),subframe=t-int(t));bpy.context.view_layer.update()
    rotations={name:(source.matrix_world@p.matrix).to_quaternion()@restq[name].inverted()@target_rest[name] for name,p in mapping.items() if name in target_rest}
    hips=(source.matrix_world@mapping['Hips'].head)*ratio
    # Remove source travel, retain the captured side-to-side/vertical movement.
    hips.x-=(first.x+(last.x-first.x)*phase)*ratio
    hips.y-=(first.y+(last.y-first.y)*phase)*ratio
    for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    for p in rig.pose.bones:
        if p.name not in rotations:continue
        m=rig.matrix_world@p.matrix
        position=hips if p.name=='Hips' else m.translation
        p.matrix=rig.matrix_world.inverted()@Matrix.LocRotScale(position,rotations[p.name],m.to_scale())
        bpy.context.view_layer.update()

def lowest_skin():
    graph=bpy.context.evaluated_depsgraph_get();lowest=1000
    for ob in character:
        ev=ob.evaluated_get(graph);mesh=ev.to_mesh()
        try:
            coords=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('co',coords)
            coords=coords.reshape(-1,3);world=np.asarray(ev.matrix_world,dtype=np.float32)
            lowest=min(lowest,float((coords@world[2,:3]+world[2,3]).min()))
        finally:ev.to_mesh_clear()
    return lowest

# The approved aimed pose supplies the contact correction; Mixamo supplies its
# captured motion as an additive layer. Keep both source and authored pose editable.
with bpy.data.libraries.load(str(HERE/'Nam_Crawl_Prone_Reference_v3.blend'),link=False) as (_from,_to):_to.scenes=['Crawl '+k for k in ['unarmed','rifle','aim','knife','grenade','stone']]
authored={};authored_contacts={}
for _reference_scene in _to.scenes:
    bpy.context.window.scene=_reference_scene;_reference_scene.frame_set(0)
    _reference_rig=next(o for o in _reference_scene.objects if o.type=='ARMATURE')
    authored[_reference_scene.name.removeprefix('Crawl ').split('.')[0]]=({p.name:p.matrix_basis.copy() for p in _reference_rig.pose.bones},_reference_rig.matrix_world.copy())
    authored_contacts[_reference_scene.name.removeprefix('Crawl ').split('.')[0]]={p.name:(_reference_rig.matrix_world@p.matrix).copy() for p in _reference_rig.pose.bones}
bpy.context.window.scene=scene
source_first={}
for key in sources:
    retarget(0,key);source_first[key]=({p.name:p.matrix_basis.copy() for p in rig.pose.bones},pos('Hips').copy())

def reset_fingers(side):
    for bone in rig.pose.bones:
        if bone.name.startswith(side+'Hand') and bone.name!=side+'Hand':bone.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()

def clench_empty_hand(side,kind):
    # Hold the authored wrist angle relative to the moving forearm throughout
    # the loop. Do not inherit Mixamo wrist flexion/twist on an empty fist.
    rig.pose.bones[side+'Hand'].matrix_basis=authored[kind][0][side+'Hand'].copy()
    bpy.context.view_layer.update()
    reset_fingers(side)
    long=(pos(side+'HandMiddle1')-pos(side+'Hand')).normalized()
    across=(pos(side+'HandIndex1')-pos(side+'HandPinky1')).normalized()
    inward=long.cross(across).normalized()
    if side=='Right':inward=-inward
    for finger in ['Index','Middle','Ring','Pinky']:
        direction=(pos(side+'Hand'+finger+'2')-pos(side+'Hand'+finger+'1')).normalized()
        axis=direction.cross(inward).normalized()
        for segment,angle in [(1,70),(2,95),(3,70)]:
            rotate_bone(side+'Hand'+finger+str(segment),angle,axis)
    # Lay the thumb across the outside of the curled index finger.
    thumb=side+'HandThumb'
    solve(thumb+'1',thumb+'2',thumb+'3',pos(side+'HandIndex2')-inward*.012,
          pos(thumb+'1')+long*.04-inward*.025)
    bpy.context.view_layer.update()

def ground_with_props():
    low=lowest_skin()
    active=set()
    for parent,meshes in props.values():
        if not any(not ob.hide_render for ob in meshes):continue
        active.add(parent)
        for ob in meshes:
            if ob.hide_render:continue
            points=np.empty(len(ob.data.vertices)*3,dtype=np.float32);ob.data.vertices.foreach_get('co',points)
            matrix=np.asarray(ob.matrix_world,dtype=np.float32)
            low=min(low,float((points.reshape(-1,3)@matrix[2,:3]+matrix[2,3]).min()))
    delta=-.014-low
    rig.location.z+=delta
    for parent in active:parent.location.z+=delta
    bpy.context.view_layer.update()

def rifle_contact(weight):
    for side,sign in [('Left',1),('Right',-1)]:
        wrist=Vector((-.015,.015,.07)).lerp(Vector((-.015,.065,-.005)),weight) if side=='Right' else Vector((0,-.28,.025)).lerp(foregrip+Vector((.035,.075,-.045)),weight)
        solve(side+'Arm',side+'ForeArm',side+'Hand',gun.location+gun.rotation_quaternion@wrist,pos(side+'ForeArm'))
        reset_fingers(side)
        direction=Vector((0,-.8*weight,-1+.4*weight)) if side=='Right' else Vector((-.35*weight,-1,-.05))
        normal=Vector((1,0,0)) if side=='Right' else Vector((0,0,1))
        direction=gun.rotation_quaternion@direction;normal=gun.rotation_quaternion@normal
        hand_normals[side]=normal;palm(side,direction,normal);fingers(side,.92)
        solve(side+'HandThumb1',side+'HandThumb2',side+'HandThumb3',pos(side+'HandIndex2')+normal*.012,pos(side+'HandThumb1')+Vector((sign*.05,0,.025)))

def stabilize_crawl_contacts(kind,phase):
    reference=authored_contacts[kind]
    # Keep the feet at their authored ground height. The captured knee swing
    # must not drive a boot underground and lift the entire character.
    for side in ['Left','Right']:
        foot=side+'Foot';knee=side+'Leg'
        target=reference[foot].translation.lerp(pos(foot),.65)
        target.z=reference[foot].translation.z
        hint=reference[knee].translation.copy()
        solve(side+'UpLeg',knee,foot,target,hint)
        bone=rig.pose.bones[foot];world=rig.matrix_world@bone.matrix
        bone.matrix=rig.matrix_world.inverted()@Matrix.LocRotScale(world.translation,reference[foot].to_quaternion(),world.to_scale())
        bpy.context.view_layer.update()
    free=['Left','Right'] if kind=='unarmed' else ['Left'] if kind in ['knife','grenade','stone'] else []
    for side in free:
        target=pos(side+'Hand').copy();target.z=reference[side+'Hand'].translation.z
        solve(side+'Arm',side+'ForeArm',side+'Hand',target,reference[side+'ForeArm'].translation)

def pose_mixamo(phase,kind):
    if kind=='transition':
        pose_mixamo(0,'rifle');rest=snapshot();world_rest=rig.matrix_world.copy();gp0=gun.location.copy();gq0=gun.rotation_quaternion.copy()
        pose_mixamo(phase,'aim');aim=snapshot();world_aim=rig.matrix_world.copy();gp1=gun.location.copy();gq1=gun.rotation_quaternion.copy()
        weight=aim_progress(phase)
        rig.matrix_world=Matrix.LocRotScale(world_rest.translation.lerp(world_aim.translation,weight),world_rest.to_quaternion().slerp(world_aim.to_quaternion(),weight),world_rest.to_scale())
        for bone in rig.pose.bones:
            a,b=rest[bone.name],aim[bone.name]
            bone.matrix_basis=Matrix.LocRotScale(a.translation.lerp(b.translation,weight),a.to_quaternion().slerp(b.to_quaternion(),weight),a.to_scale().lerp(b.to_scale(),weight))
        bpy.context.view_layer.update();gun.location=gp0.lerp(gp1,weight);gun.rotation_quaternion=gq0.slerp(gq1,weight);rifle_contact(weight);ground_with_props();return
    # Exact duplicate endpoint; keep a short local-space blend into the loop seam.
    p=0 if phase>=1 else phase
    base='unarmed' if kind=='unarmed' else 'aim' if kind=='aim' else 'rifle'
    retarget(p,base)
    moving={bone.name:bone.matrix_basis.copy() for bone in rig.pose.bones};hip_delta=pos('Hips')-source_first[base][1]
    reference,reference_world=authored[kind];rig.matrix_world=reference_world.copy()
    for bone in rig.pose.bones:
        ref=reference[bone.name];delta=source_first[base][0][bone.name].to_quaternion().inverted()@moving[bone.name].to_quaternion()
        amount=1 if kind=='aim' else .16 if bone.name in ['Hips','Spine','Spine1','Spine2','Neck','Head'] else .32 if any(part in bone.name for part in ['Leg','Foot']) else .45
        if kind=='aim' and not any(word in bone.name for word in ['Hips','Spine','Neck','Head','Leg','Foot']):amount=0
        rotation=ref.to_quaternion()@Quaternion().slerp(delta,amount)
        bone.matrix_basis=Matrix.LocRotScale(ref.translation,rotation,ref.to_scale())
    if kind!='aim':rig.location+=Vector((hip_delta.x*.18,hip_delta.y*.18,0))
    bpy.context.view_layer.update()
    if kind!='aim':stabilize_crawl_contacts(kind,p)
    for parent,meshes in props.values():
        for ob in meshes:ob.hide_render=True
    if kind=='unarmed':
        clench_empty_hand('Left',kind);clench_empty_hand('Right',kind);ground_with_props();return
    head=pos('Head');chest=pos('Spine2')
    if kind in ['rifle','aim']:
        gun.rotation_mode='QUATERNION'
        gun.rotation_quaternion=Quaternion() if kind=='aim' else Quaternion((0,0,1),math.radians(75))@Quaternion((0,1,0),math.radians(-75))
        gun.location=Vector((-.14,-.98,.268)) if kind=='aim' else Vector((head.x,head.y-.10,.16))
        for ob in guns:ob.hide_render=False
        rifle_contact(1 if kind=='aim' else 0)
        ground_with_props()
        return
    # A held small prop uses the captured right-arm motion, leaving the left arm free.
    clench_empty_hand('Left',kind)
    wrist=pos('RightHand');wrist.z=max(wrist.z,.15)
    solve('RightArm','RightForeArm','RightHand',wrist,pos('RightForeArm'))
    reset_fingers('Right');hand_normals['Right']=Vector((1,0,0));palm('Right',(0,0,-1),(1,0,0));fingers('Right',.95)
    parent,meshes=props[kind]
    for ob in meshes:ob.hide_render=False
    parent.location=pos('RightHand')+Vector((.035,-.025,-.065));parent.rotation_mode='QUATERNION'
    parent.rotation_quaternion=Quaternion((0,0,1),-.3) if kind=='knife' else Quaternion((0,1,0),math.pi/2) if kind=='grenade' else Quaternion()
    if kind=='knife':parent.location.y-=.08
    ground_with_props()

def snapshot():return {p.name:p.matrix_basis.copy() for p in rig.pose.bones}
def apply_snapshot(local,location,rotation):
    rig.location=location;rig.rotation_quaternion=rotation
    for p in rig.pose.bones:p.matrix_basis=local[p.name]
    bpy.context.view_layer.update()

validation={};actions={}
KINDS=['unarmed','rifle','aim','knife','grenade','stone','transition']
for kind in KINDS:
    count=NKEY*2 if kind=='transition' else NKEY
    pose_mixamo(0,kind);first=snapshot();first_location=rig.location.copy();first_rotation=rig.rotation_quaternion.copy()
    action=bpy.data.actions.new('Nam_Mixamo_Prone_'+kind.title());action.use_fake_user=True;actions[kind]=action
    samples=[]
    for frame in range(count+1):
        phase=frame/count;pose_mixamo(phase,kind)
        local=snapshot();location=rig.location.copy();rotation=rig.rotation_quaternion.copy()
        blend=smooth((phase-.88)/.12)
        if blend>0:
            local={name:Matrix.LocRotScale(local[name].translation.lerp(first[name].translation,blend),local[name].to_quaternion().slerp(first[name].to_quaternion(),blend),local[name].to_scale().lerp(first[name].to_scale(),blend)) for name in local}
            location=location.lerp(first_location,blend);rotation=rotation.slerp(first_rotation,blend)
        apply_snapshot(local,location,rotation)
        rig.animation_data_create();rig.animation_data.action=action
        rig.keyframe_insert('location',frame=frame);rig.keyframe_insert('rotation_quaternion',frame=frame)
        for bone in rig.pose.bones:
            bone.keyframe_insert('location',frame=frame,group=bone.name);bone.keyframe_insert('rotation_quaternion',frame=frame,group=bone.name)
        samples.append({'left_foot':list(pos('LeftFoot')),'right_hand':list(pos('RightHand')),'head':list(pos('Head'))})
    folder=OUT/kind;folder.mkdir(exist_ok=True)
    validation[kind]={'source':sources['unarmed' if kind=='unarmed' else 'aim' if kind=='aim' else 'rifle'][0].animation_data.action.name,'keys':count+1,'duration':count/30,'start':samples[0],'end':samples[-1]}
    if '--focus-aim' in sys.argv and kind not in ['aim','transition']:continue
    if '--focus-fists' in sys.argv and kind not in ['unarmed','knife','grenade','stone']:continue
    if '--bake-only' in sys.argv:continue
    preview_count=NPREVIEW*2 if kind=='transition' else NPREVIEW
    pose_mixamo(0,kind);rig.animation_data_create();rig.animation_data.action=action;scene.frame_set(0)
    attached=None
    prop_key='rifle' if kind=='transition' else kind
    if prop_key in props:
        attached=props[prop_key][0];world=attached.matrix_world.copy();attached.parent=rig;attached.parent_type='BONE';attached.parent_bone='RightHand'
        bpy.context.view_layer.update();attached.matrix_world=world;bpy.context.view_layer.update()
    for i in ([0,preview_count//4,preview_count//2,preview_count*3//4] if STILLS else range(preview_count)):
        scene.frame_set(i*2);bpy.context.view_layer.update()
        camera();scene.render.filepath=str(folder/f'frame-{i:03}.png');bpy.ops.render.render(write_still=True)
        if kind=='transition':
            camera(True);scene.render.filepath=str(folder/f'side-{i:03}.png');bpy.ops.render.render(write_still=True)
    scene.frame_set(count//4);camera(True);scene.render.filepath=str(folder/'side.png');bpy.ops.render.render(write_still=True)
    if attached:
        world=attached.matrix_world.copy();attached.parent=None;attached.matrix_world=world
    validation[kind]={'source':sources['unarmed' if kind=='unarmed' else 'aim' if kind=='aim' else 'rifle'][0].animation_data.action.name,'keys':count+1,'duration':count/30,'start':samples[0],'end':samples[-1]}
    print('MIXAMO_VARIANT_COMPLETE',kind,flush=True)

# Save a directly playable scene per variant, with its prop bone attachment.
scene.name='Mixamo source studio';scene.frame_start=0;scene.frame_end=NKEY-1;reviews=[]
for kind in KINDS:
    bpy.context.window.scene=scene;pose_mixamo(0,kind);camera()
    rig.animation_data_create();rig.animation_data.action=actions[kind];scene.frame_set(0)
    parent=None
    prop_key='rifle' if kind=='transition' else kind
    if prop_key in props:
        parent=props[prop_key][0];world=parent.matrix_world.copy();parent.parent=rig;parent.parent_type='BONE';parent.parent_bone='RightHand'
        bpy.context.view_layer.update();parent.matrix_world=world;bpy.context.view_layer.update()
    bpy.ops.scene.new(type='FULL_COPY');review=bpy.context.scene;review.name='Mixamo '+kind;review.frame_end=(NKEY*2 if kind=='transition' else NKEY)-1;reviews.append(review)
    bpy.context.window.scene=scene
    if parent:
        world=parent.matrix_world.copy();parent.parent=None;parent.matrix_world=world
assert original_rest=={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
bpy.context.window.scene=reviews[1]
note=bpy.data.texts.new('Mixamo source notes');note.write('Actual Mixamo Prone Forward, Prone Idle and Crawl Forward, downloaded through the user session. Retargeted onto original Nam rest skeleton, with prop contact corrections and skin grounding. Source actions/rigs retained hidden from renders. Six editable preview scenes. No Unity gameplay bindings.\n')
bpy.ops.file.pack_all()
blend_path=HERE/'Nam_Crawl_Mixamo_Review_v4.blend'
try:bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
except RuntimeError:
    pending=Path(str(blend_path)+'@')
    if not pending.exists():raise
    content=pending.read_bytes()
    if content[:7]!=b'BLENDER' and content[:4]!=bytes.fromhex('28b52ffd'):raise
    blend_path.write_bytes(content)
    print('Recovered completed Blender temporary save',flush=True)
(OUT/'mixamo-retarget-validation.json').write_text(json.dumps({'source_rest_preserved':True,'gameplay_changed':False,'variants':validation},indent=2))
print('MIXAMO_REVIEW_COMPLETE',flush=True)
