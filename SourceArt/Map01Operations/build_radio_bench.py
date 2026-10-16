"""A small timber workbench sized to lift the original radio to a usable height."""
from pathlib import Path
import bpy

here=Path(__file__).resolve().parent
root=here.parents[1]
out=root/'Assets/_Project/Art/Environment/Map01Operations/RadioWorkbench.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True)
wood=bpy.data.materials.new('SV_Wood');wood.diffuse_color=(.26,.16,.075,1)
edge=bpy.data.materials.new('SV_Edge');edge.diffuse_color=(.38,.25,.12,1)
bench=bpy.data.objects.new('RadioWorkbench',None);bpy.context.collection.objects.link(bench)
def beam(name,at,size,material):
    bpy.ops.mesh.primitive_cube_add(size=1,location=at)
    ob=bpy.context.object;ob.name=name;ob.dimensions=size;ob.parent=bench
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bevel=ob.modifiers.new('Worn edges','BEVEL');bevel.width=.006;bevel.segments=1
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=bevel.name)
    ob.data.materials.append(material)
for x in [-.45,.45]:
    for y in [-.23,.23]:beam('Bench leg',(x,y,.35),(.09,.09,.70),wood)
    beam('Side rail',(x,0,.24),(.07,.53,.08),edge)
for j in range(5):beam('Top plank',(-.44+j*.22,0,.71),(.21,.65,.08),wood)
beam('Long brace',(0,.23,.27),(.94,.06,.09),edge)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,object_types={'EMPTY','MESH'},bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)
bpy.context.scene.unit_settings.system='METRIC'
bpy.ops.wm.save_as_mainfile(filepath=str(here/'RadioWorkbench.blend'))
print('Radio workbench exported: 1.10 x 0.65 x 0.75 m')
