"""Build reusable tropical assets in Blender; export compact Unity mesh data.
Run: blender --background --python SourceArt/Map01_Realism/build_assets.py
No external assets or textures are required. Coordinates in Blender are Z-up.
"""
import bpy, math, random, json, gzip, os
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'SourceArt','Map01_Realism');os.makedirs(OUT,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
random.seed(921)
palette=[('Bark',(.28,.245,.17,1)),('Young bark',(.39,.34,.23,1)),('Leaf shade',(.10,.23,.035,1)),('Leaf mid',(.22,.37,.065,1)),('Leaf light',(.34,.48,.12,1)),('Dry leaf',(.43,.38,.19,1)),('Timber',(.31,.23,.135,1)),('Thatch',(.46,.39,.22,1))]
mats=[]
for name,color in palette:
 m=bpy.data.materials.new(name);m.diffuse_color=color;m.use_nodes=True;m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=color;m.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.86;mats.append(m)
class Geo:
 def __init__(self):self.v=[];self.f=[];self.mi=[]
 def face(self,pts,mat):
  n=len(self.v);self.v.extend([tuple(p) for p in pts]);self.f.append(tuple(range(n,n+len(pts))));self.mi.append(mat)
 def rod(self,points,radii,mat=0,sides=6):
  for k in range(len(points)-1):
   a,b=Vector(points[k]),Vector(points[k+1]);q=(b-a).to_track_quat('Z','Y');rings=[]
   for p,r in [(a,radii[k]),(b,radii[k+1])]:rings.append([p+q@Vector((math.cos(j*math.tau/sides)*r,math.sin(j*math.tau/sides)*r,0)) for j in range(sides)])
   for j in range(sides):self.face([rings[0][j],rings[0][(j+1)%sides],rings[1][(j+1)%sides],rings[1][j]],mat)
 def leaf(self,points,widths,mat=3,ridge=.03,torn=False):
  pts=list(map(Vector,points))
  for i in range(len(pts)-1):
   tangent=(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized();side=tangent.cross(Vector((0,0,1))).normalized()
   if side.length<.1:side=Vector((1,0,0))
   for sign in [-1,1]:
    w=widths[i]*(.76 if torn and i%3==0 else 1);wn=widths[i+1]*(.68 if torn and i%3==2 else 1)
    self.face([pts[i]+Vector((0,0,ridge)),pts[i]+side*w*sign,pts[i+1]+side*wn*sign,pts[i+1]+Vector((0,0,ridge))],mat)
 def box(self,c,s,mat=6,angle=0):
  c=Vector(c);vs=[]
  for x,y,z in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]:
   p=Vector((x*s[0]/2,y*s[1]/2,z*s[2]/2));p=Vector((p.x*math.cos(angle)-p.z*math.sin(angle),p.y,p.x*math.sin(angle)+p.z*math.cos(angle)));vs.append(c+p)
  for f in [(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]:self.face([vs[j] for j in f],mat)
 def mesh(self,name):
  me=bpy.data.meshes.new(name);me.from_pydata(self.v,[],self.f);me.update()
  for m in mats:me.materials.append(m)
  for p,m in zip(me.polygons,self.mi):p.material_index=m
  ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);return ob

def forest(variant,lod):
 r=random.Random(800+variant);g=Geo();height=8.7+variant*.5;lean=Vector((r.uniform(-.5,.5),r.uniform(-.4,.4),0));top=Vector((0,0,height))+lean
 g.rod([(0,0,0),(.08,-.04,height*.42),top],[.34,.22,.09],0,8 if lod==0 else 5)
 # Flared buttress/stilt roots as in the wetland reference.
 for j in range(5 if lod<2 else 3):
  a=j*math.tau/5+.3*variant;g.rod([(math.cos(a)*1.0,math.sin(a)*1.0,0),(math.cos(a)*.4,math.sin(a)*.4,.75),(0,0,2.1)],[.11,.11,.14],0,4)
 for k in range(6 if lod<2 else 4):
  a=k*2.4+variant;center=top+Vector((math.cos(a)*1.65,math.sin(a)*1.65,-1.0-r.random()*2.4))
  g.rod([(0,0,height*.52),center],[.12,.025],0,5 if lod==0 else 3)
  for b in range([9,5,3][lod]):
   ba=b*2.4+a;tip=center+Vector((math.cos(ba)*r.uniform(.4,1.4),math.sin(ba)*r.uniform(.4,1.4),r.uniform(-.25,.55)))
   if lod==0:g.rod([center,tip],[.025,.006],1,3)
   for j in range([7,4,2][lod]):
    p=tip+Vector((r.uniform(-.5,.5),r.uniform(-.5,.5),r.uniform(-.4,.4)))
    d=Vector((math.cos(ba+j)*.65,math.sin(ba+j)*.65,r.uniform(-.2,.3)))*(1+lod*.4)
    g.leaf([p,p+d*.45,p+d],[0,.15*(1+lod*.5),0],2+r.randrange(3),.025)
 return g

def palm(lod):
 g=Geo();h=9;points=[]
 for i in range(10):
  u=i/9;points.append((.95*u*u,.12*math.sin(u*3),h*u))
 g.rod(points,[.26-i*.009 for i in range(10)],1,9 if lod==0 else 5)
 if lod==0:
  for j in range(1,27):
   u=j/28;p=Vector((.95*u*u,.12*math.sin(u*3),h*u));g.rod([p,p+Vector((0,0,.035))],[.265-u*.08,.265-u*.08],0,8)
 crown=Vector(points[-1])
 for k in range([11,9,7][lod]):
  a=k*math.tau/[11,9,7][lod];d=Vector((math.cos(a),math.sin(a),0));side=Vector((-d.y,d.x,0));length=3.6+(k%3)*.2
  def curve(u):return crown+d*length*u+Vector((0,0,math.sin(u*math.pi)*.6-u*u*1.6))
  if lod<2:g.rod([curve(j/6) for j in range(7)],[.035*(1-j/7) for j in range(7)],3,3)
  count=[16,10,6][lod]
  for j in range(1,count):
   u=j/count;p=curve(u);reach=.78*math.sin(u*math.pi)**.6
   for sign in [-1,1]:
    q=p+side*sign*reach+d*.2-Vector((0,0,.12+.22*u));g.leaf([p,(p+q)*.5+Vector((0,0,.06)),q],[.01,.06*(1+lod*.25),0],2+(j+k)%3,.012)
 for j in range(4):
  a=j*math.tau/4;p=crown+Vector((math.cos(a)*.3,math.sin(a)*.3,-.3));g.rod([p-Vector((0,0,.18)),p,p+Vector((0,0,.2))],[.08,.18,.07],5,6)
 return g

def banana(lod):
 g=Geo();g.rod([(0,0,0),(.1,0,1.7),(.12,0,3.3)],[.22,.17,.07],3,7 if lod==0 else 5)
 for k in range([9,7,5][lod]):
  a=k*2.4;d=Vector((math.cos(a),math.sin(a),0));base=Vector((.1,0,2.3+(k%3)*.38));length=2.4+(k%2)*.4
  n=[10,6,4][lod];pts=[];width=[]
  for j in range(n+1):
   u=j/n;pts.append(base+d*length*u+Vector((0,0,math.sin(u*math.pi)*.9-u*u*.75)));width.append(.48*math.sin(u*math.pi)**.7)
  g.leaf(pts,width,2+k%3,.035,lod==0)
  if lod==0:g.rod(pts,[.021*(1-j/(n+1)) for j in range(n+1)],4,3)
 # Furled emerging spear.
 g.leaf([(0,0,2.6),(.08,0,3.9),(.12,0,4.5)],[.03,.10,0],4)
 return g

def grass(lod):
 g=Geo();r=random.Random(321)
 for i in range([32,16,7][lod]):
  a=r.random()*math.tau;d=Vector((math.cos(a),math.sin(a),0));p=Vector((r.uniform(-.2,.2),r.uniform(-.2,.2),0));height=r.uniform(.65,1.25);bend=r.uniform(.35,.8)
  n=4 if lod==0 else 2;pts=[];width=[]
  for j in range(n+1):
   u=j/n;pts.append(p+d*bend*u*u+Vector((0,0,height*(u-.25*u*u))));width.append(.027*(1-u)**.6)
  g.leaf(pts,width,2+i%3,.003)
 return g

def house(lod):
 g=Geo();g.box((0,0,.1),(6,5,.2))
 # Boards with narrow gaps, framed windows and a walk-through front doorway.
 for side in [-1,1]:
  for j in range(22 if lod==0 else 11):
   z=.2+(j+.5)*2.85/(22 if lod==0 else 11)
   for y,span in [(-1.7,1.45),(1.7,1.45)]:g.box((side*2.9,y,z),(.15,span,2.85/(22 if lod==0 else 11)-.012))
   if z<1.1 or z>2.25:g.box((side*2.9,0,z),(.15,2,2.85/(22 if lod==0 else 11)-.012))
  for x in [-2,2]:
   for j in range(7):g.box((x-1+(j+.5)*2/7,-2.4,1.65),(2/7-.015,.14,3))
  g.box((side*2.9,0,1.05),(.25,2.1,.10),1);g.box((side*2.9,0,2.3),(.25,2.1,.10),1)
  for y in [-2.4,2.4]:g.box((side*2.88,y,1.6),(.21,.21,3.2),0)
  slope=-side*.40;g.box((side*1.65,0,3.55),(3.8,5.9,.15),7,slope)
  if lod==0:
   for j in range(46):
    y=-2.95+(j+.5)*5.9/46;g.box((side*1.65,y,3.64),(3.87+random.uniform(-.1,.1),.045,.036),5 if j%4==0 else 7,slope)
   for x in [side*.75,side*1.5,side*2.3]:g.box((x,0,4.27-abs(x)*.42),(.07,5.9,.05),0)
 g.box((0,2.4,1.65),(5.8,.15,3));g.box((0,-2.4,2.9),(2,.15,.5));g.box((0,0,4.3),(.18,6,.15),0)
 return g

payload={'meshes':[]};counts={}
def export(ob):
 me=ob.data;me.calc_loop_triangles();v=[];norm=[];col=[];tri=[]
 for face in me.loop_triangles:
  p=me.polygons[face.polygon_index];c=mats[p.material_index].diffuse_color
  for idx in face.vertices:
   pos=me.vertices[idx].co;n=p.normal;v.extend([round(pos.x,5),round(pos.z,5),round(pos.y,5)]);norm.extend([round(n.x,5),round(n.z,5),round(n.y,5)]);col.extend([round(x,4) for x in c])
  n=len(v)//3;tri.extend([n-3,n-1,n-2])
 payload['meshes'].append({'id':ob.name,'vertices':v,'normals':norm,'colors':col,'triangles':tri});counts[ob.name]=len(tri)//3
for variant in range(5):
 for lod in range(3):export(forest(variant,lod).mesh('Forest_%d_LOD%d'%(variant,lod)))
for name,fn in [('Coconut',palm),('Banana',banana),('Grass',grass),('House',house)]:
 for lod in range(3 if name!='House' else 2):export(fn(lod).mesh(name+'_LOD'+str(lod)))
# Arrange the source library for easy inspection in Blender without altering exported local coordinates.
for i,ob in enumerate(bpy.context.scene.objects):ob.location=(i%6*12,i//6*14,0)
bpy.context.scene['purpose']='Reference-based tropical asset library: shared meshes, authored LODs, vertex colour shading'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'TropicalAssets.blend'))
with gzip.open(os.path.join(OUT,'TropicalAssets.meshdata.json.gz'),'wt',encoding='utf8') as f:json.dump(payload,f,separators=(',',':'))
with open(os.path.join(OUT,'mesh-budget.json'),'w') as f:json.dump(counts,f,indent=2)
print('TROPICAL_ASSETS_COMPLETE',json.dumps(counts),flush=True)
