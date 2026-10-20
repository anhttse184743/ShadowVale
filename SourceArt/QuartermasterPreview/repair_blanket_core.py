"""Expose the green inner fold on the footward long face as well as its side."""
from pathlib import Path
import bpy
from mathutils import Vector
HERE=Path(__file__).resolve().parent
OUT=HERE/'CotsSuppliesPreview/BeddingRevision2'
source=OUT/'FieldCots_ArmyBedding_Proposal.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
inner=bpy.data.materials['Pressed olive blanket interior']
inner.diffuse_color=(.085,.16,.035,1)
inner.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.085,.16,.035,1)
camo=bpy.data.materials['Military camouflage folded cover']
cots=[o for o in bpy.data.objects if o.type=='EMPTY' and o.name.startswith('FieldCot')]
for root in cots:
 for ob in list(root.children):
  if ob.name.startswith('Camouflage vertical side fold') and ob.location.y<.70:bpy.data.objects.remove(ob,do_unlink=True)
 for x in [-.284,.284]:
  bpy.ops.mesh.primitive_cube_add(size=1);ob=bpy.context.object;ob.name='Camouflage exposed-core border';ob.parent=root;ob.location=(x,.549,.552);ob.dimensions=(.028,.020,.135)
  bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);ob.data.materials.append(camo)
  bevel=ob.modifiers.new('Folded edge bevel','BEVEL');bevel.width=.005;bevel.segments=3;ob.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
bpy.ops.wm.save_as_mainfile(filepath=str(source))
hero=bpy.data.objects['FieldCot row 3']
for root in cots:
 for ob in [root,*root.children_recursive]:ob.hide_render=root!=hero
wall=bpy.data.objects['Walnut wall for five-cot placement']
for ob in [wall,*wall.children_recursive]:ob.hide_render=True
S=bpy.context.scene;cam=S.camera;cam.location=(1.3,-1.4,1.6);cam.rotation_euler=(Vector((0,.69,.53))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.25
S.cycles.samples=20;S.render.filepath=str(OUT/'04_restored_inner_fold.png');bpy.ops.render.render(write_still=True)
