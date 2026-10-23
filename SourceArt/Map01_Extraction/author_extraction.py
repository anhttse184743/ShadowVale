"""Blender 5.2, no add-ons. Run --background --python author_extraction.py.
Retargets Mixamo world/rest rotation deltas to the original 28-bone rigs, then
authors contacts with two-bone IK. Skeleton topology/rest transforms are untouched.
Exports each character separately; source actions/meshes are retained in the blend.
"""
import bpy, math, json, sys, wave, struct
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/_Project/Art/Characters/Animations/Extraction'
HERE=Path(__file__).resolve().parent
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene; scene.render.fps=30
scene.unit_settings.system='METRIC'
CLIPS={'Radio_Receive':7,'Seated_Rifle_Ready':3,'Seated_Rifle_Fire':1.2,
       'Seated_Rifle_Reload':3.6,'Seated_Lower_Weapon':2,'Board_Boat_StepDown':6,
       'Boat_Turn_And_Sit':3.2,'Boat_Seated_Travel':4,'Rifle_Stow':1.8,'Oar_Pickup':2,'Row_Start':1.6,'Row_Loop':2.4,'Row_Stop':2}
BOARD=[(-.68,1.48,87),(-.35,1.31,87),(-.10,1.08,87),(.15,.85,87),(.35,.65,87),(1,.145,87),(1.55,-.13,87)]

def import_rig(path,label):
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path))
    # FBX import can replace the scene rate with the source take rate.
    scene.render.fps=30; scene.render.fps_base=1.0
    objects=list(set(bpy.data.objects)-before)
    rig=next(o for o in objects if o.type=='ARMATURE'); rig.name=label
    return rig,objects

sources={}
source_collection=bpy.data.collections.new('SOURCE — untouched Mixamo reference');scene.collection.children.link(source_collection)
for name,path in [('Gunplay','Characters/Animations/Clips/Gunplay.fbx'),('Talking','cutsence/Talking.fbx'),('Walk','Characters/Animations/Clips/Standard Walk.fbx')]:
    rig,objects=import_rig(ROOT/'Assets/_Project/Art'/path,'Source_'+name)
    sources[name]=rig
    for o in objects:
        for c in list(o.users_collection): c.objects.unlink(o)
        source_collection.objects.link(o);o.hide_render=True
    rig.animation_data.action.name='SOURCE_'+name
    rig.animation_data.action.use_fake_user=True

def smooth(t): return max(0,min(1,t))**2*(3-2*max(0,min(1,t)))
def mix(a,b,t): return Vector(a).lerp(Vector(b),t)
def along(points,t):
    p=max(0,min(1,t))*(len(points)-1); i=min(int(p),len(points)-2)
    return mix(points[i],points[i+1],p-i)
def empty(name):
    o=bpy.data.objects.new(name,None);scene.collection.objects.link(o);o.empty_display_type='SPHERE';o.empty_display_size=.04;return o
def set_head(rig,pb,head):
    # Hips is connected to the original root bone. Its local translation is ignored
    # by Blender, so move the existing root (never disconnect or add bones).
    root=rig.pose.bones['root']
    mat=root.matrix.copy();mat.translation += Vector(head)-pb.head;root.matrix=mat

def solve_leg(rig,side,world_target):
    """Resolve the two-bone contact in the forward knee plane, without stretching.
    This avoids the reverse-knee ambiguity of the character's bowed rest legs.
    The editable IK constraints/targets are retained in the source rig.
    """
    upper=rig.pose.bones[side+'UpLeg'];lower=rig.pose.bones[side+'Leg'];foot=rig.pose.bones[side+'Foot']
    start=upper.head.copy();end=rig.matrix_world.inverted() @ world_target
    axis=end-start;distance=axis.length;axis.normalize()
    l1=upper.bone.length;l2=lower.bone.length
    distance=max(abs(l1-l2)+.001,min(l1+l2-.001,distance))
    a=(l1*l1-l2*l2+distance*distance)/(2*distance)
    h=math.sqrt(max(0,l1*l1-a*a))
    pole=Vector((0,-1,0));pole=(pole-axis*pole.dot(axis)).normalized()
    knee=start+axis*a+pole*h
    for bone,head,tail in [(upper,start,knee),(lower,knee,start+axis*distance)]:
        rest=bone.bone.matrix_local
        rotation=(bone.bone.tail_local-bone.bone.head_local).rotation_difference(tail-head) @ rest.to_quaternion()
        bone.matrix=Matrix.LocRotScale(head,rotation,Vector((1,1,1)))
        bpy.context.view_layer.update()
    foot.matrix=Matrix.LocRotScale(lower.tail,foot.bone.matrix_local.to_quaternion(),Vector((1,1,1)))
    bpy.context.view_layer.update()
def target_pose(rig,source,frame):
    action=source.animation_data.action
    start,end=action.frame_range
    scene.frame_set(int(start+(frame % max(1,end-start))))
    for pb in rig.pose.bones:
        pb.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    rotations={}
    basis=rig.matrix_world.to_quaternion().inverted() @ source.matrix_world.to_quaternion()
    source_hips=source.pose.bones['mixamorig:Hips']
    hips_delta=basis @ source_hips.matrix.to_quaternion() @ source_hips.bone.matrix_local.to_quaternion().inverted() @ basis.inverted()
    for pb in rig.pose.bones:
        sb=source.pose.bones.get('mixamorig:'+pb.name)
        rest=pb.bone.matrix_local
        # Mixamo imports with +90 degrees on the armature object; character rigs do not.
        # Conjugate the delta into the target armature space before applying its rest pose.
        basis=rig.matrix_world.to_quaternion().inverted() @ source.matrix_world.to_quaternion()
        rotation=(hips_delta.inverted() @ basis @ sb.matrix.to_quaternion() @ sb.bone.matrix_local.to_quaternion().inverted() @ basis.inverted() @ rest.to_quaternion()) if sb else rest.to_quaternion()
        local_rest=(pb.parent.bone.matrix_local.inverted() @ rest).to_quaternion() if pb.parent else rest.to_quaternion()
        parent=rotations[pb.parent.name] if pb.parent else Quaternion()
        pb.rotation_mode='QUATERNION';pb.rotation_quaternion=local_rest.inverted() @ parent.inverted() @ rotation
        rotations[pb.name]=rotation
    bpy.context.view_layer.update()

reports=[]; characters=[]
for who,path in [('Nam','Player/Player.fbx'),('Hung','NPCs/hung.fbx')]:
    rig,objects=import_rig(ROOT/'Assets/_Project/Art/Characters'/path,who)
    characters.append((who,rig,objects))
    assert len(rig.data.bones)==28
    rest={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
    controls={}; constraints=[]
    for side in ['Left','Right']:
        foot=empty(who+'_'+side+'_FootContact');controls[side]=foot
        leg=rig.pose.bones[side+'Leg'];ik=leg.constraints.new('IK');ik.name='Planted foot — two bone solve';ik.target=foot;ik.chain_count=2;ik.use_stretch=False
        constraints.append(ik)
        rot=rig.pose.bones[side+'Foot'].constraints.new('COPY_ROTATION');rot.target=foot;rot.owner_space='WORLD';rot.target_space='WORLD';constraints.append(rot)
    hand=empty(who+'_LeftHand_ShelfContact');controls['Hand']=hand
    ik=rig.pose.bones['LeftForeArm'].constraints.new('IK');ik.target=hand;ik.chain_count=2;ik.use_stretch=False;constraints.append(ik)
    right_hand=empty(who+'_RightHand_WeaponGrip');controls['RightHand']=right_hand
    right_ik=rig.pose.bones['RightForeArm'].constraints.new('IK');right_ik.target=right_hand;right_ik.chain_count=2;right_ik.use_stretch=False;constraints.append(right_ik)
    rig.animation_data_create()
    for clip,seconds in CLIPS.items():
        n=round(seconds*30);action=bpy.data.actions.new(who+'_'+clip);action.use_fake_user=True
        rig.animation_data.action=action
        max_error=0.0
        for frame in range(n+1):
            t=frame/30;p=frame/n
            # Detach the destination action during constraint evaluation: otherwise a
            # depsgraph refresh reapplies the previous baked keys over the authored pose.
            rig.animation_data.action=None
            for c in constraints:c.mute=True
            source=sources['Talking' if clip=='Radio_Receive' else 'Walk' if clip=='Board_Boat_StepDown' else 'Gunplay']
            target_pose(rig,source,frame if clip not in ['Seated_Rifle_Ready','Boat_Seated_Travel','Seated_Lower_Weapon'] else 12)
            # Source data is read at its sampled frame; output action is keyed independently.
            for c in constraints:c.mute=False
            seated=clip.startswith(('Seated','Row_')) or clip in ['Boat_Seated_Travel','Rifle_Stow','Oar_Pickup']
            sit_weight=smooth((p-.25)/.75) if clip=='Boat_Turn_And_Sit' else (1 if seated else 0)
            hips=rig.pose.bones['Hips']
            hip_rest=hips.bone.head_local
            set_head(rig,hips,(hip_rest.x,hip_rest.y+sit_weight*9,hip_rest.z*(1-sit_weight)+42*sit_weight))
            bpy.context.view_layer.update()
            if seated or clip=='Boat_Turn_And_Sit':
                for side,sign in [('Left',1),('Right',-1)]:
                    # Feet point outboard. Knees remain in front of the bench.
                    controls[side].location=rig.matrix_world @ Vector((sign*14,7-38*sit_weight,10.6+(6.8 if who=="Hung" else 0)))
            elif clip=='Board_Boat_StepDown':
                root=along(BOARD,p)
                segment=min(int(p*6),5);phase=min(1,p*6-segment)
                # Bring both feet onto each narrow tread before the next descent.
                # Each foot has a stationary contact interval, with a short lifted swing.
                set_head(rig,hips,(hip_rest.x,hip_rest.y,hip_rest.z-34*math.sin(math.pi*phase)))
                for side,offset,sign in [('Left',0,1),('Right',.5,-1)]:
                    swing=max(0,min(1,(phase-offset)*2))
                    a=Vector(BOARD[segment]);b=Vector(BOARD[segment+1])
                    world=a.lerp(b,smooth(swing))
                    lift=math.sin(math.pi*swing)*.08
                    local=Vector((sign*.13,-(world.x-root.x),world.y-root.y+.106+lift))*100
                    controls[side].location=rig.matrix_world @ local
                # Hand supports the transfer from the shelf, then releases before sitting.
                world=Vector((.32,.62,86.78)); local=Vector((.22,-(world.x-root.x),world.y-root.y))*100
                hand.location=rig.matrix_world @ local
            else:
                for side in ['Left','Right']:controls[side].location=rig.matrix_world @ rig.data.bones[side+'Foot'].head_local
            for side in ['Left','Right']:
                controls[side].rotation_mode='QUATERNION'
                controls[side].rotation_quaternion=(rig.matrix_world @ rig.data.bones[side+'Foot'].matrix_local).to_quaternion()
            ik.influence=(smooth((p-.5)*10)*(1-smooth((p-.85)*10))) if clip=='Board_Boat_StepDown' else 0
            right_ik.influence=0
            # Authored radio hand to ear; the other arm lowers the rifle.
            if clip=='Radio_Receive':
                hand.location=rig.matrix_world @ Vector((14,-3,174))
                ik.influence=smooth(p*6)*(1-smooth((p-.8)*5))
                right_hand.location=rig.matrix_world @ Vector((-18,-26,106))
                right_ik.influence=ik.influence
            if clip in ['Seated_Rifle_Ready','Seated_Rifle_Fire','Seated_Rifle_Reload']:
                recoil=2*math.sin(p*math.pi*8) if clip=='Seated_Rifle_Fire' else 0
                right_hand.location=rig.matrix_world @ Vector((-12,-28+recoil,91))
                hand.location=rig.matrix_world @ Vector((3,-54+recoil,89))
                ik.influence=right_ik.influence=1
            if clip=='Seated_Rifle_Reload':
                grip=Vector((-12,-28,91))
                hand.location=rig.matrix_world @ (grip+Vector((9,4,-12-11*math.sin(math.pi*p))))
                ik.influence=smooth(p*5)*(1-smooth((p-.8)*5))
            # Gentle torso balance; no pelvis or leg motion on the seated loops.
            if seated:
                spine=rig.pose.bones['Spine1'];spine.rotation_mode='QUATERNION'
                spine.rotation_quaternion @= Quaternion((0,1,0),math.sin(t*math.pi/2)*.012)
            if clip in ['Seated_Lower_Weapon','Boat_Seated_Travel']:
                w=smooth(p) if clip=='Seated_Lower_Weapon' else 1
                hand.location=rig.matrix_world @ Vector((13,-34,48))
                right_hand.location=rig.matrix_world @ Vector((-15,-23,52))
                ik.influence=right_ik.influence=w
            if clip=='Rifle_Stow':
                hand.location=rig.matrix_world @ Vector((3+9*smooth(p),-54+29*smooth(p),89-29*smooth(p)))
                right_hand.location=rig.matrix_world @ Vector((-12-6*smooth(p),-28+46*smooth(p),91-26*smooth(p)))
                ik.influence=right_ik.influence=1
            if clip.startswith('Row_') or clip=='Oar_Pickup':
                phase=(t/2.4)*2*math.pi
                if clip=='Row_Start':phase=p*math.pi*.5
                if clip=='Row_Stop':phase=(1-p)*math.pi*.5
                forward=-25*math.cos(phase)
                recovery=max(0,math.sin(phase))*14+(30*smooth(p) if clip=='Row_Stop' else 0)
                weight=smooth(p) if clip=='Oar_Pickup' else 1
                right_hand.location=rig.matrix_world @ Vector((-18,18,65)).lerp(Vector((-28,forward,94+recovery)),weight)
                hand.location=rig.matrix_world @ Vector((12,-25,60)).lerp(Vector((-56,forward-12,63+recovery)),weight)
                ik.influence=right_ik.influence=1
                spine=rig.pose.bones['Spine1'];spine.rotation_mode='QUATERNION'
                spine.rotation_quaternion @= Quaternion((1,0,0),math.cos(phase)*.09)
            if clip=='Boat_Turn_And_Sit':
                hand.location=rig.matrix_world @ Vector((16,-26,104-52*sit_weight))
                right_hand.location=rig.matrix_world @ Vector((-15,-23,108-53*sit_weight))
                ik.influence=right_ik.influence=1
            bpy.context.view_layer.update()
            # Bake the unambiguous forward-bending knee solution after the contact solve.
            for c in constraints[:4]:c.mute=True
            for side in ['Left','Right']:solve_leg(rig,side,controls[side].location)
            evaluated=rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
            matrices={pb.name:evaluated.pose.bones[pb.name].matrix.copy() for pb in rig.pose.bones}
            for side in ['Left','Right']:
                error=((rig.matrix_world @ evaluated.pose.bones[side+'Foot'].head)-controls[side].location).length
                max_error=max(max_error,error)
            for c in constraints:c.mute=True
            rig.animation_data.action=action
            for pb in rig.pose.bones:
                base=(matrices[pb.parent.name] @ pb.parent.bone.matrix_local.inverted() @ pb.bone.matrix_local) if pb.parent else pb.bone.matrix_local
                pb.matrix_basis=base.inverted() @ matrices[pb.name];pb.rotation_mode='QUATERNION'
                for prop in ['location','rotation_quaternion','scale']:pb.keyframe_insert(prop,frame=frame,group=pb.name)
        assert rest=={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
        scene.render.fps=30;scene.render.fps_base=1.0
        scene.frame_start=0;scene.frame_end=n;scene.frame_set(0)
        bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
        bpy.ops.export_scene.fbx(filepath=str(OUT/(who+'_'+clip+'.fbx')),use_selection=True,object_types={'ARMATURE'},
            add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,
            bake_anim_simplify_factor=0,bake_anim_step=1,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
        reports.append({'character':who,'clip':clip,'frames':n+1,'fps':30,'bones':28,'max_ik_error_m':max_error})
    # Display seated reference; all actions remain selectable in the Action Editor.
    rig.animation_data.action=bpy.data.actions[who+('_Row_Loop' if who=='Hung' else '_Seated_Rifle_Ready')]
    rig.location=(1.55,-(87 if who=='Nam' else 85.4),-.13)
    rig.scale*=.89
    rig.rotation_euler.z=0 if who=="Hung" else -math.pi/2

# Exact Unity mesh references, converted from Y-up to Blender Z-up.
reference=bpy.data.collections.new('REFERENCE — Map 1 jetty (stationary), hull and benches');scene.collection.children.link(reference)
geo=HERE/'jetty-reference.json'
if geo.exists():
    for part in json.loads(geo.read_text())['parts']:
        vs=[(v['x'],-v['z'],v['y']) for v in part['vertices']]
        faces=[part['triangles'][i:i+3][::-1] for i in range(0,len(part['triangles']),3)]
        mesh=bpy.data.meshes.new(part['name']);mesh.from_pydata(vs,[],faces);mesh.update()
        obj=bpy.data.objects.new(part['name'],mesh);reference.objects.link(obj)
        mat=bpy.data.materials.new(part['name']+'_Preview');mat.diffuse_color=(.22,.29,.18,1) if 'step' in part['name'].lower() else (.24,.13,.055,1)
        mesh.materials.append(mat)
# Editable paddle prop follows the two baked hand contacts in every rowing take.
hung_rig=bpy.data.objects.get('Hung')
if hung_rig:
    paddle=bpy.data.objects.new('Hung_Paddle_Contact_Rig',None);scene.collection.objects.link(paddle)
    contact=paddle.constraints.new('COPY_LOCATION');contact.target=hung_rig;contact.subtarget='RightHand'
    track=paddle.constraints.new('DAMPED_TRACK');track.target=hung_rig;track.subtarget='LeftHand';track.track_axis='TRACK_Z'
    for name,centre,length in [('Oar shaft',.45,1.4),('Oar blade',1.3,.55)]:
        source=next((o for o in reference.objects if o.name==name),None)
        if source:
            prop=source.copy();prop.data=source.data.copy();scene.collection.objects.link(prop)
            prop.name='Working_'+name;source.hide_render=True
            vs=[v.co.copy() for v in prop.data.vertices]
            midpoint=sum(vs,Vector())/len(vs)
            extent=max(v.y for v in vs)-min(v.y for v in vs)
            for vertex in prop.data.vertices:
                q=vertex.co-midpoint;vertex.co=Vector((q.x,q.z,-q.y*length/max(.001,extent)))
            prop.parent=paddle;prop.location=(0,0,centre)

source_collection.hide_viewport=True
scene.frame_start=0;scene.frame_end=90;scene.frame_set(10)
for o in bpy.data.objects:
    if o.type=='EMPTY':o.hide_render=True
bpy.ops.object.light_add(type='AREA',location=(0,-85,9));bpy.context.object.data.energy=1600;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=10
bpy.ops.object.camera_add(location=(-4,-83,4));camera=bpy.context.object
camera.location=(6,-83,3)
camera.rotation_euler=(Vector((1,-87,.7))-camera.location).to_track_quat('-Z','Y').to_euler();scene.camera=camera
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Extraction Studio');scene.world.color=(.25,.25,.25)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Map01_Extraction.blend'))
(HERE/'animation-validation.json').write_text(json.dumps(reports,indent=2))
with wave.open(str(OUT/'Boat_Engine.wav'),'wb') as audio:
    audio.setparams((1,2,22050,0,'NONE','not compressed'))
    audio.writeframes(b''.join(struct.pack('<h',int(5500*(math.sin(2*math.pi*48*i/22050)+.4*math.sin(2*math.pi*96*i/22050)+.2*math.sin(2*math.pi*192*i/22050))*(.8+.2*math.sin(2*math.pi*12*i/22050)))) for i in range(22050*4)))
if '--no-render' not in sys.argv:
    scene.render.filepath=str(HERE/'seated-preview.png');bpy.ops.render.render(write_still=True)
import random
rng=random.Random(41)
for name,duration in [('Paddle_Splash',.55),('Cover_Rifle',.15)]:
    samples=[];previous=0
    for i in range(round(22050*duration)):
        t=i/22050;previous=.72*previous+.28*rng.uniform(-1,1)
        envelope=math.sin(math.pi*t/duration)*math.exp(-t*(4 if name=='Paddle_Splash' else 22))
        samples.append(struct.pack('<h',int(14000*previous*envelope)))
    with wave.open(str(OUT/(name+'.wav')),'wb') as audio:
        audio.setparams((1,2,22050,0,'NONE','not compressed'));audio.writeframes(b''.join(samples))
print('EXTRACTION_AUTHORING_COMPLETE',len(reports))
