"""Blender proposal only. Never writes to Assets or a Unity scene."""
from pathlib import Path
import bpy, math, json
from mathutils import Vector, Matrix
OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[1]
OUT=OUT/'Revision2'; OUT.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
S=bpy.context.scene
S.render.engine='CYCLES'; S.cycles.samples=24; S.cycles.use_denoising=True
S.render.resolution_x=1200; S.render.resolution_y=1000; S.render.resolution_percentage=100
S.world=bpy.data.worlds.new('Studio'); S.world.use_nodes=True
S.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.20,.23,1)
S.world.node_tree.nodes['Background'].inputs[1].default_value=.4
S.unit_settings.system='METRIC'
def mat(n,c,metal=0,grain=False):
 m=bpy.data.materials.new(n); m.diffuse_color=(*c,1); m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*c,1); p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=.65
 if grain:
  ns=m.node_tree.nodes; lk=m.node_tree.links
  tc=ns.new('ShaderNodeTexCoord'); v=ns.new('ShaderNodeVectorMath'); v.operation='MULTIPLY'; v.inputs[1].default_value=(3,65,8); lk.new(tc.outputs['Generated'],v.inputs[0])
  noise=ns.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value=3; lk.new(v.outputs[0],noise.inputs[0])
  ramp=ns.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].color=(*(a*.5 for a in c),1); ramp.color_ramp.elements[1].color=(*(a*1.4 for a in c),1)
  lk.new(noise.outputs['Fac'],ramp.inputs[0]); lk.new(ramp.outputs[0],p.inputs['Base Color'])
  bump=ns.new('ShaderNodeBump'); bump.inputs['Strength'].default_value=.18; bump.inputs['Distance'].default_value=.006; lk.new(noise.outputs['Fac'],bump.inputs['Height']); lk.new(bump.outputs[0],p.inputs['Normal'])
 return m
wood=mat('Walnut brown timber',(.075,.029,.012),grain=True); steel=mat('Worn dark iron',(.075,.085,.07),.7)
olive=mat('Olive enamel',(.18,.22,.085),.35); khaki=mat('Pith fabric',(.26,.29,.13),grain=True); brass=mat('Brass fittings',(.40,.28,.10),.7)
paper=mat('Yellowed paper',(.66,.61,.44)); ink=mat('Ink',(.08,.12,.10)); rubber=mat('Black rubber',(.025,.03,.025))
cratewood=mat('Faded olive painted wooden ammunition crate',(.115,.145,.066),grain=True)
exposed=mat('Exposed scuffed crate wood',(.26,.20,.10),grain=True)
helmetgreen=mat('Green pith helmet canvas',(.11,.24,.035))
helmetgreen.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.48
badge=mat('Red enamel badge',(.36,.012,.009),.3)
gold=mat('Badge gold',(.62,.40,.065),.65)
hazard=mat('Faded orange diamond label',(.48,.16,.025))
def group(n):
 o=bpy.data.objects.new(n,None); bpy.context.collection.objects.link(o); return o
def box(n,p,d,m,root=None):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p); o=bpy.context.object; o.name=n; o.dimensions=d
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True); o.data.materials.append(m); o.parent=root
 b=o.modifiers.new('Soft worn edges','BEVEL'); b.width=min(.012,min(d)/5); b.segments=3
 o.modifiers.new('Weighted normals','WEIGHTED_NORMAL'); return o
def text(n,p,size,root,rot=(math.pi/2,0,0)):
 bpy.ops.object.text_add(location=p,rotation=rot); o=bpy.context.object; o.name=n; o.data.body=n; o.data.size=size; o.data.extrude=.0002; o.data.materials.append(paper); o.parent=root
def cyl(n,p,r,d,m,root,rot=(0,0,0)):
 bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=r,depth=d,location=p,rotation=rot); o=bpy.context.object; o.name=n; o.parent=root; o.data.materials.append(m); return o
rack=group('01 AK47 rack - original player mesh')
for x in [-.79,.79]: box('Rack upright',(x,.10,.70),(.10,.16,1.4),wood,rack)
for z in [.08,.35,1.04]: box('Rack cross rail',(0,.08,z),(1.7,.16,.10),wood,rack)
box('Broad stable base',(0,0,.045),(1.85,.55,.09),wood,rack)
before=set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'Assets/_Project/Art/Items/ak-47.glb'))
imported=set(bpy.data.objects)-before
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
parts=[o for o in imported if o.type=='MESH']
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
source=bpy.context.object
verts=[source.matrix_world@v.co for v in source.data.vertices]
lo=Vector(tuple(min(v[k] for v in verts) for k in range(3))); hi=Vector(tuple(max(v[k] for v in verts) for k in range(3)))
long=max(range(3),key=lambda k:hi[k]-lo[k]); rotation=Vector(tuple(1 if k==long else 0 for k in range(3))).rotation_difference(Vector((0,0,1))).to_matrix()
scale=1.02/(hi[long]-lo[long]); center=(lo+hi)/2
mesh=source.data.copy()
for v,world in zip(mesh.vertices,verts):v.co=rotation@((world-center)*scale)
source.hide_render=True
source.hide_viewport=True

for i in range(6):
 o=bpy.data.objects.new('Player AK47 %02d'%i,mesh); bpy.context.collection.objects.link(o); o.parent=rack; o.location=(-.65+i*.26,-.05,.69)
 box('Barrel cradle',(-.65+i*.26,.015,1.05),(.07,.19,.055),steel,rack)
shelf=group('02 Quartermaster shelf')
for x in [-.78,.78]:
 for y in [-.24,.24]: box('Angle iron upright',(x,y,.83),(.055,.055,1.66),steel,shelf)
for z in [.08,.57,1.06,1.56]:
 for y in [-.19,0,.19]: box('Individual shelf plank',(0,y,z),(1.64,.18,.055),wood,shelf)
 box('Front steel lip',(0,-.29,z),(1.65,.025,.07),steel,shelf)
for z in [.27,.76,1.25]:
 for x in [-.405,.405]:
  # Two long, low wooden crates per tier; metal closures and end cleats.
  for yy in [-.211,.211]:
   for zz in [-.07,.07]: box('Olive painted side board',(x,yy,z+zz),(.75,.024,.134),cratewood,shelf)
  for xx in [-.365,.365]:box('Crate end board',(x+xx,0,z),(.025,.42,.28),cratewood,shelf)
  box('Crate bottom',(x,0,z-.145),(.75,.44,.025),cratewood,shelf)
  for yy in [-.145,0,.145]:box('Separate lid plank',(x,yy,z+.155),(.77,.141,.028),cratewood,shelf)
  for dx in [-.28,.28]:
   box('Front wooden end cleat',(x+dx,-.235,z),(.052,.028,.28),cratewood,shelf)
   box('Steel lid strap',(x+dx,0,z+.176),(.035,.44,.012),steel,shelf)
   box('Hinged latch',(x+dx,-.258,z+.09),(.026,.017,.105),steel,shelf)
   box('Latch clasp',(x+dx,-.271,z+.055),(.04,.014,.025),brass,shelf)
   for dz in [-.10,.10]:cyl('Round nail head',(x+dx,-.254,z+dz),.008,.008,steel,shelf,(math.pi/2,0,0))
  # Recessed carry handle on each short end.
  for side in [-1,1]:
   box('End handle recess',(x+side*.384,0,z),(.012,.16,.065),rubber,shelf)
   box('End handle grip',(x+side*.397,0,z+.012),(.018,.13,.018),steel,shelf)
  for j,line in enumerate(['DAN 7.62 x 39','LO 01 / 1968','24 KG']):text(line,(x-.19,-.226,z+.065-j*.047),.027,shelf)
  label=box('Orange transport diamond',(x+.19,-.231,z),(.085,.006,.085),hazard,shelf);label.rotation_euler.y=math.pi/4
  # Small edge chips reveal raw wood without using the stock photo texture.
  for j in range(9):
   dx=-.34+j*.081
   box('Worn paint along lower edge',(x+dx,-.226,z-.128+(j%3)*.003),(.018+(j%2)*.013,.002,.006),exposed,shelf)
for x in [-.53,0,.53]:
 # Open underside, elliptical pith crown and a real wide brim.
 vs=[]; fs=[]; N=48; rings=12
 for j in range(rings):
  a=(j/(rings-1))*(math.pi/2-.025)
  for i in range(N):
   t=2*math.pi*i/N; vs.append((x+.17*math.cos(a)**.8*math.cos(t),.20*math.cos(a)**.8*math.sin(t),1.65+.225*math.sin(a)))
 for j in range(rings-1):
  for i in range(N): a=j*N+i; b=j*N+(i+1)%N; fs.append((a,b,b+N,a+N))
 me=bpy.data.meshes.new('Pith crown'); me.from_pydata(vs,[],fs); ob=bpy.data.objects.new('Green pith helmet crown',me); bpy.context.collection.objects.link(ob); ob.parent=shelf; me.materials.append(helmetgreen)
 for p in me.polygons:p.use_smooth=True
 # Curved elliptical brim, open underneath rather than a flat disc.
 v=[];f=[]
 for j in range(5):
  q=j/4
  for i in range(N):
   t=math.tau*i/N;v.append((x+(.165+.074*q)*math.cos(t),(.195+.085*q)*math.sin(t),1.655-.045*q-.013*q*abs(math.sin(t))))
 for j in range(4):
  for i in range(N):a=j*N+i;b=j*N+(i+1)%N;f.append((a,b,b+N,a+N))
 me=bpy.data.meshes.new('Curved brim');me.from_pydata(v,[],f);ob=bpy.data.objects.new('Downturned wide green brim',me);bpy.context.collection.objects.link(ob);ob.parent=shelf;me.materials.append(helmetgreen)
 so=ob.modifiers.new('Brim thickness','SOLIDIFY');so.thickness=.009
 for p in me.polygons:p.use_smooth=True
 cyl('Crown fabric top button',(x,0,1.88),.021,.014,helmetgreen,shelf)
 # Front chinstrap on brim with two silver adjustment buckles.
 box('Dark chinstrap across front brim',(x,-.235,1.625),(.36,.018,.018),rubber,shelf)
 for dx in [-.12,.12]:
  for dd in [-.012,.012]:box('Buckle vertical edge',(x+dx+dd,-.247,1.629),(.004,.008,.035),steel,shelf)
  for dz in [-.016,.016]:box('Buckle horizontal edge',(x+dx,-.247,1.629+dz),(.026,.008,.004),steel,shelf)
 cyl('Gold badge rim',(x,-.201,1.705),.031,.009,gold,shelf,(math.pi/2,0,0))
 cyl('Red circular insignia',(x,-.207,1.705),.026,.007,badge,shelf,(math.pi/2,0,0))
 points=[]
 for i in range(10):
  a=math.pi/2+i*math.pi/5;r=.022 if i%2==0 else .009
  points.append((x+r*math.cos(a),-.213,1.705+r*math.sin(a)))
 me=bpy.data.meshes.new('Five pointed star');me.from_pydata(points,[],[tuple(range(10))]);ob=bpy.data.objects.new('Gold five-pointed star',me);bpy.context.collection.objects.link(ob);ob.parent=shelf;me.materials.append(gold)
desk=group('03 Information desk and inward facing chair')
for x in [-.65,.65]:
 for y in [-.30,.30]: box('Table leg',(x,y,.38),(.085,.085,.76),wood,desk)
for y in [-.31,.31]: box('Apron',(0,y,.65),(1.4,.055,.15),wood,desk)
for y in [-.27,-.09,.09,.27]: box('Tabletop plank',(0,y,.785),(1.55,.17,.055),wood,desk)
box('Drawer front',(-.30,-.35,.655),(.57,.05,.14),wood,desk); box('Drawer handle',(-.30,-.395,.66),(.12,.035,.025),steel,desk)
box('Radio enclosure',(.32,.12,1.01),(.52,.29,.39),olive,desk)
box('Radio front panel',(.32,-.035,1.01),(.47,.02,.32),steel,desk)
box('Frequency window',(.30,-.05,1.075),(.24,.014,.08),paper,desk)
for x in [.16,.32,.48]: cyl('Tuning dial',(x,-.07,.955),.029,.035,rubber,desk,(math.pi/2,0,0))
for x in [.15,.49]: box('Radio carry handle foot',(x,.12,1.225),(.025,.025,.05),steel,desk)
box('Radio handle',(.32,.12,1.25),(.36,.03,.025),rubber,desk)
cyl('Antenna',(.55,.20,1.43),.006,.52,steel,desk)
box('Document stack',(-.39,-.02,.825),(.37,.28,.025),paper,desk)
for i in range(7): box('Map line',(-.39,-.12+i*.033,.839),(.28 if i%2 else .20,.002,.001),ink,desk)
chair=group('Chair - front points toward table'); chair.parent=desk
# Front is +Y. Backrest is on the outer (-Y) side.
for x in [-.20,.20]:
 for y in [-1.04,-.66]: box('Chair leg',(x,y,.23),(.055,.055,.46),wood,chair)
 box('Back stile',(x,-1.04,.67),(.055,.06,.55),wood,chair)
 box('Side stretcher',(x,-.85,.20),(.035,.40,.04),wood,chair)
for y in [-1,-.87,-.74,-.64]: box('Seat plank',(0,y,.475),(.48,.095,.045),wood,chair)
for z in [.68,.85]: box('Backrest slat',(0,-1.04,z),(.47,.05,.115),wood,chair)
# Collider proposals are explicit, inspectable and excluded from beauty renders.
colliders=[]
for root,center,size in [(rack,(0,0,.70),(1.85,.55,1.4)),(shelf,(0,0,.86),(1.69,.61,1.72)),(desk,(0,0,.4),(1.55,.76,.8)),(chair,(0,-.85,.47),(.51,.46,.94))]:
 o=box('COLLIDER_PROPOSAL solid non-trigger',center,size,steel,root); o.display_type='WIRE'; o.hide_render=True; o['is_trigger']=False
 colliders.append({'parent':root.name,'center_blender':center,'size_blender':size,'trigger':False})
floor=box('Studio floor',(0,0,-.08),(200,200,.1),mat('Backdrop',(.11,.13,.13)))
wall=group('Walnut bunker wall - shelf placement preview')
for i in range(14):box('Walnut vertical wall board',(-1.3+i*.2,.395,1.15),(.194,.10,2.3),wood,wall)
wall['shelf_to_wall_gap_metres']=.03
for p,power,size in [((1,-4,6),1000,5),((-4,-1,3),650,4),((2,4,5),1200,3)]:
 bpy.ops.object.light_add(type='AREA',location=p); o=bpy.context.object; o.data.energy=power; o.data.shape='DISK'; o.data.size=size; o.rotation_euler=(Vector((0,0,.8))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(); cam=bpy.context.object; S.camera=cam; cam.data.type='ORTHO'; cam.data.ortho_scale=2.65
for root,name,target,pos in [(rack,'01_ak47_rack',(0,0,.7),(2,-4,2.2)),(shelf,'02_equipment_shelf',(0,0,.92),(2,-4,2.7)),(desk,'03_information_desk',(0,-.25,.65),(2.3,-3.8,2.4))]:
 for o in wall.children_recursive:o.hide_render=root!=shelf
 for r in [rack,shelf,desk]:
  for o in [r,*r.children_recursive]: o.hide_render=(r!=root or o.name.startswith('COLLIDER_'))
 cam.location=pos; cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
 S.render.filepath=str(OUT/(name+'.png')); bpy.ops.render.render(write_still=True)
for r in [rack,shelf,desk]:
 for o in [r,*r.children_recursive]: o.hide_render=o.name.startswith('COLLIDER_')
rack.location.x=-2.2; desk.location.x=2.2
for image in bpy.data.images:
 if image.source=='FILE':
  try:image.pack()
  except:pass
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Quartermaster_Proposal.blend'))
(OUT/'collider_proposals.json').write_text(json.dumps(colliders,indent=2))
print('PREVIEW ONLY: three renders, packed Blender source, collider proposals. Unity untouched.')


