"""Blender preview only: field cots and floor ration/ammunition stores."""
from pathlib import Path
import bpy, math, random
from mathutils import Vector
HERE=Path(__file__).resolve().parent
# Shared modeling helpers and the approved walnut palette; stop before creating props.
shared=(HERE/'build_revision2.py').read_text().split("rack=group(")[0]
exec(compile(shared,str(HERE/'build_revision2.py'),'exec'))
OUT=HERE/'CotsSuppliesPreview';OUT.mkdir(exist_ok=True)
random.seed(68)
S.cycles.samples=32;S.render.resolution_x=1400;S.render.resolution_y=1000
S.view_settings.view_transform='AgX'
canvas=mat('Faded olive woven canvas',(.115,.15,.065),grain=True)
burlap=mat('Coarse jute ration sacks',(.32,.25,.145),grain=True)
thread=mat('Heavy linen stitching',(.48,.40,.25))
frame=mat('Worn painted tubular steel',(.16,.20,.17),.7)
darkcanvas=mat('Folded army blanket',(.065,.095,.038),grain=True)
def rod(n,a,b,r,m,root):
 a,b=Vector(a),Vector(b);o=cyl(n,(a+b)/2,r,(b-a).length,m,root);o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();
 for p in o.data.polygons:p.use_smooth=True
 return o
def seam(n,pts,r,m,root):
 cu=bpy.data.curves.new(n,'CURVE');cu.dimensions='3D';cu.bevel_depth=r;cu.bevel_resolution=2
 sp=cu.splines.new('POLY');sp.points.add(len(pts)-1)
 for p,co in zip(sp.points,pts):p.co=(*co,1)
 ob=bpy.data.objects.new(n,cu);bpy.context.collection.objects.link(ob);ob.parent=root;cu.materials.append(m);return ob
def softbox(n,p,d,m,root,bevel=.09):
 o=box(n,p,d,m,root);o.modifiers[0].width=bevel;o.modifiers[0].segments=5
 return o
cot=group('FieldCot - head at positive Y')
# 2.04 x .78 m canvas cot, three crossed folding supports and exposed pivots.
for x in [-.385,.385]:rod('Long tubular side rail',(x,-1,.46),(x,1,.46),.021,frame,cot)
for y in [-1,1]:rod('Detachable end tension bar',(-.425,y,.46),(.425,y,.46),.018,frame,cot)
for y in [-.78,0,.78]:
 rod('Folding X leg A',(-.34,y-.035,.035),(.32,y-.035,.45),.017,frame,cot)
 rod('Folding X leg B',(.34,y+.035,.035),(-.32,y+.035,.45),.017,frame,cot)
 cyl('Visible steel pivot',(0,y-.059,.247),.032,.026,brass,cot,(math.pi/2,0,0))
 for x in [-.34,.34]:softbox('Rubber foot',(x,y,.027),(.085,.09,.045),rubber,cot,.012)
 rod('Cross-support locking stay',(-.24,y,.18),(.24,y,.18),.009,steel,cot)
vs=[];fs=[];NX=20;NY=44
for j in range(NY+1):
 y=-.96+j/NY*1.92
 for i in range(NX+1):
  x=-.365+i/NX*.73
  sag=.043*(1-(x/.365)**2)*(1-(y/.98)**4)
  wrinkle=.0035*math.sin(y*45+x*13)*abs(x/.365)**3
  vs.append((x,y,.475-sag+wrinkle))
for j in range(NY):
 for i in range(NX):a=j*(NX+1)+i;fs.append((a,a+1,a+NX+2,a+NX+1))
me=bpy.data.meshes.new('Slightly sagged canvas');me.from_pydata(vs,[],fs);me.materials.append(canvas)
ob=bpy.data.objects.new('Tensioned canvas sleeping surface',me);bpy.context.collection.objects.link(ob);ob.parent=cot
for p in me.polygons:p.use_smooth=True
so=ob.modifiers.new('Canvas thickness','SOLIDIFY');so.thickness=.005
for x in [-.35,.35]:
 seam('Double stitched canvas hem',[(x,-.95+k*.038,.478) for k in range(51)],.0025,thread,cot)
 for j in range(25):rod('Short hem stitch',(x-.004,-.93+j*.077,.48),(x+.004,-.93+j*.077,.48),.0009,thread,cot)
pillow=softbox('Soft cloth pillow',(0,.73,.525),(.52,.32,.13),canvas,cot,.06)
seam('Pillow stitched perimeter',[(-.22,.60,.53),(.22,.60,.53),(.24,.86,.53),(-.24,.86,.53),(-.22,.60,.53)],.002,thread,cot)
for i in range(3):softbox('Folded wool blanket layer',(0,-.68,.49+i*.021),(.64,.33,.026),darkcanvas,cot,.012)
for j in [-.76,-.69,-.62]:seam('Blanket woven stripe',[(-.30,j,.56),(.30,j,.56)],.003,khaki,cot)
cot['head_axis']='+Y';cot['dimensions_m']=[.85,2.04,.60]
supplies=group('Rations and ammunition on a low pallet')
for x in [-.94,0,.94]:box('Walnut pallet runner',(x,0,.055),(.12,1.14,.11),wood,supplies)
for y in [-.48,-.24,0,.24,.48]:box('Pallet slat',(0,y,.135),(2.10,.205,.045),wood,supplies)
def crate(x,y,z):
 for yy in [-.21,.21]:
  for zz in [-.072,.072]:box('Olive wooden crate plank',(x,y+yy,z+zz),(.76,.025,.138),cratewood,supplies)
 for xx in [-.37,.37]:box('Crate end board',(x+xx,y,z),(.025,.42,.29),cratewood,supplies)
 for yy in [-.14,0,.14]:box('Lid plank',(x,y+yy,z+.16),(.79,.137,.025),cratewood,supplies)
 box('Crate base',(x,y,z-.15),(.76,.43,.025),cratewood,supplies)
 for xx in [-.28,.28]:
  box('Crate reinforcing cleat',(x+xx,y-.235,z),(.05,.026,.29),cratewood,supplies)
  box('Metal lid band',(x+xx,y,z+.178),(.032,.44,.012),steel,supplies)
  box('Front latch',(x+xx,y-.26,z+.10),(.032,.018,.105),steel,supplies)
  for zz in [-.10,.10]:cyl('Nail',(x+xx,y-.255,z+zz),.008,.008,steel,supplies,(math.pi/2,0,0))
 for side in [-1,1]:box('Crate side carry handle',(x+side*.40,y,z),(.02,.13,.025),steel,supplies)
 for i,line in enumerate(['DAN 7.62 x 39','LO 01 / 1968','24 KG']):text(line,(x-.19,y-.226,z+.07-i*.048),.028,supplies)
 for i in range(7):box('Chipped olive paint',(x-.32+i*.095,y-.226,z-.135),(.028,.003,.007),exposed,supplies)
for z in [.34,.69]:
 for x in [-.49,.38]:crate(x,.30,z)
crate(-.44,.30,1.04)
def sack(x,y,z,angle):
 # Rounded stuffed sack with irregular corners and a stitched gusset.
 root=group('Individual filled ration sack');root.parent=supplies;root.location=(x,y,z);root.rotation_euler.z=angle
 o=softbox('Bulging jute bag',(0,0,0),(.66,.42,.245),burlap,root,.10)
 sub=o.modifiers.new('Sack softness','SUBSURF');sub.levels=2
 tex=bpy.data.textures.new('Subtle bag wrinkles','CLOUDS');tex.noise_scale=.10
 dis=o.modifiers.new('Uneven grain-filled cloth','DISPLACE');dis.texture=tex;dis.strength=.018;dis.mid_level=.5
 for p in o.data.polygons:p.use_smooth=True
 seam('Sack perimeter seam',[(-.27,-.19,0),(.27,-.19,0),(.32,-.12,0),(.32,.12,0),(.27,.19,0),(-.27,.19,0),(-.32,.12,0),(-.32,-.12,0),(-.27,-.19,0)],.003,thread,root)
 for k in range(15):rod('Hand sewn sack closure',(-.28+k*.04,-.197,-.007),(-.28+k*.04,-.197,.009),.0012,thread,root)
 tag=box('Ration marking',(0,-.209,.03),(.23,.004,.045),burlap,root)
 text('GAO / 25 KG',(-.105,-.213,.013),.028,root)
for row,z in enumerate([.30,.55,.80]):
 for k in range(3):sack(-.69+k*.66+(row%2)*.02,-.30,z,random.uniform(-.045,.045))
supplies['preview_only']=True
wall=group('Walnut wall for five-cot placement')
for i in range(45):box('Vertical walnut wall plank',(-4.4+i*.2,1.098,1.20),(.194,.10,2.4),wood,wall)
floor=box('Neutral floor',(0,0,-.07),(200,200,.10),mat('Neutral backdrop',(.12,.14,.14)))
for p,power,size in [((1,-4,6),1000,5),((-4,-1,3),650,4),((2,4,5),1100,3)]:
 bpy.ops.object.light_add(type='AREA',location=p);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.6))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;S.camera=cam;cam.data.type='ORTHO'
def visible(root,yes):
 for o in [root,*root.children_recursive]:o.hide_render=not yes
def render(name,pos,target,scale):
 cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
 S.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
visible(supplies,False);visible(wall,False)
render('01_field_cot_detail',(2.5,-3,2.3),(0,0,.28),2.85)
visible(cot,False);visible(supplies,True)
render('02_ration_ammunition_store',(3,-4,2.8),(0,0,.58),3.05)
visible(supplies,False);visible(cot,True);visible(wall,True)
cots=[cot]
for i in range(1,5):
 root=group('FieldCot row '+str(i+1))
 for child in cot.children_recursive:
  # Cot has only direct geometry children.
  ob=child.copy();ob.data=child.data;bpy.context.collection.objects.link(ob);ob.parent=root
 cots.append(root)
for i,root in enumerate(cots):root.location.x=(i-2)*1.30
# Wall inner face Y=1.048, head end bar reaches Y=1.018 => 0.03 m gap.
for root in cots:root['head_wall_gap_m']=.03
render('03_five_cots_wall_layout',(5,-7,5),(0,0,.55),8.3)
cot.location.x=-2.6;supplies.location=(5,0,0);visible(supplies,True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FieldCots_RationStores_Proposal.blend'))
(OUT/'README.txt').write_text('PREVIEW ONLY. No Unity files modified. Five 0.85m-wide cots at 1.30m centers, heads toward wall, 3cm head clearance. Supply store: olive wooden ammunition cases and jute ration sacks on walnut pallet. Pending user approval.',encoding='utf-8')
print('PREVIEW COMPLETE - no Unity changes')
