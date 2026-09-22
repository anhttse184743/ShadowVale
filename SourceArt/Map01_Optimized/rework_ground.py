import bpy,math,random,os,json
from mathutils import Vector
random.seed(925)
ROOT=os.environ.get('SHADOWVALE_PROJECT',r'G:\game\ShadowVale');OUT=os.path.join(ROOT,'SourceArt','Map01_Optimized')
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT,'ShadowVale_Map01_Master.blend'))
def rx(y):return 8+12*math.sin(y*.041)+4*math.sin(y*.105)
def h(x,y):
    hills=14*math.exp(-((x+57)**2/440+(y-48)**2/520))+7*math.exp(-((x-68)**2/650+(y-57)**2/700))
    raw=2.7+.7*math.sin(x*.11)*math.cos(y*.09)+.4*math.sin(y*.22+x*.1)+hills
    return -.8+(raw+.8)*min(1,max(0,(abs(x-rx(y))-3)/8))
def mat(n,c):
    m=bpy.data.materials.get(n) or bpy.data.materials.new(n);m.diffuse_color=(*c,1);m.use_nodes=True;m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*c,1);return m
col=bpy.data.collections.get('14_Natural trails and groundcover')
if col:
    for o in list(col.objects):bpy.data.objects.remove(o,do_unlink=True)
else:col=bpy.data.collections.new('14_Natural trails and groundcover');bpy.context.scene.collection.children.link(col)
def mesh(n,v,f,m):
    me=bpy.data.meshes.new(n);me.from_pydata(v,[],f);me.update();me.materials.append(m);o=bpy.data.objects.new(n,me);col.objects.link(o);return o
wood=bpy.data.materials['Aged teak']
def beam(n,a,b,r):
    axis=Vector(b)-Vector(a);q=axis.to_track_quat('Z','Y').to_matrix();v=[]
    for z in [0,axis.length]:
        for i in range(5):v.append(tuple(Vector(a)+q@Vector((r*math.cos(i*math.tau/5),r*math.sin(i*math.tau/5),z))))
    return mesh(n,v,[(i,(i+1)%5,(i+1)%5+5,i+5) for i in range(5)],wood)
soil=mat('Worn ochre trail',(.27,.235,.155));edge=mat('Trail grassy edge',(.205,.24,.115))
for o in list(bpy.data.objects):
    if o.name.startswith(('Supply road','Stealth bypass','Diversion flank','Base back entry','Overlook','River exit','Extended trail','Trail footprint','Thatch ribs')):bpy.data.objects.remove(o,do_unlink=True)
paths=[([(-86,-85),(-78,-73),(-66,-61),(-56,-49),(-47,-34),(-37,-22),(-27,-10),(-13,0),(-5,0)],1.9), ([(20,0),(25,1),(35,14),(44,34)],1.9),([(-47,-34),(-60,-22),(-59,-5),(-46,9),(-29,15),(-13,10),(-5,1)],1.3),([(-37,-22),(-23,-26),(-10,-18),(-9,-7),(-13,0)],1.4), ([(25,1),(39,-4),(58,3),(61,18),(54,34)],1.5),([(-46,9),(-54,24),(-55,45)],1.4), ([(44,34),(57,44),(47,57),(36,67),(35,77),(26,83),(18,85)],1.8), ([(58,3),(77,13),(83,31),(81,49),(70,61),(57,44)],1.5)]
samples=[]
for idx,(pts,width) in enumerate(paths):
    out=[]
    for i in range(len(pts)-1):
        a=Vector(pts[max(0,i-1)]);b=Vector(pts[i]);c=Vector(pts[i+1]);d=Vector(pts[min(len(pts)-1,i+2)])
        n=max(4,int((c-b).length*3))
        for j in range(n):
            t=j/n;out.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
    out.append(Vector(pts[-1]));v=[];f=[];ids=[]
    for i,p in enumerate(out):
        tangent=(out[min(len(out)-1,i+1)]-out[max(0,i-1)]).normalized();side=Vector((-tangent.y,tangent.x));w=width*(1+.12*math.sin(i*.12+idx))
        samples.append((p.x,p.y,w))
        for offset in [-.7,-.45,0,.45,.7]:
            q=p+side*w*offset;v.append((q.x,q.y,h(q.x,q.y)+.045))
        if i:
            for k in range(4):f.append(((i-1)*5+k,(i-1)*5+k+1,i*5+k+1,i*5+k));ids.append(1 if k in [0,3] else 0)
    o=mesh('Natural woodland footpath %d'%idx,v,f,soil);o.data.materials.append(edge)
    for p,m in zip(o.data.polygons,ids):p.material_index=m
# Replace large perfect discs with irregular, terrain-hugging packed earth.
for o in bpy.data.objects:
    if o.type=='MESH' and o.name.startswith(('Trampled clearing','New rest clearing')):
        cx=sum(v.co.x for v in o.data.vertices)/len(o.data.vertices);cy=sum(v.co.y for v in o.data.vertices)/len(o.data.vertices)
        for v in o.data.vertices:
            dx=v.co.x-cx;dy=v.co.y-cy;a=math.atan2(dy,dx);factor=.93+.065*math.sin(a*5)+.04*math.cos(a*9)
            v.co.x=cx+dx*factor;v.co.y=cy+dy*factor;v.co.z=h(v.co.x,v.co.y)+.05
# Woven palm thatch: individual overlapping strips, uneven hanging eaves.
thatch=[mat('Thatch straw %d'%i,c) for i,c in enumerate([(.30,.25,.15),(.38,.32,.20),(.43,.365,.235),(.34,.285,.18)])]
for o in list(bpy.data.objects):
    if not o.name.startswith(('Removable thatch roof','River shelter thatch')):continue
    coords=[o.matrix_world@v.co for v in o.data.vertices];xmin=min(v.x for v in coords);xmax=max(v.x for v in coords);ymin=min(v.y for v in coords);ymax=max(v.y for v in coords);zmin=min(v.z for v in coords);zmax=max(v.z for v in coords);ymid=(ymin+ymax)/2
    bpy.data.objects.remove(o,do_unlink=True)
    v=[];f=[];colors=[]
    for side in [-1,1]:
        extent=(ymax-ymin)/2
        for row in range(9):
            t0=row/9;t1=min(1.08,(row+1.7)/9)
            count=int((xmax-xmin)/.10)
            for j in range(count):
                x=xmin+j*(xmax-xmin)/count;w=(xmax-xmin)/count*1.12;tip=t1+random.uniform(-.025,.025);z0=zmax+(zmin-zmax)*t0+.035;z1=zmax+(zmin-zmax)*tip+.035
                start=len(v);v.extend([(x,ymid+side*extent*t0,z0),(x+w,ymid+side*extent*t0,z0),(x+w*.85,ymid+side*extent*tip,z1),(x+w*.15,ymid+side*extent*tip,z1)]);f.append((start,start+1,start+2,start+3));colors.append(random.randrange(4))
    roof=mesh('Layered palm thatch roof',v,f,thatch[0])
    for m in thatch[1:]:roof.data.materials.append(m)
    for p,c in zip(roof.data.polygons,colors):p.material_index=c
    beam('Thatched ridge cap',(xmin,ymid,zmax+.07),(xmax,ymid,zmax+.07),.095)
    # Front veranda: slender posts and open shaded porch beside existing wall.
    for x in [xmin+.25,xmax-.25]:beam('Veranda bamboo post',(x,ymin,h(x,ymin)),(x,ymin,zmin-.08),.085)
    beam('Veranda eave beam',(xmin,ymin,zmin-.1),(xmax,ymin,zmin-.1),.08)
# Four shared 3m patches: short blades, broad grass, fern and bank sedge.
greens=[mat('Groundcover %d'%i,c) for i,c in enumerate([(.19,.30,.085),(.25,.35,.10),(.13,.255,.09),(.28,.34,.15)])]
patches=[]
for kind in range(4):
    v=[];f=[]
    for tuft in range(15):
        x=random.uniform(-1.4,1.4);y=random.uniform(-1.4,1.4)
        for blade in range(7 if kind!=2 else 5):
            a=random.random()*math.tau;length=random.uniform(.28,.65)*(1.4 if kind==3 else 1);width=.045 if kind in [0,3] else .10
            base=Vector((x,y,0));tip=base+Vector((math.cos(a)*length*.55,math.sin(a)*length*.55,length));side=Vector((-math.sin(a)*width,math.cos(a)*width,0));mid=(base+tip)/2+Vector((0,0,.07));i=len(v);v.extend(map(tuple,[base-side,base+side,mid+side,mid-side,tip]));f.extend([(i,i+1,i+2,i+3),(i+3,i+2,i+4)])
            if kind==2:
                for n in [1,2,3]:
                    p=base+(tip-base)*n/4;s=side*(3*(1-n/5));i=len(v);v.extend(map(tuple,[p-s,p+Vector((0,0,.06)),p+s,p+(tip-base)*.22]));f.extend([(i,i+1,i+3),(i+1,i+2,i+3)])
    p=mesh('Grass prototype %d'%kind,v,f,greens[kind]);p.hide_render=True;patches.append(p.data)
# Spatial grid makes route exclusion cheap and leaves playable centers clear.
bins={}
for x,y,w in samples:bins.setdefault((int(x//4),int(y//4)),[]).append((x,y,w))
clearings=[(-62,-53,11),(-27,-9,10),(45,34,15),(-55,46,7),(22,85,7),(72,60,7)]
count=0
for i in range(5400):
    x=random.uniform(-108,108);y=random.uniform(-98,98);bank=abs(x-rx(y))
    if bank<4.5 or any((x-a)**2+(y-b)**2<r*r for a,b,r in clearings):continue
    near=[p for dx in [-1,0,1] for dy in [-1,0,1] for p in bins.get((int(x//4)+dx,int(y//4)+dy),[])]
    if any((x-a)**2+(y-b)**2<(w*.5+1.3)**2 for a,b,w in near):continue
    kind=3 if bank<8 else random.randrange(3);o=bpy.data.objects.new('Groundcover_%d_%04d'%(kind,count),patches[kind]);col.objects.link(o);o.location=(x,y,h(x,y)+.01);s=random.uniform(.65,1.2);o.scale=(s,s,s);o.rotation_euler.z=random.random()*math.tau;count+=1
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'ShadowVale_Map01_Master.blend'))
print('NATURAL_GROUND_COMPLETE',count,flush=True)

