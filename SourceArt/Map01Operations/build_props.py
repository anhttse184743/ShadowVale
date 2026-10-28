"""Rebuild editable Map 1 props. Blender 5.x, meters, unchanged character rigs.
blender --background --python SourceArt/Map01Operations/build_props.py
"""
from pathlib import Path
import bpy, math
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/_Project/Art/Environment/Map01Operations'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def mat(name,color,metal=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.78;p.inputs['Metallic'].default_value=metal
    return m
wood=mat('SV_Wood',(.26,.16,.075));edge=mat('SV_WoodEdge',(.38,.25,.12));dark=mat('SV_Iron',(.09,.105,.085),.65)
brass=mat('SV_Brass',(.51,.36,.13),.65);cloth=mat('SV_Cloth',(.19,.25,.13));strap=mat('SV_Strap',(.33,.32,.21));red=mat('SV_RedCloth',(.48,.09,.055))
def empty(name,parent=None,loc=(0,0,0)):
    ob=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(ob);ob.parent=parent;ob.location=loc;return ob
def box(name,loc,size,material,parent,bevel=.012):
    bpy.ops.mesh.primitive_cube_add(size=1,location=(0,0,0));ob=bpy.context.object;ob.name=name;ob.parent=parent;ob.location=loc;ob.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);ob.data.materials.append(material)
    if bevel:
        b=ob.modifiers.new('Small worn edges','BEVEL');b.width=bevel;b.segments=2
        ob.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return ob
def cylinder(name,loc,radius,depth,material,parent,axis='Z'):
    bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=radius,depth=depth);ob=bpy.context.object;ob.name=name;ob.parent=parent;ob.location=loc
    if axis=='Y':ob.rotation_euler.x=math.pi/2
    if axis=='X':ob.rotation_euler.y=math.pi/2
    ob.data.materials.append(material);return ob
warehouse=empty('Warehouse')
for side in [-1,1]:
    for j in range(12):box('WallSide', (side*1.48,-.98+j*.18,1.12),(.075,.16,2.24),wood,warehouse)
for j in range(17):box('WallBack',(-1.44+j*.18,-1.08,1.12),(.16,.075,2.24),wood,warehouse)
for x in [-1.48,1.48]:box('CornerPost',(x,-1.06,1.16),(.13,.13,2.32),edge,warehouse)
for x in [-.57,.57]:box('DoorFrame',(x,1.12,1.12),(.13,.14,2.24),edge,warehouse)
box('DoorHeader',(0,1.12,2.22),(1.28,.16,.14),edge,warehouse)
for x in [-1.03,1.03]:box('FrontPanel',(x,1.12,1.10),(.76,.07,2.2),wood,warehouse)
hinge=empty('DoorHinge',warehouse,(-.5,1.14,0))
for j in range(6):box('DoorLeaf',(.08+j*.166,0,1.06),(.15,.07,2.08),wood,hinge)
for z in [.33,1.74]:box('DoorBrace',(.5,-.05,z),(1.02,.08,.11),edge,hinge)
lock=box('DoorLock',(.88,.075,1.08),(.14,.055,.20),brass,hinge)
cylinder('DoorKeyhole',(.88,.108,1.10),.016,.008,dark,hinge,'Y');box('DoorKeySlot',(.88,.113,1.07),(.015,.008,.04),dark,hinge,.002)
box('Roof',(0,0,2.32),(3.18,2.55,.11),edge,warehouse)
chest=empty('SupplyChest');chest.location=(0,0,0)
for x in [-.58,.58]:box('ChestSide',(x,0,.31),(.065,.76,.62),wood,chest)
for y in [-.36,.36]:
    for z in [.10,.29,.48]:box('ChestPlank',(0,y,z),(1.14,.065,.17),wood,chest)
box('ChestBase',(0,0,.035),(1.16,.72,.07),dark,chest)
lid=empty('LidPivot',chest,(0,-.37,.61))
for j in range(6):box('LidSlat',(-.50+j*.2,.37,.035),(.18,.79,.08),edge,lid)
for x in [-.43,.43]:
    box('LidBand',(x,.37,.085),(.045,.80,.022),dark,lid,.004)
    box('ChestBand',(x,.398,.30),(.045,.025,.59),dark,chest,.004)
    for z in [.08,.53]:cylinder('Rivet',(x,.42,z),.016,.012,brass,chest,'Y')
lock=box('ChestLock',(0,.43,.48),(.31,.075,.15),dark,chest)
for x in [-.1,0,.1]:cylinder('CombinationDial',(x,.48,.48),.044,.045,brass,lock,'Y')
# Dials belong to lock but retain their intended world positions.
for ob in list(lock.children):ob.matrix_world=chest.matrix_world @ ob.matrix_basis
box('RedMarker',(.34,.41,.4),(.07,.015,.22),red,chest,.003)
bag=empty('SupplyBag')
box('CanvasBag',(0,0,.14),(.38,.18,.28),cloth,bag,.045)
box('FrontPocket',(0,.11,.13),(.24,.055,.17),cloth,bag,.018)
for x in [-.14,.14]:box('CanvasStrap',(x,.117,.15),(.035,.018,.28),strap,bag,.004)
bpy.ops.mesh.primitive_torus_add(major_segments=20,minor_segments=6,location=(0,0,.32),major_radius=.11,minor_radius=.012,rotation=(math.pi/2,0,0))
handle=bpy.context.object;handle.name='BagHandle';handle.parent=bag;handle.data.materials.append(strap)
box('BagRedTag',(.13,.135,.17),(.055,.015,.10),red,bag,.003)
key=empty('WarehouseKey')
bpy.ops.mesh.primitive_torus_add(major_radius=.035,minor_radius=.007,major_segments=16,minor_segments=6)
ring=bpy.context.object;ring.name='KeyRing';ring.parent=key;ring.data.materials.append(brass)
box('KeyShaft',(0,.066,0),(.012,.095,.012),brass,key,.002)
for y in [.08,.105]:box('KeyTooth',(.012,y,0),(.025,.009,.012),brass,key,.002)
roots=[warehouse,chest,bag,key]
for root in roots:
    bpy.ops.object.select_all(action='DESELECT')
    for ob in [root,*root.children_recursive]:ob.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=str(OUT/(root.name+'.fbx')),use_selection=True,object_types={'EMPTY','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)
# A spaced reference layout remains easy to edit in the .blend.
warehouse.location.x=-4;chest.location.x=0;bag.location.x=2;key.location.x=3
bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.render.fps=30
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).parent/'Map01Operations.blend'))
print('Exported Map01 operation props to',OUT)
