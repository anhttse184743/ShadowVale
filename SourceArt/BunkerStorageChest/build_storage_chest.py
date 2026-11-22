"""Build the Map 1 chest, its Unity FBX, and the shared wood/iron atlas in Blender."""
from pathlib import Path
import math
import random

import bpy
import numpy as np
from mathutils import Vector

SOURCE = Path(__file__).resolve().parent
UNITY = SOURCE.parents[1] / "Assets/_Project/Art/Environment/CommandBunker/StorageChest"
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'

# Grain runs along the atlas X axis. Each band is a different finish.
rng = np.random.default_rng(48)
size = 1024
atlas = np.ones((size, size, 4), dtype=np.float32)
y, x = np.mgrid[0:256, 0:size] / np.array([[[256]], [[size]]])
finishes = [(.145, .119, .095), (.283, .319, .286), (.225, .201, .159), (.188, .168, .139)]
for band, color in enumerate(finishes):
    grain = np.sin(y * 460 + 3 * np.sin(x * 13 + y * 10))
    fibers = np.sin(y * 1420 + 8 * np.sin(x * 6 + y * 5))
    stains = np.sin(x * 17 + np.sin(y * 20)) * np.cos(y * 26 - x * 9)
    mottling = rng.normal(0, .021, x.shape)
    broad_wear = np.sin(x * 27 + y * 19) * np.sin(y * 43 - x * 8)
    shade = .07 * grain + .035 * fibers + .23 * stains + .14 * broad_wear if band < 3 else .22 * stains
    for kx, ky in rng.uniform(.05, .95, (9, 2)):
        radius = np.sqrt(((x - kx) * 1.8) ** 2 + ((y - ky) * 8) ** 2)
        if band < 3:
            shade += (.18 * np.cos(radius * 128) - .30) * np.exp(-radius * 13)
    atlas[band * 256:(band + 1) * 256, :, :3] = np.clip(
        np.array(color) * (1 + shade[..., None]) + mottling[..., None], .025, 1
    )

texture = bpy.data.images.new("StorageChest_Albedo", size, size)
texture.colorspace_settings.name = 'sRGB'
texture.pixels.foreach_set(atlas.ravel())
texture.filepath_raw = str(UNITY / "StorageChest_Albedo.png")
texture.file_format = 'PNG'
texture.save()
texture.pack()

material = bpy.data.materials.new("StorageChest")
material.use_nodes = True
nodes = material.node_tree.nodes
bsdf = nodes.get("Principled BSDF")
bsdf.inputs["Roughness"].default_value = .84
image = nodes.new("ShaderNodeTexImage")
image.image = texture
material.node_tree.links.new(image.outputs["Color"], bsdf.inputs["Base Color"])

random.seed(12)


def finish(obj, band, direction):
    obj.data.materials.append(material)
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new()
    obj.data.update()
    shift_u, shift_v = random.uniform(.04, .36), random.uniform(.06, .38)
    for face in obj.data.polygons:
        normal_axis = max(range(3), key=lambda axis: abs(face.normal[axis]))
        u_axis = direction if direction != normal_axis else (normal_axis + 1) % 3
        v_axis = 3 - normal_axis - u_axis
        for i in face.loop_indices:
            p = obj.data.vertices[obj.data.loops[i].vertex_index].co + obj.dimensions / 2
            uv.data[i].uv = (shift_u + p[u_axis] * .59,
                             (band + shift_v + p[v_axis] * .85) / 4)
    return obj


def box(name, center, dimensions, band, direction=0, bevel=.003):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    finish(obj, band, direction)
    edge = obj.modifiers.new("Worn edges", 'BEVEL')
    edge.width, edge.segments = bevel, 2
    obj.modifiers.new("Weighted corner normals", 'WEIGHTED_NORMAL')
    return obj


def pin(name, center, radius, depth, axis):
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=radius, depth=depth, location=center)
    obj = bpy.context.object
    obj.name = name
    finish(obj, 3, 2)
    if axis == 'X':
        obj.rotation_euler.y = math.pi / 2
    if axis == 'Y':
        obj.rotation_euler.x = math.pi / 2
    return obj


def join(parts, name, origin):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        bpy.context.view_layer.objects.active = obj
        for mod in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    obj.data.name = name
    scene.cursor.location = origin
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return obj


body, lid = [], []
for side in (-1, 1):
    for row in range(3):
        z = .079 + row * .139
        body.append(box("Long board", (0, side * .253, z), (.976, .029, .137), 0))
        body.append(box("End board", (side * .473, 0, z), (.030, .477, .137), 0, 1))
    body.append(box("Bottom rail", (0, side * .27, .047), (.97, .014, .079), 0))
    for x_pos in (-.321, .321):
        body.append(box("Bottom keeper", (x_pos, side * .281, .062), (.044, .008, .118), 3, 2))
        body.append(pin("Keeper nail", (x_pos, side * .286, .096), .005, .004, 'Y'))
for i in range(4):
    body.append(box("Interior floor board", (0, -.1725 + i * .115, .034), (.918, .113, .032), 0))
    lid.append(box("Olive lid plank", (0, -.2025 + i * .135, .455), (1, .133, .048), 1))
for x_pos in (-.324, .324):
    body.append(box("Floor runner", (x_pos, 0, .015), (.085, .525, .030), 0, 1))
    lid.append(box("Cross batten", (x_pos, 0, .487), (.073, .546, .026), 2, 1))
    for y_pos in (-.226, .222):
        lid.append(pin("Batten nail", (x_pos, y_pos, .499), .006, .002, 'Z'))
    body.append(box("Latch plate", (x_pos, -.272, .383), (.058, .013, .067), 3, 2))
    lid.append(box("Latch tongue", (x_pos, -.281, .427), (.037, .011, .060), 3, 2))
    for offset in (-.019, .019):
        lid.append(box("Latch loop side", (x_pos + offset, -.290, .395), (.007, .008, .036), 3, 2))
    lid.append(box("Latch loop base", (x_pos, -.290, .376), (.045, .008, .007), 3))
    body.append(box("Hinge leaf", (x_pos, .274, .412), (.067, .016, .058), 3))
    lid.append(pin("Hinge pin", (x_pos, .282, .437), .010, .083, 'X'))
    for x_offset in (-.018, .018):
        body.append(pin("Latch rivet", (x_pos + x_offset, -.280, .401), .0045, .004, 'Y'))
        body.append(pin("Hinge rivet", (x_pos + x_offset, .283, .397), .0045, .004, 'Y'))

body = join(body, "Body", (0, 0, 0))
lid = join(lid, "Lid", (0, .282, .437))
root = bpy.data.objects.new("Storage Chest", None)
scene.collection.objects.link(root)
for obj in (body, lid):
    obj.parent = root
bpy.ops.object.select_all(action='DESELECT')
for obj in (root, body, lid):
    obj.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(filepath=str(UNITY / "StorageChest.fbx"), use_selection=True,
                         axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False)

scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 900
scene.render.resolution_y = 650
scene.render.resolution_percentage = 100
scene.world = bpy.data.worlds.new("Studio")
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs[0].default_value = (.13, .15, .17, 1)


def point_at(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()


bpy.ops.object.camera_add(location=(1.38, -1.75, 1.13))
scene.camera = bpy.context.object
scene.camera.data.type = 'ORTHO'
scene.camera.data.ortho_scale = 1.55
point_at(scene.camera, (0, 0, .24))
for position, energy, color in [((-.7, -1.3, 2), 110, (1, .87, .70)),
                                ((.2, 1.6, 1.8), 82, (.80, .87, 1))]:
    bpy.ops.object.light_add(type='AREA', location=position)
    light = bpy.context.object
    light.data.energy, light.data.color, light.data.shape, light.data.size = energy, color, 'DISK', 2
    point_at(light, (0, 0, .2))

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "BunkerStorageChest.blend"), compress=True)
