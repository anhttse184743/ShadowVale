from pathlib import Path
import bpy
HERE=Path(__file__).resolve().parent
source=HERE/'CotsSuppliesPreview/BeddingRevision2/FieldCots_ArmyBedding_Proposal.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
camo=bpy.data.materials['Military camouflage folded cover']
for root in [o for o in bpy.data.objects if o.type=='EMPTY' and o.name.startswith('FieldCot')]:
 for ob in list(root.children):
  if ob.name.startswith('Camouflage rear folded edge') or ob.name.startswith('Camouflage exposed-core border'):bpy.data.objects.remove(ob,do_unlink=True)
 bpy.ops.mesh.primitive_cube_add(size=1);ob=bpy.context.object;ob.name='Camouflage closed foot-facing long panel';ob.parent=root;ob.location=(0,.549,.552);ob.dimensions=(.60,.020,.135)
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);ob.data.materials.append(camo)
 bevel=ob.modifiers.new('Folded fabric corners','BEVEL');bevel.width=.005;bevel.segments=3;ob.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
 root['bedding_open_face']='Both sides of bed (+X and -X); foot face closed'
bpy.ops.wm.save_as_mainfile(filepath=str(source))
export=HERE/'export_cots_supplies.py'
exec(compile(export.read_text(encoding='utf-8'),str(export),'exec'))
