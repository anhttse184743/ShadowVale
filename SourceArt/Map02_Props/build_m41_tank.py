"""Map 2 boss tank: an M41-type light tank, modelled from the real vehicle's layout.

Deterministic. Run:  blender --background --python build_m41_tank.py [-- --out <dir>]
Writes next to this script: M41_Tank.blend and M41_preview_*.png; and into <dir> (default
Assets/_Project/Art/Props/Map02): M41_Tank.fbx, M41_Tank_Albedo.png, M41_Tank_Normal.png.

Units are metres, Blender Z up, the tank faces +Y. Moving parts are separate objects whose origin is their
pivot, parented so Unity gets the same hierarchy:
  M41_Tank > Hull, Track_L/R, Wheel_L1..5/R1..5, Sprocket_L/R, Idler_L/R, Roller_L1..3/R1..3,
             Turret > Gun > Gun_Muzzle, Cupola > Hatch_Commander, Hatch_Loader, MG_Mount > MG > MG_Muzzle
Real M41A1: hull 5.82 m long, 3.20 m wide, 8.21 m with the gun forward; 5 road wheels a side, rear sprocket.
"""
import math, os, sys
import bpy, bmesh
from mathutils import Vector, Matrix, noise

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[args.index("--out") + 1] if "--out" in args else os.path.join(ROOT, "Assets", "_Project", "Art", "Props", "Map02")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
col = scene.collection

# ---------------------------------------------------------------- materials (baked into one atlas later)
def material(name, rgb, rough, metal=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    return m
PAINT = material("M41_Paint", (.105, .125, .078), .72)      # faded olive drab
STEEL = material("M41_Steel", (.045, .043, .040), .6, .4)    # MG, small fittings
TRACK = material("M41_Track", (.085, .058, .042), .75, .3)   # worn, rusty track steel
RUBBER = material("M41_Rubber", (.022, .022, .022), .85)
CANVAS = material("M41_Canvas", (.040, .045, .040), .9)      # mantlet cover
RUST = material("M41_Rust", (.20, .095, .055), .8)           # exhaust/muffler boxes
RED = material("M41_TailLight", (.55, .02, .02), .3)
LENS = material("M41_Lens", (.55, .55, .48), .2)

# ---------------------------------------------------------------- helpers
def obj_from_bm(name, bm, mat, parent=None, origin=None, local=False):
    """origin = the part's pivot in world space. World-built geometry is moved so the pivot is the object origin;
    local-built geometry (wheels, tracks) is already around its pivot."""
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat)
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    if origin is not None:
        if not local: me.transform(Matrix.Translation(-Vector(origin)))
        o.location = origin
    if parent: set_parent(o, parent)
    return o

def set_parent(child, parent):
    # Keep the child where it is: its location is in world space, so cancel the parent's world transform.
    bpy.context.view_layer.update()
    child.parent = parent; child.matrix_parent_inverse = parent.matrix_world.inverted()

def empty(name, at, parent=None):
    e = bpy.data.objects.new(name, None); e.empty_display_size = .15; e.location = at; col.objects.link(e)
    if parent: set_parent(e, parent)
    return e

def add_box(bm, center, size, taper_top=(0, 0), mat_index=0):
    """Box in bm; taper_top shrinks the top face by (dx, dy) for sloped armour."""
    cx, cy, cz = center; sx, sy, sz = (s / 2 for s in size); tx, ty = taper_top
    pts = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
           (-sx + tx, -sy + ty, sz), (sx - tx, -sy + ty, sz), (sx - tx, sy - ty, sz), (-sx + tx, sy - ty, sz)]
    v = [bm.verts.new((cx + x, cy + y, cz + z)) for x, y, z in pts]
    for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([v[i] for i in f]).material_index = mat_index
    return v

def add_cylinder(bm, center, radius, depth, axis='X', segments=16, radius2=None, mat_index=0):
    r2 = radius if radius2 is None else radius2
    ring1, ring2 = [], []
    for i in range(segments):
        a = 2 * math.pi * i / segments; c, s = math.cos(a), math.sin(a)
        for ring, r, d in ((ring1, radius, -depth / 2), (ring2, r2, depth / 2)):
            if axis == 'X': p = (d, c * r, s * r)
            elif axis == 'Y': p = (c * r, d, s * r)
            else: p = (c * r, s * r, d)
            ring.append(bm.verts.new(Vector(center) + Vector(p)))
    for i in range(segments):
        j = (i + 1) % segments
        bm.faces.new((ring1[i], ring1[j], ring2[j], ring2[i])).material_index = mat_index
    bm.faces.new(list(reversed(ring1))).material_index = mat_index; bm.faces.new(ring2).material_index = mat_index

def add_rod(bm, a, b, radius, segments=8, mat_index=0):
    """A round bar from a to b (cables, handles, rails)."""
    a, b = Vector(a), Vector(b); d = (b - a); L = d.length
    if L < 1e-5: return
    z = d.normalized(); x = z.orthogonal().normalized(); y = z.cross(x)
    r1 = [bm.verts.new(a + (x * math.cos(2 * math.pi * i / segments) + y * math.sin(2 * math.pi * i / segments)) * radius) for i in range(segments)]
    r2 = [bm.verts.new(v.co + d) for v in r1]
    for i in range(segments):
        j = (i + 1) % segments; bm.faces.new((r1[i], r1[j], r2[j], r2[i])).material_index = mat_index

def add_x(bm, center, w, h, plane, mat_index=0, t=.012):
    """Pressed X on a box face; plane 'yz' (side face, normal x), 'xy' (lid, normal z)."""
    c = Vector(center)
    for sgn in (-1, 1):
        if plane == 'yz': a, b = c + Vector((0, -w / 2, -sgn * h / 2)), c + Vector((0, w / 2, sgn * h / 2))
        else: a, b = c + Vector((-w / 2, -sgn * h / 2, 0)), c + Vector((w / 2, sgn * h / 2, 0))
        add_rod(bm, a, b, t, 4, mat_index)

def add_handle(bm, center, length, axis, height=.05, r=.011, mat_index=0):
    """U-shaped grab handle standing off a surface (axis = direction it runs, up = +z)."""
    c = Vector(center); d = Vector(axis).normalized() * length / 2; up = Vector((0, 0, height))
    if height > 0:
        add_rod(bm, c - d, c - d + up, r, 6, mat_index); add_rod(bm, c + d + up, c + d, r, 6, mat_index)
    add_rod(bm, c - d + up, c + d + up, r, 6, mat_index)

def loft(bm, rings, mat_index=0, cap=True):
    """Bridge closed outlines (lists of Vector) bottom to top."""
    vr = [[bm.verts.new(p) for p in ring] for ring in rings]
    n = len(rings[0])
    for a, b in zip(vr, vr[1:]):
        for i in range(n):
            j = (i + 1) % n; bm.faces.new((a[i], a[j], b[j], b[i])).material_index = mat_index
    if cap: bm.faces.new(list(reversed(vr[0]))).material_index = mat_index; bm.faces.new(vr[-1]).material_index = mat_index

def mesh(name, build, mats, parent=None, origin=None, local=False):
    bm = bmesh.new(); build(bm)
    me_mat = mats[0]
    o = obj_from_bm(name, bm, me_mat, parent, origin, local)
    for m in mats[1:]: o.data.materials.append(m)
    return o

root = empty("M41_Tank", (0, 0, 0))

# ---------------------------------------------------------------- hull
WHEEL_R, WHEEL_Z = .32, .36
WHEELS_Y = (-1.85, -0.95, -0.05, 0.85, 1.75)
SPROCKET = (-2.55, .62, .34)   # y, z, r  (rear drive)
IDLER = (2.58, .70, .30)       # front, raised
ROLLERS_Y, ROLLER_Z, ROLLER_R = (-1.42, 0.0, 1.3), .90, .10
TRACK_X, TRACK_W = 1.30, .52

def build_hull(bm):
    P, R, T, L = 0, 1, 2, 3   # paint, rust, red, lens
    # Lower hull between the tracks with sloped nose and tail.
    nose = [Vector(p) for p in ((-1.02, 2.62, .52), (1.02, 2.62, .52), (1.02, 2.95, 1.05), (-1.02, 2.95, 1.05))]
    add_box(bm, (0, -0.05, .78), (2.06, 5.4, .66))
    loft(bm, [[Vector((-1.02, 2.55, .48)), Vector((1.02, 2.55, .48)), Vector((1.02, 2.55, 1.08)), Vector((-1.02, 2.55, 1.08))],
              [Vector((-1.0, 2.95, .66)), Vector((1.0, 2.95, .66)), Vector((1.0, 2.98, 1.08)), Vector((-1.0, 2.98, 1.08))]])
    loft(bm, [[Vector((-1.02, -2.72, .55)), Vector((-1.02, -2.72, 1.08)), Vector((1.02, -2.72, 1.08)), Vector((1.02, -2.72, .55))],
              [Vector((-1.0, -2.95, .68)), Vector((-1.0, -2.95, 1.08)), Vector((1.0, -2.95, 1.08)), Vector((1.0, -2.95, .68))]])
    # Upper hull: deck slightly narrower, glacis sloping to the nose.
    add_box(bm, (0, -0.45, 1.32), (2.12, 4.95, .46), taper_top=(.05, .02))
    loft(bm, [[Vector((-1.06, 2.05, 1.09)), Vector((1.06, 2.05, 1.09)), Vector((1.06, 2.05, 1.55)), Vector((-1.06, 2.05, 1.55))],
              [Vector((-1.02, 2.97, 1.09)), Vector((1.02, 2.97, 1.09)), Vector((1.0, 2.97, 1.17)), Vector((-1.0, 2.97, 1.17))]])
    # Fenders over the tracks, curving down front and rear.
    for side in (-1, 1):
        x = side * TRACK_X
        add_box(bm, (x, 0.05, 1.07), (.6, 5.55, .03))
        loft(bm, [[Vector((x - .3, 2.82, 1.055)), Vector((x + .3, 2.82, 1.055)), Vector((x + .3, 2.82, 1.085)), Vector((x - .3, 2.82, 1.085))],
                  [Vector((x - .3, 3.15, .86)), Vector((x + .3, 3.15, .86)), Vector((x + .3, 3.15, .89)), Vector((x - .3, 3.15, .89))]], cap=True)
        loft(bm, [[Vector((x - .3, -2.72, 1.055)), Vector((x - .3, -2.72, 1.085)), Vector((x + .3, -2.72, 1.085)), Vector((x + .3, -2.72, 1.055))],
                  [Vector((x - .3, -3.0, .9)), Vector((x - .3, -3.0, .93)), Vector((x + .3, -3.0, .93)), Vector((x + .3, -3.0, .9))]], cap=True)
        # Stowage boxes along the fender, X pressed into lid and side, lid seam.
        for y0, y1 in ((-.95, .45), (.5, 1.75)):
            yc, ln = (y0 + y1) / 2, y1 - y0 - .04
            add_box(bm, (x + side * .02, yc, 1.3), (.5, ln, .44), taper_top=(.01, .01))
            add_x(bm, (x + side * .02, yc, 1.525), .4, ln - .12, 'xy')
            add_x(bm, (x + side * .275, yc, 1.3), ln - .14, .3, 'yz')
            add_box(bm, (x + side * .02, yc, 1.46), (.52, ln + .01, .015))
        # Muffler/exhaust boxes at the rear of the fenders (rust-brown), exhaust pipe out the back.
        add_box(bm, (x + side * .02, -1.85, 1.28), (.52, 1.65, .4), mat_index=R)
        add_cylinder(bm, (x - side * .12, -2.82, 1.2), .065, .36, 'Y', 10, mat_index=R)
        # Headlight guards with twin lamps at the front of the deck.
        add_box(bm, (side * .74, 2.72, 1.33), (.34, .1, .28))
        for dx in (-.07, .07):
            add_cylinder(bm, (side * .74 + dx, 2.79, 1.33), .055, .06, 'Y', 10, mat_index=L)
        # Tow cable along the side, slack between its clips, eye at each end.
        cable = [(side * 1.1, 1.9, 1.56), (side * 1.12, 1.3, 1.585), (side * 1.11, .6, 1.565), (side * 1.12, -.1, 1.59), (side * 1.11, -.8, 1.565), (side * 1.1, -1.05, 1.57)]
        for a, b in zip(cable, cable[1:]): add_rod(bm, a, b, .016, 6)
        for end in (cable[0], cable[-1]): add_cylinder(bm, end, .05, .02, 'X', 10)
        # Rear louvres at the lower corners of the back plate.
        for k in range(5): add_box(bm, (side * .55, -2.96, .86 + k * .055), (.4, .04, .02))
        # Tail lights in their guards.
        add_box(bm, (side * .88, -2.945, 1.18), (.16, .05, .14))   # guard behind the lamp face
        add_cylinder(bm, (side * .88, -2.97, 1.18), .045, .05, 'Y', 10, mat_index=T)
        # Front towing eyes and rear lifting rings.
        add_cylinder(bm, (side * .45, 3.0, .8), .07, .12, 'X', 10)
        add_cylinder(bm, (side * .98, -2.9, 1.18), .06, .05, 'X', 10)
    # Air cleaner on the right front fender (perforated drum).
    add_cylinder(bm, (1.32, 1.95, 1.2), .13, .5, 'Y', 14)
    for k in range(5): add_cylinder(bm, (1.32, 1.76 + k * .095, 1.2), .132, .025, 'Y', 14, mat_index=R)
    # Driver's hatch and periscopes on the front deck, left of the gun.
    add_cylinder(bm, (-.48, 2.25, 1.57), .3, .04, 'Z', 18)
    for dx in (-.14, .12): add_box(bm, (-.48 + dx, 2.55, 1.6), (.16, .1, .07))
    add_handle(bm, (-.48, 2.2, 1.59), .16, (1, 0, 0), .03)
    # Engine access panels with handles beside the grilles.
    for side in (-1, 1):
        add_box(bm, (side * .55, -.95, 1.565), (.8, .45, .02))
        add_handle(bm, (side * .55, -.95, 1.575), .18, (1, 0, 0), .035)
    # Engine deck grilles (slats) behind the turret, rear towing pintle ring.
    for i in range(9):
        add_box(bm, (0, -1.35 - i * .14, 1.56), (1.6, .06, .03))
    add_box(bm, (0, -2.25, 1.56), (1.8, .7, .02))
    ring = bmesh.ops.create_circle(bm, cap_ends=False, radius=.17, segments=16)
    for v in ring["verts"]: v.co = Vector((-.62, -3.02, .95)) + Matrix.Rotation(math.radians(90), 3, 'Y') @ v.co
    bmesh.ops.extrude_edge_only(bm, edges=list({e for v in ring["verts"] for e in v.link_edges}))
    add_box(bm, (-.62, -2.95, .95), (.08, .12, .08))
    # Suspension arms from the hull side to each wheel hub.
    for side in (-1, 1):
        for y in WHEELS_Y:
            add_box(bm, (side * 1.05, y + .2, .5), (.12, .5, .1))
hull = mesh("Hull", build_hull, [PAINT, RUST, RED, LENS], root)

# ---------------------------------------------------------------- running gear
def wheel_builder(radius, width, dual=True, hub=.11):
    def build(bm):
        if dual:
            for dx in (-width * .27, width * .27):
                add_cylinder(bm, (dx, 0, 0), radius, width * .42, 'X', 20, mat_index=1)          # rubber tyre
                add_cylinder(bm, (dx * 1.05, 0, 0), radius * .78, width * .44, 'X', 20)         # steel disc
        else:
            add_cylinder(bm, (0, 0, 0), radius, width, 'X', 16, mat_index=1)
        add_cylinder(bm, (0, 0, 0), hub, width * 1.12, 'X', 8)                                   # hub cap
    return build

def sprocket_builder(radius, width, teeth=13):
    def build(bm):
        add_cylinder(bm, (0, 0, 0), radius * .82, width, 'X', 24)
        add_cylinder(bm, (0, 0, 0), radius * .3, width * 1.2, 'X', 10)
        for i in range(teeth):
            a = 2 * math.pi * i / teeth
            v = add_box(bm, (0, math.cos(a) * radius * .9, math.sin(a) * radius * .9), (width * .9, .07, .09))
            rot = Matrix.Rotation(a + math.pi / 2, 3, 'X'); c = Vector((0, math.cos(a) * radius * .9, math.sin(a) * radius * .9))
            for vert in v: vert.co = c + rot @ (vert.co - c)
    return build

for side, tag in ((-1, "L"), (1, "R")):
    x = side * TRACK_X
    for i, y in enumerate(WHEELS_Y, 1):
        mesh(f"Wheel_{tag}{i}", wheel_builder(WHEEL_R, TRACK_W * .9), [PAINT, RUBBER], root, (x, y, WHEEL_Z), True)
    for i, y in enumerate(ROLLERS_Y, 1):
        mesh(f"Roller_{tag}{i}", wheel_builder(ROLLER_R, .2, dual=False, hub=.05), [PAINT, RUBBER], root, (x, y, ROLLER_Z), True)
    mesh(f"Sprocket_{tag}", sprocket_builder(SPROCKET[2], .26), [PAINT], root, (x, SPROCKET[0], SPROCKET[1]), True)
    mesh(f"Idler_{tag}", wheel_builder(IDLER[2], TRACK_W * .8), [PAINT, RUBBER], root, (x, IDLER[0], IDLER[1]), True)

# ---------------------------------------------------------------- tracks: links with spiked grousers along the run
def track_path():
    """Closed loop (y, z) around sprocket, under the road wheels, round the idler and back over the rollers."""
    pts = []
    def arc(cy, cz, r, a0, a1, n):
        for i in range(n + 1):
            a = a0 + (a1 - a0) * i / n; pts.append((cy + math.cos(a) * r, cz + math.sin(a) * r))
    t = .045
    arc(SPROCKET[0], SPROCKET[1], SPROCKET[2] + t, math.radians(110), math.radians(250), 8)        # rear, down
    for y in (-2.15,) + WHEELS_Y + (2.2,): pts.append((y, .025 + (0.02 if y in (-2.15, 2.2) else 0)))   # ground run
    arc(IDLER[0], IDLER[1], IDLER[2] + t, math.radians(-75), math.radians(85), 8)                 # front, up
    for y, sag in ((1.9, .02), (1.3, 0), (.65, .025), (0, 0), (-.7, .025), (-1.42, 0), (-2.1, .02)):
        pts.append((y, ROLLER_Z + ROLLER_R + t - sag))                                              # top run over rollers
    return pts

def resample(pts, step):
    seg = [(Vector((0,) + a), Vector((0,) + b)) for a, b in zip(pts, pts[1:] + pts[:1])]
    total = sum((b - a).length for a, b in seg); n = int(total / step); out = []
    for k in range(n):
        d = k * total / n
        for a, b in seg:
            L = (b - a).length
            if d <= L: p = a.lerp(b, d / L if L else 0); out.append((p, (b - a).normalized())); break
            d -= L
    return out

LINKS = resample(track_path(), .152)
def track_builder(side):
    def build(bm):
        center = Vector((0, sum(p.y for p, _ in LINKS) / len(LINKS), sum(p.z for p, _ in LINKS) / len(LINKS)))
        for p, tang in LINKS:
            out = Vector((0, -tang.z, tang.y))
            if out.dot(p - center) < 0: out = -out                     # grousers point away from the wheels
            basis = Matrix((Vector((1, 0, 0)), tang, out)).transposed()
            def put(vs):
                for v in vs: v.co = p + basis @ v.co
            put(add_box(bm, (0, 0, 0), (TRACK_W, .135, .045)))                       # shoe
            spike = [bm.verts.new(c) for c in ((-TRACK_W / 2, -.06, .022), (TRACK_W / 2, -.06, .022), (TRACK_W / 2, .06, .022),
                                               (-TRACK_W / 2, .06, .022), (-TRACK_W / 2, .0, .075), (TRACK_W / 2, .0, .075))]
            for f in ((0, 1, 5, 4), (2, 3, 4, 5), (0, 4, 3), (1, 2, 5)): bm.faces.new([spike[i] for i in f])
            put(spike)
            put(add_box(bm, (0, 0, -.045), (.07, .05, .05)))                         # guide horn
    return build
for side, tag in ((-1, "L"), (1, "R")):
    mesh(f"Track_{tag}", track_builder(side), [TRACK], root, (side * TRACK_X, 0, 0), True)

# ---------------------------------------------------------------- turret, gun, cupola, hatches, MG
RING = Vector((0, .35, 1.56))
def outline(z, inset, front_y, front_w, mid_w, rear_y, rear_w):
    return [Vector((x, y, z)) for x, y in (
        (-front_w + inset, front_y - inset), (front_w - inset, front_y - inset), (1.02 - inset, .85), (mid_w - inset, .1),
        (mid_w - inset, -.55), (rear_w - inset, rear_y + inset), (-rear_w + inset, rear_y + inset), (-mid_w + inset, -.55),
        (-mid_w + inset, .1), (-1.02 + inset, .85))]

def build_turret(bm):
    P, C = 0, 1
    base = [v + RING for v in outline(0, 0, 1.28, .58, 1.14, -1.25, 1.0)]
    belt = [v + RING for v in outline(.32, -.03, 1.3, .6, 1.17, -1.27, 1.02)]
    top = [v + RING for v in outline(.72, .2, 1.12, .46, 1.14, -1.2, 1.0)]
    loft(bm, [base, belt, top])
    # Rear bustle, overhanging the engine deck.
    add_box(bm, RING + Vector((0, -1.5, .46)), (1.8, .58, .5), taper_top=(.04, .03))   # flush with the roof
    # Ventilator dome with its six bolts, and turret-ring skirt.
    add_cylinder(bm, RING + Vector((0, -.95, .74)), .2, .06, 'Z', 16)
    for k in range(6): add_cylinder(bm, RING + Vector((math.cos(k * math.pi / 3) * .15, -.95 + math.sin(k * math.pi / 3) * .15, .78)), .018, .02, 'Z', 6)
    # Lifting eyes at the corners of the roof.
    for sx, sy in ((-.85, .7), (.85, .7), (-.85, -1.05), (.85, -1.05)):
        add_cylinder(bm, RING + Vector((sx, sy, .76)), .05, .025, 'X', 10)
    add_cylinder(bm, RING + Vector((0, 0, -.02)), 1.0, .06, 'Z', 24)
    # Grab handles along the sides (small rails).
    for side in (-1, 1):
        for y in (.6, .25, -.15, -.55, -.95):
            for z in (.52, .3): add_handle(bm, RING + Vector((side * 1.15, y, z)), .16, (0, 1, 0), .0, .012)
    # Bustle rail.
    add_handle(bm, RING + Vector((0, -1.82, .55)), 1.5, (1, 0, 0), .0, .014)
    # Periscope housings at the front of the roof.
    add_box(bm, RING + Vector((-.42, .62, .76)), (.22, .14, .1))
    add_box(bm, RING + Vector((.5, .55, .76)), (.18, .12, .1))
    # Mantlet cover (canvas), the gun's armoured face.
    add_cylinder(bm, RING + Vector((0, 1.16, .38)), .33, 1.02, 'X', 20, mat_index=C)   # sits in the turret face
turret = mesh("Turret", build_turret, [PAINT, CANVAS], root, RING)

TRUNNION = RING + Vector((0, 1.3, .38))
def build_gun(bm):
    add_cylinder(bm, TRUNNION + Vector((0, .45, 0)), .13, .6, 'Y', 16)                 # recoil sleeve
    add_cylinder(bm, TRUNNION + Vector((0, 2.05, 0)), .085, 2.8, 'Y', 16, radius2=.068)  # barrel
    add_cylinder(bm, TRUNNION + Vector((0, 3.55, 0)), .095, .24, 'Y', 16)                # muzzle brake body
    for side in (-1, 1): add_box(bm, TRUNNION + Vector((side * .13, 3.55, 0)), (.08, .22, .16))   # brake baffles (T)
gun = mesh("Gun", build_gun, [PAINT], turret, TRUNNION)
empty("Gun_Muzzle", TRUNNION + Vector((0, 3.7, 0)), gun)

CUPOLA = RING + Vector((.42, -.25, .72))
def build_cupola(bm):
    add_cylinder(bm, CUPOLA + Vector((0, 0, .09)), .4, .18, 'Z', 24)
    for i in range(6):                                                                   # vision blocks
        a = 2 * math.pi * i / 6 + .3
        v = add_box(bm, CUPOLA + Vector((math.cos(a) * .4, math.sin(a) * .4, .1)), (.12, .07, .08))
cupola = mesh("Cupola", build_cupola, [PAINT], turret, CUPOLA)
HINGE_C = CUPOLA + Vector((0, -.3, .19))
def build_hatch_c(bm):
    add_cylinder(bm, CUPOLA + Vector((0, 0, .21)), .31, .045, 'Z', 24)
    add_box(bm, CUPOLA + Vector((0, .05, .25)), (.18, .03, .03))                         # handle
    add_cylinder(bm, HINGE_C, .035, .3, 'X', 8)                                          # hinge
mesh("Hatch_Commander", build_hatch_c, [PAINT], cupola, HINGE_C)

LOADER = RING + Vector((-.45, .05, .72))
HINGE_L = LOADER + Vector((-.3, 0, .03))
def build_hatch_l(bm):
    ring8 = [Vector((math.cos(2 * math.pi * i / 8 + math.pi / 8) * .3, math.sin(2 * math.pi * i / 8 + math.pi / 8) * .3, 0)) + LOADER for i in range(8)]
    loft(bm, [ring8, [v + Vector((0, 0, .05)) for v in ring8]])
    add_cylinder(bm, HINGE_L, .03, .3, 'Y', 8)
mesh("Hatch_Loader", build_hatch_l, [PAINT], turret, HINGE_L)

MOUNT = RING + Vector((-.18, -.55, .72))
def build_mount(bm):
    add_cylinder(bm, MOUNT + Vector((0, 0, .17)), .05, .34, 'Z', 10)
    add_box(bm, MOUNT + Vector((0, 0, .36)), (.1, .12, .08))
mount = mesh("MG_Mount", build_mount, [STEEL], turret, MOUNT)
MG_PIVOT = MOUNT + Vector((0, 0, .42))
def build_mg(bm):
    add_box(bm, MG_PIVOT + Vector((0, -.05, .06)), (.13, .55, .14))                      # receiver
    add_box(bm, MG_PIVOT + Vector((0, -.38, .07)), (.08, .12, .1))                       # spade grips
    add_cylinder(bm, MG_PIVOT + Vector((0, .5, .07)), .045, .56, 'Y', 10)                # perforated jacket
    add_cylinder(bm, MG_PIVOT + Vector((0, .95, .07)), .022, .5, 'Y', 8)                 # barrel
    add_box(bm, MG_PIVOT + Vector((.1, -.02, .02)), (.08, .2, .12))                      # ammo can
mg = mesh("MG", build_mg, [STEEL], mount, MG_PIVOT)
empty("MG_Muzzle", MG_PIVOT + Vector((0, 1.2, .07)), mg)
antenna = mesh("Antenna", lambda bm: add_cylinder(bm, RING + Vector((.85, -1.45, 1.55)), .008, 1.9, 'Z', 6), [STEEL], turret)

# ---------------------------------------------------------------- worn look, baked to one atlas for Unity
def worn(mat, dirt, wear, bump):
    nt = mat.node_tree; n, l = nt.nodes, nt.links; b = n["Principled BSDF"]
    coord = n.new("ShaderNodeTexCoord")
    base = n.new("ShaderNodeRGB"); base.outputs[0].default_value = b.inputs["Base Color"].default_value
    patch = n.new("ShaderNodeTexNoise"); patch.inputs["Scale"].default_value = 3.2; patch.inputs["Detail"].default_value = 8
    l.new(coord.outputs["Object"], patch.inputs["Vector"])
    faded = n.new("ShaderNodeMix"); faded.data_type = 'RGBA'; l.new(base.outputs[0], faded.inputs[6])
    faded.inputs[7].default_value = (min(1, base.outputs[0].default_value[0] * 1.5), min(1, base.outputs[0].default_value[1] * 1.45), min(1, base.outputs[0].default_value[2] * 1.4), 1)
    fr = n.new("ShaderNodeMapRange"); fr.inputs["From Min"].default_value = .45; fr.inputs["From Max"].default_value = .75; fr.inputs["To Max"].default_value = wear
    l.new(patch.outputs["Fac"], fr.inputs["Value"]); l.new(fr.outputs["Result"], faded.inputs[0])
    sep = n.new("ShaderNodeSeparateXYZ"); l.new(n.new("ShaderNodeNewGeometry").outputs["Position"], sep.inputs["Vector"])
    low = n.new("ShaderNodeMapRange"); low.inputs["From Min"].default_value = 0; low.inputs["From Max"].default_value = 1.3
    low.inputs["To Min"].default_value = dirt; low.inputs["To Max"].default_value = 0; l.new(sep.outputs["Z"], low.inputs["Value"])
    mud = n.new("ShaderNodeTexNoise"); mud.inputs["Scale"].default_value = 9; l.new(coord.outputs["Object"], mud.inputs["Vector"])
    mask = n.new("ShaderNodeMath"); mask.operation = 'MULTIPLY'; l.new(low.outputs["Result"], mask.inputs[0]); l.new(mud.outputs["Fac"], mask.inputs[1])
    dirty = n.new("ShaderNodeMix"); dirty.data_type = 'RGBA'; dirty.inputs[7].default_value = (.16, .12, .08, 1)
    l.new(faded.outputs[2], dirty.inputs[6]); l.new(mask.outputs[0], dirty.inputs[0]); l.new(dirty.outputs[2], b.inputs["Base Color"])
    grain = n.new("ShaderNodeTexNoise"); grain.inputs["Scale"].default_value = 45; l.new(coord.outputs["Object"], grain.inputs["Vector"])
    bp = n.new("ShaderNodeBump"); bp.inputs["Strength"].default_value = bump; bp.inputs["Distance"].default_value = .004
    l.new(grain.outputs["Fac"], bp.inputs["Height"]); l.new(bp.outputs["Normal"], b.inputs["Normal"])
worn(PAINT, .5, .28, .12); worn(STEEL, .6, .4, .25); worn(TRACK, .5, .5, .3); worn(RUBBER, .4, .2, .1); worn(CANVAS, .3, .5, .3); worn(RUST, .3, .7, .25)

meshes = [o for o in col.objects if o.type == 'MESH']
for o in (hull, turret):
    # Slightly bevelled armour edges catch the light like the real plate and casting.
    for o2 in col.objects: o2.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    mod = o.modifiers.new("Edges", 'BEVEL'); mod.width = .012; mod.segments = 1; mod.limit_method = 'ANGLE'
    bpy.ops.object.modifier_apply(modifier=mod.name)
for o in meshes:
    for o2 in col.objects: o2.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(38))
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = hull
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.003, scale_to_bounds=False)
bpy.ops.uv.pack_islands(margin=.002); bpy.ops.object.mode_set(mode='OBJECT')

images = {k: bpy.data.images.new("M41_Tank_" + k, 2048, 2048, alpha=False) for k in ("Albedo", "Normal")}
images["Normal"].colorspace_settings.name = 'Non-Color'
scene.render.engine = 'CYCLES'; scene.cycles.samples = 4; scene.cycles.device = 'CPU'; scene.render.bake.margin = 4
def target(kind):
    for mat in bpy.data.materials:
        if not mat.use_nodes or not mat.name.startswith("M41_"): continue
        node = mat.node_tree.nodes.get("BakeTarget") or mat.node_tree.nodes.new("ShaderNodeTexImage")
        node.name = "BakeTarget"; node.image = images[kind]; mat.node_tree.nodes.active = node
scene.render.bake.use_pass_direct = scene.render.bake.use_pass_indirect = False; scene.render.bake.use_pass_color = True
target("Albedo"); bpy.ops.object.bake(type='DIFFUSE')
target("Normal"); bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT')
os.makedirs(OUT, exist_ok=True)
for k, img in images.items():
    img.filepath_raw = os.path.join(OUT, "M41_Tank_" + k + ".png"); img.file_format = 'PNG'; img.save()
game = bpy.data.materials.new("M41_Worn"); game.use_nodes = True; gn, gl = game.node_tree.nodes, game.node_tree.links
gb = gn["Principled BSDF"]; gb.inputs["Roughness"].default_value = .7
ta = gn.new("ShaderNodeTexImage"); ta.image = images["Albedo"]; gl.new(ta.outputs["Color"], gb.inputs["Base Color"])
tn = gn.new("ShaderNodeTexImage"); tn.image = images["Normal"]; nm = gn.new("ShaderNodeNormalMap"); gl.new(tn.outputs["Color"], nm.inputs["Color"]); gl.new(nm.outputs["Normal"], gb.inputs["Normal"])
for o in meshes:
    o.data.materials.clear(); o.data.materials.append(game)
tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
print("triangles", tris, "objects", len(meshes))

# ---------------------------------------------------------------- review renders (Eevee): side, front, top, three-quarter
for o in col.objects: o.select_set(False)
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
scene.view_settings.view_transform = 'Standard'
scene.world = bpy.data.worlds.new("World"); scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.2, .2, .21, 1)
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN')); col.objects.link(sun); sun.rotation_euler = (math.radians(45), math.radians(10), math.radians(35)); sun.data.energy = 3.2
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); col.objects.link(cam); scene.camera = cam
scene.render.resolution_x, scene.render.resolution_y = 1100, 640
for label, loc, rot, ortho in (("side", (11, .9, 1.4), (90, 0, 90), 8.6), ("front", (0, 12, 1.5), (90, 0, 180), 4.2),
                               ("top", (0, .9, 14), (0, 0, 0), 9.2), ("3q", (8.2, 7.8, 5.2), (64, 0, 133), None), ("rear3q", (-7.4, -7.8, 4.6), (65, 0, -43), None)):
    cam.location = loc; cam.rotation_euler = [math.radians(a) for a in rot]
    cam.data.type = 'ORTHO' if ortho else 'PERSP'
    if ortho: cam.data.ortho_scale = ortho
    else: cam.data.lens = 45
    scene.render.filepath = os.path.join(HERE, f"M41_preview_{label}.png"); bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "M41_Tank.blend"))
for o in col.objects: o.select_set(o.type in ('MESH', 'EMPTY'))
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "M41_Tank.fbx"), use_selection=True, object_types={'MESH', 'EMPTY'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         mesh_smooth_type='FACE', add_leaf_bones=False, bake_space_transform=False, path_mode='STRIP')
print("exported", OUT)
