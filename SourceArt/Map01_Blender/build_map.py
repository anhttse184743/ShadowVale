import bpy, bmesh, math, random, os, json
from mathutils import Vector
from math import sin, cos, exp, sqrt, pi
random.seed(41)
OUT=os.path.abspath('outputs')
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):
    if c.name!='Collection': bpy.data.collections.remove(c)
base=bpy.data.collections.get('Collection'); base.name='00_Terrain'
cols={}
for name in ['01_Water','02_Paths','03_Village_Supply','04_Patrol_Cover','05_Bridge','06_Abandoned_Base','07_Overlook','08_Forest','09_Undergrowth','10_Gameplay_Anchors','11_Lighting_Cameras']:
    c=bpy.data.collections.new(name); bpy.context.scene.collection.children.link(c); cols[name]=c
current=base
def move(o):
    for c in list(o.users_collection): c.objects.unlink(o)
    current.objects.link(o); return o
def mat(n,c,rough=.8,metal=0):
    m=bpy.data.materials.new(n); m.diffuse_color=(*c,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*c,1); p.inputs['Roughness'].default_value=rough; p.inputs['Metallic'].default_value=metal
    return m
earth=mat('Forest earth',(0.19,.235,.105)); soil=mat('Worn ochre trail',(.40,.29,.145)); bank=mat('Riverbank silt',(.27,.255,.16))
wood=mat('Aged teak',(.19,.125,.068)); plank=mat('Sun bleached wood',(.38,.285,.16)); roof=mat('Palm thatch',(.34,.30,.15)); dark=mat('Interior shadow',(.055,.073,.047))
stone=mat('Weathered rock',(.29,.32,.255)); sand=mat('Sandbag canvas',(.42,.405,.25)); olive=mat('Faded olive canvas',(.20,.235,.115)); metal=mat('Rusted steel',(.24,.185,.12),.65,.35)
water=mat('Jade stream',(.075,.24,.225),.22,.15); paper=mat('Route intelligence papers',(.73,.67,.45)); ink=mat('Route markings',(.38,.075,.035))
leaves=[mat('Canopy %02d'%i,c) for i,c in enumerate([(.105,.22,.065),(.16,.28,.075),(.235,.34,.10),(.12,.255,.12),(.28,.37,.105)])]
fernmat=mat('Fern new growth',(.27,.39,.085)); foam=mat('Water highlights',(.46,.61,.51),.3)
def mesh(n,v,f,m):
    me=bpy.data.meshes.new(n); me.from_pydata(v,[],f); me.update(); o=bpy.data.objects.new(n,me); current.objects.link(o)
    if m:o.data.materials.append(m)
    return o
def cube(n,loc,scale,m,rot=0,bevel=0):
    v=[(a*scale[0]/2,b*scale[1]/2,c*scale[2]/2) for a,b,c in [(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]]
    o=mesh(n,v,[(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)],m);o.location=loc;o.rotation_euler.z=rot
    if bevel:
        mod=o.modifiers.new('Worn edges','BEVEL');mod.width=bevel;mod.segments=2
        o.modifiers.new('Weighted corners','WEIGHTED_NORMAL')
    return o
icocache={}
def ico(n,loc,scale,m,sub=1):
    key=(m.name,sub)
    if key not in icocache:
        me=bpy.data.meshes.new(n);bm=bmesh.new();bmesh.ops.create_icosphere(bm,subdivisions=sub,radius=1);bm.to_mesh(me);bm.free();me.materials.append(m);icocache[key]=me
    o=bpy.data.objects.new(n,icocache[key]);current.objects.link(o);o.location=loc;o.scale=scale;return o
def beam(n,a,b,r,m,vertices=8):
    d=Vector(b)-Vector(a);v=[]
    for z,rr in [(-d.length/2,r),(d.length/2,r*.87)]:
        v.extend([(rr*cos(i*2*pi/vertices),rr*sin(i*2*pi/vertices),z) for i in range(vertices)])
    f=[tuple(reversed(range(vertices))),tuple(range(vertices,2*vertices))]+[(i,(i+1)%vertices,(i+1)%vertices+vertices,i+vertices) for i in range(vertices)]
    o=mesh(n,v,f,m);o.location=(Vector(a)+Vector(b))/2;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();return o
def riverx(y):return 8+12*sin(y*.041)+4*sin(y*.105)
def h(x,y):
    dist=abs(x-riverx(y)); hills=14*exp(-((x+57)**2/440+(y-48)**2/520))+7*exp(-((x-68)**2/650+(y-57)**2/700))
    raw=2.7+.7*sin(x*.11)*cos(y*.09)+.4*sin(y*.22+x*.1)+hills
    return -.8+(raw+.8)*min(1,max(0,(dist-3)/8))
def ground(x,y,d=0):return (x,y,h(x,y)+d)
# A continuous sculpted land surface, with lowered stream bed.
v=[];f=[];nx=181;ny=161
for j in range(ny):
    y=j-80
    for i in range(nx):v.append((i-90,y,h(i-90,y)))
for j in range(ny-1):
    for i in range(nx-1):
        a=j*nx+i;f.append((a,a+1,a+1+nx,a+nx))
terrain=mesh('MAP01 • 180m x 160m terrain',v,f,earth)
for p in terrain.data.polygons:p.use_smooth=True
# Color variation is procedural and embedded; no external textures.
nodes=earth.node_tree.nodes;links=earth.node_tree.links;p=nodes.get('Principled BSDF')
noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=.19;noise.inputs['Detail'].default_value=3
tex=nodes.new('ShaderNodeTexCoord');links.new(tex.outputs['Generated'],noise.inputs['Vector']);noise.inputs['Scale'].default_value=35
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.08,.12,.038,1);ramp.color_ramp.elements[1].color=(.29,.31,.12,1);links.new(noise.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs[0],p.inputs['Base Color'])
cube('Diorama earth foundation',(0,0,-4),(180,160,6),dark,bevel=1)
current=cols['01_Water']
v=[];f=[]
for j in range(321):
    y=-80+j*.5;x=riverx(y)
    for s in [-1,1]:v.append((x+s*(3.5+.5*sin(y*.15)),y,.05))
    if j:f.append((2*j-2,2*j-1,2*j+1,2*j))
mesh('Continuous stream',v,f,water)
for k in range(180):
    y=random.uniform(-78,78);x=riverx(y)+random.choice([-1,1])*random.uniform(3.2,6)
    ico('Rounded river boulder',ground(x,y,.35),(random.uniform(.4,1.4),random.uniform(.5,1.7),random.uniform(.35,.8)),stone,2)
for k in range(80):
    y=random.uniform(-79,79);x=riverx(y)+random.uniform(-2.8,2.8)
    cube('Stream ripple',(x,y,.09),(random.uniform(.25,1.1),.055,.016),foam,random.uniform(-.2,.2))
paths=[('Supply road',[(-66,-61),(-56,-49),(-47,-34),(-37,-22),(-27,-10),(-13,0),(8,0),(25,1),(35,14),(44,34)],3.4),('Stealth bypass',[(-47,-34),(-60,-22),(-59,-5),(-46,9),(-29,15),(-13,10),(-5,1)],1.7),('Diversion flank',[(-37,-22),(-23,-26),(-10,-18),(-9,-7),(-13,0)],2.0),('Base back entry',[(25,1),(39,-4),(58,3),(61,18),(54,34)],2),('Overlook',[(-46,9),(-54,24),(-55,45)],2),('River exit',[(44,34),(57,44),(47,57),(36,67)],2.8)]
def resample(points):
    out=[]
    for a,b in zip(points,points[1:]):
        d=math.dist(a,b);n=max(2,int(d/.65))
        for i in range(n):out.append((a[0]+(b[0]-a[0])*i/n,a[1]+(b[1]-a[1])*i/n))
    out.append(points[-1]);return out
allpaths=[]
current=cols['02_Paths']
for name,pts,width in paths:
    points=resample(pts);allpaths.extend([(x,y,width) for x,y in points]);v=[];f=[]
    for i,(x,y) in enumerate(points):
        nxt=points[min(i+1,len(points)-1)];prev=points[max(i-1,0)];dx=nxt[0]-prev[0];dy=nxt[1]-prev[1];l=sqrt(dx*dx+dy*dy) or 1
        for s in [-1,1]:
            xx=x-s*dy/l*width/2;yy=y+s*dx/l*width/2;v.append((xx,yy,h(xx,yy)+.06))
        if i:f.append((2*i-2,2*i-1,2*i+1,2*i))
    mesh(name,v,f,soil)
clearings=[(-62,-53,13),(-27,-9,12),(45,34,17),(-55,46,9),(57,-5,5)]
def clearing(x,y,rx,ry):
    v=[ground(x,y,.07)]+[ground(x+rx*cos(i*2*pi/48),y+ry*sin(i*2*pi/48),.07) for i in range(48)]
    mesh('Trampled clearing',v,[(0,i+1,(i+1)%48+1) for i in range(48)],soil)
for x,y,r in clearings:clearing(x,y,r,r*.8)
def crate(x,y,z=None,size=1.25):
    z=h(x,y) if z is None else z
    cube('Supply crate',(x,y,z+size/2),(size,size,size),plank,bevel=.035)
    for d in [-.37,.37]:cube('Crate band',(x+d*size,y,z+size/2),(.07,size+.03,size+.04),wood)
def bags(x,y,count=6,angle=0):
    for row in range(2):
        for i in range(count):
            t=(i-(count-1)/2)*.85+row*.2;xx=x+t*cos(angle);yy=y+t*sin(angle)
            cube('Cover • sandbag',ground(xx,yy,.32+row*.43),(.98,.58,.46),sand,angle,.16)
def hut(x,y,w=7,d=5,abandoned=False):
    z=h(x,y)+.6
    for dx in [-w/2+.3,w/2-.3]:
        for dy in [-d/2+.3,d/2-.3]:beam('Stilt post',(x+dx,y+dy,h(x+dx,y+dy)),(x+dx,y+dy,z+3),.14,wood)
    for i in range(int(w/.3)):cube('Floor board',(x-w/2+i*.3,y,z),(.28,d,.15),plank)
    for i in range(int(w/.45)):
        if abandoned and i%6==0:continue
        cube('Rear timber wall',(x-w/2+i*.45,y+d/2,z+1.35),(.42,.12,2.6),wood)
    for dx in [-w/2,w/2]:
        cube('Side wall',(x+dx,y,z+1.1),(.13,d,2.2),plank)
    for dx in [-w/2+1,w/2-1]:cube('Front wall with doorway',(x+dx,y-d/2,z+1.1),(2,.12,2.2),plank)
    v=[(x-w/2-.6,y-d/2-.55,z+2.7),(x+w/2+.6,y-d/2-.55,z+2.7),(x+w/2+.6,y,z+4),(x-w/2-.6,y,z+4),(x-w/2-.6,y+d/2+.55,z+2.7),(x+w/2+.6,y+d/2+.55,z+2.7)]
    o=mesh('Removable thatch roof',v,[(0,1,2,3),(3,2,5,4)],roof);o['cutaway_roof']=True
    for i in range(int(w/.35)+3):
        xx=x-w/2-.4+i*.35
        beam('Thatch ribs',(xx,y-d/2-.58,z+2.73),(xx,y,z+4.03),.045,plank)
        beam('Thatch ribs',(xx,y,z+4.03),(xx,y+d/2+.58,z+2.73),.045,plank)
    for i in range(3):cube('Entry step',(x,y-d/2-.4-i*.35,z-.15-i*.19),(1.6,.45,.18),plank)
    return z
current=cols['03_Village_Supply']
hut(-70,-49);hut(-55,-57,6,4);hut(-70,-62,6,5)
for x,y in [(-62,-57),(-61,-57),(-59,-49),(-59,-47)]:crate(x,y)
for i in range(13):
    x=-78+i*1.5;beam('Bamboo fence',ground(x,-42),ground(x,-42,1.6),.075,plank)
for z in [.65,1.25]:beam('Fence rail',ground(-78,-42,z),ground(-60,-42,z),.06,wood)
current=cols['04_Patrol_Cover']
bags(-30,-13,6,.35);bags(-19,-6,5,-.8);bags(-34,-2,4,1.5)
for x,y in [(-24,-3),(-22,-3),(-24,-1)]:crate(x,y)
beam('Fallen trunk cover',ground(-42,-17,.6),ground(-36,-11,.6),.62,wood,12)
for x,y in [(-32,-18),(-38,5),(-17,9),(-8,-15)]:ico('Tall rock LOS blocker',ground(x,y,1.2),(2.5,1.8,2.1),stone,2)
current=cols['05_Bridge']
# Dry approaches meet a full-span bridge across the sunken river channel.
for i in range(55):
    x=-4+i*.43;cube('Bridge decking',(x,0,3.05),(.40,4,.22),plank)
for y in [-1.65,1.65]:
    beam('Bridge longitudinal beam',(-4,y,2.7),(19.3,y,2.7),.19,wood)
    for x in [-3,2,7,12,18]:
        beam('Bridge pile',(x,y,-.7),(x,y,4.25),.17,wood)
    beam('Bridge handrail',(-3,y,4.1),(18,y,4.1),.075,wood)
    for x in [-2,7,16]:beam('Bridge cross brace',(x,y,.2),(x+3,y,2.7),.11,wood)
for a,b in [(-8,-4),(19,24)]:
    for i in range(12):
        x=a+(b-a)*i/11;z=h(a if a<0 else b,0)*(1-i/11)+3.05*(i/11) if a<0 else 3.05*(1-i/11)+h(b,0)*i/11
        cube('Bridge approach ramp',(x,0,z),(.46,4,.18),plank)
current=cols['06_Abandoned_Base']
floorz=hut(46,39,10,7,True);hut(34,33,6,5,True)
# Open intelligence shelter, visible from the isometric camera.
z=h(51,26)
cube('Intelligence shelter floor',(51,26,z+.15),(7,5,.3),plank)
for x in [47.8,54.2]:
    beam('Shelter upright',(x,28,z),(x,28,z+3.6),.13,wood)
cube('Back wall of intelligence shelter',(51,28.3,z+1.6),(7,.16,3),wood)
cube('Map table',(51,26,z+1.3),(3.3,1.9,.16),plank)
for x in [49.6,52.4]:
    for y in [25.3,26.7]:cube('Table leg',(x,y,z+.7),(.12,.12,1.2),wood)
cube('Evidence • terrain map',(50.8,26,z+1.395),(2,1.4,.025),paper)
for i in range(6):
    cube('Secret supply route on map',(50.1+i*.25,26+.32*sin(i),z+1.414),(.38,.035,.008),ink,i*.3)
for x,y in [(52.2,25.6),(52.1,26.4)]:cube('Evidence • route notes',(x,y,z+1.41),(.45,.55,.035),paper,.12)
for x,y in [(39,24),(41,24),(42,40),(55,37)]:crate(x,y)
bags(43,19,10);bags(60,30,8,pi/2);bags(30,41,6,.2)
for i in range(18):
    x=29+i*1.8
    if 43<x<49:continue
    beam('Broken perimeter stakes',ground(x,46),ground(x,46,random.uniform(1,2)),.075,wood)
for x,y in [(38,28),(40,28),(56,39)]:
    bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=.46,depth=1.25,location=ground(x,y,.65));o=move(bpy.context.object);o.name='Rusted empty fuel drum';o.data.materials.append(metal)
current=cols['07_Overlook']
x,y=-55,46;z=h(x,y)
for dx in [-2,2]:
    for dy in [-2,2]:beam('Watch platform support',(x+dx,y+dy,h(x+dx,y+dy)),(x+dx,y+dy,z+5),.2,wood)
cube('Observation platform',(x,y,z+4),(5,5,.23),plank)
for dy in [-2.3,2.3]:beam('Watch railing',(x-2.3,y+dy,z+5),(x+2.3,y+dy,z+5),.1,wood)
mesh('Watch hut roof',[(x-3,y-3,z+6),(x+3,y-3,z+6),(x+3,y+3,z+6),(x-3,y+3,z+6),(x,y,z+7.3)],[(0,1,4),(1,2,4),(2,3,4),(3,0,4)],roof)
for i in range(12):beam('Ladder rung',(x-1,y-3,z+i*.34),(x+0,y-3,z+i*.34),.06,plank)
bags(-55,38,6)
# Shared mesh vegetation prototypes: detailed silhouette without unique heavy meshes.
current=cols['08_Forest']
templates=[]
for t in range(5):
    parts=[]
    parts.append(beam('trunk',(0,0,0),(.18,-.1,7.6),.3,wood))
    for a in range(5):
        an=a*2*pi/5+t;ex=2.0*cos(an);ey=2*sin(an)
        parts.append(beam('branch',(0,0,4.5),(ex,ey,7.5),.12,wood))
        parts.append(ico('crown',(ex,ey,7.8+random.uniform(-.5,.8)),(2.1,1.9,1.8),leaves[(t+a)%5],2))
    parts.append(ico('crown',(0,0,9),(2.6,2.3,2),leaves[t],2))
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=parts[0];bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');o.name='Canopy master %d'%t;templates.append(o);o.hide_render=True;o.hide_viewport=True
def nearpath(x,y,margin):return any((x-px)**2+(y-py)**2<(w/2+margin)**2 for px,py,w in allpaths)
count=0
for k in range(2100):
    x=random.uniform(-88,88);y=random.uniform(-78,78)
    if abs(x-riverx(y))<7 or nearpath(x,y,3.2) or any((x-a)**2+(y-b)**2<(r+2)**2 for a,b,r in clearings):continue
    # Retain clear views into playable areas from the fixed south-facing camera.
    o=bpy.data.objects.new('Forest canopy %04d'%count,random.choice(templates).data);current.objects.link(o);o.location=ground(x,y);s=random.uniform(.65,1.18);o.scale=(s,s,s*random.uniform(.8,1.2));o.rotation_euler.z=random.random()*2*pi;count+=1
# Palm fronds are individually shaped, bent ribbon meshes.
def palm(x,y,s=1):
    z=h(x,y);beam('Palm trunk',(x,y,z),(x+.6*s,y,z+8*s),.18*s,wood)
    for k in range(9):
        a=k*2*pi/9;v=[]
        for j in range(7):
            t=j/6;rad=t*4.6*s;zz=z+s*(8+1.8*sin(t*pi)-1.8*t);wid=.56*sin(t*pi)*s
            for side in [-1,1]:v.append((x+.6*s+rad*cos(a)+side*wid*sin(a),y+rad*sin(a)-side*wid*cos(a),zz))
        mesh('Palm frond',v,[(j*2,j*2+1,j*2+3,j*2+2) for j in range(6)],random.choice(leaves))
for x,y in [(-77,-35),(-49,-51),(-78,-64),(-64,-34),(-42,22),(25,12),(29,47),(65,37),(59,20),(-5,-35),(4,27),(27,60),(65,-47),(-73,62),(-18,44)]:palm(x,y,random.uniform(.85,1.2))
current=cols['09_Undergrowth']
for k in range(1250):
    x=random.uniform(-88,88);y=random.uniform(-78,78)
    if abs(x-riverx(y))<5 or nearpath(x,y,.9) or any((x-a)**2+(y-b)**2<(r*.85)**2 for a,b,r in clearings):continue
    if k%3==0:ico('Mossy rock',ground(x,y,.3),(random.uniform(.4,1.5),random.uniform(.4,1.3),random.uniform(.4,.9)),stone,1)
    else:
        z=h(x,y);v=[];f=[]
        for j in range(7):
            a=j*2*pi/7;rr=random.uniform(.65,1.5);idx=len(v)
            v.extend([(x,y,z),(x+rr*.6*cos(a)-.18*sin(a),y+rr*.6*sin(a)+.18*cos(a),z+.6),(x+rr*cos(a),y+rr*sin(a),z+.3),(x+rr*.6*cos(a)+.18*sin(a),y+rr*.6*sin(a)-.18*cos(a),z+.6)]);f.append((idx,idx+1,idx+2,idx+3))
        mesh('Forest fern',v,f,fernmat)
current=cols['10_Gameplay_Anchors'];anchors=[]
def anchor(n,x,y,kind):
    o=bpy.data.objects.new(n,None);current.objects.link(o);o.location=ground(x,y,1);o.empty_display_type='SPHERE';o.empty_display_size=.7;o['role']=kind;anchors.append({'name':n,'role':kind,'blender_xyz':list(o.location)})
for n,x,y,k in [('Spawn_Nam',-63,-55,'spawn'),('Companion_Hung',-61,-54,'companion'),('Supply_pickup',-62,-57,'loot'),('Crafting_intro',-59,-49,'crafting'),('Stamina_trail',-47,-34,'tutorial'),('Patrol_01',-30,-10,'patrol'),('Patrol_02',-23,-7,'patrol'),('Patrol_03',-27,-1,'patrol'),('Noise_diversion',-12,-18,'distraction'),('Investigation_reinforcements',28,8,'investigate'),('Hung_dialogue_after_encounter',24,1,'story'),('Evidence_secret_routes',51,26,'quest_evidence'),('Exit_toward_river',36,67,'exit')]:anchor(n,x,y,k)
for i,(x,y) in enumerate([(-30,-13),(-19,-6),(-34,-2),(43,19),(60,30),(30,41)]):anchor('Cover_%02d'%i,x,y,'cover')
current=cols['11_Lighting_Cameras']
world=bpy.data.worlds.new('Forest atmosphere');bpy.context.scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.43,.56,.65,1);world.node_tree.nodes['Background'].inputs[1].default_value=.35
bpy.ops.object.light_add(type='SUN',location=(-80,-60,100));sun=move(bpy.context.object);sun.name='Late morning sun';sun.rotation_euler=(.45,-.55,-.4);sun.data.energy=3;sun.data.angle=.13
bpy.ops.object.light_add(type='AREA',location=(0,-60,120));light=move(bpy.context.object);light.data.energy=18000;light.data.shape='DISK';light.data.size=110
def camera(n,loc,target,ortho):
    bpy.ops.object.camera_add(location=loc);o=move(bpy.context.object);o.name=n;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();o.data.type='ORTHO';o.data.ortho_scale=ortho;o.data.lens=45;o.data.clip_end=1500;return o
cam=camera('CAM_01 • Isometric overview',(195,-250,240),(0,0,0),228)
top=camera('CAM_02 • Top down layout',(0,0,300),(0,0,0),195)
detail=camera('CAM_03 • Abandoned base',(90,-27,70),(45,31,3),64)
scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=1800;scene.render.resolution_y=1500;scene.render.resolution_percentage=100
scene.world.color=(.15,.18,.2);scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.unit_settings.system='METRIC';scene['Map']='MAP 1 — Những dấu chân trong rừng';scene['Scope']='Environment only. Gameplay anchors are metadata, no AI or combat implementation.'
scene['Route']='Supply village > patrol clearing (stealth / diversion / combat) > wooden bridge > abandoned base > river exit'
scene['Reference']='ShadowVale capstone proposal + user Map 1 narrative + supplied forest map image. Original procedural assets.'
# Open directly on the overview with material colors and sensible clipping.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.clip_end=1500;area.spaces.active.shading.color_type='MATERIAL'
bpy.ops.object.select_all(action='DESELECT');terrain.select_set(True);bpy.context.view_layer.objects.active=terrain
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'ShadowVale_Map01.blend'))
with open(os.path.join(OUT,'Map01_anchors.json'),'w',encoding='utf8') as f:json.dump({'units':'meters','coordinates':'Blender Z-up','anchors':anchors},f,ensure_ascii=False,indent=2)
scene.render.filepath=os.path.join(OUT,'Map01_Isometric.png');bpy.ops.render.render(write_still=True)
scene.camera=top;scene.render.resolution_x=1600;scene.render.resolution_y=1400;scene.render.filepath=os.path.join(OUT,'Map01_TopDown.png');bpy.ops.render.render(write_still=True)
scene.camera=detail;scene.render.resolution_x=1500;scene.render.resolution_y=1100;scene.render.filepath=os.path.join(OUT,'Map01_AbandonedBase.png');bpy.ops.render.render(write_still=True)
print('MAP_COMPLETE',count,'trees',len(bpy.data.objects),'objects')
