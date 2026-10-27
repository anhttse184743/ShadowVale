"""Editable pose proposal using the game's existing characters. No gameplay changes."""
import bpy, math
from pathlib import Path
from mathutils import Vector, Matrix
ROOT=Path.cwd(); OUT=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
def load(path,label,texture):
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/path))
    objects=set(bpy.data.objects)-before
    rig=next(o for o in objects if o.type=='ARMATURE');rig.name=label
    rig.animation_data_clear()
    for p in rig.pose.bones:
        p.matrix_basis=Matrix.Identity(4)
        for c in list(p.constraints):p.constraints.remove(c)
    for o in objects:
        if o.type in ('CAMERA','LIGHT'):bpy.data.objects.remove(o,do_unlink=True)
        elif o.type=='MESH':
            for m in o.data.materials:
                if not m:continue
                m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF')
                if label=='Nam':
                    name='T_Nam_Gear.png' if 'Gear' in o.name else 'T_Nam_BaloRanRi.png' if 'Balo' in o.name else 'texture_pbr_20250901.png'
                    f=ROOT/texture/name
                    if f.exists():
                        node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=bpy.data.images.load(str(f),check_existing=True);m.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
                bs.inputs['Roughness'].default_value=.85
    for im in bpy.data.images:
        if im.source=='FILE' and not Path(im.filepath).exists():
            files=list((ROOT/texture).rglob(Path(im.filepath.replace('\\','/')).name))
            if files:im.filepath=str(files[0]);im.reload()
    return rig
nam=load('Assets/_Project/Art/Characters/Player/Player.fbx','Nam',Path('Assets/_Project/Art/Characters/Player/Textures'))
tam=load('Assets/_Project/Art/Characters/NPCs/Anh Tâm.fbx','Tam',Path('Assets/_Project/Art/Characters/NPCs/Anh Tâm Textures'))
def bone(r,n):return next(p for p in r.pose.bones if p.name.split(':')[-1]==n)
def pos(r,n):return r.matrix_world@bone(r,n).head
def aim(r,n,d):
    p=bone(r,n);w=r.matrix_world@p.matrix
    current=r.matrix_world.to_3x3()@(p.tail-p.head)
    q=current.normalized().rotation_difference(Vector(d).normalized())
    p.matrix=r.matrix_world.inverted()@Matrix.LocRotScale(w.translation,q@w.to_quaternion(),w.to_scale());bpy.context.view_layer.update()
def solve(r,upper,lower,end,target,hint):
    origin=pos(r,upper);a=(pos(r,lower)-origin).length;b=(pos(r,end)-pos(r,lower)).length
    delta=Vector(target)-origin;dist=min(delta.length,a+b-.003);axis=delta.normalized()
    pole=Vector(hint)-origin;pole=(pole-axis*pole.dot(axis)).normalized()
    x=(a*a-b*b+dist*dist)/(2*dist);elbow=origin+axis*x+pole*math.sqrt(max(0,a*a-x*x))
    aim(r,upper,elbow-origin);aim(r,lower,Vector(target)-pos(r,lower))
nam.location.x=-.33;tam.location.x=.33
bpy.context.view_layer.update()
for r in (nam,tam):
    for n in ('LeftUpLeg','RightUpLeg'):aim(r,n,(0,0,-1))
    for n in ('LeftLeg','RightLeg'):aim(r,n,(0,0,-1))
    for n in ('LeftFoot','RightFoot'):aim(r,n,(0,-1,-.12))
    for n in ('Spine','Spine1','Spine2'):aim(r,n,((-.12 if r==tam else .04),-.06,1))
    aim(r,'Head',(.08,-.15,1) if r==tam else (0,0,1))
tam.location.z-=.065;bpy.context.view_layer.update()
# Tâm's right arm rests across Nam's shoulders; the hand hangs in front.
shoulder=pos(nam,'LeftArm')
solve(tam,'RightArm','RightForeArm','RightHand',shoulder+Vector((-.07,-.14,-.13)),shoulder+Vector((.12,.03,.13)))
aim(tam,'RightHand',(0,-.2,-1))
# Nam's supporting arm wraps around Tâm's waist, leaving his right hand free.
waist=pos(tam,'Spine')
solve(nam,'LeftArm','LeftForeArm','LeftHand',waist+Vector((.09,.12,.02)),waist+Vector((-.30,.20,-.1)))
aim(nam,'LeftHand',(1,-.2,.05))
aim(nam,'RightArm',(-.23,-.12,-1));aim(nam,'RightForeArm',(-.1,-.8,-.42));aim(nam,'RightHand',(0,-1,-.15))
aim(tam,'LeftArm',(.18,-.05,-1));aim(tam,'LeftForeArm',(-.2,-.1,-1));aim(tam,'LeftHand',(0,0,-1))
# Editable, deliberately simple pistol silhouette for hand placement review.
def mat(name,c):
    m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*c,1);return m
metal=mat('Pistol placement proxy',(.045,.05,.055))
hand=pos(nam,'RightHand')
for name,offset,scale in [('K54 placement slide',(0,-.08,-.02),(.027,.105,.025)),('K54 placement grip',(0,-.005,-.075),(.024,.035,.065))]:
    bpy.ops.mesh.primitive_cube_add(size=2,location=hand+Vector(offset));o=bpy.context.object;o.name=name;o.scale=scale;o.data.materials.append(metal)
bpy.ops.mesh.primitive_plane_add(size=200);bpy.context.object.data.materials.append(mat('Neutral earth',(.15,.18,.15)))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.world=bpy.data.worlds.new('Review world');scene.world.color=(.28,.28,.28)
for loc,power,size in [((2,-4,5),850,4),((-3,-1,3),500,3),((0,3,4),700,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1400;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
bpy.ops.object.camera_add(location=(-2,-5,2.2));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,.95))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=2.65;scene.camera=cam
scene.view_settings.view_transform='AgX'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Nam_Tam_Escort_Proposal.blend'))
scene.render.filepath=str(OUT/'Nam_Tam_Escort_Preview.png');bpy.ops.render.render(write_still=True)
