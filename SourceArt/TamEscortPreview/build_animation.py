"""Author a paired shoulder-assist loop on the original game skeletons.
Blender -b -t 6 --python SourceArt/TamEscortPreview/build_animation.py [-- --render]
"""
import bpy, math, json, sys
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion
HERE=Path(__file__).resolve().parent
source=(HERE/'build_preview.py').read_text()
exec(compile(source[:source.index('nam.location.x=')],str(HERE/'build_preview.py'),'exec'))
scene=bpy.context.scene
OUT=HERE/'Animation_v4';OUT.mkdir(exist_ok=True)
FPS=30;N=18;PERIOD=N/FPS;SPEED=7/3.6
scene.render.fps=FPS;scene.frame_start=1;scene.frame_end=N+1
rest={r:{p.name:p.matrix_basis.copy() for p in r.pose.bones} for r in (nam,tam)}
for r in (nam,tam):
    r.animation_data_create();r.animation_data.action=bpy.data.actions.new(r.name+'_ShoulderAssist_Walk_7kph')
    for p in r.pose.bones:p.rotation_mode='QUATERNION'
nam.location=Vector((-.29,0,0));tam.location=Vector((.19,.02,0))
bpy.ops.object.select_all(action='DESELECT');tam.select_set(True);bpy.context.view_layer.objects.active=tam
bpy.ops.object.mode_set(mode='EDIT');tam.data.edit_bones[bone(tam,'RightShoulder').name].use_connect=False;bpy.ops.object.mode_set(mode='OBJECT')
def solve(r,upper,lower,end,target,hint):
    origin=pos(r,upper);a=(pos(r,lower)-origin).length;b=(pos(r,end)-pos(r,lower)).length
    delta=Vector(target)-origin;axis=delta.normalized();d=max(abs(a-b)+.002,min(a+b-.002,delta.length))
    pole=Vector(hint)-origin;pole=(pole-axis*pole.dot(axis)).normalized()
    x=(a*a-b*b+d*d)/(2*d);elbow=origin+axis*x+pole*math.sqrt(max(0,a*a-x*x))
    aim(r,upper,elbow-origin);aim(r,lower,origin+axis*d-pos(r,lower))
    return (pos(r,end)-Vector(target)).length
def rotate(r,n,angle,axis):
    p=bone(r,n);w=r.matrix_world@p.matrix
    p.matrix=r.matrix_world.inverted()@Matrix.LocRotScale(w.translation,Quaternion(Vector(axis),angle)@w.to_quaternion(),w.to_scale());bpy.context.view_layer.update()
def palm(r,side,direction,normal):
    if not any(p.name.split(':')[-1]==side+'HandMiddle1' for p in r.pose.bones):
        aim(r,side+'Hand',direction);return Vector(normal)
    p=bone(r,side+'Hand');w=r.matrix_world@p.matrix
    along=(pos(r,side+'HandMiddle1')-pos(r,side+'Hand')).normalized()
    across=(pos(r,side+'HandIndex1')-pos(r,side+'HandPinky1')).normalized()
    norm=along.cross(across).normalized()
    if side=='Right':norm=-norm
    d=Vector(direction).normalized();n=Vector(normal);n=(n-d*n.dot(d)).normalized()
    src=Matrix((along,norm,along.cross(norm))).transposed();dst=Matrix((d,n,d.cross(n))).transposed()
    p.matrix=r.matrix_world.inverted()@Matrix.LocRotScale(w.translation,(dst@src.inverted()).to_quaternion()@w.to_quaternion(),w.to_scale());bpy.context.view_layer.update()
    return n
def curl(r,side,normal,amount):
    for finger in ('Index','Middle','Ring','Pinky'):
        for segment in range(1,4):
            n=side+'Hand'+finger+str(segment)
            if not any(p.name.split(':')[-1]==n for p in r.pose.bones):continue
            p=bone(r,n);d=(r.matrix_world.to_3x3()@(p.tail-p.head)).normalized()
            rotate(r,n,math.radians((48 if segment==1 else 62)*amount),d.cross(normal).normalized())
contacts=[];foot_samples=[]
for frame in range(0,N+2):
    scene.frame_set(frame);phase=(frame-1)/N;theta=phase*math.tau
    for r in (nam,tam):
        r.location.z=(-.015 if frame==0 else -.07+.008*math.cos(2*theta))-(.10 if r==nam else 0)
        for p in r.pose.bones:p.matrix_basis=rest[r][p.name].copy()
        bpy.context.view_layer.update()
        # Shared weight shift keeps shoulder contact stable; Tâm's body sags slightly.
        hip=bone(r,'Hips');w=r.matrix_world@hip.matrix
        w.translation+=Vector((.018*math.sin(theta),0,0))
        hip.matrix=r.matrix_world.inverted()@w;bpy.context.view_layer.update()
        if r==tam:rotate(r,'Hips',math.radians(-2),(0,1,0))
        for n in ('Spine','Spine1','Spine2'):aim(r,n,((-.025 if r==tam else .015),-.015,1))
        aim(r,'Neck',(0,-.015,1));aim(r,'Head',(0,-.04 if r==tam else -.015,1))
        if r==tam:
            aim(r,'RightShoulder',(-math.cos(math.radians(30)),0,math.sin(math.radians(30))))
        for side,offset,sign in [('Left',0,1),('Right',.5,-1)]:
            u=(phase+offset)%1
            # Feet travel backwards at exactly 1.944 m/s during stance.
            stance=.50;travel=SPEED*PERIOD*stance
            if u<stance:y=-travel/2+travel*u/stance;lift=0
            else:
                swing=(u-stance)/(1-stance);smooth=swing*swing*(3-2*swing)
                y=travel/2-travel*smooth
                lift=(.035 if r==tam and side=='Right' else .095)*math.sin(math.pi*swing)
            if frame==0:y=0;lift=0
            hippos=pos(r,side+'UpLeg');target=Vector((r.location.x+sign*.105,y+r.location.y,.105+lift))
            solve(r,side+'UpLeg',side+'Leg',side+'Foot',target,hippos+Vector((0,-1,-.3)))
            aim(r,side+'Foot',(0,-1,-.35 if lift>0 else -.17))
            foot_samples.append({'frame':frame,'actor':r.name,'side':side,'error':(pos(r,side+'Foot')-target).length,'target':list(target),'actual':list(pos(r,side+'Foot')),'hip':list(hippos)})
    # The arm wraps behind Nam's neck; the palm rests on his far (right) shoulder.
    target=pos(nam,'RightArm')+Vector((.07,.015,.08))
    shoulder_error=solve(tam,'RightArm','RightForeArm','RightHand',target,pos(nam,'Neck')+Vector((.03,.25,.12)))
    n=palm(tam,'Right',(-.9,-.25,-.16),(0,0,-1));curl(tam,'Right',n,.18)
    # Nam reaches behind Tâm's back and supports low at his hip; fingers point down.
    target=pos(tam,'Hips')+Vector((.13,.14,.15))
    waist_error=solve(nam,'LeftArm','LeftForeArm','LeftHand',target,pos(nam,'LeftArm')+Vector((.05,.34,-.25)))
    n=palm(nam,'Left',(.72,-.12,-.68),(-.6,-.8,0));curl(nam,'Left',n,.18)
    aim(nam,'RightArm',(-.14,-.08,-1));aim(nam,'RightForeArm',(-.03,-.72,-.58))
    n=palm(nam,'Right',(0,-1,-.12),(.95,0,-.1));curl(nam,'Right',n,.9)
    # Tâm protects his injury with the free arm, as in the reference.
    target=pos(tam,'Spine')+Vector((.12,-.20,-.07))
    solve(tam,'LeftArm','LeftForeArm','LeftHand',target,pos(tam,'LeftArm')+Vector((.15,-.03,-.35)))
    n=palm(tam,'Left',(-.35,0,-1),(0,1,0));curl(tam,'Left',n,.3)
    contacts.append({'frame':frame,'shoulder_error_m':shoulder_error,'waist_error_m':waist_error})
    for r in (nam,tam):
        r.keyframe_insert(data_path='location',frame=frame)
        for p in r.pose.bones:
            p.keyframe_insert(data_path='location',frame=frame,group=p.name)
            p.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=p.name)
            p.keyframe_insert(data_path='scale',frame=frame,group=p.name)
for r in (nam,tam):
    action=r.animation_data.action
    for slot in action.slots:
        for layer in action.layers:
            for strip in layer.strips:
                channelbag=strip.channelbag(slot)
                if channelbag:
                    for curve in channelbag.fcurves:
                        for key in curve.keyframe_points:key.interpolation='LINEAR'
    bpy.ops.object.select_all(action='DESELECT');r.select_set(True);bpy.context.view_layer.objects.active=r
    bpy.ops.export_scene.fbx(filepath=str(OUT/(r.name+'_ShoulderAssist_Walk.fbx')),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
    original=r.animation_data.action;idle=original.copy();r.animation_data.action=idle
    scene.frame_start=0;scene.frame_end=1;scene.frame_set(0)
    idle_root=r.location.copy();idle_pose={p.name:(p.location.copy(),p.rotation_quaternion.copy(),p.scale.copy()) for p in r.pose.bones}
    for key in (0,1):
        scene.frame_set(key);r.location=idle_root;r.keyframe_insert(data_path='location',frame=key)
        for p in r.pose.bones:
            p.location,p.rotation_quaternion,p.scale=idle_pose[p.name]
            p.keyframe_insert(data_path='location',frame=key);p.keyframe_insert(data_path='rotation_quaternion',frame=key);p.keyframe_insert(data_path='scale',frame=key)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(r.name+'_ShoulderAssist_Idle.fbx')),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
    r.animation_data.action=original;bpy.data.actions.remove(idle)
    scene.frame_start=1;scene.frame_end=N+1
# Real game K54 model, rigidly following Nam's free hand.
scene.frame_set(1);bpy.context.view_layer.update()
before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/_Project/Art/Environment/Map01Items/Meshes/Item_pistol_k54.fbx'))
parts=[o for o in set(bpy.data.objects)-before if o.type=='MESH']
gun=bpy.data.objects.new('K54_hand_attachment',None);scene.collection.objects.link(gun)
for o in parts:
    world=o.matrix_world.copy();o.parent=None;o.data=o.data.copy();o.data.transform(world);o.matrix_world=Matrix.Identity(4);o.parent=gun
gun.rotation_euler=(math.pi/2,0,-math.pi/2)
for frame in range(0,N+2):
    scene.frame_set(frame);bpy.context.view_layer.update();gun.location=pos(nam,'RightHand')+Vector((0,-.025,-.025));gun.keyframe_insert(data_path='location',frame=frame)
def mat(n,c):
    m=bpy.data.materials.new(n);m.use_nodes=True;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*c,1);m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.88;return m
bpy.ops.mesh.primitive_plane_add(size=100);floor=bpy.context.object;floor.data.materials.append(mat('Earth review floor',(.13,.17,.14)))
scene.world=bpy.data.worlds.new('Review world');scene.world.color=(.22,.22,.22)
for loc,power,size in [((-2,-4,5),850,4),((3,-1,3),550,3),((0,3,4),800,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(-2.5,-5.8,2.3));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,.95))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=2.5;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True
scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
scene.frame_set(1)
# Keep the authored gait in place; speed is documented and stance foot velocity calibrated above.
scene['movement_speed_mps']=SPEED;scene['loop_seconds']=PERIOD;scene['preview_only']=True
receipt={'revision':4,'nam_knee_lowering_m':.10,'tam_shoulder_lift_m':0,'standing_review_frame':0,'tam_offset_towards_nam_m':.10,'tam_right_shoulder_elevation_degrees':30,'tam_hip_counter_tilt_degrees':-2,'speed_kmh':7,'speed_mps':SPEED,'fps':FPS,'loop_frames':N,'loop_seconds':PERIOD,'source':'Blender authored paired animation; original game rigs','shoulder_max_error_m':max(x['shoulder_error_m'] for x in contacts),'waist_max_error_m':max(x['waist_error_m'] for x in contacts),'foot_max_error_m':max(x['error'] for x in foot_samples),'contacts':contacts}
receipt['worst_foot']=max(foot_samples,key=lambda x:x['error'])
(OUT/'validation.json').write_text(json.dumps(receipt,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Nam_Tam_ShoulderAssist.blend'))
if '--render' in sys.argv:
    frames=OUT/'frames';frames.mkdir(exist_ok=True)
    for frame in ([0] if '--draft' in sys.argv else range(1,N+1)):
        scene.frame_set(frame);scene.render.filepath=str(frames/f'{frame:03}.png');bpy.ops.render.render(write_still=True)
    for label,loc in [('side',(-5,-.8,2.0)),('back',(2,5,2.4))]:
        scene.frame_set(0);cam.location=loc;cam.rotation_euler=(Vector((0,0,.95))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(OUT/(label+'.png'));bpy.ops.render.render(write_still=True)
print('SHOULDER_ASSIST_AUTHORED',json.dumps({k:v for k,v in receipt.items() if k!='contacts'}))
