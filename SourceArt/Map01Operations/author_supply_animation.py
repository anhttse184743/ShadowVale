"""Author stationary supply interactions on Nam's original rig; bake 30 fps.
References, per-frame contacts and original rest bones stay editable in the blend.
blender --background --python SourceArt/Map01Operations/author_supply_animation.py
"""
from pathlib import Path
import bpy, math, json, sys
from mathutils import Vector, Matrix, Quaternion
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[1];ART=ROOT/'Assets/_Project/Art'
OUT=ART/'Characters/Animations/Map01Operations';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.render.fps=30;scene.unit_settings.system='METRIC'
def load(path,name):
    before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(path));scene.render.fps=30
    objects=list(set(bpy.data.objects)-before);rig=next(o for o in objects if o.type=='ARMATURE');rig.name=name;return rig,objects
nam,objects=load(ART/'Characters/Player/Player.fbx','Nam')
nam.animation_data_create()
rest={b.name:[list(row) for row in b.matrix_local] for b in nam.data.bones}
refs={}
for name,file in [('Idle','Nam_Rifle_Idle'),('Open','Nam_OpenChest'),('Pickup','Nam_PickUp')]:
    rig,obs=load(ART/('Characters/Animations/Nam/'+file+'.fbx'),'REFERENCE_'+name);refs[name]=rig
    for ob in obs:ob.hide_render=True;ob.hide_set(True)
    rig.animation_data.action.name='REFERENCE_'+name;rig.animation_data.action.use_fake_user=True
def smooth(x):x=max(0,min(1,x));return x*x*(3-2*x)
def source(name,frame):
    scene.frame_set(round(frame));bpy.context.view_layer.update();return {b.name:b.matrix.copy() for b in refs[name].pose.bones}
def interpolate(a,b,w):
    out={}
    for name in a:
        if name not in b:continue
        p,q,s=a[name].decompose();p2,q2,s2=b[name].decompose();out[name]=Matrix.LocRotScale(p.lerp(p2,w),q.slerp(q2,w),s.lerp(s2,w))
    return out
def apply(matrices):
    nam.animation_data_create();nam.animation_data.action=None
    for pb in nam.pose.bones:pb.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    for pb in nam.pose.bones:
        if pb.name in matrices:pb.matrix=matrices[pb.name];bpy.context.view_layer.update()
def reach(side,target,pole,weight=1,smooth_wrist=False):
    upper=nam.pose.bones[side+'Arm'];lower=nam.pose.bones[side+'ForeArm'];hand=nam.pose.bones[side+'Hand']
    origin=upper.head.copy();goal=nam.matrix_world.inverted()@Vector(target);a=(lower.head-origin).length;b=(hand.head-lower.head).length
    delta=goal-origin;d=max(abs(a-b)+.01,min((a+b)*.965,delta.length));axis=delta.normalized()
    bend=Vector(pole);bend=(bend-axis*bend.dot(axis)).normalized();along=(a*a+d*d-b*b)/(2*d)
    elbow=origin+axis*along+bend*math.sqrt(max(0,a*a-along*along));goal=origin+axis*d
    wrist=lower.matrix.to_quaternion().inverted()@hand.matrix.to_quaternion();neutral=lower.bone.matrix_local.to_quaternion().inverted()@hand.bone.matrix_local.to_quaternion()
    original_wrist=wrist.copy()
    difference=wrist@neutral.inverted();difference.normalize()
    if difference.w<0:difference.negate()
    if difference.angle>math.radians(18):difference=Quaternion().slerp(difference,math.radians(18)/difference.angle)
    wrist=original_wrist.slerp(difference@neutral,weight) if smooth_wrist else difference@neutral
    for bone,head,tail in [(upper,origin,elbow),(lower,elbow,goal)]:
        m=bone.matrix.copy();q=(bone.tail-bone.head).rotation_difference(tail-head)@m.to_quaternion();solved=Matrix.LocRotScale(head,q,m.to_scale())
        p0,q0,s0=m.decompose();p1,q1,s1=solved.decompose();bone.matrix=Matrix.LocRotScale(p0.lerp(p1,weight),q0.slerp(q1,weight),s0.lerp(s1,weight));bpy.context.view_layer.update()
    m=hand.matrix.copy();hand.matrix=Matrix.LocRotScale(hand.head,lower.matrix.to_quaternion()@wrist,m.to_scale());bpy.context.view_layer.update()
def key(action,frame):
    basis={b.name:b.matrix_basis.copy() for b in nam.pose.bones};nam.animation_data.action=action
    for b in nam.pose.bones:
        b.rotation_mode='QUATERNION';b.matrix_basis=basis[b.name]
        for prop in ['location','rotation_quaternion','scale']:b.keyframe_insert(prop,frame=frame,group=b.name)
radio_only='--radio-only' in sys.argv
collect_only='--collect-only' in sys.argv
report=[];idle=source('Idle',1)
for clip in (['Nam_RadioPanel'] if radio_only else ['Nam_SupplyCollect'] if collect_only else ['Nam_SupplyCollect','Nam_SupplyHandoff']):
    action=bpy.data.actions.new(clip);action.use_fake_user=True
    for frame in range(1,206):
        t=(frame-1)/30;nam.animation_data.action=None
        if clip=='Nam_RadioPanel':
            pose={name:matrix.copy() for name,matrix in idle.items()}
        elif clip.endswith('Collect'):
            if t<.5:pose=interpolate(idle,source('Open',1),smooth(t/.5))
            elif t<3:pose=source('Open',1+(t-.5)/2.5*145)
            elif t<3.5:pose=interpolate(source('Open',146),source('Pickup',145),smooth((t-3)/.5))
            elif t<5.4:pose=source('Pickup',145+(t-3.5)/1.9*70)
            else:pose=interpolate(source('Pickup',215),idle,smooth((t-5.4)/1.4))
        else:
            if t<1:pose=idle
            elif t<4:pose=interpolate(idle,source('Pickup',145),smooth((t-1)/3))
            else:pose=interpolate(source('Pickup',145),idle,smooth((t-4)/2.8))
        # Keep contact actions stationary; preserve the source's vertical body motion.
        if 'root' in pose and 'root' in idle:
            shift=idle['root'].translation-pose['root'].translation;shift.z=0
            for matrix in pose.values():matrix.translation+=shift
        apply(pose)
        if clip=='Nam_RadioPanel':
            phase=t/6.8*math.tau
            # Lean from the spine with planted feet rather than extending the arms beyond reach.
            for spine_name,angle in [('Spine',14),('Spine1',10)]:
                spine=nam.pose.bones.get(spine_name)
                if spine is not None:
                    matrix=spine.matrix.copy();orientation=nam.matrix_world.to_quaternion()
                    pitch=orientation.inverted()@Quaternion(Vector((1,0,0)),math.radians(angle))@orientation
                    spine.matrix=Matrix.LocRotScale(spine.head,pitch@matrix.to_quaternion(),matrix.to_scale());bpy.context.view_layer.update()
            head=nam.pose.bones.get('Head')
            if head is not None:
                matrix=head.matrix.copy();orientation=nam.matrix_world.to_quaternion()
                pitch=orientation.inverted()@Quaternion(Vector((1,0,0)),math.radians(8))@orientation
                head.matrix=Matrix.LocRotScale(head.head,pitch@matrix.to_quaternion(),matrix.to_scale());bpy.context.view_layer.update()
            reach('Left',(-.16,-.55,1.38),(-1,-.4,-.6),1)
            reach('Right',(.08+.018*math.sin(phase),-.58,1.39+.012*math.sin(phase)),(1,-.4,-.6),1)
            for pb in nam.pose.bones:
                if 'Hand' not in pb.name or not pb.name[-1].isdigit():continue
                finger=next((name for name in ['Index','Middle','Ring','Pinky','Thumb'] if name in pb.name),None)
                if finger is None:continue
                joint=int(pb.name[-1])-1
                if joint<0 or joint>2:continue
                angle=([15,-15,-10] if finger=='Thumb' else [15,25,15])[joint]
                if pb.name.startswith('RightHandIndex'):angle+=6*math.sin(phase)
                pb.rotation_mode='QUATERNION';pb.rotation_quaternion=Quaternion(Vector((1,0,0)),math.radians(angle))
            bpy.context.view_layer.update();key(action,frame);continue
        if clip.endswith('Collect'):
            w=smooth((t-.8)/.35)*(1-smooth((t-1.8)/.35))
            if w>0:reach('Left',(-.18,-.45,.72+.20*smooth((t-1.1)/.7)),(-1,-.4,-.6),w)
            w=smooth((t-2.8)/.5)*(1-smooth((t-5.15)/.3))
            if w>0:
                lift=smooth((t-3.3)/1.3);reach('Right',(.12,-(.73-.25*lift),.58+.40*lift),(1,-.4,-.6),w,smooth_wrist=True)
        else:
            w=1-smooth((t-5.2)/.3);lower=smooth((t-1.5)/3.7)
            if w>0:reach('Right',(.12,-(.35+.25*lower),.98-.66*lower),(1,-.4,-.6),w)
        grip=smooth((t-3.15)/.25)*(1-smooth((t-5.2)/.3)) if clip.endswith('Collect') else 1-smooth((t-5.1)/.35)
        for pb in nam.pose.bones:
            if not pb.name.startswith('RightHand') or not pb.name[-1].isdigit():continue
            finger=next((name for name in ['Index','Middle','Ring','Pinky','Thumb'] if name in pb.name),None)
            if finger is None:continue
            joint=int(pb.name[-1])-1
            if joint<0 or joint>2:continue
            original=pb.matrix_basis.to_quaternion();target=Quaternion(Vector((1,0,0)),math.radians(([25,-40,-45] if finger=='Thumb' else [55,70,40])[joint]))
            if finger=='Thumb' and joint==0:target=target@Quaternion(Vector((0,0,1)),math.radians(30))
            pb.rotation_mode='QUATERNION';pb.rotation_quaternion=original.slerp(target,grip)
        bpy.context.view_layer.update();key(action,frame)
    assert {b.name:[list(row) for row in b.matrix_local] for b in nam.data.bones}==rest
    nam.animation_data.action=action;scene.frame_start=1;scene.frame_end=205;scene.frame_set(1)
    bpy.ops.object.select_all(action='DESELECT');nam.hide_set(False);nam.select_set(True);bpy.context.view_layer.objects.active=nam
    name=nam.name;nam.name='Armature'
    bpy.ops.export_scene.fbx(filepath=str(OUT/(clip+'.fbx')),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)
    nam.name=name;report.append({'clip':clip,'fps':30,'duration':6.8,'bones':len(nam.data.bones),'rest_preserved':True,'max_wrist_delta_degrees':18})
if radio_only:
    # Measured from the existing Unity radio face relative to its NavMesh standing marker.
    bpy.ops.mesh.primitive_cube_add(location=(0,-.66,1.39),scale=(.40,.025,.23))
    bpy.context.object.name='REFERENCE_RadioFace'
else:
    bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/_Project/Art/Environment/Map01Operations/SupplyChest.fbx'))
    for ob in list(bpy.context.selected_objects):
        if ob.parent is None:ob.location.y=-1.10
scene.frame_start=1;scene.frame_end=205
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/('Nam_RadioPanel.blend' if radio_only else 'Nam_SupplyCollect_Wrist.blend' if collect_only else 'Nam_SupplyInteractions.blend')))
(HERE/('radio-animation-receipt.json' if radio_only else 'collect-wrist-receipt.json' if collect_only else 'animation-receipt.json')).write_text(json.dumps(report,indent=2),encoding='utf-8')
print('Authored and exported supply clips; rest skeleton preserved.')
