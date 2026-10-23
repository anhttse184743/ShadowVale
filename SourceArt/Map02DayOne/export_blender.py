import bpy,json,sys,hashlib,math
from pathlib import Path
from mathutils import Matrix
root=Path(__file__).resolve().parents[2];out=Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/_Project/Art/Characters/Player/Player.fbx'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');rig.name='Nam_OriginalSkeleton'
scene=bpy.context.scene;scene.render.fps=30
# Unity left-handed Y-up -> Blender right-handed Z-up. Do not edit original bones/weights.
c=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
ci=c.inverted();inverse=rig.matrix_world.inverted();records=[]
for path in sorted(out.glob('*.json')):
 data=json.loads(path.read_text(encoding='utf-8-sig'))
 if 'frames' not in data:continue
 rig.animation_data_create();action=bpy.data.actions.new(data['action']);rig.animation_data.action=action
 scene.frame_start=1;scene.frame_end=len(data['frames'])
 for frame in data['frames']:
  scene.frame_set(round(frame['time']*30)+1)
  by={b['name']:b['matrix'] for b in frame['bones']}
  # Matrices come from the calibrated Unity Avatar, with body/fingers already baked.
  for bone in sorted(rig.pose.bones,key=lambda b:len(b.parent_recursive)):
   if bone.name not in by:continue
   flat=by[bone.name];m=Matrix([[flat[col*4+row] for col in range(4)] for row in range(4)])
   bone.matrix=inverse@c@m@ci
   bone.rotation_mode='QUATERNION';bone.keyframe_insert(data_path='location');bone.keyframe_insert(data_path='rotation_quaternion');bone.keyframe_insert(data_path='scale')
  bpy.context.view_layer.update()
 rig['source_action_json']=path.name;rig['source_sha256']=hashlib.sha256(path.read_bytes()).hexdigest()
 records.append(dict(action=data['action'],frames=scene.frame_end,sha256=rig['source_sha256']))
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
 export=out/'FBX';export.mkdir(exist_ok=True)
 bpy.ops.export_scene.fbx(filepath=str(export/(data['action']+'.fbx')),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'DayOne_Performances.blend'))
(out/'export_receipt.json').write_text(json.dumps(records,ensure_ascii=False,indent=2))
print('DAY_ONE_BLENDER_EXPORT_PASS',len(records))
