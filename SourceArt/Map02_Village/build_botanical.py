"""Blender-authored rice, banyan and royal poinciana for Map 2. Z-up source, Y-up Unity export."""
import bpy, math, random, json, gzip, os
from mathutils import Vector
OUT=os.path.dirname(os.path.abspath(__file__)); ROOT=os.path.abspath(os.path.join(OUT,'../..'))
source=open(ROOT+'/SourceArt/Map01_Realism/build_assets.py',encoding='utf-8-sig').read()
exec(source[source.index('random.seed(921)'):source.index('def forest(')])
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
palette=[('Furrowed grey bark',(.31,.29,.24,1)),('Root highlights',(.46,.43,.35,1)),('Banyan dark foliage',(.065,.18,.045,1)),('Banyan green',(.14,.30,.07,1)),('Sunlit leaves',(.27,.40,.095,1)),('Ripe straw',(.72,.52,.085,1)),('Golden grain',(.91,.68,.18,1)),('Young rice',(.20,.51,.055,1)),('Fresh leaf',(.35,.61,.09,1)),('Rice shadow',(.075,.31,.025,1)),('Scarlet flowers',(.86,.035,.018,1)),('Orange red petals',(1,.14,.025,1)),('Deep crimson',(.53,.014,.015,1))]
mats=[]
for name,color in palette:
 m=bpy.data.materials.new(name);m.diffuse_color=color;m.use_nodes=True;m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=color;m.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.88;mats.append(m)
def leaf(g,p,d,width,mat):
 p=Vector(p);d=Vector(d);side=d.cross(Vector((0,0,1))).normalized()*width
 g.face([p,p+d*.45+side,p+d,p+d*.45-side],mat)
def rice(ripe,lod):
 g=Geo();r=random.Random(50+int(ripe));count=3 if lod==0 else 2
 for j in range(count):
  a=j*2.4;d=Vector((math.cos(a),math.sin(a),0));p=d*.085;h=r.uniform(.95,1.24) if ripe else r.uniform(.85,1.12);top=p+d*.07+Vector((0,0,h))
  g.rod([p,top],[.009,.005],5 if ripe else 7,3)
  for k in range(3 if lod==0 else 2):
   base=p+Vector((0,0,.20+k*.20));direction=Vector((math.cos(a+k*2),math.sin(a+k*2),0));tip=base+direction*(.33+.08*k)+Vector((0,0,.58-k*.05));mid=base*.45+tip*.55+Vector((0,0,.12))
   g.leaf([base,mid,tip],[.006,.040 if ripe else .047,0],(5 if k%2==0 else 6) if ripe else 7+k%3,.003)
  tip=top+d*(.30 if ripe else .07)+Vector((0,0,-.20 if ripe else .18));mid=(top+tip)*.5+Vector((0,0,.065))
  g.rod([top,mid,tip],[.007,.006,.002],5 if ripe else 8,3)
  for k in range(7 if lod==0 else 4):
   t=(k+.5)/(7 if lod==0 else 4);center=(1-t)**2*top+2*(1-t)*t*mid+t*t*tip
   for side in [-1,1]:
    direction=Vector((-d.y,d.x,-.65 if ripe else .4))*side*.045+Vector((0,0,-.03 if ripe else .025))
    leaf(g,center,direction,.018 if ripe else .012,6 if ripe else 8)
 return g

def tree(kind,lod):
 g=Geo();r=random.Random(391 if kind=='Banyan' else 129);banyan=kind=='Banyan';arms=[]
 if banyan:
  for j in range(9):
   a=j*2.4;rad=.45+r.random()*.45;base=Vector((math.cos(a)*rad,math.sin(a)*rad,0));top=Vector((math.cos(a)*1.3,math.sin(a)*1.3,5.7+r.random()))
   g.rod([base,base+Vector((.1,0,2.2)),top],[.43,.35,.17],j%2,8 if lod==0 else 5)
  for j in range(15):
   a=j*math.tau/15;d=Vector((math.cos(a),math.sin(a),0));g.rod([d*r.uniform(2.2,3.3),d*1.2+Vector((0,0,.5)),d*.5+Vector((0,0,2.8))],[.05,.24,.32],j%2,6)
 else:g.rod([(0,0,0),(.2,0,1.6),(-.18,.05,3.1),(.35,0,4.7)],[.66,.51,.39,.24],0,10 if lod==0 else 6)
 for j in range(11 if banyan else 9):
  a=j*2.4;reach=r.uniform(4.5,6.8) if banyan else r.uniform(5.2,7.5);d=Vector((math.cos(a),math.sin(a),0));base=Vector((0,0,4.3 if banyan else 2.6));end=d*reach+Vector((0,0,r.uniform(7.7,10) if banyan else r.uniform(5.5,7.4)));mid=d*reach*.48+Vector((0,0,6 if banyan else 4.9));g.rod([base,mid,end],[.24,.15,.045],0,7 if lod==0 else 5)
  for k in range(4):
   ba=a+(k-1.5)*.42;center=end+Vector((math.cos(ba)*r.uniform(.3,1.6),math.sin(ba)*r.uniform(.3,1.6),r.uniform(-.1,.7)));g.rod([mid,center],[.09,.015],1,4);arms.append(center)
 if banyan:
  for j in range(70 if lod==0 else 30):
   center=r.choice(arms);p=center+Vector((r.uniform(-.8,.8),r.uniform(-.8,.8),-.35));bottom=r.uniform(.10,2.4) if j%3 else .05;end=Vector((p.x+r.uniform(-.3,.3),p.y+r.uniform(-.3,.3),bottom));g.rod([p,(p+end)*.5+Vector((.1,.06,0)),end],[.035,.045,.075 if bottom<.2 else .013],j%2,4 if lod==0 else 3)
 # Fill the crown interior as well as branch tips; avoid a hollow ring from above.
 for j in range(17 if banyan else 32):
  a=j*2.4;rad=math.sqrt((j+.5)/(17 if banyan else 32))*4.8;arms.append(Vector((math.cos(a)*rad,math.sin(a)*rad,(9.0 if banyan else 8.0)+r.uniform(-.5,.7))))
 for center in arms:
  for j in range(65 if lod==0 else 27):
   a=r.random()*math.tau;rad=math.sqrt(r.random())*(1.9 if banyan else 2.1);p=center+Vector((math.cos(a)*rad,math.sin(a)*rad,r.uniform(-.6,.9) if banyan else r.uniform(-.25,.4)))
   if banyan:
    for k in range(3 if lod==0 else 2):
     a2=a+k*2.2;leaf(g,p,Vector((math.cos(a2)*.62,math.sin(a2)*.62,r.uniform(-.12,.24)))*(1 if lod==0 else 1.35),.17 if lod==0 else .25,2+r.randrange(3))
   else:
    d=Vector((math.cos(a),math.sin(a),.08));side=Vector((-d.y,d.x,0))
    for k in range(5 if lod==0 else 3):
     for s in [-1,1]:leaf(g,p+d*k*.13,side*s*.24+d*.09,.055 if lod==0 else .10,2+r.randrange(3))
    if j%2==0:
     flower=p+Vector((0,0,.12))
     for k in range(5):
      aa=k*math.tau/5;leaf(g,flower,Vector((math.cos(aa)*.28,math.sin(aa)*.28,.07)),.13,10+r.randrange(3))
 return g
payload={'meshes':[]};counts={}
def export(ob):
 me=ob.data;me.calc_loop_triangles();v=[];norm=[];col=[];tri=[];lookup={}
 for face in me.loop_triangles:
  poly=me.polygons[face.polygon_index];c=mats[poly.material_index].diffuse_color;ids=[]
  for idx in face.vertices:
   p=me.vertices[idx].co;n=poly.normal;key=tuple(round(x,5) for x in (p.x,p.z,p.y,n.x,n.z,n.y))+tuple(round(x,4) for x in c)
   if key not in lookup:
    lookup[key]=len(v)//3;v.extend(key[:3]);norm.extend(key[3:6]);col.extend(key[6:])
   ids.append(lookup[key])
  tri.extend([ids[0],ids[2],ids[1]])
 payload['meshes'].append(dict(id=ob.name,vertices=v,normals=norm,colors=col,triangles=tri));counts[ob.name]=dict(vertices=len(v)//3,triangles=len(tri)//3)
for kind in ['Rice_Ripe','Rice_Young','Banyan','Flamboyant']:
 for lod in range(2):
  g=rice(kind=='Rice_Ripe',lod) if kind.startswith('Rice') else tree(kind,lod)
  ob=g.mesh(kind+'_LOD'+str(lod));export(ob)
for i,ob in enumerate(bpy.context.scene.objects):ob.location=((i//2)*20,(i%2)*22,0)
bpy.context.scene['notes']='Map 2 reference models: golden drooping panicles, upright young green rice, banyan fused roots and aerial roots, scarlet umbrella royal poinciana. Local meshes exported for Unity.'
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/Map02_Botanical_Models.blend')
with gzip.open(OUT+'/Botanical.meshdata.json.gz','wt',encoding='utf8') as f:json.dump(payload,f,separators=(',',':'))
with open(OUT+'/botanical-budget.json','w') as f:json.dump(counts,f,indent=2)
print('BOTANICAL_COMPLETE',json.dumps(counts),flush=True)


