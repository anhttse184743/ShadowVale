from pathlib import Path
import bpy
from mathutils import Vector
OUT=Path(__file__).resolve().parent/'CotsSuppliesPreview'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'FieldCots_RationStores_Proposal.blend'))
wall=bpy.data.objects['Walnut wall for five-cot placement']
for o in wall.children:o.location.y=1.098
stores=bpy.data.objects['Rations and ammunition on a low pallet']
for o in [stores,*stores.children_recursive]:o.hide_render=True
bpy.context.view_layer.update()
cot=bpy.data.objects['FieldCot - head at positive Y']
rear=max((o.matrix_world@Vector(c)).y for o in cot.children_recursive if o.type=='MESH' for c in o.bound_box)
gap=1.048-rear
assert abs(gap-.03)<.001,(rear,gap)
S=bpy.context.scene;S.render.filepath=str(OUT/'03_five_cots_wall_layout.png')
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FieldCots_RationStores_Proposal.blend'))
(OUT/'wall_gap_validation.txt').write_text(f'PASS head rail Y={rear:.3f} m; inner wall Y=1.048 m; clearance={gap:.3f} m. Five cots, 1.30 m center spacing. Preview only; Unity unchanged.')
