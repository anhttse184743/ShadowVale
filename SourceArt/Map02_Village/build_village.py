import bpy,math,random,json,gzip,os
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'));OUT=os.path.dirname(__file__)
source=open(ROOT+'/SourceArt/Map01_Realism/build_assets.py',encoding='utf-8-sig').read()
exec(source[source.index('random.seed(921)'):source.index('def forest(')])
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
hs=open(ROOT+'/SourceArt/Map01_Optimized/build_house.py',encoding='utf-8-sig').read()
exec(hs[hs.index('def house(lod):'):hs.index("payload={'meshes':[]}")])
payload={'meshes':[]};counts={}
exec(source[source.index('def export(ob):'):source.index('for variant in range(5):')])
for lod in range(2):
 g=house(lod)
 # Raised long Mekong house with veranda cross-braces and substantial timber stilts.
 g.v=[(x*1.45,y,z) for x,y,z in g.v]
 for x in [-4.2,0,4.2]:
  for y in [-3.9,-.8,2.3]:g.rod([(x,y,-1.5),(x,y,.1)],[.12,.12],0,8)
 for side in [-1,1]:
  for z in [.55,1.1]:g.rod([(side*1.25,-4,z),(side*4.2,-4,z)],[.045,.045],1,6)
  for x in [1.3,2.75]:
   g.rod([(side*x,-4,.3),(side*(x+1.4),-4,1.1)],[.025,.025],1,5)
   g.rod([(side*x,-4,1.1),(side*(x+1.4),-4,.3)],[.025,.025],1,5)
 for j in range(7):g.box((0,-5.1-j*.3,-.45-j*.15),(2,.32,.16),6)
 export(g.mesh('Village_Stilt_LOD'+str(lod)))
g=Geo()
for i in range(9):
 a=i*2.4;p=Vector((math.cos(a)*.12,math.sin(a)*.12,0));tip=p+Vector((math.cos(a)*.22,math.sin(a)*.22,.6+(i%3)*.1))
 g.leaf([p,(p+tip)*.5,tip],[.012,.025,0],3 if i%2 else 4,.004)
export(g.mesh('Rice_Clump'))
g=Geo()
for i in range(12):
 a=i*math.tau/12;b=(i+1)*math.tau/12
 g.face([(0,0,.025),(.48*math.cos(a),.48*math.sin(a),0),(.48*math.cos(b),.48*math.sin(b),0)],3)
g.rod([(0,0,-.35),(0,0,0)],[.02,.02],2,5)
export(g.mesh('Lotus_Leaf'))
for i,o in enumerate(bpy.context.scene.objects):o.location=(i*15,0,0)
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/Village_Assets.blend')
with gzip.open(OUT+'/Village.meshdata.json.gz','wt',encoding='utf8') as f:json.dump(payload,f,separators=(',',':'))
print(counts)
