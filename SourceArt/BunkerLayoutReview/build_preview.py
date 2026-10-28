from pathlib import Path
import bpy, math, json
from mathutils import Vector
OUT=Path(__file__).resolve().parent
source=OUT.parent/'CommandBunkerPreview'/'build_preview.py'
exec(compile(source.read_text(encoding='utf-8').split('for i,x in enumerate([-.93,0,.93]):')[0],str(source),'exec'))
OUT=Path(__file__).resolve().parent
# Reuse approved workbench detail; place its rearmost tabletop edge 30 mm from wall.
for ob in collections['01 Crafting workbench'].objects: ob.location += Vector((-3.2,2.72,0))
current=collections['03 Chairs facing table']
chair(-3.9,1.26,0,1);chair(-2.5,1.26,0,2)
# Mission board remains at the rear of the meeting area.
for ob in collections['02 Mission board'].objects: ob.location += Vector((3.15,.84,0))
current=bpy.data.collections.new('06 Meeting table - NEW PROPOSAL');scene.collection.children.link(current)
meeting=current
for i in range(5):cube('Meeting solid worn plank',(1.15+(i-2)*.235,-.20,.77),( .228,3.3,.065),woodlight if i%2 else wood,.009)
for x in [.72,1.58]:
    for y in [-1.55,1.15]:cube('Meeting mortised leg',(x,y,.365),(.10,.10,.73),wood,.009)
    cube('Meeting long apron',(x,-.20,.65),(.065,3.02,.16),wooddark)
    cube('Meeting low stretcher',(x,-.20,.19),(.055,2.8,.07),wood)
for y in [-1.55,1.15]:cube('Meeting cross brace',(1.15,y,.64),(.94,.065,.16),wood)
for i in range(95):
    x=random.uniform(.59,1.71);y=random.uniform(-1.78,1.36)
    line('Meeting wear scratch',[(x,y,.803),(x+random.uniform(-.003,.003),y+random.uniform(.025,.19),.803)],.0005,wooddark)
current=collections['03 Chairs facing table']
for i,y in enumerate([-1.25,-.20,.85]):
    chair(.10,y,-math.pi/2,10+i);chair(2.2,y,math.pi/2,20+i)
current=meeting
glass=mat('Smoked lamp glass',(.55,.43,.24),.18)
glass.node_tree.nodes.get('Principled BSDF').inputs['Transmission Weight'].default_value=.72
flame=mat('Warm lamp flame',(1,.37,.035),.4)
bs=flame.node_tree.nodes.get('Principled BSDF');bs.inputs['Emission Color'].default_value=(1,.35,.025,1);bs.inputs['Emission Strength'].default_value=6
for y in [-.95,.65]:
    cylinder('Kerosene lamp reservoir',(1.15,y,.85),.085,.09,iron)
    cylinder('Kerosene lamp glass chimney',(1.15,y,1.0),.045,.23,glass)
    cylinder('Lamp flame',(1.15,y,.94),.012,.05,flame)
    for dx in [-.07,.07]:rod('Lamp protective frame',(1.15+dx,y,.86),(1.15+dx,y,1.14),.006,iron)
    cylinder('Lamp chimney cap',(1.15,y,1.14),.065,.025,iron)
    data=bpy.data.lights.new('Oil lamp practical','POINT');data.energy=12;data.color=(1,.50,.16);data.shadow_soft_size=.08
    ob=bpy.data.objects.new('Oil lamp practical',data);current.objects.link(ob);ob.location=(1.15,y,1.02)
# Helmets and paper kept sparse, following the reference's simple meeting furniture.
for x,y in [(.83,-.6),(1.50,.2)]:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,location=(x,y,.84));ob=collect(bpy.context.object);ob.name='Olive pith helmet';ob.scale=(.14,.17,.075);ob.data.materials.append(olive)
    cylinder('Pith helmet brim',(x,y,.812),.18,.015,olive)
cube('Meeting notes',(1.12,-.18,.808),(.28,.37,.006),paper,.002)
current=collections['04 Preview room']
for i in range(37):cube('Context floorboard',(0,-4+i*.24,-.045),(10,.232,.075),floorwood,.003)
wall=mat('Earth olive bunker wall',(.11,.12,.075),.96,grain=True)
cube('Entrance side wall',(-3.3,2.945,1.5),(3.4,.11,3),wall,.005)
# wall front plane = 2.890; workbench rearmost top edge = 2.860.
cube('Door far wall',(3.35,2.945,1.5),(3.3,.11,3),wall,.005)
cube('Door lintel',(-.15,2.945,2.72),(2.9,.16,.56),wooddark)
for x in [-1.55,1.3]:cube('Tunnel doorway post',(x,2.87,1.23),(.14,.20,2.46),wooddark)
cube('Tunnel passage floor',(-.15,4.3,-.06),(2.7,3,.08),floorwood)
cube('Tunnel darkness',(-.15,5.7,1.25),(2.7,.12,2.5),wooddark)
current=collections['05 Lighting']
def area(name,p,power,color,size,target):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);current.objects.link(ob);ob.location=p;ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
area('Soft warm room light',(-3,-1,4.5),700,(1,.80,.58),4,(0,0,.5))
area('Doorway cool fill',(0,4,2.2),220,(.66,.78,1),2,(-2,1,1))
area('Front fill',(3,-4,3),450,(.85,.89,1),4,(0,0,1))
data=bpy.data.cameras.new('Proposal camera');cam=bpy.data.objects.new('Proposal camera',data);current.objects.link(cam);scene.camera=cam
scene.render.resolution_x=1500;scene.render.resolution_y=1050;scene.cycles.samples=24
views=[('01_layout',(-7.9,-10.8,7.3),(-.8,.65,.7),45),('02_workbench',(-6.5,-2.8,3.7),(-3.05,2.0,.8),48),('03_meeting',(4.5,-5.7,3.3),(1.15,-.2,.72),52)]
for name,pos,target,lens in views:
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();data.lens=lens
    scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'BunkerLayout_Proposal.blend'))
(OUT/'proposal.json').write_text(json.dumps({'status':'PREVIEW_ONLY','unity_modified':False,'craft_chairs':2,'craft_wall_clearance_m':.03,'meeting_dimensions_m':[1.168,3.3,.8025],'meeting_chairs':6,'placement':'Conceptual doorway context; exact Unity doorway alignment pending approval'},indent=2))
