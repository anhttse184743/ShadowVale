"""Deterministic Blender source for Map 2 roadside planting; no external scripts required."""
import bpy, math, random, json, gzip, os
from mathutils import Vector
OUT=os.path.dirname(os.path.abspath(__file__))
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
COLORS=[(.16,.27,.065,1),(.32,.40,.13,1),(.52,.51,.22,1),(.065,.19,.035,1),(.14,.32,.055,1),(.28,.44,.095,1),(.36,.31,.20,1),(.46,.52,.18,1)]
mats=[]
for i,c in enumerate(COLORS):
 m=bpy.data.materials.new(['Bamboo jade','Bamboo olive','Raised stem nodes','Leaf shadow','Leaf green','Sunlit leaf','Branch bark','Dry grass tips'][i]);m.diffuse_color=c;mats.append(m)
class Geo:
 def __init__(self): self.v=[];self.f=[];self.m=[]
 def face(self,vs,mat,two=False):
  n=len(self.v);self.v.extend([tuple(v) for v in vs]);self.f.append(tuple(range(n,n+len(vs))));self.m.append(mat)
  if two:self.f.append(tuple(reversed(range(n,n+len(vs)))));self.m.append(mat)
 def rod(self,points,radii,mat,sides=6):
  for j in range(len(points)-1):
   a,b=Vector(points[j]),Vector(points[j+1]);d=(b-a).normalized();u=d.cross(Vector((0,1,0))).normalized();v=d.cross(u).normalized()
   for k in range(sides):
    p=u*math.cos(k*math.tau/sides)+v*math.sin(k*math.tau/sides);q=u*math.cos((k+1)*math.tau/sides)+v*math.sin((k+1)*math.tau/sides)
    self.face([a+p*radii[j],a+q*radii[j],b+q*radii[j+1],b+p*radii[j+1]],mat)
 def leaf(self,p,d,w,mat):
  p=Vector(p);d=Vector(d);side=d.cross(Vector((0,0,1))).normalized()*w;mid=p+d*.48+Vector((0,0,.045));self.face([p,mid+side,p+d,mid-side],mat,True)
 def mesh(self,name):
  me=bpy.data.meshes.new(name);me.from_pydata(self.v,[],self.f);me.update();ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
  for m in mats:me.materials.append(m)
  for p,m in zip(me.polygons,self.m):p.material_index=m
  return ob

def bamboo(lod):
 g=Geo();r=random.Random(261001);count=27 if lod==0 else 16
 for j in range(count):
  a=j*2.39996;rad=math.sqrt((j+.5)/count)*1.35;d=Vector((math.cos(a),math.sin(a),0));base=d*rad;h=r.uniform(6.2,9.0);lean=d*r.uniform(1.5,3.1);pts=[base+lean*(t/10)**1.8+Vector((0,0,h*t/10)) for t in range(11)]
  g.rod(pts,[.067*(1-.65*t/10) for t in range(11)],j%2,7 if lod==0 else 4)
  if lod==0:
   for k in range(1,10):
    p=pts[k];axis=(pts[k+1]-pts[k]).normalized();rr=.071*(1-.65*k/10);g.rod([p-axis*.027,p+axis*.027],[rr,rr],2,7)
  for k in range(5 if lod==0 else 4):
   t=6+k;start=pts[t];az=a+k*2.2;branch=Vector((math.cos(az),math.sin(az),.20))*r.uniform(1.4,2.1);tip=start+branch+Vector((0,0,-.2));g.rod([start,start+branch*.6+Vector((0,0,.25)),tip],[.022,.013,.003],0,4)
   for n in range(16 if lod==0 else 8):
    p=start+branch*(.15+n/(19 if lod==0 else 11));side=Vector((-branch.y,branch.x,0)).normalized()*(1 if n%2 else -1)
    g.leaf(p,side*r.uniform(.43,.68)+branch.normalized()*.25+Vector((0,0,-.24)),.055 if lod==0 else .10,3+n%3)
  # Dense, irregular terminal sprays complete the leafy fan above the visible culms.
  for n in range(80 if lod==0 else 38):
   az=r.random()*math.tau;rad=math.sqrt(r.random())*1.45;center=pts[9]+d*.5;pos=center+Vector((math.cos(az)*rad,math.sin(az)*rad,r.uniform(-.65,.65)));direction=Vector((math.cos(az),math.sin(az),r.uniform(-.55,.15)))*r.uniform(.48,.78)
   g.leaf(pos,direction,.065 if lod==0 else .095,3+n%3)
 return g

def shrub(lod,tree=False):
 g=Geo();r=random.Random(260 if tree else 99);arms=10 if tree else 14
 if tree:g.rod([(0,0,0),(.08,0,1.5),(-.08,.05,3.7)],[.22,.16,.055],6,8)
 for j in range(arms):
  a=j*2.399;d=Vector((math.cos(a),math.sin(a),0));reach=r.uniform(1.5,2.7) if tree else r.uniform(.45,1.1);end=d*reach+Vector((0,0,r.uniform(4.0,5.8) if tree else r.uniform(.65,1.4)));base=Vector((0,0,2 if tree else .06));g.rod([base,(base+end)*.55,end],[.075 if tree else .035,.037 if tree else .02,.008],6,6 if lod==0 else 4)
  for k in range((42 if tree else 27) if lod==0 else (17 if tree else 12)):
   az=r.random()*math.tau;rad=math.sqrt(r.random())*(.9 if tree else .47);p=end+Vector((math.cos(az)*rad,math.sin(az)*rad,r.uniform(-.3,.4)));dd=Vector((math.cos(az),math.sin(az),r.uniform(-.35,.45)))*(.45 if tree else .32)
   g.leaf(p,dd,.12 if tree else .075,3+r.randrange(3))
 return g

def grass(lod):
 g=Geo();r=random.Random(734)
 for j in range(22 if lod==0 else 9):
  a=r.random()*math.tau;d=Vector((math.cos(a),math.sin(a),0));base=d*r.uniform(.01,.24);h=r.uniform(.15,.43);side=Vector((-d.y,d.x,0))*r.uniform(.010,.021);mid=base+d*.07+Vector((0,0,h*.6));tip=base+d*r.uniform(.1,.27)+Vector((0,0,h));g.face([base-side,base+side,mid+side*.55,tip,mid-side*.55],3+j%3,True)
 return g
payload={'meshes':[]};budget={}
for name,fn in [('Bamboo',bamboo),('Hedge',lambda l:shrub(l)),('RoadTree',lambda l:shrub(l,True)),('Meadow',grass)]:
 for lod in range(2):
  ob=fn(lod).mesh(name+'_LOD'+str(lod));me=ob.data;me.calc_loop_triangles();v=[];norm=[];colors=[];tri=[];lookup={}
  for face in me.loop_triangles:
   poly=me.polygons[face.polygon_index];c=COLORS[poly.material_index];ids=[]
   for idx in face.vertices:
    p=me.vertices[idx].co;n=poly.normal;key=tuple(round(x,5) for x in (p.x,p.z,p.y,n.x,n.z,n.y))+c
    if key not in lookup:lookup[key]=len(v)//3;v.extend(key[:3]);norm.extend(key[3:6]);colors.extend(key[6:])
    ids.append(lookup[key])
   tri.extend([ids[0],ids[2],ids[1]])
  payload['meshes'].append(dict(id=ob.name,vertices=v,normals=norm,colors=colors,triangles=tri));budget[ob.name]={'vertices':len(v)//3,'triangles':len(tri)//3};ob.location=(list(['Bamboo','Hedge','RoadTree','Meadow']).index(name)*12,lod*15,0)
bpy.context.scene['notes']='Map 2: flared bamboo culms with raised nodes, hanging lanceolate leaves; leafy bund hedges; small roadside trees; curved meadow blades. Unity export is Y up, scale in metres.'
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/Map02_Roadside_Details.blend')
with gzip.open(OUT+'/Roadside.meshdata.json.gz','wt',encoding='utf8') as f:json.dump(payload,f,separators=(',',':'))
with open(OUT+'/roadside-budget.json','w') as f:json.dump(budget,f,indent=2)
print('ROADSIDE_BLENDER_COMPLETE',budget)


