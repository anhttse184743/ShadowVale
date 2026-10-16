"""Run in Blender Text Editor after editing the existing .blend. Exports ONE action."""
import bpy
CHARACTER = "Hung"
ACTION_NAME = "Hung_Row_Loop"
OUTPUT_FBX = r"G:/game/ShadowVale/Assets/_Project/Art/Characters/Animations/Extraction/Hung_Row_Loop.fbx"
rig = next(o for o in bpy.data.objects if o.type == "ARMATURE" and o.name == CHARACTER)
action = bpy.data.actions[ACTION_NAME]
rig.animation_data_create(); rig.animation_data.action = action
scene = bpy.context.scene; scene.render.fps = 30; scene.render.fps_base = 1
scene.frame_start = round(action.frame_range[0]); scene.frame_end = round(action.frame_range[1])
bpy.ops.object.select_all(action="DESELECT"); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=OUTPUT_FBX, use_selection=True, object_types={"ARMATURE"},
    add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0, bake_anim_step=1,
    axis_forward="-Z", axis_up="Y")
print("Exported", ACTION_NAME, "to", OUTPUT_FBX)
