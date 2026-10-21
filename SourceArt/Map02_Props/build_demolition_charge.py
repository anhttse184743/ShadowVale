"""Map 2 demolition charge (khối bộc phá): a block wrapped in blue plastic and lashed with green straps.

Deterministic. Run:  blender --background --python build_demolition_charge.py
Writes, next to this script: DemolitionCharge.blend, DemolitionCharge_preview.png
and the game mesh Assets/_Project/Art/Props/Map02/DemolitionCharge.fbx (metres, Y-up for Unity).
Reference: field photo of a militia charge, a near-cube block about 20 cm a side.
"""
import math, os, random, sys
import bpy, bmesh
from mathutils import Vector, noise
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
FBX = os.path.join(ROOT, "Assets", "_Project", "Art", "Props", "Map02", "DemolitionCharge.fbx")
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if "--fbx" in args: FBX = args[args.index("--fbx") + 1]   # e.g. a scratch path while reviewing
W, D, H = .20, .20, .21          # width (x), depth (y), height (z): a near-cube block, as in the photo
STRAP_W, STRAP_T = .010, .0026   # strap width and thickness: flat plastic tape
random.seed(7)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

def material(name, rgb, rough, sheen=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = rough
    if "Coat Weight" in bsdf.inputs: bsdf.inputs["Coat Weight"].default_value = sheen
    return m

plastic = material("Charge_BluePlastic", (.035, .07, .28), .42, .15)
strap = material("Charge_GreenStrap", (.10, .20, .06), .7)

def link(obj):
    scene.collection.objects.link(obj); return obj

# --- Wrapped block: a box with softly rounded edges (bevel first, then even density), fine plastic creases.
mesh = bpy.data.meshes.new("Charge_Block"); block = link(bpy.data.objects.new("Charge_Block", mesh))
bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1); bmesh.ops.scale(bm, vec=(W, D, H), verts=bm.verts); bm.to_mesh(mesh); bm.free()
bevel = block.modifiers.new("Round", 'BEVEL'); bevel.width = .017; bevel.segments = 5; bevel.profile = .5
dense = block.modifiers.new("Density", 'SUBSURF'); dense.subdivision_type = 'SIMPLE'; dense.levels = dense.render_levels = 2
bpy.context.view_layer.objects.active = block
for m in list(block.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
bm = bmesh.new(); bm.from_mesh(mesh); bm.normal_update()
for v in bm.verts:
    # Plastic wrap: small creases along the surface normal, never pushed out at the corners.
    ridge = (1 - abs(noise.noise(v.co * 26 + Vector((1, 7, 3))))) ** 5 * .0026   # crumpled lines in the wrap
    crease = noise.noise(v.co * 70) * .0006 + noise.noise(v.co * 12) * .0012
    v.co += v.normal * (ridge + crease)
bm.to_mesh(mesh); bm.free(); mesh.materials.append(plastic)
for p in mesh.polygons: p.use_smooth = True

# --- Straps: closed bands projected onto the plastic so they sit tight, as a tied-on tape does.
tree = BVHTree.FromObject(block, bpy.context.evaluated_depsgraph_get())
def band(name, plane, offset, wobble=0.0, steps=96):
    """plane 'yz' at x=offset (over the top and under), 'xz' at y=offset, 'xy' horizontal at z=offset."""
    bm = bmesh.new(); verts = []
    for i in range(steps):
        a = 2 * math.pi * i / steps; c, s_ = math.cos(a), math.sin(a)
        w = math.sin(a * 3) * wobble
        if plane == 'yz': d = Vector((0, c, s_)); origin = Vector((offset + w, 0, 0)); side = Vector((1, 0, 0))
        elif plane == 'xz': d = Vector((c, 0, s_)); origin = Vector((0, offset + w, 0)); side = Vector((0, 1, 0))
        else: d = Vector((c, s_, 0)); origin = Vector((0, 0, offset + w)); side = Vector((0, 0, 1))
        hit, normal, _, _ = tree.ray_cast(origin + d * 1.0, -d)
        if hit is None: hit, normal = origin + d * .1, d
        for dw, dt in ((-1, .15), (1, .15), (1, 1), (-1, 1)):
            verts.append(bm.verts.new(hit + side * dw * STRAP_W / 2 + normal * dt * STRAP_T))
    for i in range(steps):
        a, b = i * 4, ((i + 1) % steps) * 4
        for k in range(4):
            bm.faces.new((verts[a + k], verts[a + (k + 1) % 4], verts[b + (k + 1) % 4], verts[b + k]))
    m = bpy.data.meshes.new(name); bm.to_mesh(m); bm.free(); m.materials.append(strap)
    return link(bpy.data.objects.new(name, m))

# As in the photo: a tight grid of hand-tied tape - four ties each way over the top, three round the sides.
straps = [band(f"Strap_X{i}", 'yz', x, w) for i, (x, w) in enumerate(((-.066, .002), (-.023, -.002), (.021, .0025), (.064, -.0015)))]
straps += [band(f"Strap_Y{i}", 'xz', y, w) for i, (y, w) in enumerate(((-.065, -.002), (-.022, .002), (.024, -.0025), (.066, .0015)))]
straps += [band(f"Strap_H{i}", 'xy', z, w) for i, (z, w) in enumerate(((-.062, .002), (-.004, -.002), (.055, .0025)))]

# --- Knots where straps meet on top, and the loop the charge is carried by.
def knot(name, at, r=.011):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=r, location=at)
    o = bpy.context.active_object; o.name = name; o.scale = (1.3, 1, .7); o.data.materials.append(strap); return o
knots = [knot("Knot_Top", (.021, .024, H / 2 + .005), .008), knot("Knot_Top2", (-.066, -.022, H / 2 + .004), .006), knot("Knot_Side", (W / 2 + .004, .024, -.004), .006)]

curve = bpy.data.curves.new("Carry_Loop", 'CURVE'); curve.dimensions = '3D'; curve.bevel_depth = .0035; curve.bevel_resolution = 2
spline = curve.splines.new('BEZIER'); spline.bezier_points.add(2)
for pt, co in zip(spline.bezier_points, ((-.035, 0, H / 2 + .004), (.0, .005, H / 2 + .035), (.04, 0, H / 2 + .004))):
    pt.co = co; pt.handle_left_type = pt.handle_right_type = 'AUTO'
loop = link(bpy.data.objects.new("Carry_Loop", curve)); curve.materials.append(strap)
bpy.context.view_layer.objects.active = loop; loop.select_set(True); bpy.ops.object.convert(target='MESH')

# --- Join into one mesh with two materials, sit it on the ground, apply transforms.
for o in scene.objects: o.select_set(o.type == 'MESH')
bpy.context.view_layer.objects.active = block
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
bpy.ops.object.join(); charge = bpy.context.active_object; charge.name = "DemolitionCharge"
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
charge.location.z = H / 2 + STRAP_T; bpy.ops.object.transform_apply(location=True)
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40)) if hasattr(bpy.ops.object, "shade_smooth_by_angle") else None
print("triangles", sum(len(p.vertices) - 2 for p in charge.data.polygons))

# --- Worn look, baked to textures for Unity: crumpled plastic facets, faded and dusty blue, frayed green tape.
TEX = os.path.dirname(FBX)
def worn(mat, base, faded, dirt_amount, bump_scale, facets):
    nt = mat.node_tree; nodes, links = nt.nodes, nt.links
    bsdf = nodes["Principled BSDF"]
    coord = nodes.new("ShaderNodeTexCoord")
    patch = nodes.new("ShaderNodeTexNoise"); patch.inputs["Scale"].default_value = 14; patch.inputs["Detail"].default_value = 6
    links.new(coord.outputs["Object"], patch.inputs["Vector"])
    fade = nodes.new("ShaderNodeMix"); fade.data_type = 'RGBA'
    fade.inputs[6].default_value = (*base, 1); fade.inputs[7].default_value = (*faded, 1)
    ramp = nodes.new("ShaderNodeValToRGB"); ramp.color_ramp.elements[0].position = .42; ramp.color_ramp.elements[1].position = .7
    links.new(patch.outputs["Fac"], ramp.inputs["Fac"]); links.new(ramp.outputs["Color"], fade.inputs[0])
    # Dust and mud, heavier towards the bottom where it sat on the ground.
    sep = nodes.new("ShaderNodeSeparateXYZ"); links.new(coord.outputs["Object"], sep.inputs["Vector"])
    low = nodes.new("ShaderNodeMapRange"); low.inputs["From Min"].default_value = -.1; low.inputs["From Max"].default_value = .06
    low.inputs["To Min"].default_value = dirt_amount; low.inputs["To Max"].default_value = 0; links.new(sep.outputs["Z"], low.inputs["Value"])
    grime = nodes.new("ShaderNodeTexNoise"); grime.inputs["Scale"].default_value = 30; links.new(coord.outputs["Object"], grime.inputs["Vector"])
    dirt_mask = nodes.new("ShaderNodeMath"); dirt_mask.operation = 'MULTIPLY'; links.new(low.outputs["Result"], dirt_mask.inputs[0]); links.new(grime.outputs["Fac"], dirt_mask.inputs[1])
    dirt = nodes.new("ShaderNodeMix"); dirt.data_type = 'RGBA'; dirt.inputs[7].default_value = (.17, .14, .10, 1)
    links.new(fade.outputs[2], dirt.inputs[6]); links.new(dirt_mask.outputs[0], dirt.inputs[0])
    # Small pale specks, as in the photo.
    speck = nodes.new("ShaderNodeTexVoronoi"); speck.inputs["Scale"].default_value = 120; links.new(coord.outputs["Object"], speck.inputs["Vector"])
    speck_mask = nodes.new("ShaderNodeMath"); speck_mask.operation = 'LESS_THAN'; speck_mask.inputs[1].default_value = .035
    links.new(speck.outputs["Distance"], speck_mask.inputs[0])
    specked = nodes.new("ShaderNodeMix"); specked.data_type = 'RGBA'; specked.inputs[7].default_value = (.55, .6, .72, 1)
    links.new(dirt.outputs[2], specked.inputs[6]); links.new(speck_mask.outputs[0], specked.inputs[0])
    links.new(specked.outputs[2], bsdf.inputs["Base Color"])
    # Crumpled facets (cell edges) plus fine crinkle.
    cells = nodes.new("ShaderNodeTexVoronoi"); cells.feature = 'DISTANCE_TO_EDGE'; cells.inputs["Scale"].default_value = facets
    links.new(coord.outputs["Object"], cells.inputs["Vector"])
    fine = nodes.new("ShaderNodeTexNoise"); fine.inputs["Scale"].default_value = 60; links.new(coord.outputs["Object"], fine.inputs["Vector"])
    bump1 = nodes.new("ShaderNodeBump"); bump1.inputs["Strength"].default_value = bump_scale; bump1.inputs["Distance"].default_value = .002
    links.new(cells.outputs["Distance"], bump1.inputs["Height"])
    bump2 = nodes.new("ShaderNodeBump"); bump2.inputs["Strength"].default_value = bump_scale * .5; bump2.inputs["Distance"].default_value = .001
    links.new(fine.outputs["Fac"], bump2.inputs["Height"]); links.new(bump1.outputs["Normal"], bump2.inputs["Normal"])
    links.new(bump2.outputs["Normal"], bsdf.inputs["Normal"])
worn(plastic, (.030, .062, .25), (.085, .12, .30), .45, 1.0, 17)
worn(strap, (.085, .17, .045), (.16, .22, .10), .35, .25, 40)

bpy.context.view_layer.objects.active = charge; charge.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=.01); bpy.ops.object.mode_set(mode='OBJECT')
images = {}
for kind in ("Albedo", "Normal"):
    img = bpy.data.images.new("DemolitionCharge_" + kind, 1024, 1024, alpha=False); img.colorspace_settings.name = 'sRGB' if kind == "Albedo" else 'Non-Color'
    images[kind] = img
scene.render.engine = 'CYCLES'; scene.cycles.samples = 8; scene.cycles.device = 'CPU'
scene.render.bake.margin = 6
def target(kind):
    for mat in (plastic, strap):
        node = mat.node_tree.nodes.get("BakeTarget") or mat.node_tree.nodes.new("ShaderNodeTexImage")
        node.name = "BakeTarget"; node.image = images[kind]; mat.node_tree.nodes.active = node
target("Albedo"); scene.render.bake.use_pass_direct = scene.render.bake.use_pass_indirect = False; scene.render.bake.use_pass_color = True
bpy.ops.object.bake(type='DIFFUSE')
target("Normal"); bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT')
for kind, img in images.items():
    img.filepath_raw = os.path.join(TEX, "DemolitionCharge_" + kind + ".png"); img.file_format = 'PNG'; img.save()
print("baked", TEX)
# The game material is just these two images; show the preview with them too.
game = bpy.data.materials.new("Charge_Worn"); game.use_nodes = True; nodes, links = game.node_tree.nodes, game.node_tree.links
bsdf = nodes["Principled BSDF"]; bsdf.inputs["Roughness"].default_value = .55
albedo = nodes.new("ShaderNodeTexImage"); albedo.image = images["Albedo"]; links.new(albedo.outputs["Color"], bsdf.inputs["Base Color"])
normal_tex = nodes.new("ShaderNodeTexImage"); normal_tex.image = images["Normal"]
normal_map = nodes.new("ShaderNodeNormalMap"); links.new(normal_tex.outputs["Color"], normal_map.inputs["Color"]); links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
charge.data.materials.clear(); charge.data.materials.append(game)

# --- Preview render (Eevee, soft daylight) for review.
cam = link(bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))); cam.location = (.55, -.68, .52)
cam.rotation_euler = (math.radians(62), 0, math.radians(39)); scene.camera = cam; cam.data.lens = 50
sun = link(bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))); sun.rotation_euler = (math.radians(50), 0, math.radians(30)); sun.data.energy = 3.5
bpy.ops.mesh.primitive_plane_add(size=3); ground = bpy.context.active_object; ground.data.materials.append(material("Ground", (.36, .30, .22), .95))
scene.world = bpy.data.worlds.new("World"); scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.45, .47, .5, 1)
scene.view_settings.view_transform = 'Standard'
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
scene.render.resolution_x, scene.render.resolution_y = 900, 700
scene.render.filepath = os.path.join(HERE, "DemolitionCharge_preview.png"); bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "DemolitionCharge.blend"))
# --- Export only the charge, Y-up, metres, for Unity.
os.makedirs(os.path.dirname(FBX), exist_ok=True)
for o in scene.objects: o.select_set(o == charge)
bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', add_leaf_bones=False, bake_space_transform=True)
print("exported", FBX)
