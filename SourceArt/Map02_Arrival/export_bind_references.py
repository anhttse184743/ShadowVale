"""Export a genuine unanimated mesh bind reference for each original 28-bone rig."""
import bpy
from mathutils import Matrix
from pathlib import Path
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[1]
bpy.ops.wm.open_mainfile(filepath=str(HERE/'Map02_Arrival.blend'))
for c in bpy.data.collections:c.hide_viewport=False
for ob in bpy.data.objects:ob.hide_viewport=False;ob.hide_set(False)
for who in ['Nam','Hung']:
 rig=bpy.data.objects[who];rig.animation_data.action=None
 rig.location=(0,0,0);rig.rotation_euler=(0,0,0);rig.scale*=1/.89
 for p in rig.pose.bones:
  for c in p.constraints:c.mute=True
  p.matrix_basis=Matrix.Identity(4)
 rig.data.pose_position='REST';bpy.context.view_layer.update()
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
 meshes=[o for o in bpy.data.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers)]
 assert meshes,(who,'missing original character mesh')
 for o in meshes:o.select_set(True)
 bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(ROOT/'Assets/_Project/Art/Characters/Animations/Map02Arrival'/(who+'_Arrival_Bind.fbx')),
  use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
 print('NEUTRAL_MESH_BIND_EXPORTED',who,len(rig.data.bones),len(meshes),flush=True)
