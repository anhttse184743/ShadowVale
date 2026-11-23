"""Blender-authored, metre-scale village gate and shielded kerosene lamp."""
import bpy, math, random, os, sys
from mathutils import Vector

ROOT = sys.argv[sys.argv.index('--') + 1]
ASSET = os.path.join(ROOT, 'Assets/_Project/Map02/Resources/Map02DayOne/NightWatch')
SOURCE = os.path.join(ROOT, 'SourceArt/NightWatch_20261123')
os.makedirs(ASSET + '/Textures', exist_ok=True)
os.makedirs(SOURCE, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
random.seed(1123)

def u(v): return Vector((v[0], -v[2], v[1]))
def parent(obj, owner):
    world = obj.matrix_world.copy()
    obj.parent = owner
    obj.matrix_world = world
    return obj
def empty(name, pos=(0,0,0), owner=None):
    o = bpy.data.objects.new(name, None); bpy.context.collection.objects.link(o); o.location = u(pos)
    bpy.context.view_layer.update()
    return parent(o, owner) if owner else o
def mat(name, color, metal=0, rough=.65):
    m = bpy.data.materials.new(name); m.diffuse_color = (*color, 1); m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value = (*color,1)
    p.inputs['Metallic'].default_value = metal; p.inputs['Roughness'].default_value = rough
    return m
wood = mat('NW_Wood', (.38,.23,.10)); bamboo = mat('NW_Bamboo', (.37,.34,.15)); iron = mat('NW_Iron', (.09,.10,.085), .72, .57)
brass = mat('NW_Brass', (.30,.24,.10), .65, .43); rope = mat('NW_Rope', (.44,.36,.21)); glass = mat('NW_Glass', (.7,.65,.45), 0, .15)
flame = mat('NW_Flame', (1,.48,.09), 0, .35)
flame.node_tree.nodes.get('Principled BSDF').inputs['Emission Color'].default_value = (1,.27,.035,1)
flame.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'].default_value = 3
glass.node_tree.nodes.get('Principled BSDF').inputs['Transmission Weight'].default_value = .82

# Authored grain image, packed in the editable .blend and shared by Unity materials.
size = 512; pixels = []
for y in range(size):
    for x in range(size):
        grain = math.sin(x*.24 + math.sin(y*.017)*1.7) + .4*math.sin(x*.83+y*.003)
        knot = math.sin(math.sqrt(((x-180)*.8)**2+((y-310)*.23)**2)*.35)
        value = .58 + grain*.075 + knot*.035 + random.uniform(-.045,.045)
        pixels.extend((value*.75, value*.49, value*.24, 1))
img = bpy.data.images.new('NightWatch_WoodGrain', width=size, height=size)
img.pixels = pixels; img.filepath_raw = ASSET+'/Textures/WoodGrain.png'; img.file_format = 'PNG'; img.save(); img.pack()
tex = wood.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = img
wood.node_tree.links.new(tex.outputs['Color'], wood.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])

def finish(o, name, material, owner, bevel=0, smooth=False):
    o.name = name; o.data.materials.append(material)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = o.modifiers.new('Rounded worn edges', 'BEVEL'); mod.width = bevel; mod.segments = 2
        mod = o.modifiers.new('Weighted face normals', 'WEIGHTED_NORMAL')
    if smooth:
        for p in o.data.polygons: p.use_smooth = True
    bpy.context.view_layer.update()
    return parent(o, owner)
def box(name, pos, dims, material, owner, bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1, location=u(pos)); o=bpy.context.object; o.dimensions=(dims[0],dims[2],dims[1])
    return finish(o,name,material,owner,bevel)
def rod(name, a, b, radius, material, owner, vertices=12):
    a,b=u(a),u(b); d=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=d.length, location=(a+b)*.5)
    o=bpy.context.object; o.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    return finish(o,name,material,owner,.0015,True)
def tube(name, points, radius, material, owner):
    curve=bpy.data.curves.new(name,'CURVE'); curve.dimensions='3D'; curve.bevel_depth=radius; curve.bevel_resolution=2
    spline=curve.splines.new('POLY'); spline.points.add(len(points)-1)
    for p,v in zip(spline.points,points): p.co=(*u(v),1)
    obj=bpy.data.objects.new(name,curve); bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj; obj.select_set(True); bpy.ops.object.convert(target='MESH'); obj=bpy.context.object
    return finish(obj,name,material,owner,0,True)
def lathe(name, profile, material, owner, segments=32):
    verts=[]; faces=[]
    for y,r in profile:
        for i in range(segments):
            a=i*math.tau/segments; verts.append(u((math.sin(a)*r,y,math.cos(a)*r)))
    for j in range(len(profile)-1):
        for i in range(segments):
            n=(i+1)%segments; faces.append((j*segments+i,j*segments+n,(j+1)*segments+n,(j+1)*segments+i))
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    o=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(o)
    for p in mesh.polygons: p.use_smooth=True
    o.data.materials.append(material); return parent(o,owner)

gate=empty('VillageGate'); frame=empty('GateFrame',owner=gate)
for x in (-1.02,1.02):
    box('GatePost', (x,.86,0),(.16,1.72,.18),wood,frame,.014)
    box('PostCap',(x,1.72,0),(.21,.055,.23),wood,frame)
    box('GroundFoot',(x,.035,0),(.23,.07,.27),wood,frame)
# Wide wings visually join the gateway to the edge of the village instead of a freestanding prop.
for side in (-1,1):
    wing=empty('FenceWingLeft' if side<0 else 'FenceWingRight',owner=frame)
    for i in range(24):
        x=side*(1.16+i*.15); h=1.42+random.uniform(-.065,.10)
        rod('BambooPale',(x,-.045,0),(x+random.uniform(-.015,.015),h,0),.035,bamboo,wing)
        for y in (.32,1.03):
            tube('RopeBinding',[(x+math.cos(a)*.044,y+math.sin(a)*.018,-.005+math.sin(a)*.044) for a in [k*math.tau/12 for k in range(13)]],.005,rope,wing)
    for row in range(18):
        box('WovenBambooStrip',(side*2.87,.14+row*.068,-.035 if row%2 else -.055),(3.58,.048,.016),bamboo,wing,.003)
    for y in (.32,1.03): rod('FenceRail',(side*1.10,y,.04),(side*4.66,y,.04),.04,bamboo,wing)
hinge=empty('GateLeafHinge',(-.91,0,0),gate)
for i in range(11):
    x=-.825+i*.165; h=1.40+random.uniform(-.02,.025)
    box('GatePlank%02d'%i,(x,.13+h*.5,0),(.15,h,.052),wood,hinge,.006)
for y in (.34,1.32):
    box('LeafCrossRail',(0,y,-.046),(1.79,.095,.055),wood,hinge)
    for x in (-.76,-.38,0,.38,.76): rod('IronNail',(x,y,-.078),(x,y,-.092),.009,iron,hinge)
diagonal=box('DiagonalBrace',(0,.83,.06),(1.77,.095,.04),wood,hinge)
diagonal.rotation_euler[1]=math.radians(-23)
for y in (.34,1.32):
    box('HingeStrap',(-.74,y,-.095),(.37,.055,.022),iron,hinge,.004)
    rod('HingePin',(-.93,y-.067,-.095),(-.93,y+.067,-.095),.024,iron,frame)
    for x in (-.84,-.69,-.60): rod('HingeRivet',(x,y,-.108),(x,y,-.119),.011,iron,hinge)
bolt=empty('BoltSlide',(.66,.90,-.135),hinge)
box('SlidingIronBolt',(.66,.90,-.135),(.49,.033,.036),iron,bolt,.006)
box('BoltGrip',(.50,.947,-.155),(.026,.094,.038),brass,bolt,.005)
for x in (.62,.81,1.015):
    owner=frame if x>1 else hinge
    box('ReceiverPlate' if x>1 else 'BoltGuidePlate',(x,.90,-.073),(.115,.16,.018),iron,owner,.004)
    box('ReceiverTop',(x,.934,-.119),(.10,.023,.09),iron,owner,.004)
    box('ReceiverBottom',(x,.866,-.119),(.10,.023,.09),iron,owner,.004)
    box('ReceiverBridge',(x,.90,-.165),(.10,.09,.018),iron,owner,.004)
    for y in (.837,.963): rod('MountScrew',(x,y,-.084),(x,y,-.099),.009,brass,owner)
# Bolt engages receiver at x=1.015 when seated, retracts 0.25 m along the leaf.
bolt.location.x += .18
bpy.context.view_layer.update()

lamp=empty('ShieldedOilLamp')
lathe('OilReservoir',[(-.02,.08),(0,.11),(.025,.12),(.065,.10),(.09,.063),(.11,.06)],brass,lamp)
rod('WickCollar',(0,.10,0),(0,.135,0),.037,iron,lamp,24)
lathe('GlassChimney',[(.12,.055),(.15,.078),(.24,.079),(.30,.057),(.35,.052)],glass,lamp)
fl=lathe('WarmFlame',[(.13,.015),(.155,.022),(.19,.012),(.22,.001)],flame,lamp,16)
hood=empty('LampHood',(0,0,0),lamp)
verts=[]; faces=[]; steps=40
for i in range(steps+1):
    a=math.radians(55+250*i/steps)
    for r,y in ((.10,.105),(.13,.33),(.138,.35),(.112,.10)):
        verts.append(u((math.sin(a)*r,y,math.cos(a)*r)))
for i in range(steps):
    for j in range(4): faces.append((i*4+j,(i+1)*4+j,(i+1)*4+(j+1)%4,i*4+(j+1)%4))
mesh=bpy.data.meshes.new('Formed sheet steel hood'); mesh.from_pydata(verts,[],faces); mesh.update()
obj=bpy.data.objects.new('OpaqueDirectionalShield',mesh); bpy.context.collection.objects.link(obj); obj.data.materials.append(iron); parent(obj,hood)
lathe('VentilatedCap',[(.345,.14),(.365,.14),(.39,.075),(.42,.065),(.43,.04)],iron,lamp)
for angle in (38,-38,180):
    a=math.radians(angle); x,z=math.sin(a)*.105,math.cos(a)*.105
    rod('GlassGuard',(x,.06,z),(x,.37,z),.006,iron,lamp)
tube('CarryHandle',[(math.cos(a)*.12,.39+math.sin(a)*.22,0) for a in [math.pi*i/24 for i in range(25)]],.007,iron,lamp)
rod('SupportPost',(0,-.9,0),(0,-.035,0),.035,wood,lamp)
box('StandFoot',(0,-.89,0),(.26,.025,.22),wood,lamp)

def export(root,name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in [root]+list(root.children_recursive): o.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=ASSET+'/'+name+'.fbx',use_selection=True,object_types={'MESH','EMPTY'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,
        use_mesh_modifiers=True,add_leaf_bones=False,path_mode='AUTO')
# Batch the fixed mesh parts by material, while keeping every real hinge/slide pivot.
for owner in [frame,hinge]+[o for o in gate.children_recursive if o.name.startswith('FenceWing')]:
    groups={}
    for obj in list(owner.children):
        if obj.type=='MESH' and obj.name!='ReceiverPlate':
            groups.setdefault(obj.data.materials[0].name,[]).append(obj)
    for material,objects in groups.items():
        if len(objects)<2: continue
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects: obj.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        # Apply edge finishing before mesh batching.
        for obj in objects:
            bpy.context.view_layer.objects.active=obj
            for mod in list(obj.modifiers): bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join()
        bpy.context.object.name=owner.name+'_'+material
        bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)

export(gate,'VillageGate'); export(lamp,'ShieldedLamp')
section=empty('FenceSection',(2.87,0,0))
wing=bpy.data.objects['FenceWingRight']
for original in list(wing.children):
    if original.type!='MESH': continue
    obj=original.copy();obj.data=original.data.copy();bpy.context.collection.objects.link(obj)
    obj.matrix_world=original.matrix_world.copy();parent(obj,section)
section.location=u((0,0,0));bpy.context.view_layer.update()
export(section,'FenceSection')
section.location=u((-6,0,0))
lamp.location.x=3.8
bpy.context.scene.unit_settings.system='METRIC'
bpy.ops.wm.save_as_mainfile(filepath=SOURCE+'/NightWatchProps.blend')
print('NIGHT_WATCH_MODELS_EXPORTED',ASSET)
