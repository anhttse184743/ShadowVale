"""Export the reviewed M41_Tank.blend into Unity (run only after the .blend has been approved):
  blender --background M41_Tank.blend --python export_m41_tank.py
Writes into Assets/_Project/Art/Props/Map02: M41_Tank.fbx, M41_Tank_Albedo.png, M41_Tank_Normal.png.
Then run ShadowVale.Map01.EditorTools.Map02ModelSetup.Run in Unity to rebuild the prefab and material.
"""
import os

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Assets", "_Project", "Art", "Props", "Map02")

for k in ("Albedo", "Normal"):
    img = bpy.data.images["M41_Tank_" + k]
    with open(os.path.join(OUT, f"M41_Tank_{k}.png"), "wb") as f:
        f.write(img.packed_file.data)  # the packed bytes are the baked PNG itself, untouched by colour management
for o in bpy.context.scene.objects: o.select_set(o.type in ('MESH', 'EMPTY'))
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "M41_Tank.fbx"), use_selection=True, object_types={'MESH', 'EMPTY'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         mesh_smooth_type='FACE', add_leaf_bones=False, bake_space_transform=False, path_mode='STRIP')
print("exported", os.path.abspath(OUT))
