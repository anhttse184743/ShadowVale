"""Blender-only proposal. Does not modify or export any Unity scene or asset."""
from pathlib import Path
import bpy, math, random, json
from mathutils import Vector

OUT=Path(__file__).resolve().parent
random.seed(198)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.cycles.max_bounces=5
scene.render.film_transparent=False
scene.render.resolution_x=1500
scene.render.resolution_y=1050
scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Bunker ambient');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.16,.18,.20,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.28
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'

def mat(name,color,rough=.7,metal=0,grain=False):
    m=bpy.data.materials.new(name);m.use_nodes=True
    nodes=m.node_tree.nodes;links=m.node_tree.links;bs=nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=rough;bs.inputs['Metallic'].default_value=metal
    if grain:
        tex=nodes.new('ShaderNodeTexCoord');scale=nodes.new('ShaderNodeVectorMath');scale.operation='MULTIPLY';scale.inputs[1].default_value=(1.8,95,12)
        links.new(tex.outputs['Generated'],scale.inputs[0]);noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=3;noise.inputs['Detail'].default_value=3
        links.new(scale.outputs[0],noise.inputs['Vector']);ramp=nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].position=.2;ramp.color_ramp.elements[0].color=(*(c*.56 for c in color),1)
        ramp.color_ramp.elements[1].position=.78;ramp.color_ramp.elements[1].color=(*(min(1,c*1.35) for c in color),1)
        links.new(noise.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs[0],bs.inputs['Base Color'])
        bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.22;bump.inputs['Distance'].default_value=.008
        links.new(noise.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs['Normal'],bs.inputs['Normal'])
    return m
wood=mat('Weathered teak | fine grain',(.12,.060,.027),grain=True)
woodlight=mat('Worn plank edges',(.19,.103,.045),grain=True)
wooddark=mat('Old dark timber',(.045,.022,.009),grain=True)
iron=mat('Aged blued iron',(.07,.078,.072),.4,.72)
steel=mat('Worn tool steel',(.32,.34,.31),.3,.8)
brass=mat('Dull brass',(.38,.25,.085),.36,.75)
olive=mat('Faded olive paint',(.14,.17,.09),.74)
paper=mat('Old cream paper',(.65,.58,.39),.92)
ink=mat('Charcoal ink',(.064,.073,.052),.92)
red=mat('Tactical red',(.40,.045,.022),.72)
blue=mat('River blue ink',(.12,.24,.26),.85)
cloth=mat('Khaki cotton',(.28,.29,.17),.96)
white=mat('Bandage linen',(.58,.56,.45),.96)
wallwood=mat('Bunker wall planks',(.075,.045,.021),grain=True)
floorwood=mat('Worn floorboards',(.095,.064,.038),grain=True)
stain=mat('Old oil marks',(.032,.025,.017),.88)

collections={}
for name in ['01 Crafting workbench','02 Mission board','03 Chairs facing table','04 Preview room','05 Lighting']:
    c=bpy.data.collections.new(name);scene.collection.children.link(c);collections[name]=c
current=collections['01 Crafting workbench']
def collect(ob):
    for c in list(ob.users_collection):c.objects.unlink(ob)
    current.objects.link(ob);return ob
def cube(name,p,size,m,bevel=.007,rot=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p);ob=collect(bpy.context.object);ob.name=name;ob.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if rot:ob.rotation_euler=rot
    ob.data.materials.append(m)
    if bevel:
        mod=ob.modifiers.new('Soft worn edges','BEVEL');mod.width=bevel;mod.segments=3
        ob.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL')
    return ob
def cylinder(name,p,r,depth,m,rotation=None,vertices=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=p);ob=collect(bpy.context.object);ob.name=name
    if rotation:ob.rotation_euler=rotation
    ob.data.materials.append(m);mod=ob.modifiers.new('Rounded rim','BEVEL');mod.width=min(.003,r*.15);mod.segments=2
    ob.modifiers.new('Weighted normals','WEIGHTED_NORMAL');return ob
def rod(name,a,b,r,m):
    a,b=Vector(a),Vector(b);ob=cylinder(name,(a+b)/2,r,(b-a).length,m);ob.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return ob
def line(name,points,r,m):
    data=bpy.data.curves.new(name,'CURVE');data.dimensions='3D';data.bevel_depth=r;data.bevel_resolution=2
    poly=data.splines.new('POLY');poly.points.add(len(points)-1)
    for p,v in zip(poly.points,points):p.co=(*v,1)
    ob=bpy.data.objects.new(name,data);current.objects.link(ob);ob.data.materials.append(m);return ob
font=bpy.data.fonts.load('C:/Windows/Fonts/arial.ttf')
bold=bpy.data.fonts.load('C:/Windows/Fonts/arialbd.ttf')
def text(name,body,p,size,m,wall=False,boldface=False,center=False):
    data=bpy.data.curves.new(name,'FONT');data.body=body;data.size=size;data.font=bold if boldface else font;data.extrude=.00025
    if center:data.align_x='CENTER'
    ob=bpy.data.objects.new(name,data);current.objects.link(ob);ob.location=p
    if wall:ob.rotation_euler=(math.pi/2,0,0)
    data.materials.append(m);return ob

# Workbench: actual joinery, apron, shelf, repaired plank edges and fasteners.
for i in range(6):
    cube('Separate solid top plank %02d'%i,(0,-.4+(i-2.5)*.181,.91+random.uniform(-.0007,.0007)),(2.8,.175,.075),woodlight if i%3==0 else wood,.009)
for x in [-1.22,1.22]:
    for y in [-.8,0]:
        cube('Square timber leg',(x,y,.44),(.115,.115,.88),wood,.008)
        cube('Black iron foot shoe',(x,y,.05),(.126,.126,.12),iron,.003)
    cube('Lower side bearer',(x,-.4,.24),(.085,.91,.10),wooddark)
for y in [-.84,.04]:
    cube('Mortised apron',(0,y,.79),(2.58,.065,.17),wooddark)
    cube('Lower stretcher',(0,y,.26),(2.54,.065,.08),wood)
for i in range(4):cube('Open lower shelf',(0,-.72+i*.21,.31),(2.5,.195,.045),wood)
for x in [-1.23,1.23]:
    for y in [-.84,.04]:
        for z in [.77,.84]:cylinder('Iron fixing nail',(x,y-.039,z),.011,.012,iron,(math.pi/2,0,0),12)
# A proper little iron vice, clamped to the front edge.
cube('Vice base',(-.96,-.88,.974),(.26,.24,.045),iron)
cube('Vice body',(-.96,-.87,1.041),(.19,.19,.1),iron)
for y in [-.79,-.97]:
    cube('Vice jaw',(-.96,y,1.10),(.25,.045,.075),iron,.004)
    cube('Steel jaw face',(-.96,y+(.024 if y<-.8 else -.024),1.11),(.22,.008,.035),steel,.001)
rod('Vice lead screw',(-.96,-1.10,1.04),(-.96,-.85,1.04),.014,steel)
rod('Sliding vice handle',(-1.12,-1.10,1.04),(-.80,-1.10,1.04),.008,steel)
for x in [-1.12,-.80]:cylinder('Vice handle end',(x,-1.10,1.04),.013,.02,iron,(0,math.pi/2,0))
# Repair supplies, rag, paper, hand tools, oil can, drawer.
cube('Repair drawer',(0,.005,.70),(.63,.10,.13),woodlight)
rod('Drawer pull',(-.10,-.065,.70),(.10,-.065,.70),.012,iron)
cube('Folded work cloth',(.73,-.25,.964),(.52,.34,.027),cloth,.018)
for j in range(6):line('Cotton fold',[(.49+i*.045,-.38+j*.052,.980+math.sin(i*1.1)*.002) for i in range(12)],.0018,white)
cube('Maintenance instruction sheet',(.10,-.40,.954),(.60,.38,.003),paper,.001,rot=(0,0,-.05))
text('Sheet title','SỔ SỬA CHỮA',(-.14,-.31,.958),.040,ink,boldface=True)
for i in range(6):cube('Written repair line',(.08,-.37-i*.032,.957),(.39-random.random()*.12,.002,.001),ink,0)
rod('Hammer wooden handle',(.34,-.78,.973),(.70,-.59,.973),.022,woodlight)
cube('Forged hammer head',(.71,-.59,.985),(.15,.065,.065),steel,.012,rot=(0,0,-.25))
rod('Screwdriver handle',(-.42,-.72,.981),(-.31,-.48,.981),.021,wooddark)
rod('Screwdriver shaft',(-.31,-.48,.981),(-.24,-.31,.981),.005,steel)
rod('Tool wrench',(.30,-.06,.978),(.67,-.09,.978),.013,steel)
for x in [.30,.67]:
    bpy.ops.mesh.primitive_torus_add(major_radius=.026,minor_radius=.008,major_segments=16,minor_segments=6,location=(x,-.06 if x==.30 else -.09,.978));ob=collect(bpy.context.object);ob.name='Wrench closed end';ob.data.materials.append(steel)
cylinder('Olive oil can',(-1.08,-.19,1.015),.075,.13,olive)
cylinder('Oil can cap',(-1.08,-.19,1.089),.028,.02,brass)
rod('Bent oil spout',(-1.08,-.19,1.10),(-.98,-.19,1.21),.007,brass)
rod('Oil spout tip',(-.98,-.19,1.21),(-.94,-.19,1.21),.006,brass)
cube('Small field repair tin',(.96,-.66,.992),(.30,.20,.075),olive,.012)
cube('Tin lid seam',(.96,-.66,1.03),(.307,.207,.012),iron,.005)
for i in range(2):cylinder('Linen bandage roll',(.73+i*.16,-.25,1.025),.05,.10,white,(math.pi/2,0,0))
for i in range(12):cylinder('Loose fixing screw',(-.59+random.random()*.14,-.18+random.random()*.11,.958),.003,.025,steel,(math.pi/2,0,random.random()*3),8)
for i in range(24):
    x=random.uniform(-1.32,1.32);y=random.uniform(-.9,.08)
    line('Fine surface scratch',[(x,y,.950),(x+random.uniform(.025,.12),y+random.uniform(-.003,.003),.950)],.0006,wooddark)
for i in range(4):
    bpy.ops.mesh.primitive_torus_add(major_radius=.03+i*.005,minor_radius=.001,major_segments=36,minor_segments=4,location=(-.74,-.19,.950))
    ob=collect(bpy.context.object);ob.name='Faded oil can ring';ob.data.materials.append(stain)
cube('Under-bench wooden supply box',(-.74,-.42,.48),(.65,.53,.30),woodlight,.01)
for z in [.38,.55]:cube('Crate iron binding',(-.74,-.42,z),(.66,.54,.018),iron,.002)
text('Supply box stencil','VẬT TƯ',(-.74,-.697,.47),.06,ink,wall=True,boldface=True,center=True)
cube('Canvas tool roll lower shelf',(.56,-.4,.38),(.68,.39,.10),cloth,.035)

# Board, facing the room. Documents are individually pinned and slightly layered.
current=collections['02 Mission board']
cube('Mission board backing',(0,1.94,1.82),(2.6,.10,1.56),wooddark)
for x in [-1.27,1.27]:cube('Board vertical frame',(x,1.865,1.82),(.095,.075,1.62),woodlight,.01)
for z in [1.06,2.58]:cube('Board horizontal frame',(0,1.865,z),(2.445,.075,.095),woodlight,.01)
for i in range(12):cube('Inset board tongue and groove',(-1.16+i*.212,1.88,1.82),(.207,.018,1.39),wood,.003)
cube('Painted title plate',(0,1.808,2.43),(1.9,.018,.21),olive,.005)
text('Mission board heading','NHIỆM VỤ',(0,1.793,2.378),.135,paper,wall=True,boldface=True,center=True)
cube('Folded tactical map paper',(-.46,1.818,1.78),(1.33,.008,1.02),paper,.003)
# Creases and thin contour lines provide authored detail without modern UI elements.
for x in [-.68,-.26]:cube('Map fold',(x,1.812,1.78),(.002,.001,1.0),woodlight,0)
for z in [1.62,1.95]:cube('Horizontal map fold',(-.46,1.811,z),(1.31,.001,.002),woodlight,0)
for cx,cz in [(-.82,1.55),(-.22,1.96)]:
    for ring in range(1,8):
        pts=[]
        for j in range(81):
            a=j*math.tau/80;r=.044*ring*(1+.13*math.sin(3*a)+.09*math.cos(5*a))
            x=cx+r*math.cos(a);z=cz+r*.65*math.sin(a)
            x=max(-1.10,min(.19,x));z=max(1.30,min(2.26,z));pts.append((x,1.808,z))
        line('Printed terrain contour',pts,.0014,woodlight)
line('River on tactical map',[(-.48+.065*math.sin(i*.42),1.803,1.29+i*.0121) for i in range(80)],.006,blue)
route=[(-.98,1.798,1.38),(-.71,1.794,1.64),(-.38,1.796,1.74),(-.12,1.794,2.10)]
line('Red thread route',route,.003,red)
for x,y,z in route:
    cylinder('Red map pin',(x,y-.004,z),.010,.015,red,(math.pi/2,0,0),16)
text('Map caption','KHU A  /  TỶ LỆ 1:25.000',(-1.05,1.800,2.20),.032,ink,wall=True,boldface=True)
for i,title in enumerate(['01  TRINH SÁT','02  TIẾP TẾ','03  LIÊN LẠC']):
    z=2.16-i*.34
    cube('Individual order sheet %d'%i,(.69,1.804-i*.002,z),(.74,.006,.29),paper,.002)
    text('Order title '+str(i),title,(.36,1.796-i*.002,z+.073),.043,ink,wall=True,boldface=True)
    for j in range(4):cube('Handwritten order line',(.65,1.795-i*.002,z+.017-j*.035),(.57-random.random()*.18,.001,.002),ink,0)
    cylinder('Brass paper tack',(.69,1.788-i*.002,z+.127),.010,.014,brass,(math.pi/2,0,0),16)
cube('Document ledge',(0,1.74,1.04),(2.445,.24,.055),woodlight)
rod('Board pencil',(.35,1.74,1.08),(.64,1.74,1.08),.007,woodlight)

# Chair local front is +Y. Both long rows face the bench centre.
current=collections['03 Chairs facing table']
def chair(x,y,angle,index):
    before=set(bpy.data.objects)
    for sx in [-.18,.18]:
        for sy in [-.17,.17]:cube('Chair leg',(sx,sy,.225),(.045,.045,.45),wood,.005)
        cube('Side stretcher',(sx,0,.19),(.027,.36,.035),wooddark,.003)
    for j in range(4):cube('Chair seat plank',(-.16+j*.107,0,.458),(.10,.43,.035),woodlight,.005)
    for sx in [-.18,.18]:cube('Chair back post',(sx,-.19,.69),(.045,.045,.48),wood,.005)
    for z in [.65,.85]:cube('Chair back slat',(0,-.16,z),(.40,.045,.09),woodlight,.005)
    root=bpy.data.objects.new('Chair %02d | front toward table'%index,None);current.objects.link(root)
    for ob in set(bpy.data.objects)-before-{root}:ob.parent=root
    root.location=(x,y,0);root.rotation_euler.z=angle
for i,x in enumerate([-.93,0,.93]):
    chair(x,-1.43,0,i+1)
    chair(x,.63,math.pi,i+4)

current=collections['04 Preview room']
for i in range(25):cube('Preview floorboard',(0,-2.8+i*.24,-.045),(6.6,.233,.075),floorwood,.003)
for i in range(28):cube('Preview rear wall board',(-3.3+i*.244,2.13,1.6),(.236,.11,3.2),wallwood,.003)
for x in [-2.8,2.8]:cube('Bunker wall upright',(x,2.02,1.6),(.13,.15,3.2),wooddark)

current=collections['05 Lighting']
def area(name,p,power,color,size,target):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);current.objects.link(ob);ob.location=p;ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
area('Warm bunker practical',(-1.8,-1.1,4.0),450,(1,.79,.52),3.0,(0,0,.7))
area('Soft doorway fill',(4,-4,3.7),650,(.70,.80,1),4,(0,0,1))
area('Board illumination',(0,.5,3.3),160,(1,.85,.61),2,(0,1.8,1.7))
cameraData=bpy.data.cameras.new('Review camera');camera=bpy.data.objects.new('Review camera',cameraData);current.objects.link(camera);scene.camera=camera
views=[('01_overview',(4.9,-6.8,4.7),(0,.1,1.0),48),('02_workbench',(3.5,-4.1,3.3),(0,-.35,.75),58),('03_mission_board',(2.5,-3.1,2.75),(0,1.84,1.83),72)]
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CommandBunker_Proposal.blend'))
for filename,position,target,lens in views:
    camera.location=position;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();cameraData.lens=lens
    scene.render.filepath=str(OUT/(filename+'.png'));bpy.ops.render.render(write_still=True)
camera.location=views[0][1];camera.rotation_euler=(Vector(views[0][2])-camera.location).to_track_quat('-Z','Y').to_euler();cameraData.lens=48
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CommandBunker_Proposal.blend'))
(OUT/'proposal.json').write_text(json.dumps({'status':'PREVIEW_ONLY_NOT_APPLIED','bench_metres':[2.8,1.08,.948],'board_metres':[2.63,.22,1.62],'chairs':6,'chair_front_axis_blender':'+Y','unity_modified':False},indent=2),encoding='utf-8')
print('PREVIEW COMPLETE — Unity assets and scenes were not changed.')
