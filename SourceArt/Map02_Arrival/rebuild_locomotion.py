"""Repair only the walking/stair actions; preserve existing boat stands and paddle stow."""
import bpy,ast,json,math
from pathlib import Path
from mathutils import Matrix,Quaternion,Vector
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[1]
OUT=ROOT/'Assets/_Project/Art/Characters/Animations/Map02Arrival'
bpy.ops.wm.open_mainfile(filepath=str(HERE/'Map02_Arrival.blend'))
scene=bpy.context.scene;scene.render.fps=30
for col in bpy.data.collections:col.hide_viewport=False
for ob in bpy.data.objects:
 if ob.name.startswith('Source_'):ob.hide_viewport=False;ob.hide_set(False);ob.hide_render=True
tree=ast.parse((ROOT/'SourceArt/Map01_Extraction/author_extraction.py').read_text())
for node in tree.body:
 if isinstance(node,ast.FunctionDef) and node.name in ['target_pose','solve_leg']:
  exec(compile(ast.Module(body=[node],type_ignores=[]),'retarget-helper','exec'))
report=[];source=bpy.data.objects['Source_Walk']
for who in ['Nam','Hung']:
 rig=bpy.data.objects[who];saved=(rig.location.copy(),rig.rotation_euler.copy(),rig.scale.copy())
 rig.location=(0,0,0);rig.rotation_euler=(0,0,0);rig.scale*=1/.89
 for pb in rig.pose.bones:
  for c in pb.constraints:c.mute=True
 rest={b.name:tuple(v for row in b.matrix_local for v in row) for b in rig.data.bones}
 for suffix,seconds in [('Walk_Ashore',1.2),('Jetty_StepUp',3.4)]:
  name=who+'_'+suffix;old=bpy.data.actions.get(name)
  if old:old.name=name+'_Static_Backup'
  action=bpy.data.actions.new(name);action.use_fake_user=True;frames=round(seconds*30)
  for f in range(frames+1):
   rig.animation_data.action=None
   start,end=source.animation_data.action.frame_range
   sourceFrame=(f/frames)*(end-start) if suffix=='Walk_Ashore' else f*1.05
   target_pose(rig,source,sourceFrame)
   if suffix=='Jetty_StepUp':
    for side,offset in [('Left',0),('Right',math.pi)]:
     q=rig.matrix_world@rig.pose.bones[side+'Foot'].head
     q.z+=.12*max(0,math.sin(f/30*math.pi*2/.68+offset));solve_leg(rig,side,q)
   matrices={p.name:p.matrix.copy() for p in rig.pose.bones}
   rig.animation_data.action=None;scene.frame_set(f);rig.animation_data.action=action
   for pb in rig.pose.bones:
    parent=matrices[pb.parent.name] if pb.parent else Matrix.Identity(4)
    restparent=pb.parent.bone.matrix_local if pb.parent else Matrix.Identity(4)
    pb.matrix_basis=(parent@restparent.inverted()@pb.bone.matrix_local).inverted()@matrices[pb.name]
    pb.rotation_mode='QUATERNION'
    for prop in ['location','rotation_quaternion','scale']:pb.keyframe_insert(prop,frame=f,group=pb.name)
  samples=[]
  for frame in [0,frames//4,frames//2,3*frames//4]:
   scene.frame_set(frame);samples.append(rig.pose.bones['LeftUpLeg'].rotation_quaternion.copy())
  motion=max(samples[0].rotation_difference(q).angle for q in samples[1:])
  assert motion>.1,(name,motion)
  assert rest=={b.name:tuple(v for row in b.matrix_local for v in row) for b in rig.data.bones}
  scene.frame_start=0;scene.frame_end=frames;scene.frame_set(0)
  bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
  bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,
   bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,bake_anim_step=1,axis_forward='-Z',axis_up='Y')
  report.append(dict(clip=name,bones=28,fps=30,left_leg_variation_radians=motion,rest_preserved=True))
 rig.location,rig.rotation_euler,rig.scale=saved
for col in bpy.data.collections:
 if col.name.startswith('REFERENCE') and 'Map 2' not in col.name:col.hide_viewport=True
scene.frame_start=0;scene.frame_end=36;scene.frame_set(12)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Map02_Arrival.blend'))
(HERE/'locomotion-validation.json').write_text(json.dumps(report,indent=2))
print('LOCOMOTION_REBUILT',report)
