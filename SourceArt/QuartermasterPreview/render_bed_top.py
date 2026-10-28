from pathlib import Path
import bpy
from mathutils import Vector
OUT=Path(__file__).resolve().parent/'CotsSuppliesPreview/BeddingRevision2'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'FieldCots_ArmyBedding_Proposal.blend'))
for root in bpy.data.objects:
 if root.type=='EMPTY' and (root.name.startswith('FieldCot') or root.name.startswith('Walnut wall')):
  show=root.name=='FieldCot row 3'
  for o in [root,*root.children_recursive]:o.hide_render=not show
S=bpy.context.scene;cam=S.camera;cam.location=(0,0,4);cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=3.35
S.render.filepath=str(OUT/'02_top_view_layout.png');bpy.ops.render.render(write_still=True)
