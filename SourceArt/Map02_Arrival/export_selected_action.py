"""Load in Blender's Text Editor. Exports one edited action without rebuilding the source."""
import bpy
from pathlib import Path
CHARACTER = "Nam"
ACTION_NAME = "Nam_Jetty_StepUp"
ROOT = Path(__file__).resolve().parents[2]
OUTPUT_FBX = ROOT / "Assets/_Project/Art/Characters/Animations/Map02Arrival" / (ACTION_NAME + ".fbx")
rig=bpy.data.objects[CHARACTER];action=bpy.data.actions[ACTION_NAME]
original=(rig.location.copy(),rig.rotation_euler.copy(),rig.scale.copy(),rig.animation_data.action)
try:
    # The .blend displays the actor in the map. Export the original rig space instead.
    rig.location=(0,0,0);rig.rotation_euler=(0,0,0);rig.scale*=1/.89
    rig.animation_data.action=action
    scene=bpy.context.scene;scene.render.fps=30;scene.render.fps_base=1
    scene.frame_start=max(0,round(action.frame_range[0]));scene.frame_end=round(action.frame_range[1])
    # Frame -1 is a neutral bind reference, not part of the motion clip.
    if action.frame_range[0]<0:scene.frame_set(-1)
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT_FBX),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,
        bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
        bake_anim_step=1,axis_forward='-Z',axis_up='Y')
finally:
    rig.location,rig.rotation_euler,rig.scale,rig.animation_data.action=original
print('Exported',ACTION_NAME,OUTPUT_FBX)
