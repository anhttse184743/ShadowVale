"""Blender 5.2: preserve current rigs, author rescue/pair clips, bake at 30 fps.
Run: blender -b --python SourceArt/Map01_Rescue/author_rescue_cinematics.py
The source models, reference actions, control guides and terrain references stay
editable in Map01_Rescue_Cinematics.blend. Only new clip FBXs are exported.
"""
import bpy,bmesh,math,json,sys,argparse
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion

HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--clips',nargs='+');opts=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []);selected=set(opts.clips or [])
ART=ROOT/'Assets/_Project/Art';OUT=ART/'Characters/Animations/RescueStory';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.render.fps=30;scene.render.fps_base=1;scene.unit_settings.system='METRIC'

def load(path,name):
    before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(path));scene.render.fps=30
    objects=list(set(bpy.data.objects)-before);rig=next(o for o in objects if o.type=='ARMATURE');rig.name=name
    return rig,objects

characters={};originals={};actions={}
for name,path in [('Nam','Characters/Player/Player.fbx'),('Hung','Characters/NPCs/hung.fbx'),('Enemy','Characters/Enemies/lính rig.fbx')]:
    rig,objects=load(ART/path,name);characters[name]=(rig,objects)
    originals[name]={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
for name,path in [('HungBound','Characters/NPCs/Animations/Hung_BiTroi.fbx'),('NamPair','Characters/Animations/Nam/Nam_Takedown.fbx'),('EnemyPair','Characters/Animations/Enemy/Enemy_Takedown_Victim.fbx'),('EnemyReady','Characters/Animations/Enemy/Enemy_Rifle_AimIdle.fbx')]:
    rig,objects=load(ART/path,'REFERENCE_'+name);actions[name]=rig
    if rig.animation_data and rig.animation_data.action:rig.animation_data.action.name='REFERENCE_'+name;rig.animation_data.action.use_fake_user=True
    for o in objects:o.hide_render=True;o.hide_set(True)
rig,objects=load(ART/'Characters/Animations/Enemy/Enemy_Death_Knees.fbx','REFERENCE_KneelingFall');actions['KneelingFall']=rig
rig.animation_data.action.name='REFERENCE_KneelingFall';rig.animation_data.action.use_fake_user=True
for o in objects:o.hide_render=True;o.hide_set(True)

def smooth(x):x=max(0,min(1,x));return x*x*(3-2*x)
def pose(rig,source,frame):
    rig.animation_data_create();rig.animation_data.action=None
    scene.frame_set(int(frame));bpy.context.view_layer.update()
    for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    # Animation-only FBXs store posed default nodes, so local matrix_basis is
    # relative to a different default skeleton. Transfer evaluated armature-space
    # transforms instead, retaining the current target's original rest matrices.
    source_matrices={pb.name:pb.matrix.copy() for pb in source.pose.bones}
    for pb in rig.pose.bones:
        sb=source.pose.bones.get(pb.name)
        if sb:
            pb.matrix=source_matrices[pb.name]
            bpy.context.view_layer.update()
    bpy.context.view_layer.update()

def root_shift(rig,delta):
    pb=rig.pose.bones['root'];m=pb.matrix.copy();m.translation+=Vector(delta);pb.matrix=m;bpy.context.view_layer.update()
def rotate(rig,bone,angle,axis='X'):
    pb=rig.pose.bones[bone];m=pb.matrix.copy();q=Quaternion(Vector({'X':(1,0,0),'Y':(0,1,0),'Z':(0,0,1)}[axis]),math.radians(angle));
    pb.matrix=Matrix.LocRotScale(m.translation,q@m.to_quaternion(),m.to_scale());bpy.context.view_layer.update()
def solve(rig,upper_name,lower_name,end_name,target,pole,obstacle=None):
    upper=rig.pose.bones[upper_name];lower=rig.pose.bones[lower_name];end=rig.pose.bones[end_name]
    origin=upper.head.copy();target=rig.matrix_world.inverted()@Vector(target)
    a=(lower.head-origin).length;b=(end.head-lower.head).length;direction=target-origin
    if a<.001 or b<.001 or direction.length<.001:return
    distance=max(abs(a-b)+.01,min(a+b-.01,direction.length));direction.normalize();target=origin+direction*distance
    bend=Vector(pole);bend=(bend-direction*bend.dot(direction)).normalized()
    along=(a*a+distance*distance-b*b)/(2*distance);height=math.sqrt(max(0,a*a-along*along))
    elbow=origin+direction*along+bend*height
    if obstacle:
        best=float('inf')
        for n in range(25):
            angle=0 if n==0 else ((n+1)//2)*15*(-1 if n%2==0 else 1)
            candidate=origin+direction*along+(Quaternion(direction,math.radians(angle))@bend)*height
            score=obstacle.arm(rig.matrix_world@origin,rig.matrix_world@candidate,rig.matrix_world@target)*1000+abs(angle)*.0001
            if score<best:best=score;elbow=candidate
            if score<.00001:break
    end_world=end.matrix.to_quaternion().copy()
    wrist_local=lower.matrix.to_quaternion().inverted()@end_world
    if end_name.endswith('Hand'):
        rest=lower.bone.matrix_local.to_quaternion().inverted()@end.bone.matrix_local.to_quaternion()
        delta=wrist_local@rest.inverted();delta.normalize()
        if delta.w<0:delta.negate()
        # A contact must be solved at the shoulder/elbow, not by folding the
        # wrist to compensate for unreachable grips. Keep a nearly straight
        # hand relative to its forearm on these close paired actions.
        if delta.angle>math.radians(25):delta=Quaternion().slerp(delta,math.radians(25)/delta.angle)
        wrist_local=delta@rest
    for bone,head,tail in [(upper,origin,elbow),(lower,elbow,target)]:
        m=bone.matrix.copy();current=bone.tail-bone.head
        q=current.rotation_difference(tail-head)@m.to_quaternion()
        bone.matrix=Matrix.LocRotScale(head,q,m.to_scale());bpy.context.view_layer.update()
    orientation=lower.matrix.to_quaternion()@wrist_local if end_name.endswith('Hand') else end_world
    m=end.matrix.copy();end.matrix=Matrix.LocRotScale(end.head,orientation,m.to_scale());bpy.context.view_layer.update()

class HeadEnvelope:
    """Convex planes from the existing skinned head, including its helmet.
    The palm touches its surface; arm capsules must remain outside it.
    """
    def __init__(self,rig,objects):
        self.rig=rig
        h=rig.matrix_world@rig.pose.bones['Head'].matrix
        origin=h.translation;q=h.to_quaternion()
        points=[]
        for obj in objects:
            if obj.type!='MESH':continue
            groups={g.index:g.name for g in obj.vertex_groups}
            for v in obj.data.vertices:
                if sum(g.weight for g in v.groups if groups[g.group]=='Head')<.7:continue
                points.append(q.inverted()@(obj.matrix_world@v.co-origin))
        bm=bmesh.new()
        for p in points:bm.verts.new(p)
        bmesh.ops.convex_hull(bm,input=list(bm.verts),use_existing_faces=False)
        bm.normal_update()
        self.planes=[(f.normal.copy(),f.normal.dot(f.verts[0].co)) for f in bm.faces]
        bm.free()
        # Mouth lies below the nose, rather than at the forehead/helmet.
        face=max(-(q@p).y for p in points if abs((q@p).x)<.05 and .015<(q@p).z<.045)
        self.mouth=q.inverted()@Vector((0,-face-.027,.03))
    def frame(self):
        m=self.rig.matrix_world@self.rig.pose.bones['Head'].matrix
        return m.translation,m.to_quaternion()
    def point(self):
        o,q=self.frame();return o+q@self.mouth
    def depth(self,a,b,radius):
        o,q=self.frame();q=q.inverted();worst=0
        for i in range(13):
            p=q@(a.lerp(b,i/12)-o)
            worst=max(worst,min(d+radius-n.dot(p) for n,d in self.planes))
        return max(0,worst)
    def arm(self,a,b,c):return max(self.depth(a,b,.05),self.depth(b,c,.035))

# Capture from the original neutral mesh, before applying any paired pose.
head_envelope=HeadEnvelope(*characters['Enemy'])

# Close-up hand variant only; the original imported meshes remain preserved.
# Weld export seams, recover quads and subdivide before skinning, not at runtime.
hand_report=[];hug_hands=[]
nam,nam_objects=characters['Nam']
for side in ['Left','Right']:
    original=next(o for o in nam_objects if o.type=='MESH' and o.name=='Player_'+side+'Hand')
    refined=original.copy();refined.data=original.data.copy();refined.name='Hug_'+side+'Hand';scene.collection.objects.link(refined)
    bpy.context.view_layer.objects.active=refined;refined.select_set(True)
    bm=bmesh.new();bm.from_mesh(refined.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00015/min(refined.matrix_world.to_scale()))
    bmesh.ops.join_triangles(bm,faces=list(bm.faces),angle_face_threshold=math.radians(35),angle_shape_threshold=math.radians(40),cmp_uvs=True,cmp_materials=True)
    bm.to_mesh(refined.data);bm.free();refined.data.update()
    for poly in refined.data.polygons:poly.use_smooth=True
    sub=refined.modifiers.new('Close-up hand topology','SUBSURF');sub.subdivision_type='CATMULL_CLARK';sub.levels=sub.render_levels=1;sub.uv_smooth='PRESERVE_BOUNDARIES'
    while refined.modifiers.find(sub.name)>0:bpy.ops.object.modifier_move_up(modifier=sub.name)
    bpy.ops.object.modifier_apply(modifier=sub.name)
    # Blend neighboring skin weights gently across each finger joint.
    neighbors=[[] for _ in refined.data.vertices]
    for edge in refined.data.edges:
        a,b=edge.vertices;neighbors[a].append(b);neighbors[b].append(a)
    weights=[{g.group:g.weight for g in v.groups} for v in refined.data.vertices]
    for group in refined.vertex_groups:group.remove(list(range(len(weights))))
    for i,original_weights in enumerate(weights):
        blended={g:w*.88 for g,w in original_weights.items()}
        for n in neighbors[i]:
            for g,w in weights[n].items():blended[g]=blended.get(g,0)+w*.12/max(1,len(neighbors[i]))
        strongest=sorted(blended.items(),key=lambda p:p[1],reverse=True)[:4];total=sum(w for _,w in strongest)
        for g,w in strongest:
            if total>0:refined.vertex_groups[g].add([i],w/total,'REPLACE')
    hand_report.append({'side':side,'original_vertices':len(original.data.vertices),'refined_vertices':len(refined.data.vertices),'triangles':sum(len(f.vertices)-2 for f in refined.data.polygons),'bones':len(nam.data.bones)})
    original.hide_render=True;original.hide_set(True);hug_hands.append(refined)
# Export the neutral variant separately. Unity remaps its weights/bind poses to
# the existing hand renderer and swaps only the mesh during the bush branch.
nam.animation_data_create();nam.animation_data.action=None
for pb in nam.pose.bones:pb.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update();bpy.ops.object.select_all(action='DESELECT');nam.select_set(True)
for obj in hug_hands:obj.select_set(True)
bpy.context.view_layer.objects.active=nam;old_name=nam.name;nam.name='Armature'
(OUT/'Meshes').mkdir(exist_ok=True)
if not selected:
    bpy.ops.export_scene.fbx(filepath=str(OUT/'Meshes/Nam_HugHands.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)
nam.name=old_name
(HERE/'hand-mesh-validation.json').write_text(json.dumps(hand_report,indent=2),encoding='utf-8')

guides={}
for name,(rig,objects) in characters.items():
    for side in ['Left','Right']:
        guide=bpy.data.objects.new(name+'_'+side+'_FootContact',None);scene.collection.objects.link(guide);guide.empty_display_type='SPHERE';guide.empty_display_size=.035;guides[(name,side)]=guide
        c=rig.pose.bones[side+'Leg'].constraints.new('IK');c.name='Editable planted-foot contact (unmute for manual adjustment)';c.target=guide;c.chain_count=2;c.use_stretch=False;c.mute=True
    for side in ['Left','Right']:
        guide=bpy.data.objects.new(name+'_'+side+'_HandContact',None);scene.collection.objects.link(guide);guide.empty_display_type='SPHERE';guide.empty_display_size=.025;guides[(name,side+'Hand')]=guide
        c=rig.pose.bones[side+'ForeArm'].constraints.new('IK');c.name='Editable hand contact (unmute for manual adjustment)';c.target=guide;c.chain_count=2;c.use_stretch=False;c.mute=True

def leg(rig,name,side,target,pole=(0,-1,0)):
    guides[(name,side)].location=Vector(target)
    solve(rig,side+'UpLeg',side+'Leg',side+'Foot',target,pole)
def hand(rig,name,side,target,obstacle=None):
    guides[(name,side+'Hand')].location=Vector(target)
    solve(rig,side+'Arm',side+'ForeArm',side+'Hand',target,(1 if side=='Left' else -1,-.5,-.6),obstacle)
def world(rig,bone):return rig.matrix_world@rig.pose.bones[bone].head
def key(rig,action,frame,name):
    bases={pb.name:pb.matrix_basis.copy() for pb in rig.pose.bones}
    rig.animation_data.action=action
    for pb in rig.pose.bones:
        pb.rotation_mode='QUATERNION';pb.matrix_basis=bases[pb.name]
        pb.keyframe_insert('location',frame=frame,group=pb.name);pb.keyframe_insert('rotation_quaternion',frame=frame,group=pb.name);pb.keyframe_insert('scale',frame=frame,group=pb.name)
    for key_,guide in guides.items():
        if key_[0]==name:guide.keyframe_insert('location',frame=frame)
def export(rig,objects,action,name,seconds):
    if selected and action.name not in selected:
        report.append({'clip':action.name,'seconds':seconds,'fps':30,'bone_count':len(rig.data.bones),'rest_preserved':True});return
    assert {b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}==originals[name]
    rig.animation_data.action=action;scene.frame_start=1;scene.frame_end=round(seconds*30)+1;scene.frame_set(1)
    bpy.ops.object.select_all(action='DESELECT');rig.hide_set(False);rig.select_set(True);bpy.context.view_layer.objects.active=rig
    old_name=rig.name;old_location=rig.location.copy();rig.name='Armature'
    # Preview anchors place the pair diagonally, but the imported Humanoid
    # reference root must retain the established export origin. Baking that
    # preview placement into the FBX changes BodyT and offsets the whole torso.
    rig.location=(0,0,0)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(action.name+'.fbx')),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,
        bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True)
    rig.name=old_name;rig.location=old_location
    report.append({'clip':action.name,'seconds':seconds,'fps':30,'bone_count':len(rig.data.bones),'rest_preserved':True})

report=[]
# Bound Hùng: source kneeling pose, wrists behind the back throughout both clips.
rig,objects=characters['Hung'];rig.location=(0,0,0)
for clip,seconds in [('Hung_Captive_Recoil',3),('Hung_Bound_Execution',3)]:
    action=bpy.data.actions.new(clip);action.use_fake_user=True
    for i in range(round(seconds*30)+1):
        t=i/30;p=t/seconds;pose(rig,actions['HungBound'],2)
        feet={side:world(rig,side+'Foot').copy() for side in ['Left','Right']}
        if clip.endswith('Recoil'):
            recoil=math.sin(math.pi*smooth(min(1,max(0,(t-.8)/1.1)))) if .8<t<1.9 else 0
            rotate(rig,'Spine1',14*recoil);rotate(rig,'Head',-10*recoil)
        else:
            # One forward collapse from Hung's own bound pose. No foreign
            # pelvis/spine world rotations, axial roll or standing transition.
            impact=math.sin(math.pi*max(0,min(1,t/.42))) if t<.42 else 0
            fall=smooth((t-.28)/2.1)
            rotate(rig,'Hips',65*fall)
            rotate(rig,'Spine',12*fall)
            rotate(rig,'Spine1',18*fall-5*impact)
            rotate(rig,'Spine2',12*fall)
            rotate(rig,'Neck',8*fall)
            rotate(rig,'Head',4*fall-3*impact)
            root_shift(rig,(0,-.14*fall/rig.scale.y,-.16*fall/rig.scale.z))
        for side in ['Left','Right']:leg(rig,'Hung',side,feet[side])
        hips=world(rig,'Hips');up=(world(rig,'Spine2')-hips).normalized();right=(world(rig,'RightArm')-world(rig,'LeftArm')).normalized();back=right.cross(up).normalized()
        # Wrists stay bound behind the torso even during the upper-body fall.
        for side,sign in [('Left',1),('Right',-1)]:hand(rig,'Hung',side,hips+back*.17+up*.18-right*(sign*.035))
        key(rig,action,i+1,'Hung')
    export(rig,objects,action,'Hung',seconds)

# Interrogator stays on his feet and keeps both grips on the existing rifle.
rig,objects=characters['Enemy'];rig.location=(0,0,0)
action=bpy.data.actions.new('Enemy_Interrogate_Strike');action.use_fake_user=True
for i in range(91):
    t=i/30;pose(rig,actions['EnemyReady'],2)
    amount=math.sin(math.pi*smooth(t/1.4)) if t<1.4 else 0
    rotate(rig,'Spine1',-12*amount);rotate(rig,'RightArm',-18*amount,'X');rotate(rig,'LeftArm',-18*amount,'X');rotate(rig,'Head',6*math.sin(t*2))
    key(rig,action,i+1,'Enemy')
export(rig,objects,action,'Enemy',3)

# Compact paired action. Both actors crouch into a stable, readable struggle;
# translation of the pair through real foliage is controlled by Unity anchors.
for clip,seconds in [('Bush_GrabPull',2.6),('Bush_StruggleLoop',2),('Bush_Finish',2.4),('Bush_Escape',1.4)]:
    made={name:bpy.data.actions.new(name+'_'+clip) for name in ['Nam','Enemy']}
    for action in made.values():action.use_fake_user=True
    for i in range(round(seconds*30)+1):
        t=i/30;p=t/seconds;loop=math.sin(2*math.pi*t/seconds)
        for name in ['Nam','Enemy']:
            rig,objects=characters[name];rig.location=(0,0,0) if name=='Nam' else (-.15,-.43,0)
            pose(rig,actions['EnemyReady'] if name=='Enemy' else actions['NamPair'],2 if name=='Enemy' else 73)
            hips=rig.pose.bones['Hips'].head;target=.66
            if clip=='Bush_GrabPull':target=.66 if name=='Nam' else .96-.30*smooth((t-1.2)/1.4)
            if clip=='Bush_StruggleLoop':target=.66+.012*loop
            if clip=='Bush_Finish':target=.66-.4*smooth((t-1.10)/1.25) if name=='Enemy' else .66
            if clip=='Bush_Escape':target=.66 if name=='Nam' else .66+.30*smooth(p)
            root_shift(rig,(-hips.x,-hips.y,target/rig.scale.z-hips.z))
            if name=='Nam':
                lean=smooth((t-.35)/.75) if clip=='Bush_GrabPull' else 1-smooth((t-1.05)/.65) if clip=='Bush_Finish' else 1-smooth((t-.18)/.8) if clip=='Bush_Escape' else 1
                # Bring the shoulder into reach instead of overextending an
                # elbow or bending the wrist backwards to reach the torso.
                rotate(rig,'Spine',5*lean);rotate(rig,'Spine1',12*lean)
            if clip=='Bush_StruggleLoop':rotate(rig,'Spine1',(3 if name=='Nam' else -4)*loop);rotate(rig,'Head',2*loop)
            if clip=='Bush_Finish' and name=='Enemy':rotate(rig,'Hips',70*smooth((t-1.10)/1.25));rotate(rig,'Spine1',12*smooth((t-1.10)/1.25))
            if clip=='Bush_Escape':rotate(rig,'Spine1',(12 if name=='Enemy' else -8)*math.sin(math.pi*p));rotate(rig,'Head',-4*math.sin(math.pi*p))
            for side,sign in [('Left',1),('Right',-1)]:
                target=rig.location+Vector((sign*.14,.04,.105))
                if clip=='Bush_GrabPull':target.y+=.02*math.sin(t*math.pi*2+(0 if side=='Left' else math.pi))
                if clip=='Bush_Escape':
                    if name=='Enemy':
                        progress=.55*smooth((t-.15)/.95);step=.55*smooth((t-(.20 if side=='Left' else .65))/(.55 if side=='Left' else .65));target.y+=progress-step
                        lift=max(0,min(1,(t-(.20 if side=='Left' else .65))/(.55 if side=='Left' else .65)));target.z+=.08*math.sin(math.pi*lift)
                    else:
                        progress=.28*smooth((t-.25)/.95);step=.28*smooth((t-(.35 if side=='Left' else .65))/.55);target.y+=step-progress
                        lift=max(0,min(1,(t-(.35 if side=='Left' else .65))/.55));target.z+=.055*math.sin(math.pi*lift)
                leg(rig,name,side,target)
        nam=characters['Nam'][0];enemy=characters['Enemy'][0]
        shoulders=(world(enemy,'LeftArm')+world(enemy,'RightArm'))*.5
        targets={'Left':shoulders+Vector((.12,-.12,-.20)), 'Right':shoulders+Vector((-.16,-.12,-.18))}
        release=smooth((t-.18)/.8) if clip=='Bush_Escape' else smooth((t-.95)/.75) if clip=='Bush_Finish' else 0
        engage=smooth((t-.35)/.8) if clip=='Bush_GrabPull' else 1
        # Both closed fists wrap the torso. No hand covers the face and no
        # knife is drawn in this branch; the ordinary neck takedown is separate.
        fist={'Thumb':(40,-70,-70),'Index':(95,75,80),'Middle':(80,90,50),'Ring':(90,65,90),'Pinky':(85,90,70)}
        for pb in nam.pose.bones:
            if not ('Hand' in pb.name and any(f in pb.name for f in fist)):continue
            finger=next(f for f in fist if f in pb.name);joint=int(pb.name[-1])-1
            pb.rotation_mode='QUATERNION'
            degrees=(85,75,80)[joint] if finger=='Ring' and pb.name.startswith('Right') else fist[finger][joint]
            pb.rotation_quaternion=Quaternion(Vector((1,0,0)),math.radians(degrees))
            if finger=='Thumb' and joint==0:pb.rotation_quaternion=pb.rotation_quaternion@Quaternion(Vector((0,0,1)),math.radians(-40 if pb.name.startswith('Left') else 40))
        bpy.context.view_layer.update()
        for side,target in targets.items():
            sign=1 if side=='Left' else -1
            relaxed=world(nam,'Hips')+Vector((sign*.24,-.12,.10))
            # Solve the centre of each closed hand against the tunic, with
            # shoulder/elbow contact; retain the wrist limit in solve().
            for iteration in range(3):
                offset=(world(nam,side+'HandMiddle1')-world(nam,side+'Hand'))*.6
                hand(nam,'Nam',side,relaxed.lerp(target-offset,engage).lerp(relaxed,release),head_envelope)
        left=world(nam,'LeftForeArm').lerp(world(nam,'LeftHand'),.62)
        right=world(nam,'RightForeArm').lerp(world(nam,'RightHand'),.78)
        for side,target in [('Right',left+Vector((0,-.015,-.02))),('Left',right+Vector((.025,.02,.015)))]:
            sign=1 if side=='Left' else -1
            own=(world(enemy,'Hips')+Vector((sign*.22,-.06,.16))) if clip=='Bush_Escape' else world(enemy,'Spine1')+Vector((sign*.17,-.10,-.09))
            current=world(enemy,'Hips')+Vector((sign*.24,-.12,.15)) if clip=='Bush_GrabPull' else world(enemy,side+'Hand')
            hand(enemy,'Enemy',side,current.lerp(target,smooth((t-.8)/.5) if clip=='Bush_GrabPull' else engage).lerp(own,release),head_envelope)
        for name in ['Nam','Enemy']:key(characters[name][0],made[name],i+1,name)
    for name in ['Nam','Enemy']:export(*characters[name],made[name],name,seconds)

# Actual world-space jetty mesh reference from the existing extraction export.
reference=HERE/'captive-reference.json'
if reference.exists():
    data=json.loads(reference.read_text(encoding='utf-8'))
    entries=data if isinstance(data,list) else data.get('meshes',[])
    collection=bpy.data.collections.new('Actual Map 1 geometry reference');scene.collection.children.link(collection)
    for item in entries:
        if 'vertices' not in item:continue
        vertices=[(v[0],-v[2],v[1]) if isinstance(v,list) else (v['x'],-v['z'],v['y']) for v in item['vertices']]
        indices=item.get('triangles',[]);mesh=bpy.data.meshes.new(item.get('name','Map reference'));mesh.from_pydata(vertices,[],[indices[i:i+3] for i in range(0,len(indices),3)])
        obj=bpy.data.objects.new(mesh.name,mesh);collection.objects.link(obj)
    collection.hide_render=True
for name,(rig,objects) in characters.items():rig.location=(3,0,0) if name=='Hung' else (-.15,-.43,0) if name=='Enemy' else (0,0,0)
scene.frame_start=1;scene.frame_end=91;scene.frame_set(1)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Map01_Rescue_Cinematics.blend'))
(HERE/'animation-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('Authored',len(report),'clips; complete source rigs preserved.')
