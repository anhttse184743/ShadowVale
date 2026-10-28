"""Editable Blender crawl review. Never edits Unity scenes/controllers or source rigs.
blender -b -t 4 --python SourceArt/Animation/NamCrawl/build_crawl.py -- --stills
Omit --stills to render all six animated review loops.
"""
import bpy, math, json, sys
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
OUT=ROOT/'Logs/AnimationPreview/NamCrawl'
OUT.mkdir(parents=True,exist_ok=True)
STILLS='--stills' in sys.argv
REFERENCE_FIT='--reference-fit' in sys.argv or '--fit-reference' in sys.argv
RIFLE_REVIEW='--rifle-review' in sys.argv or REFERENCE_FIT
FIT={'body_yaw':-25.,'body_x':.15,'body_y':-.10,'clavicle_forward':35.,'clavicle_down':40.,'spine1':2.,'spine2':-12.,'gun_z':.248,'left_pole':.12,'right_pole':.12}
if REFERENCE_FIT:
    OUT=OUT/'ProneReference';OUT.mkdir(exist_ok=True)
    if (HERE/'prone_reference_fit.json').exists():FIT.update(json.loads((HERE/'prone_reference_fit.json').read_text())['parameters'])
N=36
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE'
scene.eevee.taa_render_samples=16
scene.cycles.samples=12
scene.cycles.use_denoising=True
scene.render.resolution_x=800;scene.render.resolution_y=600;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.fps=15
scene.world=bpy.data.worlds.new('Review world');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.20,.23,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.4
scene.view_settings.view_transform='AgX'
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/_Project/Art/Characters/Player/Player.fbx'))
rig=next(o for o in scene.objects if o.type=='ARMATURE');rig.name='Nam_Crawl_Review'
rig.animation_data_clear()
rig.rotation_mode='QUATERNION'
for p in rig.pose.bones:
    for c in list(p.constraints):p.constraints.remove(c)
    p.rotation_mode='QUATERNION'
rest={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
character=[o for o in scene.objects if o.type=='MESH']
tex=ROOT/'Assets/_Project/Art/Characters/Player/Textures'
for o in character:
    for m in o.data.materials:
        if not m:continue
        m.use_nodes=True
        bs=m.node_tree.nodes.get('Principled BSDF')
        name='T_Nam_Gear.png' if 'Gear' in o.name else 'T_Nam_BaloRanRi.png' if 'Balo' in o.name else 'texture_pbr_20250901.png'
        image=m.node_tree.nodes.new('ShaderNodeTexImage');image.image=bpy.data.images.load(str(tex/name),check_existing=True)
        m.node_tree.links.new(image.outputs['Color'],bs.inputs['Base Color'])
        bs.inputs['Roughness'].default_value=.83

def pos(name):return rig.matrix_world@rig.pose.bones[name].head
def rotate_bone(name,angle,axis=(1,0,0)):
    p=rig.pose.bones[name];world=rig.matrix_world@p.matrix
    world=Matrix.LocRotScale(world.translation,Quaternion(Vector(axis),math.radians(angle))@world.to_quaternion(),world.to_scale())
    p.matrix=rig.matrix_world.inverted()@world;bpy.context.view_layer.update()
def aim_bone(name,direction):
    p=rig.pose.bones[name];m=rig.matrix_world@p.matrix
    current=rig.matrix_world.to_3x3()@(p.tail-p.head)
    q=current.rotation_difference(Vector(direction))@m.to_quaternion()
    p.matrix=rig.matrix_world.inverted()@Matrix.LocRotScale(m.translation,q,m.to_scale());bpy.context.view_layer.update()
def solve(upper,lower,end,target,hint):
    u,l,e=[rig.pose.bones[n] for n in (upper,lower,end)]
    origin=pos(upper);a=(pos(lower)-origin).length;b=(pos(end)-pos(lower)).length
    delta=Vector(target)-origin;axis=delta.normalized();d=max(abs(a-b)+.002,min((a+b)*.98,delta.length))
    bend=Vector(hint)-origin;bend=(bend-axis*bend.dot(axis)).normalized()
    along=(a*a+d*d-b*b)/(2*d);elbow=origin+axis*along+bend*math.sqrt(max(0,a*a-along*along))
    if REFERENCE_FIT and upper in ['LeftArm','RightArm'] and elbow.z<.035:
        radius=math.sqrt(max(0,a*a-along*along));center=origin+axis*along
        up=Vector((0,0,1))-axis*axis.z
        if radius*up.length>1e-6:
            cosine=max(-1,min(1,(.035-center.z)/(radius*up.length)))
            vertical=up.normalized();horizontal=axis.cross(vertical)
            choices=[vertical*cosine+horizontal*(s*math.sqrt(max(0,1-cosine*cosine))) for s in [-1,1]]
            elbow=center+max(choices,key=lambda v:v.dot(bend))*radius
    for p,start,finish in [(u,origin,elbow),(l,elbow,origin+axis*d)]:
        world=rig.matrix_world@p.matrix;cur=rig.matrix_world.to_3x3()@(p.tail-p.head)
        q=cur.rotation_difference(finish-start)@world.to_quaternion()
        p.matrix=rig.matrix_world.inverted()@Matrix.LocRotScale(start,q,world.to_scale());bpy.context.view_layer.update()
def palm(side,forward=(0,-1,-.05),down=(0,0,-1)):
    hand=rig.pose.bones[side+'Hand'];m=rig.matrix_world@hand.matrix
    long=(pos(side+'HandMiddle1')-pos(side+'Hand')).normalized()
    across=(pos(side+'HandIndex1')-pos(side+'HandPinky1')).normalized()
    normal=long.cross(across).normalized()
    if side=='Right':normal=-normal
    target=Vector(forward).normalized();dn=Vector(down);dn=(dn-target*dn.dot(target)).normalized()
    source=Matrix((long,normal,long.cross(normal))).transposed()
    dest=Matrix((target,dn,target.cross(dn))).transposed()
    q=(dest@source.inverted()).to_quaternion()@m.to_quaternion()
    hand.matrix=rig.matrix_world.inverted()@Matrix.LocRotScale(m.translation,q,m.to_scale());bpy.context.view_layer.update()
def fingers(side,closed):
    for finger in ['Index','Middle','Ring','Pinky']:
        for segment in range(1,4):
            p=rig.pose.bones.get(side+'Hand'+finger+str(segment))
            if p:
                direction=(rig.matrix_world.to_3x3()@(p.tail-p.head)).normalized()
                inward=hand_normals[side]
                axis=direction.cross(inward).normalized()
                rotate_bone(p.name,(52 if segment==1 else 62)*closed,axis)
    for segment in range(1,4):
        p=rig.pose.bones.get(side+'HandThumb'+str(segment))
        if p:
            direction=(rig.matrix_world.to_3x3()@(p.tail-p.head)).normalized()
            axis=direction.cross(hand_normals[side]).normalized()
            rotate_bone(p.name,18*closed,axis)
    bpy.context.view_layer.update()

def material(name,color,metal=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    bs=m.node_tree.nodes['Principled BSDF'];bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=.75;bs.inputs['Metallic'].default_value=metal;return m
def load_prop(path,name):
    before=set(bpy.data.objects)
    if path.suffix=='.glb':bpy.ops.import_scene.gltf(filepath=str(path))
    else:bpy.ops.import_scene.fbx(filepath=str(path))
    objects=list(set(bpy.data.objects)-before)
    meshes=[o for o in objects if o.type=='MESH']
    # Bake imported transforms once, retain actual source geometry/materials.
    worlds={o:o.matrix_world.copy() for o in meshes}
    for o in meshes:
        o.data=o.data.copy();o.data.transform(worlds[o]);o.parent=None;o.matrix_world=Matrix.Identity(4)
    for o in objects:
        if o not in meshes:bpy.data.objects.remove(o,do_unlink=True)
    parent=bpy.data.objects.new(name,None);scene.collection.objects.link(parent)
    for o in meshes:o.parent=parent
    bpy.context.view_layer.update()
    return parent,meshes
gun,guns=load_prop(ROOT/'Assets/_Project/Art/Items/ak-47.glb','AK47_Review')
# The AK source labels its functional parts; align barrel from stock to muzzle.
def center(objects):
    coords=[o.matrix_world@Vector(c) for o in objects for c in o.bound_box]
    return sum(coords,Vector())/len(coords)
def part(fragment):return [o for o in guns if fragment.lower() in o.name.lower()]
barrel=center(part('barrel') or guns);stock=center(part('stock') or guns)
forward=(barrel-stock).normalized()
up=Vector((0,0,1))
if abs(forward.dot(up))>.9:up=Vector((0,1,0))
# Source glTF is imported with vertical +Z. Use magazine to determine the up axis.
if part('mag'):
    up=(center(guns)-center(part('mag'))).normalized()
up=(up-forward*up.dot(forward)).normalized();right=forward.cross(up).normalized()
src=Matrix((right,forward,up)).transposed();dst=Matrix(((1,0,0),(0,-1,0),(0,0,1))).transposed()
rot=dst@src.inverted()
coords=[rot@(o.matrix_world@v.co) for o in guns for v in o.data.vertices]
length=max(v.y for v in coords)-min(v.y for v in coords);factor=.88/length
grip=rot@center(part('grip') or guns)
for o in guns:
    o.data.transform(Matrix.Diagonal((factor,factor,factor,1))@rot.to_4x4())
    for v in o.data.vertices:v.co-=grip*factor
bpy.context.view_layer.update()
wood=part('woodLower')
wood_vertices=[v.co.copy() for ob in wood for v in ob.data.vertices]
# Rear underside of the actual wooden handguard, not the magazine/receiver.
foregrip=Vector((sum(v.x for v in wood_vertices)/len(wood_vertices),max(v.y for v in wood_vertices)-.025,min(v.z for v in wood_vertices)+.012))
knife,knives=load_prop(ROOT/'Assets/_Project/Art/Items/ka-bar_knife.glb','Knife_Review')
coords=[v.co for o in knives for v in o.data.vertices]
mins=Vector([min(v[i] for v in coords) for i in range(3)]);maxs=Vector([max(v[i] for v in coords) for i in range(3)])
axis=max(range(3),key=lambda i:(maxs-mins)[i]);direction=Vector([1 if i==axis else 0 for i in range(3)])
krot=direction.rotation_difference(Vector((0,1,0))).to_matrix().to_4x4();scale=.28/(maxs-mins)[axis]
for o in knives:
    for v in o.data.vertices:v.co=(krot.to_3x3()@(v.co-(mins+maxs)*.5))*scale
grenade,grenades=load_prop(ROOT/'Assets/_Project/Art/Environment/Map01Items/Meshes/Item_grenade_f1.fbx','Grenade_Review')
coords=[v.co for o in grenades for v in o.data.vertices];lo=Vector([min(v[i] for v in coords) for i in range(3)]);hi=Vector([max(v[i] for v in coords) for i in range(3)])
for o in grenades:
    for v in o.data.vertices:v.co=(v.co-(lo+hi)*.5)*(.11/max(hi-lo))
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=.045)
stone=bpy.context.object;stone.name='Stone_Review';stone.scale=(1,.8,.7);stone.data.materials.append(material('Stone',(.26,.27,.24)))
props={'rifle':(gun,guns),'aim':(gun,guns),'knife':(knife,knives),'grenade':(grenade,grenades),'stone':(stone,[stone])}
hand_normals={}

def smooth(value):
    value=max(0,min(1,value));return value*value*(3-2*value)
def aim_progress(phase):
    t=phase*4.8
    return smooth((t-.5)/.8) if t<1.3 else 1 if t<3.1 else 1-smooth((t-3.1)/.8)
def pose(phase,kind,still=False):
    rig.animation_data_clear()
    for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
    rifle_pose=kind in ['rifle','aim','transition']
    weight=aim_progress(phase) if kind=='transition' else 1 if kind=='aim' else 0
    wave=0 if still else math.sin(phase*math.tau)*(1-weight)
    heading=Quaternion((0,0,1),math.radians(FIT['body_yaw']*weight)) if REFERENCE_FIT else Quaternion()
    rig.rotation_quaternion=heading@Quaternion((1,0,0),math.pi/2)
    rig.location=(0,0,0);bpy.context.view_layer.update()
    offset=Vector((FIT['body_x']*weight,FIT['body_y']*weight,0)) if REFERENCE_FIT else Vector()
    rig.location+=offset+Vector((wave*.012,0,.245+.006*math.cos(phase*math.tau*2)))-pos('Hips');bpy.context.view_layer.update()
    rotate_bone('Spine1',-8+(8+FIT['spine1'])*weight if REFERENCE_FIT else -8+10*weight)
    rotate_bone('Spine2',FIT['spine2']*weight if REFERENCE_FIT else -12*weight)
    rotate_bone('Spine1',0 if REFERENCE_FIT else -15*weight,(0,0,1))
    rotate_bone('Neck',-25-31*weight);rotate_bone('Head',-35+24*weight)
    rotate_bone('Neck',-FIT['body_yaw']*weight if REFERENCE_FIT else 15*weight,(0,0,1))
    if rifle_pose:
        # Protract/lower the shoulder girdle through its existing joints, retaining
        # the approved chest/head lift. Bone lengths and rest transforms stay intact.
        for side,sign in [('Left',1),('Right',-1)]:
            rotate_bone(side+'Shoulder',-sign*(FIT['clavicle_forward'] if REFERENCE_FIT else 35)*weight,(0,0,1))
            rotate_bone(side+'Shoulder',sign*(FIT['clavicle_down'] if REFERENCE_FIT else 23)*weight,heading@Vector((0,1,0)))
    if rifle_pose:
        flat=Quaternion((0,0,1),math.radians(75))@Quaternion((0,1,0),math.radians(-75))
        gun.rotation_mode='QUATERNION';gun.rotation_quaternion=flat.slerp(Quaternion(),weight)
        gun.location=Vector((0,-.78,.155)).lerp(Vector((-.14,-.98,FIT['gun_z'] if REFERENCE_FIT else .275)),weight)
        gun_rotation=gun.rotation_quaternion.copy()
    for side,sign in [('Left',1),('Right',-1)]:
        pull=wave*sign;shoulder=pos(side+'Arm')
        target=(sign*.29,shoulder.y-.19-pull*.16,.085+max(0,-pull)*.035)
        if rifle_pose:
            wrist=Vector((-.015,.015+.05*weight,.07-.075*weight)) if side=='Right' else Vector((0,-.28,.025)).lerp(foregrip+Vector((.035,.075,-.045)),weight)
            target=gun.location+gun_rotation@wrist
        if kind in ['knife','grenade','stone'] and side=='Right':target=(-.31,shoulder.y-.17-pull*.10,.15+max(0,-pull)*.025)
        pole=FIT['left_pole' if side=='Left' else 'right_pole'] if REFERENCE_FIT else .12
        solve(side+'Arm',side+'ForeArm',side+'Hand',target,shoulder+Vector((sign*pole,.02,-1)) if rifle_pose else (sign*.46,shoulder.y+.15,.07))
        holding=kind!='unarmed' and (side=='Right' or rifle_pose)
        direction=(0,0,-1) if holding and side=='Right' else (0,-1,-.05)
        normal=(1,0,0) if holding and side=='Right' else (0,0,1) if rifle_pose else (0,0,-1)
        if rifle_pose:
            if side=='Left':direction=Vector((-.35*weight,-1,-.05))
            else:direction=Vector((0,-.8*weight,-1+.4*weight))
            direction=gun_rotation@Vector(direction);normal=gun_rotation@Vector(normal)
        hand_normals[side]=Vector(normal)
        palm(side,direction,normal)
        fingers(side,.95 if holding else .04)
        if rifle_pose:
            # Close the thumb over the grip, rather than leaving it splayed upward.
            target_thumb=pos(side+'HandIndex2')+hand_normals[side]*.012
            solve(side+'HandThumb1',side+'HandThumb2',side+'HandThumb3',target_thumb,pos(side+'HandThumb1')+Vector((sign*.05,0,.025)))
        # Contralateral knee advances with the opposite arm.
        bend=(1-pull)*.5
        foot_target=Vector((sign*(.19+bend*.16),.78-bend*.30,.10)).lerp(Vector((sign*.17,.86,.085)),weight)
        knee_hint=Vector((sign*.55,.31,.065)).lerp(Vector((sign*.20,.43,.055)),weight)
        if REFERENCE_FIT:foot_target=heading@foot_target+offset;knee_hint=heading@knee_hint+offset
        solve(side+'UpLeg',side+'Leg',side+'Foot',foot_target,knee_hint)
        aim_bone(side+'Foot',heading@Vector((sign*.04,.16,-.035)))
    for name,(parent,meshes) in props.items():
        for ob in meshes:ob.hide_render=True
    prop_kind='rifle' if kind=='transition' else kind
    if prop_kind in props:
        parent,meshes=props[prop_kind]
        for ob in meshes:ob.hide_render=False
        if rifle_pose:
            pass # Both hand targets were solved from the same weapon transform.
        else:
            hand=rig.matrix_world@rig.pose.bones['RightHand'].matrix
            parent.location=pos('RightHand')+Vector((.035,-.025,-.065))
            if kind=='knife':parent.location.y-=.08
            parent.rotation_mode='QUATERNION'
            parent.rotation_quaternion=Quaternion((0,0,1),-.3) if kind=='knife' else Quaternion((0,1,0),math.pi/2) if kind=='grenade' else Quaternion()
    bpy.context.view_layer.update()
    if phase==0:
        (OUT/(kind+'-bounds.json')).write_text(json.dumps({name:{'position':list(parent.location),'meshes':[(ob.name,[list(ob.matrix_world@Vector(v)) for v in ob.bound_box],ob.hide_render) for ob in meshes]} for name,(parent,meshes) in props.items()},indent=2))
        if kind=='aim':
            (OUT/'aim-contact-points.json').write_text(json.dumps({'gun_position':list(gun.location),'wood_foregrip_local':list(foregrip),'bones':{name:list(pos(name)) for name in ['Head','Spine2','LeftArm','RightArm','LeftForeArm','RightForeArm','LeftHand','RightHand']}},indent=2))

if REFERENCE_FIT:
    exec(compile((HERE/'prone_reference_fit.py').read_text(),str(HERE/'prone_reference_fit.py'),'exec'),globals())
    if '--fit-reference' in sys.argv:
        fit_reference();sys.exit(0)

bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='Review ground';floor.location.z=-.018
floor.data.materials.append(material('Ground',(.10,.13,.115)))
line=material('Half metre grid',(.19,.23,.20))
for i in range(-6,7):
    for axis in [0,1]:
        bpy.ops.mesh.primitive_cube_add(size=1,location=(i*.5 if axis==0 else 0,0 if axis==0 else i*.5,-.014))
        o=bpy.context.object;o.name='Review grid';o.dimensions=(.007,6,.002) if axis==0 else (6,.007,.002);o.data.materials.append(line)
def light(name,loc,power,size):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.2))-o.location).to_track_quat('-Z','Y').to_euler()
light('Soft key',(-2,-3,5),650,4);light('Fill',(3,1,3),450,3)
camdata=bpy.data.cameras.new('Review camera');cam=bpy.data.objects.new('Review camera',camdata);scene.collection.objects.link(cam);scene.camera=cam
camdata.type='ORTHO';camdata.ortho_scale=2.65
def camera(side=False):
    camdata.ortho_scale=3.1 if side else 2.8
    cam.location=(-3,.1,.85) if side else (-2.4,-2.7,2.1)
    cam.rotation_euler=(Vector((0,-.13,.20))-cam.location).to_track_quat('-Z','Y').to_euler()

def locals_snapshot():return {p.name:p.matrix_basis.copy() for p in rig.pose.bones}
validation={}
KINDS=['unarmed','rifle','aim','knife','grenade','stone']+(['transition'] if RIFLE_REVIEW else [])
for kind in KINDS:
    count=N*2 if kind=='transition' else N
    pose(0,kind);first=locals_snapshot();pose(1,kind);last=locals_snapshot()
    error=max(max(abs(first[n][r][c]-last[n][r][c]) for r in range(4) for c in range(4)) for n in first)
    assert error<.001,(kind,error)
    action=bpy.data.actions.new('Nam_ProneCrawl_'+kind.title());action.use_fake_user=True
    for frame in range(count+1):
        pose(frame/count,kind);local=locals_snapshot()
        rig.animation_data_create();rig.animation_data.action=action
        rig.keyframe_insert('location',frame=frame);rig.keyframe_insert('rotation_quaternion',frame=frame)
        for p in rig.pose.bones:
            p.matrix_basis=local[p.name]
            p.keyframe_insert('location',frame=frame,group=p.name);p.keyframe_insert('rotation_quaternion',frame=frame,group=p.name)
    validation[kind]={'frames':count+1,'duration':count/15,'loop_seam_max':error}
    folder=OUT/kind;folder.mkdir(exist_ok=True)
    if RIFLE_REVIEW and kind not in ['rifle','aim','transition']:continue
    for frame in ([0,count//4,count//2,count*3//4] if STILLS else range(count)):
        scene.frame_set(frame);pose(frame/count,kind)
        camera();scene.render.filepath=str(folder/f'frame-{frame:03}.png');bpy.ops.render.render(write_still=True)
        if kind=='transition':
            camera(True);scene.render.filepath=str(folder/f'side-{frame:03}.png');bpy.ops.render.render(write_still=True)
    pose(.25,kind);camera(True);scene.render.filepath=str(folder/'side.png');bpy.ops.render.render(write_still=True)
    if kind=='aim':
        pose(0,kind,True);camera();scene.render.filepath=str(folder/'aim-still.png');bpy.ops.render.render(write_still=True)
    print('VARIANT_COMPLETE',kind,flush=True)
# Each variant gets a playable Blender scene, including its held prop.
# Source rest matrices are never edited; the review does not overwrite source FBXs.
assert rest=={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
if REFERENCE_FIT:
    pose(0,'aim');reference_metrics=reference_measure()
scene.name='Source studio'
scene.frame_start=0;scene.frame_end=N-1
review_scenes=[]
for kind in KINDS:
    bpy.context.window.scene=scene
    pose(0,kind);camera()
    rig.animation_data_create();rig.animation_data.action=bpy.data.actions['Nam_ProneCrawl_'+kind.title()]
    scene.frame_set(0)
    attached=None
    prop_kind='rifle' if kind=='transition' else kind
    if prop_kind in props:
        attached=props[prop_kind][0];world=attached.matrix_world.copy()
        attached.parent=rig;attached.parent_type='BONE';attached.parent_bone='RightHand'
        bpy.context.view_layer.update();attached.matrix_world=world;bpy.context.view_layer.update()
    bpy.ops.scene.new(type='FULL_COPY')
    review=bpy.context.scene;review.name='Crawl '+kind;review.frame_start=0;review.frame_end=(N*2 if kind=='transition' else N)-1
    review_scenes.append(review)
    bpy.context.window.scene=scene
    if attached:
        world=attached.matrix_world.copy();attached.parent=None;attached.matrix_world=world
bpy.context.window.scene=scene
pose(0,'unarmed');scene.frame_start=0;scene.frame_end=N-1;camera()
rig.animation_data_create();rig.animation_data.action=bpy.data.actions['Nam_ProneCrawl_Unarmed']
note=bpy.data.texts.new('READ ME - crawl review')
note.write('Six separate Crawl scenes are selectable in the Scene dropdown. Press Space to preview the active 2.4-second in-place loop. The six original actions live in the Action Editor. Held props are attached to the right hand in each review scene. This is a Blender review draft: no Unity gameplay bindings or source-model rest changes.\n')
bpy.context.window.scene=review_scenes[-1] if RIFLE_REVIEW else review_scenes[0]
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(HERE/('Nam_Crawl_Prone_Reference_v3.blend' if REFERENCE_FIT else 'Nam_Crawl_Rifle_Review_v2.blend' if RIFLE_REVIEW else 'Nam_Crawl_Review.blend')))
if REFERENCE_FIT:
    (OUT/'rig-angle-validation.json').write_text(json.dumps(reference_metrics,indent=2))
(OUT/'validation.json').write_text(json.dumps({'original_rest_preserved':True,'gameplay_changed':False,'variants':validation},indent=2))
print('CRAWL_REVIEW_COMPLETE',flush=True)
