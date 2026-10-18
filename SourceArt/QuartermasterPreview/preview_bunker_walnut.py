"""Recolor the existing command bunker proposal only; Unity remains untouched."""
from pathlib import Path
import bpy
from mathutils import Vector
HERE=Path(__file__).resolve().parent
OUT=HERE/'Revision2'; OUT.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(HERE.parent/'CommandBunkerPreview/CommandBunker_Proposal.blend'))
palette={'Weathered teak':(.075,.029,.012),'Worn plank edges':(.105,.045,.019),'Old dark timber':(.040,.014,.006),'Bunker wall planks':(.060,.023,.010),'Worn floorboards':(.080,.032,.014)}
changed=[]
for m in bpy.data.materials:
 for prefix,color in palette.items():
  if not m.name.startswith(prefix):continue
  m.diffuse_color=(*color,1)
  if m.use_nodes:
   m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*color,1)
   for n in m.node_tree.nodes:
    if n.type=='VALTORGB':
     n.color_ramp.elements[0].color=(*(c*.56 for c in color),1)
     n.color_ramp.elements[-1].color=(*(c*1.35 for c in color),1)
  m['preview_finish']='Walnut brown - pending approval'; changed.append(m.name)
S=bpy.context.scene; S.cycles.samples=24; S.render.resolution_x=1400; S.render.resolution_y=1000
cam=S.camera;cam.location=(4.9,-6.8,4.7);cam.rotation_euler=(Vector((0,.1,1))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=48
S.render.filepath=str(OUT/'04_command_bunker_walnut.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CommandBunker_Walnut_Proposal.blend'))
bpy.ops.render.render(write_still=True)
print('Preview materials recolored:',changed)
