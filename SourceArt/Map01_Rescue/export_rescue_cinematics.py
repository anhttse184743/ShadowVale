"""Export the edited rescue actions without regenerating the Blender source.
Blender: open the saved .blend, then run this script from Text Editor.
CLI: blender -b Map01_Rescue_Cinematics.blend --python export_rescue_cinematics.py
Optional: -- --clips Nam_Bush_StruggleLoop Enemy_Bush_StruggleLoop
"""
import bpy,sys,argparse
from pathlib import Path
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
OUT=ROOT/'Assets/_Project/Art/Characters/Animations/RescueStory'
CLIPS={'Hung_Captive_Recoil':3,'Hung_Bound_Execution':3,'Enemy_Interrogate_Strike':3}
for who in ['Nam','Enemy']:
 for take,duration in [('GrabPull',2.6),('StruggleLoop',2),('Finish',2.4),('Escape',1.4)]:CLIPS[who+'_Bush_'+take]=duration
parser=argparse.ArgumentParser();parser.add_argument('--clips',nargs='+',choices=list(CLIPS));parser.add_argument('--output',type=Path,default=OUT)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
OUT=args.output
if not bpy.data.filepath:bpy.ops.wm.open_mainfile(filepath=str(HERE/'Map01_Rescue_Cinematics.blend'))
OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene;scene.render.fps=30;scene.render.fps_base=1
for name in args.clips or list(CLIPS):
 who=name.split('_')[0];rig=bpy.data.objects.get(who);action=bpy.data.actions.get(name)
 if rig is None or action is None:raise RuntimeError('Missing rig/action '+name)
 saved_action=rig.animation_data.action;saved_name=rig.name;saved_location=rig.location.copy()
 old_start,old_end=scene.frame_start,scene.frame_end
 constraints=[(c,c.mute) for pb in rig.pose.bones for c in pb.constraints]
 baked=None
 try:
  bpy.ops.object.select_all(action='DESELECT');rig.hide_set(False);rig.select_set(True);bpy.context.view_layer.objects.active=rig
  rig.animation_data.action=action;scene.frame_start=1;scene.frame_end=round(CLIPS[name]*30)+1
  # Bake manual FK/IK while the preview anchors and hand/foot guides still agree.
  # Then export those local poses from the stable Humanoid reference origin.
  bpy.ops.nla.bake(frame_start=1,frame_end=scene.frame_end,step=1,only_selected=False,visual_keying=True,clear_constraints=False,
                   clear_parents=False,use_current_action=False,bake_types={'POSE'})
  baked=rig.animation_data.action;baked.name='EXPORT_ONLY_'+name
  for c,_ in constraints:c.mute=True
  rig.name='Armature';rig.location=(0,0,0)
  bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,
    bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,
    bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)
  print('Exported',name,'at 30 fps; original source action retained.')
 finally:
  rig.name=saved_name;rig.location=saved_location;rig.animation_data.action=saved_action
  for c,mute in constraints:c.mute=mute
  scene.frame_start,scene.frame_end=old_start,old_end
  if baked is not None:bpy.data.actions.remove(baked)
# Do not save over the user's .blend: export must preserve their editable source.
