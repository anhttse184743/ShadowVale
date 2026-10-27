"""Blender source for the Day 2 ladder and Yagi antenna; Unity dimensions in metres."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Project/Map01/Resources/Story/Antenna'
SRC = Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, color, metallic=0, roughness=.5):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Metallic'].default_value=metallic; bs.inputs['Roughness'].default_value=roughness
    return m

wood=material('Ladder_Wood',(.30,.19,.085),0,.8)
edge=material('Wood_Endgrain',(.19,.10,.038),0,.9)
metal=material('Antenna_Aluminium',(.56,.60,.63),.8,.28)
steel=material('Mount_Steel',(.22,.25,.26),.75,.45)
dark=material('Coax_Rubber',(.025,.03,.026),0,.8)
rope=material('Lashing_Rope',(.42,.32,.17),0,.95)
brass=material('Connector_Brass',(.53,.36,.12),.7,.35)

# Fine, high contrast wood fibres are exported as a real texture for Unity.
grain=bpy.data.images.new('LadderWoodGrain',width=256,height=1024,alpha=False)
pixels=[]
for y in range(1024):
    for x in range(256):
        u=x/256;v=y/1024
        wave=u+math.sin(v*16)*.003+math.sin(v*39+u*8)*.0015
        fibre=.75+.12*math.sin(wave*math.tau*71)+.06*math.sin(wave*math.tau*129+v*2)
        growth=math.sin(wave*math.tau*13+math.sin(v*7)*.4)
        fibre=max(.36,min(1,fibre-(max(0,growth)**12)*.20))
        pixels.extend((fibre,fibre*.93,fibre*.80,1))
grain.pixels=pixels;grain.filepath_raw=str(OUT/'LadderWoodGrain.png');grain.file_format='PNG';grain.save()
tex=wood.node_tree.nodes.new('ShaderNodeTexImage');tex.image=grain
mix=wood.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(.30,.19,.085,1)
wood.node_tree.links.new(tex.outputs['Color'],mix.inputs[1]);wood.node_tree.links.new(mix.outputs['Color'],wood.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])

def xyz(p): return (p[0],-p[2],p[1])
def finish(o,name,mat):
    o.name=name; o.data.materials.append(mat); return o
def box(name,p,size,mat,bevel=.005):
    bpy.ops.mesh.primitive_cube_add(size=1,location=xyz(p)); o=bpy.context.object
    o.dimensions=(size[0],size[2],size[1]); bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Soft manufactured edges','BEVEL');mod.width=bevel;mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(o,name,mat)
def rod(name,a,b,r,mat,vertices=12):
    a,b=Vector(xyz(a)),Vector(xyz(b)); delta=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=delta.length,location=(a+b)/2)
    o=bpy.context.object;o.rotation_euler=delta.to_track_quat('Z','Y').to_euler()
    for poly in o.data.polygons: poly.use_smooth=len(poly.vertices)==4
    return finish(o,name,mat)
def tube(name,points,r,mat):
    c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.resolution_u=1;c.bevel_depth=r;c.bevel_resolution=2
    sp=c.splines.new('POLY');sp.points.add(len(points)-1)
    for dst,p in zip(sp.points,points): dst.co=(*xyz(p),1)
    o=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(o);o.data.materials.append(mat)
    bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o.select_set(False)
    return o
def export(name,objects):
    # One mesh per material keeps these reusable props inexpensive in Unity.
    merged=[]
    groups={mat:[o for o in objects if o.data.materials[0]==mat] for mat in {o.data.materials[0] for o in objects}}
    for mat,group in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for o in group:o.select_set(True)
        bpy.context.view_layer.objects.active=group[0]
        bpy.ops.object.join();joined=bpy.context.object;joined.name=name+'_'+mat.name
        # Each exported asset has its root at the ground / mast base.
        bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        merged.append(joined)
    bpy.ops.object.select_all(action='DESELECT')
    for o in merged:o.select_set(True)
    bpy.context.view_layer.objects.active=merged[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True)
    return merged

# 4.8 m ladder. The player stays between the rails; the tree is behind it.
for x in (-.37,.37):
    box('Continuous timber rail',(x,2.4,.35),(.085,4.8,.105),wood,.012)
    box('Steel foot shoe',(x,.10,.35),(.095,.20,.115),steel,.006)
    for y in (.45,2.4,4.45):
        box('Rail repair band',(x,y,.35),(.092,.065,.115),steel,.003)
        rod('Band rivet',(x,y,.285),(x,y,.277),.014,metal,8)
for i in range(16):
    y=.3+i*.285
    rod('Rounded hardwood rung',(-.405,y,.35),(.405,y,.35),.032,wood,16)
    for x in (-.418,.418):
        rod('Rung end peg',(x-.008,y,.35),(x+.008,y,.35),.019,edge)
    # Narrow cut tread grooves catch highlights without a texture dependency.
    for x in (-.22,-.11,0,.11,.22):
        box('Rung grip notch',(x,y+.03,.35),(.012,.003,.035),edge,.001)
for y in (1.2,3.9):
    for turn in range(4):
        h=y+turn*.018
        points=[(.48*math.sin(a),h,.75-.48*math.cos(a)) for a in [j*math.tau/48 for j in range(49)]]
        tube('Tree safety lashing',points,.008,rope)
    tube('Lashing knot',[(.34,y,.32),(.45,y+.07,.27),(.39,y+.14,.30),(.31,y+.07,.29),(.40,y,.28),(.43,y-.16,.32)],.012,rope)
ladder=export('FieldLadder',list(bpy.context.scene.objects))

# Yagi array: a separate mast, a long boom, ten tapered elements, driven loop,
# saddle clamps, hex fasteners, rubber end caps and a coaxial feed box.
rod('Tubular mast',(0,0,0),(0,1.45,0),.024,metal,20)
box('Boom',(0,1.35,0),(.048,.045,1.9),metal,.004)
for i in range(10):
    z=-.84+i*.18; half=.68-i*.037
    rod('Yagi element %02d'%i,(-half,1.38,z),(half,1.38,z),.014,metal)
    box('Insulated element saddle',(0,1.365,z),(.08,.045,.06),dark,.006)
    for x in (-half,half):rod('Element weather cap',(x-.014,1.38,z),(x+.014,1.38,z),.016,dark)
    rod('Element clamp screw',(0,1.39,z),(0,1.413,z),.013,steel,6)
box('Mast mounting saddle',(0,1.30,0),(.15,.13,.11),steel,.008)
for y in (.48,.76):
    box('Tree mounting plate',(0,y,.09),(.23,.08,.035),steel,.006)
    for x in (-.08,.08):
        rod('Hex mounting bolt',(x,y,.035),(x,y,.125),.014,metal,6)
    tube('U bolt clamp',[(-.06,y,.07),(-.06,y,-.035),(0,y,-.065),(.06,y,-.035),(.06,y,.07)],.009,steel)
box('Coax feed enclosure',(0,1.28,-.33),(.13,.09,.15),dark,.012)
rod('Coax connector',(0,1.23,-.33),(0,1.18,-.33),.018,brass)
tube('Coax drip loop',[(0,1.18,-.33),(.04,1.02,-.34),(.12,.98,-.22),(.13,1.12,-.08),(.04,1.2,0),(.03,.5,0)],.009,dark)
tube('Folded driven element',[(-.39,1.38,-.35),(-.48,1.38,-.38),(-.48,1.38,-.43),(-.39,1.38,-.46),(.39,1.38,-.46),(.48,1.38,-.43),(.48,1.38,-.38),(.39,1.38,-.35)],.009,metal)
antenna=export('FieldAntenna',[o for o in bpy.context.scene.objects if o not in ladder])

# Keep an editable source and a studio render of both exported models.
for o in antenna:o.location.x+=1.65
bpy.ops.object.select_all(action='DESELECT')
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.world.color=(.22,.22,.22)
bpy.ops.object.camera_add(location=(8,-11,7));cam=bpy.context.object
target=Vector((.5,0,2.4));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=6.1;scene.camera=cam
for location,power,size in [((1,-4,7),1500,5),((-4,-1,4),900,4),((3,4,6),1800,3)]:
    bpy.ops.object.light_add(type='AREA',location=location);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(SRC/'models-preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'DayTwo_Antenna_Ladder.blend'))
bpy.ops.render.render(write_still=True)
(SRC/'asset-receipt.json').write_text(json.dumps({'ladder_meshes':len(ladder),'antenna_meshes':len(antenna),'ladder_height':4.8,'rung_spacing':.285,'export_axis':'-Z forward / Y up'},indent=2))
print('ANTENNA_LADDER_EXPORT_PASS')
