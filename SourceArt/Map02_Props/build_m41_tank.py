"""Map 2 boss tank: an M41-type light tank, modelled from the real vehicle's layout.

Deterministic. Run:  blender --background --python build_m41_tank.py [-- --out <dir>] [--quick]
Writes into <dir> (default: next to this script) M41_Tank.blend, with its baked textures packed in,
and M41_preview_*.png. --quick skips the texture bake and renders the procedural materials (for checking
shapes fast; its .blend is not for export). Nothing goes into Unity from here: once the .blend is approved,
export it with
  blender --background M41_Tank.blend --python export_m41_tank.py

Units are metres, Blender Z up, the tank faces +Y. Moving parts are separate objects whose origin is their
pivot, parented so Unity gets the same hierarchy:
  M41_Tank > Hull, Track_L/R, Wheel_L1..5/R1..5, Sprocket_L/R, Idler_L/R, Roller_L1..3/R1..3,
             Turret > Gun > Gun_Muzzle, Cupola > Hatch_Commander, Hatch_Loader, MG_Mount > MG > MG_Muzzle
Real M41A1: hull 5.82 m long, 3.20 m wide, 2.73 m high; 5 road wheels a side, rear sprocket, raised front idler.
Heights are scaled from the road wheels in the reference shots (wheel 0.66 m across).
"""
import math, os, sys
import bpy, bmesh
from mathutils import Vector, Matrix, noise

HERE = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[args.index("--out") + 1] if "--out" in args else HERE
QUICK = "--quick" in args

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
col = scene.collection

# ---------------------------------------------------------------- materials (baked into one atlas later)
def material(name, rgb, rough, metal=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    return m
PAINT = material("M41_Paint", (.112, .140, .088), .72)       # olive drab, the reference's lit sRGB ~(94,104,84)
STEEL = material("M41_Steel", (.032, .033, .033), .55, .4)    # small dark fittings, vision blocks, bores
GUNMETAL = material("M41_Gunmetal", (.26, .27, .27), .42, .6) # the .50 cal, bare and light as in the reference
TRACK = material("M41_Track", (.125, .078, .056), .72, .3)    # rust-brown track steel
RUBBER = material("M41_Rubber", (.024, .023, .022), .9)
CANVAS = material("M41_Canvas", (.030, .031, .030), .92)       # mantlet cover, near black
RUST = material("M41_Rust", (.165, .108, .092), .82)           # muffler covers: mauve-brown
RED = material("M41_TailLight", (.55, .02, .02), .3)
LENS = material("M41_Lens", (.62, .62, .55), .2)

# ---------------------------------------------------------------- helpers
def obj_from_bm(name, bm, mat, parent=None, origin=None, local=False, data=None):
    """origin = the part's pivot in world space. World-built geometry is moved so the pivot is the object origin;
    local-built geometry (wheels, tracks) is already around its pivot. data reuses an existing mesh."""
    if data is None:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])   # every part faces outward, whichever axis it was built on
        me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat)
        if origin is not None and not local: me.transform(Matrix.Translation(-Vector(origin)))
    else:
        me = data
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    if origin is not None: o.location = origin
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
    return ring1 + ring2

def add_rod(bm, a, b, radius, segments=8, mat_index=0):
    """A round bar from a to b (cables, handles, rails, guards)."""
    a, b = Vector(a), Vector(b); d = (b - a); L = d.length
    if L < 1e-5: return
    z = d.normalized(); x = z.orthogonal().normalized(); y = z.cross(x)
    r1 = [bm.verts.new(a + (x * math.cos(2 * math.pi * i / segments) + y * math.sin(2 * math.pi * i / segments)) * radius) for i in range(segments)]
    r2 = [bm.verts.new(v.co + d) for v in r1]
    for i in range(segments):
        j = (i + 1) % segments; bm.faces.new((r1[i], r1[j], r2[j], r2[i])).material_index = mat_index

def add_path(bm, pts, radius, segments=6, mat_index=0):
    for a, b in zip(pts, pts[1:]): add_rod(bm, a, b, radius, segments, mat_index)

def add_x(bm, center, w, h, plane, mat_index=0, t=.011):
    """Pressed X on a face; plane 'yz' (side face, normal x), 'xy' (lid, normal z)."""
    c = Vector(center)
    for sgn in (-1, 1):
        if plane == 'yz': a, b = c + Vector((0, -w / 2, -sgn * h / 2)), c + Vector((0, w / 2, sgn * h / 2))
        else: a, b = c + Vector((-w / 2, -sgn * h / 2, 0)), c + Vector((w / 2, sgn * h / 2, 0))
        add_rod(bm, a, b, t, 4, mat_index)

def add_staple(bm, center, length, along, out, height=.045, r=.011, mat_index=0):
    """Grab handle: a bar standing off a surface on two legs. along = the bar's direction, out = surface normal."""
    c = Vector(center); d = Vector(along).normalized() * length / 2; up = Vector(out).normalized() * height
    add_path(bm, [c - d, c - d + up, c + d + up, c + d], r, 6, mat_index)

def loft(bm, rings, mat_index=0, cap=True):
    """Bridge closed outlines (lists of Vector) in order."""
    vr = [[bm.verts.new(p) for p in ring] for ring in rings]
    n = len(rings[0])
    for a, b in zip(vr, vr[1:]):
        for i in range(n):
            j = (i + 1) % n; bm.faces.new((a[i], a[j], b[j], b[i])).material_index = mat_index
    if cap: bm.faces.new(list(reversed(vr[0]))).material_index = mat_index; bm.faces.new(vr[-1]).material_index = mat_index
    return vr

def extrude_yz(bm, pts, x0, x1, mat_index=0):
    """A plate whose side profile is pts [(y, z)], spanning x0..x1."""
    return loft(bm, [[Vector((x0, y, z)) for y, z in pts], [Vector((x1, y, z)) for y, z in pts]], mat_index)

def extrude_xz(bm, pts, y0, y1, mat_index=0):
    """A shape whose cross-section is pts [(x, z)], running y0..y1."""
    return loft(bm, [[Vector((x, y0, z)) for x, z in pts], [Vector((x, y1, z)) for x, z in pts]], mat_index)

def lathe(bm, profile, center, axis='X', segments=32, mat_of=lambda r0, r1: 0):
    """Solid of revolution. profile = [(radius, axial)] from one pole (radius 0) to the other; mat_of(r0, r1)
    picks the material of each band from its radii."""
    c = Vector(center)
    ax = {'X': Vector((1, 0, 0)), 'Y': Vector((0, 1, 0)), 'Z': Vector((0, 0, 1))}[axis]
    u = {'X': Vector((0, 1, 0)), 'Y': Vector((1, 0, 0)), 'Z': Vector((1, 0, 0))}[axis]; v = ax.cross(u)
    rings = []
    for r, a in profile:
        if r < 1e-6: rings.append([bm.verts.new(c + ax * a)])
        else: rings.append([bm.verts.new(c + ax * a + (u * math.cos(2 * math.pi * k / segments) + v * math.sin(2 * math.pi * k / segments)) * r) for k in range(segments)])
    for (ra, aa), (rb, ab), A, B in zip(profile, profile[1:], rings, rings[1:]):
        m = mat_of(ra, rb)
        for k in range(segments):
            j = (k + 1) % segments
            if len(A) == 1: f = (A[0], B[k], B[j])
            elif len(B) == 1: f = (A[k], A[j], B[0])
            else: f = (A[k], A[j], B[j], B[k])
            bm.faces.new(f).material_index = m

def bez(p0, c, p1, n):
    """Quadratic Bezier from p0 (excluded) to p1 (included) through control c, n points."""
    out = []
    for i in range(1, n + 1):
        t = i / n; a, b, d = (1 - t) ** 2, 2 * (1 - t) * t, t * t
        out.append((a * p0[0] + b * c[0] + d * p1[0], a * p0[1] + b * c[1] + d * p1[1]))
    return out

def rounded(pts, r, n=3):
    """Round the corners of a closed 2D outline with radius-ish r (Bezier through each corner)."""
    out = []
    for i, p in enumerate(pts):
        a, b = Vector(pts[i - 1]), Vector(pts[(i + 1) % len(pts)]); p = Vector(p)
        ra = min(r, (a - p).length * .45); rb = min(r, (b - p).length * .45)
        s = p + (a - p).normalized() * ra; e = p + (b - p).normalized() * rb
        out.append((s.x, s.y)); out += bez((s.x, s.y), (p.x, p.y), (e.x, e.y), n)
    return out

def bevel_edges(bm, offset, segments=2, min_angle=35):
    """Round every sharp edge built so far (armour plate, boxes); details added afterwards stay crisp."""
    bm.edges.ensure_lookup_table()
    edges = [e for e in bm.edges if e.is_manifold and e.calc_face_angle(0) > math.radians(min_angle)]
    if edges:
        bmesh.ops.bevel(bm, geom=edges + list({v for e in edges for v in e.verts}), offset=offset, segments=segments,
                        profile=.5, affect='EDGES', clamp_overlap=True)

def smoothstep(a, b, x):
    t = max(0, min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t)

def catmull(knots, y):
    """Smooth curve through knots [(y, value)] sorted by y."""
    if y <= knots[0][0]: return knots[0][1]
    if y >= knots[-1][0]: return knots[-1][1]
    for i in range(len(knots) - 1):
        if knots[i][0] <= y <= knots[i + 1][0]: break
    y0, p0 = knots[max(0, i - 1)]; y1, p1 = knots[i]; y2, p2 = knots[i + 1]; y3, p3 = knots[min(len(knots) - 1, i + 2)]
    t = (y - y1) / (y2 - y1)
    m1 = (p2 - p0) / (y2 - y0) * (y2 - y1) if y2 != y0 else 0; m2 = (p3 - p1) / (y3 - y1) * (y2 - y1) if y3 != y1 else 0
    t2, t3 = t * t, t * t * t
    return (2 * t3 - 3 * t2 + 1) * p1 + (t3 - 2 * t2 + t) * m1 + (-2 * t3 + 3 * t2) * p2 + (t3 - t2) * m2

def mesh(name, build, mats, parent=None, origin=None, local=False):
    bm = bmesh.new(); build(bm)
    o = obj_from_bm(name, bm, mats[0], parent, origin, local)
    for m in mats[1:]: o.data.materials.append(m)
    return o

root = empty("M41_Tank", (0, 0, 0))

# ---------------------------------------------------------------- layout
WHEEL_R, WHEEL_Z = .33, .42
WHEELS_Y = (-1.82, -0.93, -0.04, 0.85, 1.74)
SPROCKET = (-2.6, .80, .35)    # y, z, r  (rear drive)
IDLER = (2.56, .98, .27)       # front, raised
ROLLERS_Y, ROLLER_Z, ROLLER_R = (-1.38, -.05, 1.28), 1.11, .10
TRACK_X, TRACK_W = 1.33, .53
FENDER = 1.375                 # fender top
DECK = 1.78                    # hull roof / turret ring
HULL_X = 1.05                  # hull side plates (inside the tracks)
FENDER_X = 1.74                # fender outer edge, past the track
BOX_X, BOX_W = 1.41, .64       # stowage boxes on the fenders

# ---------------------------------------------------------------- hull
def glacis_z(y):  # the upper front plate, from the deck edge down to the nose
    return DECK - (y - 1.95) * (.48 / 1.03)

def build_hull(bm):
    P, R, T, L, S = 0, 1, 2, 3, 4   # paint, rust, red, lens, steel
    # Hull body: one side profile spanning the hull width - belly, lower nose, upper glacis, deck, rear plate.
    extrude_yz(bm, [(-2.74, .58), (2.5, .58), (2.98, 1.22), (2.98, 1.30), (1.95, DECK), (-2.88, DECK), (-2.97, DECK - .08), (-2.97, .96)], -HULL_X, HULL_X)
    # Fenders over the tracks: long flat run, dropping at the front and the back, with a turned-down outer lip.
    for side in (-1, 1):
        x0, x1 = sorted((side * (HULL_X - .01), side * FENDER_X))
        top = [(-3.02, FENDER - .12), (-2.84, FENDER), (2.7, FENDER), (3.07, FENDER - .19)]
        extrude_yz(bm, top + [(y, z - .028) for y, z in reversed(top)], x0, x1)
        lx0, lx1 = sorted((side * (FENDER_X - .022), side * (FENDER_X + .005)))
        extrude_yz(bm, [(-2.84, FENDER - .07), (2.7, FENDER - .07), (2.7, FENDER), (-2.84, FENDER)], lx0, lx1)
        # Stowage boxes on the fender (front short, middle long), lids with a lip, rounded by the bevel below.
        for y0, y1 in ((-1.06, .82), (.86, 2.04)):
            add_box(bm, (side * BOX_X, (y0 + y1) / 2, FENDER + .19), (BOX_W, y1 - y0, .38), taper_top=(.008, .008))
            add_box(bm, (side * BOX_X, (y0 + y1) / 2, FENDER + .37), (BOX_W + .025, y1 - y0 + .025, .03))
        # Muffler covers on the rear of the fenders: a rounded hood (rust-brown).
        e = FENDER_X - .015; hood = [(1.075, FENDER), (e, FENDER), (e, FENDER + .24), (e - .055, FENDER + .31), (BOX_X, FENDER + .335), (1.13, FENDER + .31), (1.075, FENDER + .24)]
        hood = [(side * x, z) for x, z in rounded(hood, .06, 3)]
        extrude_xz(bm, hood, -2.76, -1.1, mat_index=R)
        # Headlight housings on the front of the fenders.
        add_box(bm, (side * .84, 2.78, FENDER + .1), (.34, .14, .2))
    bevel_edges(bm, .018, 2)

    for side in (-1, 1):
        # Pressed panels on the boxes: a raised plate with an X on the outer face, an X on the lid.
        for y0, y1 in ((-1.06, .82), (.86, 2.04)):
            yc, ln = (y0 + y1) / 2, y1 - y0
            ox = BOX_X + BOX_W / 2
            add_box(bm, (side * (ox + .007), yc, FENDER + .18), (.014, ln - .14, .24))
            add_x(bm, (side * (ox + .017), yc, FENDER + .18), ln - .26, .17, 'yz')
            add_x(bm, (side * BOX_X, yc, FENDER + .392), BOX_W - .16, ln - .2, 'xy')
            for yy in (y0 + .14, y1 - .14): add_box(bm, (side * (ox + .015), yy, FENDER + .33), (.03, .07, .05), mat_index=S)   # latches
        # Exhaust pipe out of the back of each muffler, dark bore.
        add_cylinder(bm, (side * 1.27, -2.88, FENDER + .14), .062, .28, 'Y', 12, mat_index=R)
        add_cylinder(bm, (side * 1.27, -3.025, FENDER + .14), .045, .01, 'Y', 12, mat_index=S)
        # Twin headlamps in their housing, a cage guard round them.
        for dx in (-.075, .075):
            add_cylinder(bm, (side * .84 + dx, 2.86, FENDER + .1), .058, .03, 'Y', 14, mat_index=S)
            add_cylinder(bm, (side * .84 + dx, 2.872, FENDER + .1), .048, .01, 'Y', 14, mat_index=L)
        gx, gz, gy = side * .84, FENDER, 2.93
        for dx in (-.19, .19): add_path(bm, [(gx + dx, 2.76, gz + .22), (gx + dx, gy, gz + .22), (gx + dx, gy, gz + .01)], .011)
        add_rod(bm, (gx - .19, gy, gz + .22), (gx + .19, gy, gz + .22), .011)
        # Tow cable along the boxes: slack between clips, an eye at each end.
        cable = [(side * 1.43, 1.95, FENDER + .4), (side * 1.47, 1.35, FENDER + .41), (side * 1.5, .85, FENDER + .4), (side * 1.47, .2, FENDER + .42),
                 (side * 1.5, -.45, FENDER + .4), (side * 1.45, -.95, FENDER + .41)]
        add_path(bm, cable, .015, 6, S)
        for end in (cable[0], cable[-1]): add_cylinder(bm, end, .045, .02, 'Z', 10, mat_index=S)
        # Rear plate: louvred grilles low on each side, tail lights in round guards, lifting eyes.
        for k in range(6): add_box(bm, (side * .58, -2.985, 1.04 + k * .055), (.42, .05, .022))
        add_box(bm, (side * .58, -2.975, 1.18), (.48, .02, .4))
        add_cylinder(bm, (side * .92, -3.0, 1.56), .05, .04, 'Y', 12, mat_index=T)
        add_cylinder(bm, (side * .92, -2.99, 1.56), .085, .03, 'Y', 14)
        add_path(bm, [(side * .84, -2.97, 1.5), (side * .84, -3.07, 1.56), (side * .92, -3.08, 1.66), (side * 1.0, -3.07, 1.56), (side * 1.0, -2.97, 1.5)], .01)
        add_cylinder(bm, (side * .98, -2.92, DECK + .03), .055, .025, 'X', 10)
        # Front: tow hooks on the nose, lifting eyes at the glacis corners.
        add_path(bm, [(side * .5, 2.9, 1.02), (side * .5, 3.04, 1.0), (side * .5, 3.06, .9), (side * .5, 2.96, .86)], .025)
        add_cylinder(bm, (side * .98, 2.2, glacis_z(2.2) + .03), .05, .025, 'X', 10)
        # Suspension: trailing arm from the hull side to each hub, bump stop above, shock absorbers front and rear.
        for i, y in enumerate(WHEELS_Y):
            add_rod(bm, (side * (HULL_X + .02), y + .34, WHEEL_Z + .2), (side * (TRACK_X - .1), y, WHEEL_Z), .055, 6)
            add_box(bm, (side * (HULL_X + .04), y + .1, WHEEL_Z + .42), (.1, .14, .09))
            if i in (0, 1, 4): add_rod(bm, (side * (HULL_X + .05), y - .2, WHEEL_Z + .52), (side * (HULL_X + .06), y + .12, WHEEL_Z + .12), .035, 8, S)
        for y in ROLLERS_Y: add_box(bm, (side * (HULL_X + .05), y, ROLLER_Z), (.12, .1, .12))
        add_cylinder(bm, (side * (HULL_X + .07), SPROCKET[0], SPROCKET[1]), .2, .14, 'X', 20)     # final drive
        add_rod(bm, (side * (HULL_X + .03), IDLER[0] - .35, IDLER[1] - .12), (side * (TRACK_X - .12), IDLER[0], IDLER[1]), .05, 6)
    # Air cleaner: perforated drum on the right front fender, its duct running back to the hull.
    add_cylinder(bm, (1.45, 2.3, FENDER + .13), .12, .38, 'Y', 18)
    for k in range(7):
        for j in range(9):
            a = math.radians(-40 + j * 28); add_cylinder(bm, (1.45 + math.cos(a) * .121, 2.15 + k * .05, FENDER + .13 + math.sin(a) * .121), .011, .006, 'Y', 6, mat_index=S)
    add_path(bm, [(1.38, 2.1, FENDER + .2), (1.25, 1.9, FENDER + .42), (1.1, 1.4, DECK + .03)], .04, 8)
    # Driver's hatch and periscopes on the glacis, left of the gun; weld seams round the plates.
    gn = Vector((0, .48, 1.03)).normalized(); rot = Vector((0, 0, 1)).rotation_difference(gn).to_matrix()
    hc = Vector((-.5, 2.14, glacis_z(2.14)))
    for v in add_cylinder(bm, (0, 0, 0), .3, .045, 'Z', 24): v.co = hc + rot @ v.co
    for v in add_cylinder(bm, (0, 0, 0), .2, .07, 'Z', 16): v.co = hc + rot @ v.co
    for dx in (-.16, .12):
        for v in add_box(bm, (0, 0, 0), (.15, .08, .1)): v.co = hc + rot @ (v.co + Vector((dx, .36, .03)))
    add_staple(bm, hc + rot @ Vector((0, -.1, .045)), .14, (1, 0, 0), gn, .03)
    for a, b in (((-HULL_X, 1.95, DECK), (HULL_X, 1.95, DECK)), ((-HULL_X, 2.98, 1.3), (HULL_X, 2.98, 1.3)), ((-HULL_X, 2.98, 1.22), (HULL_X, 2.98, 1.22)),
                 ((-HULL_X, 1.95, DECK), (-HULL_X, 2.98, 1.3)), ((HULL_X, 1.95, DECK), (HULL_X, 2.98, 1.3)), ((-HULL_X, -2.97, DECK - .08), (HULL_X, -2.97, DECK - .08))):
        add_rod(bm, a, b, .012, 5)
    # Engine deck: air grilles beside the turret bustle and across the deck behind it, access plates with handles.
    for side in (-1, 1):
        cx = side * .82
        add_box(bm, (cx, -1.25, DECK + .01), (.4, 1.2, .02), mat_index=S)
        for k in range(14): add_box(bm, (cx, -1.8 + k * .085, DECK + .03), (.38, .03, .035))
        add_box(bm, (side * .5, -2.17, DECK + .01), (.9, .42, .02), mat_index=S)
        for k in range(16): add_box(bm, (side * (.07 + k * .054), -2.17, DECK + .03), (.026, .4, .035))
        add_box(bm, (side * .5, -2.66, DECK + .012), (.86, .44, .024))
        add_staple(bm, (side * .5, -2.66, DECK + .024), .16, (1, 0, 0), (0, 0, 1), .035)
        for yy in (-2.5, -2.82): add_cylinder(bm, (side * .14, yy, DECK + .03), .022, .02, 'Z', 8)
    add_box(bm, (0, -2.43, DECK + .012), (1.95, .04, .03))
    # Rear towing pintle and the stowed tow cable eye hanging at the left rear corner.
    add_box(bm, (0, -3.0, 1.08), (.2, .1, .14)); add_cylinder(bm, (0, -3.07, 1.08), .07, .06, 'Z', 12, mat_index=S)
    add_torus(bm, (-1.18, -3.02, 1.3), .12, .025, 'Y')
    add_rod(bm, (-1.18, -3.02, 1.42), (-1.18, -2.98, 1.62), .03, 8)

def add_torus(bm, center, R, r, axis='Y', seg=18, tube=6, mat_index=0):
    c = Vector(center)
    ax = {'X': Vector((1, 0, 0)), 'Y': Vector((0, 1, 0)), 'Z': Vector((0, 0, 1))}[axis]
    u = ax.orthogonal().normalized(); v = ax.cross(u)
    rings = []
    for i in range(seg):
        a = 2 * math.pi * i / seg; d = u * math.cos(a) + v * math.sin(a)
        rings.append([bm.verts.new(c + d * (R + r * math.cos(2 * math.pi * k / tube)) + ax * r * math.sin(2 * math.pi * k / tube)) for k in range(tube)])
    for i in range(seg):
        A, B = rings[i], rings[(i + 1) % seg]
        for k in range(tube):
            j = (k + 1) % tube; bm.faces.new((A[k], A[j], B[j], B[k])).material_index = mat_index

hull = mesh("Hull", build_hull, [PAINT, RUST, RED, LENS, STEEL], root)

# ---------------------------------------------------------------- running gear: turned profiles
def disc_profile(R, a, h, outward, hub=True):
    """One wheel disc of radius R centred at axial a, half-width h, its dished face toward 'outward' (+1/-1):
    tyre, rim lip, dish, hub boss. Radii are for a 0.33 m wheel and scale with R."""
    s = R / .33; o = outward
    def p(r, d): return (r * s, a + o * d)
    pts = [p(0, -(h - .03)), p(.15, -(h - .03)), p(.215, -(h - .006)), p(.238, -h), p(.316, -h), p(.33, -(h - .022)),
           p(.33, h - .022), p(.316, h), p(.238, h), p(.226, h - .016), p(.2, h - .05), p(.14, h - .062), p(.125, h - .05)]
    pts += [p(.12, h - .01), p(.112, h + .012), p(.05, h + .016), p(.048, h + .045), p(0, h + .045)] if hub else [p(.08, h - .03), p(0, h - .03)]
    return pts

def tyre_or_steel(R):
    return lambda r0, r1: 1 if min(r0, r1) >= .238 * R / .33 - 1e-6 else 0

def wheel_builder(R, side, dual=True, h=.085, gap=.125):
    def build(bm):
        if dual:
            for a in (gap, -gap):
                lathe(bm, disc_profile(R, side * a, h, side if a > 0 else -side, hub=a > 0), (0, 0, 0), 'X', 32, tyre_or_steel(R))
            add_cylinder(bm, (side * (gap + h + .055), 0, 0), .03 * R / .33, .025, 'X', 8, mat_index=2)       # hub nut
            for k in range(8):                                                                                 # cap bolts
                ang = k * math.pi / 4; rr = .085 * R / .33
                add_cylinder(bm, (side * (gap + h + .016), math.cos(ang) * rr, math.sin(ang) * rr), .008, .01, 'X', 6, mat_index=2)
        else:
            lathe(bm, disc_profile(R, 0, h, side), (0, 0, 0), 'X', 24, tyre_or_steel(R))
    return build

def sprocket_builder(R, side, teeth=13):
    def build(bm):
        for a in (.075, -.075):
            o = 1 if a * side > 0 else -1          # each disc dished away from the other
            lathe(bm, [(0, a - o * .03), (R * .6, a - o * .03), (R * .86, a - o * .02), (R * .86, a + o * .02), (R * .6, a + o * .012),
                       (R * .3, a - o * .005), (R * .26, a + o * .045), (0, a + o * .045)],
                  (0, 0, 0), 'X', 32, lambda r0, r1: 1 if max(r0, r1) <= R * .6 + 1e-6 and min(r0, r1) >= R * .3 - 1e-6 else 0)
            for i in range(teeth):                                                     # teeth: tapered blocks round the rim
                ang = 2 * math.pi * (i + .5 * (a > 0)) / teeth
                base, tip = R * .84, R * 1.08
                pts = []
                for rr, w in ((base, .065), (tip, .028)):
                    for sgn in (-1, 1):
                        t = ang + sgn * w / rr
                        pts.append((math.cos(t) * rr, math.sin(t) * rr))
                q = [pts[0], pts[1], pts[3], pts[2]]
                loft(bm, [[Vector((a - .02, y, z)) for y, z in q], [Vector((a + .02, y, z)) for y, z in q]])
        add_cylinder(bm, (side * .14, 0, 0), R * .28, .06, 'X', 16)                     # hub
        for k in range(8):
            ang = k * math.pi / 4; add_cylinder(bm, (side * .175, math.cos(ang) * R * .2, math.sin(ang) * R * .2), .014, .02, 'X', 6, mat_index=1)
    return build

for side, tag in ((-1, "L"), (1, "R")):
    x = side * TRACK_X
    wheel_data = None
    for i, y in enumerate(WHEELS_Y, 1):
        if wheel_data is None:
            w = mesh(f"Wheel_{tag}{i}", wheel_builder(WHEEL_R, side), [PAINT, RUBBER, STEEL], root, (x, y, WHEEL_Z), True); wheel_data = w.data
        else:
            obj_from_bm(f"Wheel_{tag}{i}", None, None, root, (x, y, WHEEL_Z), True, data=wheel_data)
    roller_data = None
    for i, y in enumerate(ROLLERS_Y, 1):
        if roller_data is None:
            r = mesh(f"Roller_{tag}{i}", wheel_builder(ROLLER_R, side, h=.045, gap=.08), [PAINT, RUBBER, STEEL], root, (x, y, ROLLER_Z), True); roller_data = r.data
        else:
            obj_from_bm(f"Roller_{tag}{i}", None, None, root, (x, y, ROLLER_Z), True, data=roller_data)
    mesh(f"Sprocket_{tag}", sprocket_builder(SPROCKET[2], side), [PAINT, STEEL], root, (x, SPROCKET[0], SPROCKET[1]), True)
    mesh(f"Idler_{tag}", wheel_builder(IDLER[2], side), [PAINT, RUBBER, STEEL], root, (x, IDLER[0], IDLER[1]), True)

# ---------------------------------------------------------------- tracks: links with grousers, end connectors and guide horns
LINK_T = .024   # half the shoe thickness
def track_path():
    """Closed loop (y, z) around sprocket, under the road wheels, round the idler and back over the rollers."""
    pts = []
    def arc(cy, cz, r, a0, a1, n):
        for i in range(n + 1):
            a = a0 + (a1 - a0) * i / n; pts.append((cy + math.cos(a) * r, cz + math.sin(a) * r))
    t = LINK_T + .004
    ground = WHEEL_Z - WHEEL_R - t
    top = ROLLER_Z + ROLLER_R + t
    arc(SPROCKET[0], SPROCKET[1], SPROCKET[2] * .9 + t, math.radians(100), math.radians(250), 10)  # rear, down
    for y in (-2.2,) + WHEELS_Y + (2.15,): pts.append((y, ground + (.03 if y in (-2.2, 2.15) else 0)))   # ground run
    arc(IDLER[0], IDLER[1], IDLER[2] + t, math.radians(-62), math.radians(88), 10)                   # front, up
    for y, sag in ((2.0, .01), (1.28, 0), (.62, .022), (-.05, 0), (-.72, .022), (-1.38, 0), (-2.1, .015)):
        pts.append((y, top - sag))                                                                   # top run over rollers
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
LINK_MESH = None
def link_mesh():
    """One shoe in link space: x across, y along the run, z out of the loop (toward the ground on the ground run)."""
    global LINK_MESH
    if LINK_MESH: return LINK_MESH
    bm = bmesh.new()
    add_box(bm, (0, 0, 0), (TRACK_W - .1, .128, LINK_T * 2))
    bevel_edges(bm, .008, 1)
    for sgn in (-1, 1):                                                       # grouser spikes, staggered halves
        x0, x1 = (0, sgn * (TRACK_W / 2 - .05))
        xa, xb = sorted((x0, x1)); yc = .012 * sgn
        sp = [bm.verts.new(c) for c in ((xa, yc - .055, LINK_T), (xb, yc - .055, LINK_T), (xb, yc + .055, LINK_T), (xa, yc + .055, LINK_T),
                                        (xa, yc, LINK_T + .05), (xb, yc, LINK_T + .05))]
        for f in ((0, 1, 5, 4), (2, 3, 4, 5), (0, 4, 3), (1, 2, 5), (3, 2, 1, 0)): bm.faces.new([sp[i] for i in f])
        add_box(bm, (sgn * (TRACK_W / 2 - .025), .064, 0), (.05, .06, .06))  # end connectors over the pins
    add_box(bm, (0, 0, -LINK_T - .035), (.05, .07, .07), taper_top=(.01, .015))   # centre guide horn, between the twin discs
    add_cylinder(bm, (0, .064, 0), .016, TRACK_W - .09, 'X', 6)               # pin
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("M41_TrackLink"); bm.to_mesh(me); bm.free(); me.materials.append(TRACK)
    LINK_MESH = me; return me

def build_track(side, tag):
    """Track_L/R is the link holder; Link_<tag>_000.. follow the loop in order (rear, ground run, front, top run)."""
    holder = empty(f"Track_{tag}", (side * TRACK_X, 0, 0), root)
    center = Vector((0, sum(p.y for p, _ in LINKS) / len(LINKS), sum(p.z for p, _ in LINKS) / len(LINKS)))
    for i, (p, tang) in enumerate(LINKS):
        out = Vector((0, -tang.z, tang.y))
        if out.dot(p - center) < 0: out = -out
        basis = Matrix((Vector((1, 0, 0)), tang, out)).transposed()
        o = bpy.data.objects.new(f"Link_{tag}_{i:03d}", link_mesh()); col.objects.link(o)
        o.matrix_world = Matrix.Translation(Vector((side * TRACK_X, p.y, p.z))) @ basis.to_4x4()
        set_parent(o, holder)
for side, tag in ((-1, "L"), (1, "R")):
    build_track(side, tag)

# ---------------------------------------------------------------- turret: a cast shell lofted from smooth sections
RING = Vector((0, 0, DECK))
WIDTH = [(-1.92, .95), (-1.3, 1.0), (-1.0, 1.03), (-.4, 1.07), (.2, 1.07), (.7, 1.0), (1.15, .85), (1.52, .72)]   # half-width along y

def turret_height(y): return .78 - .2 * smoothstep(.35, 1.52, y)

def turret_shape(y):
    """Half-widths at the bottom, belly and roof, the bottom height and shoulder radius at y. Behind the bustle
    seam the sides straighten and the undercut goes: the bustle is a box welded to the cast turret."""
    W = catmull(WIDTH, y); H = turret_height(y); box = smoothstep(-1.28, -1.42, y)
    return W, H, W - (.15 - .11 * box), W - (.26 - .2 * box), .09 - .04 * box, .08 * box

def turret_section(y):
    """Cross-section at y: undercut belly, sides sloping in, a rounded shoulder into the flat roof."""
    W, H, wb, wt, rr, z0 = turret_shape(y); belly = .2
    right = [(wb, z0)] + bez((wb, z0), (W - .005, z0 + .035), (W, belly), 3)
    C, B = Vector((wt, H)), Vector((W, belly)); S = C + (B - C).normalized() * rr; E = C + Vector((-rr, 0))
    right += [(S.x, S.y)] + bez((S.x, S.y), (C.x, C.y), (E.x, E.y), 4) + [(E.x * .4, H)]
    ring = right + [(-x, z) for x, z in reversed(right)]
    return [RING + Vector((x, y, z)) for x, z in ring]

def turret_side(y, z, side):
    """A point on the sloping side wall at (y, z) and its outward normal."""
    W, H, wb, wt, rr, z0 = turret_shape(y)
    t = max(0, min(1, (z - .2) / (H - .2))); x = W + (wt - W) * t
    n = Vector((H - .2, 0, W - wt)).normalized()
    return RING + Vector((side * x, y, z)), Vector((side * n.x, 0, n.z))

def build_turret(bm):
    P, C, S = 0, 1, 2
    # One cast shell from the bustle (overhanging the engine deck) to the face behind the mantlet.
    ys = [-1.92, -1.86, -1.6, -1.32, -1.0, -.8, -.6, -.4, -.2, 0, .2, .4, .55, .7, .82, .94, 1.05, 1.16, 1.26, 1.35, 1.43, 1.52]
    loft(bm, [turret_section(y) for y in ys])
    bevel_edges(bm, .03, 3, 40)
    seam = turret_section(-1.33); add_path(bm, seam + seam[:1], .009, 5)    # where the bustle is welded on
    for k in range(3): add_cylinder(bm, RING + Vector((.45 - k * .45, -1.93, .32)), .03, .02, 'Y', 8, mat_index=S)
    # Turret ring skirt under the overhang (in shadow).
    add_cylinder(bm, RING + Vector((0, 0, -.015)), .98, .05, 'Z', 32, mat_index=S)
    # Ventilator dome with six bolts at the rear of the roof.
    lathe(bm, [(0, .815), (.12, .81), (.2, .79), (.21, .77), (0, .77)], RING + Vector((0, -1.05, 0)), 'Z', 24)
    for k in range(6): add_cylinder(bm, RING + Vector((math.cos(k * math.pi / 3) * .155, -1.05 + math.sin(k * math.pi / 3) * .155, .81)), .016, .02, 'Z', 6, mat_index=S)
    # Lifting eyes at the roof corners; grab staples in two rows down each side; a rail round the bustle.
    for sx, sy in ((-.6, .62), (.6, .62), (-.6, -1.25), (.6, -1.25)):
        add_torus(bm, RING + Vector((sx, sy, .8)), .05, .014, 'Y', 12, 5)
    for side in (-1, 1):
        for y in (.75, .45, .1, -.3, -.7, -1.05):
            for z in (.36, .58):
                p, n = turret_side(y, z, side); add_staple(bm, p, .13, (0, 1, 0), n, .03, .01)
        rail = [turret_side(y, .6, side) for y in (-1.38, -1.6, -1.9)]
        add_path(bm, [p + n * .05 for p, n in rail] + [RING + Vector((side * .62, -1.99, .6))], .014)
        for p, n in rail: add_rod(bm, p, p + n * .05, .011)
    add_path(bm, [RING + Vector((-.62, -1.99, .6)), RING + Vector((.62, -1.99, .6))], .014)
    for x in (-.4, .4): add_rod(bm, RING + Vector((x, -1.99, .6)), RING + Vector((x, -1.93, .6)), .011)
    # Sight hoods on the front of the roof.
    add_box(bm, RING + Vector((.42, .3, .83)), (.26, .2, .12), taper_top=(.02, .03))
    add_box(bm, RING + Vector((.42, .41, .82)), (.2, .02, .06), mat_index=S)
    add_box(bm, RING + Vector((-.5, .45, .81)), (.16, .14, .08), taper_top=(.01, .02))
    # Mantlet: a canvas-covered rounded shield across the turret face, studs along its edges, the gun boss.
    M = RING + Vector((0, 1.6, .35)); Rm, a, b = .3, .41, .16
    prof = [(0, -(a + b))] + [(Rm * math.sin(t), -a - b * math.cos(t)) for t in (math.radians(d) for d in (25, 50, 70, 90))]
    prof += [(Rm, -a / 2), (Rm, 0), (Rm, a / 2)] + [(Rm * math.sin(t), a + b * math.cos(t)) for t in (math.radians(d) for d in (90, 70, 50, 25))] + [(0, a + b)]
    first = len(bm.verts)
    lathe(bm, prof, M, 'X', 28, lambda r0, r1: C)
    bm.verts.ensure_lookup_table()
    for v in bm.verts[first:]:
        d = v.co - M; d.y *= .7
        w = noise.noise(Vector((v.co.x * 7, v.co.y * 7, v.co.z * 7))) * .012
        v.co = M + d + (d.normalized() * w if d.length > 1e-4 else Vector())
    for e in (-52, 52):
        for k in range(9):
            x = -.4 + k * .1; er = math.radians(e)
            add_cylinder(bm, M + Vector((x, Rm * .7 * math.cos(er) + .005, Rm * math.sin(er))), .016, .03, 'Y', 6, mat_index=S)
    lathe(bm, [(0, .16), (.1, .16), (.15, .15), (.16, .1), (.16, 0), (0, 0)], M + Vector((0, .08, 0)), 'Y', 24, lambda r0, r1: C)
    add_cylinder(bm, M + Vector((.26, .19, -.03)), .035, .05, 'Y', 10, mat_index=S)          # coax MG port
    add_box(bm, M + Vector((-.3, .15, .17)), (.11, .06, .07), mat_index=S)                    # sight port
turret = mesh("Turret", build_turret, [PAINT, CANVAS, STEEL], root, RING)

TRUNNION = RING + Vector((0, 1.6, .35))
def build_gun(bm):
    add_cylinder(bm, TRUNNION + Vector((0, .32, 0)), .1, .36, 'Y', 20)                     # sleeve, mostly in the mantlet
    lathe(bm, [(0, .4), (.072, .4), (.07, .9), (.064, 2.2), (.06, 3.4), (.062, 3.45), (0, 3.45)], TRUNNION, 'Y', 20)   # barrel
    lathe(bm, [(0, 3.43), (.08, 3.43), (.088, 3.47), (.088, 3.62), (.08, 3.65), (0, 3.65)], TRUNNION, 'Y', 20)  # muzzle brake
    for side in (-1, 1): add_box(bm, TRUNNION + Vector((side * .1, 3.55, 0)), (.07, .16, .13))      # brake baffles (T)
    add_cylinder(bm, TRUNNION + Vector((0, 3.655, 0)), .036, .012, 'Y', 14, mat_index=1)           # bore
gun = mesh("Gun", build_gun, [PAINT, STEEL], turret, TRUNNION)
empty("Gun_Muzzle", TRUNNION + Vector((0, 3.7, 0)), gun)

CUPOLA = RING + Vector((.36, -.3, .78))
def build_cupola(bm):
    lathe(bm, [(0, -.03), (.38, -.03), (.38, .05), (.36, .17), (.33, .19), (.3, .19), (0, .19)], CUPOLA, 'Z', 32)
    for i in range(6):                                                                   # vision blocks
        a = 2 * math.pi * i / 6 + .3; d = Vector((math.cos(a), math.sin(a), 0))
        for v in add_box(bm, (0, 0, 0), (.06, .14, .08)): v.co = CUPOLA + d * .37 + Vector((0, 0, .11)) + Matrix.Rotation(a, 3, 'Z') @ v.co
        for v in add_box(bm, (0, 0, 0), (.02, .1, .045), mat_index=1): v.co = CUPOLA + d * .4 + Vector((0, 0, .11)) + Matrix.Rotation(a, 3, 'Z') @ v.co
cupola = mesh("Cupola", build_cupola, [PAINT, STEEL], turret, CUPOLA)
HINGE_C = CUPOLA + Vector((0, -.31, .2))
def build_hatch_c(bm):
    lathe(bm, [(0, .27), (.16, .265), (.27, .235), (.305, .205), (.305, .185), (0, .185)], CUPOLA, 'Z', 32)
    add_staple(bm, CUPOLA + Vector((-.09, .05, .265)), .16, (1, 0, 0), (0, 0, 1), .03)
    add_cylinder(bm, HINGE_C, .035, .3, 'X', 10)
    add_box(bm, CUPOLA + Vector((0, -.26, .215)), (.22, .1, .04))
mesh("Hatch_Commander", build_hatch_c, [PAINT], cupola, HINGE_C)

LOADER = RING + Vector((-.38, -.12, .78))
HINGE_L = LOADER + Vector((-.3, 0, .03))
def build_hatch_l(bm):
    oct8 = [Vector((math.cos(2 * math.pi * i / 8 + math.pi / 8) * .29, math.sin(2 * math.pi * i / 8 + math.pi / 8) * .29, 0)) + LOADER for i in range(8)]
    top8 = [LOADER + (v - LOADER) * .93 + Vector((0, 0, .055)) for v in oct8]
    loft(bm, [oct8, [v + Vector((0, 0, .035)) for v in oct8], top8])
    add_staple(bm, LOADER + Vector((.08, -.08, .055)), .14, (0, 1, 0), (0, 0, 1), .03)
    add_cylinder(bm, HINGE_L, .03, .3, 'Y', 8)
mesh("Hatch_Loader", build_hatch_l, [PAINT], turret, HINGE_L)

MOUNT = RING + Vector((-.1, -.62, .78))
def build_mount(bm):
    add_cylinder(bm, MOUNT + Vector((0, 0, .02)), .1, .04, 'Z', 14)
    add_cylinder(bm, MOUNT + Vector((0, 0, .2)), .045, .36, 'Z', 12)
    add_box(bm, MOUNT + Vector((0, 0, .38)), (.12, .12, .07))
mount = mesh("MG_Mount", build_mount, [GUNMETAL], turret, MOUNT)
MG_PIVOT = MOUNT + Vector((0, 0, .42))
def build_mg(bm):
    S = 1
    add_box(bm, MG_PIVOT + Vector((0, -.05, .07)), (.12, .56, .15))                        # receiver
    for k in range(6): add_box(bm, MG_PIVOT + Vector((.061, -.25 + k * .07, .1)), (.004, .045, .03), mat_index=S)   # receiver slots
    add_path(bm, [MG_PIVOT + Vector((-.04, -.33, .05)), MG_PIVOT + Vector((-.04, -.43, .03))], .016, 6)   # spade grips
    add_path(bm, [MG_PIVOT + Vector((.04, -.33, .05)), MG_PIVOT + Vector((.04, -.43, .03))], .016, 6)
    add_cylinder(bm, MG_PIVOT + Vector((0, .44, .08)), .045, .4, 'Y', 14)                # barrel jacket
    for k in range(7):
        for j in range(4):
            a = math.radians(-60 + j * 40)
            add_cylinder(bm, MG_PIVOT + Vector((math.cos(a) * .046, .3 + k * .045, .08 + math.sin(a) * .046)), .011, .006, 'X', 6, mat_index=S)
    add_cylinder(bm, MG_PIVOT + Vector((0, .9, .08)), .022, .55, 'Y', 10)                # barrel
    add_cylinder(bm, MG_PIVOT + Vector((0, 1.18, .08)), .03, .04, 'Y', 10)               # flash hider
    add_box(bm, MG_PIVOT + Vector((.11, -.02, .02)), (.09, .22, .13), mat_index=2)        # ammo can (painted)
mg = mesh("MG", build_mg, [GUNMETAL, STEEL, PAINT], mount, MG_PIVOT)
empty("MG_Muzzle", MG_PIVOT + Vector((0, 1.2, .08)), mg)
antenna = mesh("Antenna", lambda bm: (add_cylinder(bm, RING + Vector((.55, -1.6, .82)), .04, .1, 'Z', 10),
                                      add_cylinder(bm, RING + Vector((.55, -1.6, .95)), .012, .2, 'Z', 6)), [STEEL], turret)

# ---------------------------------------------------------------- worn look, baked to one atlas for Unity
def worn(mat, dirt, wear, bump, edge=.45, occlusion=.72, dust=.22):
    nt = mat.node_tree; n, l = nt.nodes, nt.links; b = n["Principled BSDF"]
    coord = n.new("ShaderNodeTexCoord"); geo = n.new("ShaderNodeNewGeometry")
    base = n.new("ShaderNodeRGB"); base.outputs[0].default_value = b.inputs["Base Color"].default_value
    c = base.outputs[0].default_value
    def mix(a_socket, colour, fac_socket):
        m = n.new("ShaderNodeMix"); m.data_type = 'RGBA'; l.new(a_socket, m.inputs[6]); m.inputs[7].default_value = colour
        l.new(fac_socket, m.inputs[0]); return m.outputs[2]
    def rng(src, a, b_, to_a, to_b):
        r = n.new("ShaderNodeMapRange"); r.inputs["From Min"].default_value = a; r.inputs["From Max"].default_value = b_
        r.inputs["To Min"].default_value = to_a; r.inputs["To Max"].default_value = to_b; l.new(src, r.inputs["Value"]); return r.outputs["Result"]
    def tex(scale, detail=4):
        t = n.new("ShaderNodeTexNoise"); t.inputs["Scale"].default_value = scale; t.inputs["Detail"].default_value = detail
        l.new(coord.outputs["Object"], t.inputs["Vector"]); return t.outputs["Fac"]
    # Faded paint in soft blotches.
    col_ = mix(base.outputs[0], (min(1, c[0] * 1.5), min(1, c[1] * 1.45), min(1, c[2] * 1.38), 1), rng(tex(3.2, 8), .42, .72, 0, wear))
    col_ = mix(col_, (c[0] * .62, c[1] * .64, c[2] * .62, 1), rng(tex(14, 8), .38, .68, 0, .45))
    col_ = mix(col_, (min(1, c[0] * 1.9 + .02), min(1, c[1] * 1.85 + .02), min(1, c[2] * 1.75 + .02), 1), rng(tex(70, 2), .62, .72, 0, .35))
    # Dust settles on what faces up.
    sep_n = n.new("ShaderNodeSeparateXYZ"); l.new(geo.outputs["Normal"], sep_n.inputs["Vector"])
    g = (c[0] + c[1] + c[2]) / 3
    col_ = mix(col_, (min(1, g * 1.55 + .03), min(1, g * 1.5 + .028), min(1, g * 1.35 + .02), 1), rng(sep_n.outputs["Z"], .35, 1, 0, dust))
    # Mud low down, broken up.
    sep = n.new("ShaderNodeSeparateXYZ"); l.new(geo.outputs["Position"], sep.inputs["Vector"])
    mask = n.new("ShaderNodeMath"); mask.operation = 'MULTIPLY'
    l.new(rng(sep.outputs["Z"], 0, 1.4, dirt, 0), mask.inputs[0]); l.new(tex(9), mask.inputs[1])
    col_ = mix(col_, (.15, .115, .08, 1), mask.outputs[0])
    # Worn edges: the bevel normal departs from the true normal near an edge, whatever the mesh density.
    bev = n.new("ShaderNodeBevel"); bev.samples = 16; bev.inputs["Radius"].default_value = .035
    dot = n.new("ShaderNodeVectorMath"); dot.operation = 'DOT_PRODUCT'
    l.new(bev.outputs["Normal"], dot.inputs[0]); l.new(geo.outputs["Normal"], dot.inputs[1])
    edge_mask = n.new("ShaderNodeMath"); edge_mask.operation = 'MULTIPLY'
    l.new(rng(dot.outputs["Value"], .9, .994, edge, 0), edge_mask.inputs[0]); l.new(rng(tex(22, 6), .3, .7, .7, 1), edge_mask.inputs[1])
    col_ = mix(col_, (min(1, c[0] * 2.4 + .06), min(1, c[1] * 2.3 + .06), min(1, c[2] * 2.2 + .055), 1), edge_mask.outputs[0])
    # Cavities: occlusion darkens the crevices and collects grime there.
    ao = n.new("ShaderNodeAmbientOcclusion"); ao.samples = 16; ao.inputs["Distance"].default_value = .5
    col_ = mix(col_, (.035, .032, .027, 1), rng(ao.outputs["AO"], .4, .95, .72, 0))
    lit = n.new("ShaderNodeMix"); lit.data_type = 'RGBA'; lit.blend_type = 'MULTIPLY'; lit.inputs[0].default_value = 1
    l.new(col_, lit.inputs[6]); l.new(rng(ao.outputs["AO"], 0, 1, 1 - occlusion, 1), lit.inputs[7])
    l.new(lit.outputs[2], b.inputs["Base Color"])
    bp = n.new("ShaderNodeBump"); bp.inputs["Strength"].default_value = bump; bp.inputs["Distance"].default_value = .004
    l.new(tex(45), bp.inputs["Height"]); l.new(bp.outputs["Normal"], b.inputs["Normal"])
worn(PAINT, .5, .42, .14, .95, .8, .3); worn(STEEL, .5, .3, .2, .6, .6, .1); worn(GUNMETAL, .2, .2, .1, .5, .5, .05)
worn(TRACK, .4, .6, .3, .95, .62, .3); worn(RUBBER, .35, .2, .1, .3, .5, .15); worn(CANVAS, .25, .5, .35, .35, .55, .12)
worn(RUST, .3, .7, .25, .6, .65, .2)

meshes = [o for o in col.objects if o.type == 'MESH']
seen = set(); uv_meshes = []
for o in meshes:                       # one object per mesh: shared wheels and links are unwrapped and baked once
    if o.data.name in seen: continue
    seen.add(o.data.name); uv_meshes.append(o)
for o in uv_meshes:
    for o2 in col.objects: o2.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(38))
print("triangles", sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes), "objects", len(meshes))

if not QUICK:
    for o2 in col.objects: o2.select_set(False)
    for o in uv_meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = hull
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.003, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=.002); bpy.ops.object.mode_set(mode='OBJECT')

    images = {k: bpy.data.images.new("M41_Tank_" + k, 2048, 2048, alpha=False) for k in ("Albedo", "Normal")}
    images["Normal"].colorspace_settings.name = 'Non-Color'
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 24; scene.cycles.device = 'CPU'; scene.render.bake.margin = 4
    def target(kind):
        for mat in bpy.data.materials:
            if not mat.use_nodes or not mat.name.startswith("M41_"): continue
            node = mat.node_tree.nodes.get("BakeTarget") or mat.node_tree.nodes.new("ShaderNodeTexImage")
            node.name = "BakeTarget"; node.image = images[kind]; mat.node_tree.nodes.active = node
    scene.render.bake.use_pass_direct = scene.render.bake.use_pass_indirect = False; scene.render.bake.use_pass_color = True
    for o2 in col.objects: o2.select_set(False)
    for o in uv_meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = hull
    target("Albedo"); bpy.ops.object.bake(type='DIFFUSE')
    target("Normal"); bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT')
    os.makedirs(OUT, exist_ok=True)
    for k, img in images.items():
        img.filepath_raw = os.path.join(OUT, "M41_Tank_" + k + ".png"); img.file_format = 'PNG'; img.save(); img.pack()
        os.remove(img.filepath_raw)
    game = bpy.data.materials.new("M41_Worn"); game.use_nodes = True; gn, gl = game.node_tree.nodes, game.node_tree.links
    gb = gn["Principled BSDF"]; gb.inputs["Roughness"].default_value = .62
    ta = gn.new("ShaderNodeTexImage"); ta.image = images["Albedo"]; gl.new(ta.outputs["Color"], gb.inputs["Base Color"])
    tn = gn.new("ShaderNodeTexImage"); tn.image = images["Normal"]; nm = gn.new("ShaderNodeNormalMap"); gl.new(tn.outputs["Color"], nm.inputs["Color"]); gl.new(nm.outputs["Normal"], gb.inputs["Normal"])
    for me in {o.data for o in meshes}:
        me.materials.clear(); me.materials.append(game)

# ---------------------------------------------------------------- review renders: soft studio light, the reference's angles
for o in col.objects: o.select_set(False)
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
scene.view_settings.view_transform = 'Standard'
world = bpy.data.worlds.new("World"); scene.world = world; world.use_nodes = True
wn, wl = world.node_tree.nodes, world.node_tree.links
light_bg = wn["Background"]; light_bg.inputs["Color"].default_value = (.55, .56, .58, 1); light_bg.inputs["Strength"].default_value = .42
dark_bg = wn.new("ShaderNodeBackground"); dark_bg.inputs["Color"].default_value = (.036, .036, .04, 1)
path = wn.new("ShaderNodeLightPath"); pick = wn.new("ShaderNodeMixShader")
wl.new(path.outputs["Is Camera Ray"], pick.inputs[0]); wl.new(light_bg.outputs[0], pick.inputs[1]); wl.new(dark_bg.outputs[0], pick.inputs[2])
wl.new(pick.outputs[0], wn["World Output"].inputs["Surface"])
def lamp(name, rot, energy, angle):
    s = bpy.data.objects.new(name, bpy.data.lights.new(name, 'SUN')); col.objects.link(s)
    s.rotation_euler = [math.radians(a) for a in rot]; s.data.energy = energy; s.data.angle = math.radians(angle)
lamp("Key", (38, 0, 30), 2.5, 25); lamp("Fill", (60, 0, 200), .6, 60)
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); col.objects.link(cam); scene.camera = cam
VIEWS = {   # name: (location, look-at, lens) - matched to the reference screenshots
    "side": ((10.5, .4, 3.0), (0, .2, 1.35), 50),          # right side, slightly above
    "side_left": ((-8.6, 4.6, 1.9), (0, -.2, 1.25), 42),   # left side from the front, low
    "front": ((0, 10.5, 3.3), (0, 0, 1.4), 50),
    "rear": ((0, -9.6, 3.6), (0, -.3, 1.5), 50),
    "top": ((0, .35, 17), (0, .35, 0), 50),
    "turret": ((-3.0, -4.6, 5.6), (.1, -.15, 2.35), 45),   # the roof from behind on the left
    "wheels": ((9.6, -.1, 2.2), (0, -.1, .85), 50),
}
scene.render.resolution_x, scene.render.resolution_y = 1100, 640
for label, (loc, at, lens) in VIEWS.items():
    cam.location = loc; d = Vector(at) - Vector(loc)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler() if label != "top" else (0, 0, math.pi)
    cam.data.lens = lens
    scene.render.resolution_x, scene.render.resolution_y = (640, 1100) if label == "top" else (1100, 640)
    scene.render.filepath = os.path.join(OUT, f"M41_preview_{label}.png"); bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "M41_Tank.blend"))
print("saved for review", OUT)
