"""Map 2 supply props from the team's FBX/GLB sources (ammo can, medkit bag, grenade crate, grenade, straw cart).

Run:  blender --background --python prepare_supply_props.py -- <source dir> <texture dir>
then: python pack_supply_textures.py <texture dir>

The sources come in at about a metre whatever they are, ~50k triangles and four 4096 px maps each. This scales
each to its real size, reduces it to a prop budget, and writes into Assets/_Project/Art/Props/Map02/Supplies:
<Name>.fbx (Y up, -Z forward, origin on the base centre) and raw maps to <texture dir> for the packing step.
"""
import os, sys
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "_Project", "Art", "Props", "Map02", "Supplies"))
src_dir, tex_dir = sys.argv[sys.argv.index("--") + 1:][:2]
# source file -> asset name, longest side in metres, triangle budget
PROPS = {
    "thùng lựu đạn.fbx": ("GrenadeCrate", .50, 6000),   # wooden crate of grenades, carried in both arms
    "thùng đạn.fbx": ("AmmoCan", .31, 2500),             # steel ammunition can
    "túi cứu thương.fbx": ("MedkitBag", .33, 3000),      # canvas medic's bag with the red cross
    "trái lựu đạn.fbx": ("Grenade", .11, 1200),          # a single hand grenade, lever and ring
    "xem rơm.glb": ("StrawCart", 2.95, 12000),          # hand cart of straw by the store; the supplies wait on it
}
os.makedirs(OUT, exist_ok=True); os.makedirs(tex_dir, exist_ok=True)
for file, (name, size, budget) in PROPS.items():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = os.path.join(src_dir, file)
    bpy.ops.import_scene.gltf(filepath=path) if file.endswith(".glb") else bpy.ops.import_scene.fbx(filepath=path)
    o = next(x for x in bpy.context.scene.objects if x.type == 'MESH')
    if o.parent: mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw   # glTF wraps the mesh in a root node
    for x in bpy.context.scene.objects: x.select_set(False)
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    dims = o.dimensions; k = size / max(dims)
    o.scale = (k, k, k); bpy.ops.object.transform_apply(scale=True)
    # Origin on the base centre, so a prop stands where it is put.
    lo = [min(v.co[i] for v in o.data.vertices) for i in range(3)]; hi = [max(v.co[i] for v in o.data.vertices) for i in range(3)]
    shift = [-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]]
    for v in o.data.vertices: v.co.x += shift[0]; v.co.y += shift[1]; v.co.z += shift[2]
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if tris > budget:
        mod = o.modifiers.new("Budget", 'DECIMATE'); mod.ratio = budget / tris; mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.shade_smooth()
    o.name = o.data.name = name
    after = sum(len(p.vertices) - 2 for p in o.data.polygons)
    # The packed maps, untouched, for pack_supply_textures.py (base, normal, roughness, metallic).
    for img in bpy.data.images:
        if not img.packed_file: continue
        kind = "Normal" if "normal" in img.name else "Roughness" if "roughness" in img.name else "Metallic" if "metallic" in img.name else "Albedo"
        with open(os.path.join(tex_dir, f"{name}_{kind}.png"), "wb") as f: f.write(img.packed_file.data)
    o.data.materials.clear()
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             mesh_smooth_type='FACE', bake_space_transform=False, path_mode='STRIP')
    print(f"PROP {name}: {tuple(round(d, 3) for d in o.dimensions)} m, {tris} -> {after} triangles")
