import bpy,math,random,json,gzip,os
from mathutils import Vector
ROOT=r'G:\game\ShadowVale';OUT=os.path.abspath('outputs/Map01_Sept22');os.makedirs(OUT,exist_ok=True)
source=open(ROOT+'/SourceArt/Map01_Realism/build_assets.py',encoding='utf-8-sig').read()
exec(source[source.index('random.seed(921)'):source.index('def palm(')])
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
# Branch fans fill canopy volume rather than repeating flat silhouette cards.
forest_source=source[source.index('def forest('):source.index('def palm(')]
forest_source=forest_source.replace('6 if lod<2 else 4','8 if lod<2 else 5').replace('[9,5,3]','[10,6,3]').replace('[7,4,2]','[10,6,3]').replace('.15*(1+lod*.5)','.21*(1+lod*.5)')
exec(forest_source)
payload={'meshes':[]};counts={}
exec(source[source.index('def export(ob):'):source.index('for variant in range(5):')])
for t in range(5):
 for l in range(3):export(forest(t,l).mesh('Forest_%d_LOD%d'%(t,l)))

def bridge():
 g=Geo()
 # Local X spans the stream; deck height is 0 at the placement origin.
 for j in range(61):
  x=-11.8+j*.39;g.box((x,0,random.uniform(-.015,.015)),(.37,3.7,.18),6 if j%5 else 1)
 for side in [-1,1]:
  y=side*1.73
  g.rod([(-12,y,-.26),(12,y,-.26)],[.16,.16],0,8)
  for x in [-11.7,-7.8,-3.9,0,3.9,7.8,11.7]:
   g.rod([(x,y,-3.8),(x,y,1.28)],[.18,.13],6,8)
  for z in [.52,1.12]:g.rod([(-11.8,y,z),(11.8,y,z)],[.085,.085],6,6)
  for x in [-11.7,-7.8,-3.9,0,3.9,7.8]:
   g.rod([(x,y,.32),(x+3.9,y,1.12)],[.045,.045],1,5)
   g.rod([(x+3.9,y,.32),(x,y,1.12)],[.045,.045],1,5)
 for x in [-7.8,0,7.8]:
  g.rod([(x,-1.73,-2.6),(x,1.73,-.4)],[.11,.11],0,6)
  g.rod([(x,1.73,-2.6),(x,-1.73,-.4)],[.11,.11],0,6)
 return g
export(bridge().mesh('Bridge_New'))
g=Geo();g.rod([(0,0,0),(0,0,1)],[.14,.125],0,8);export(g.mesh('House_Stilt'))
g=Geo();g.box((0,0,.09),(1.85,.36,.18),6);export(g.mesh('Stair_Step'))
g=Geo();g.box((0,0,0),(1.85,1,.12),6);export(g.mesh('Ramp_Plank'))
g=Geo();g.rod([(0,0,0),(0,0,1)],[.07,.07],6,6);export(g.mesh('Brace_Beam'))

def bunker():
 g=Geo()
 # Open north-facing timber portal and a covered corridor inside an earth mound.
 g.box((0,0,.05),(3.1,8,.18),6)
 for side in [-1,1]:
  g.box((side*1.55,0,1.3),(.22,8,2.6),6)
  for y in [-3.7,-1.8,0,1.8,3.7]:g.rod([(side*1.43,y,.15),(side*1.43,y,2.6)],[.12,.12],0,7)
  for z in [.4,.9,1.4,1.9,2.4]:g.rod([(side*1.40,-3.7,z),(side*1.40,3.7,z)],[.055,.055],1,5)
 for y in [-3.7,-1.8,0,1.8,3.7]:g.rod([(-1.55,y,2.55),(1.55,y,2.55)],[.14,.14],0,8)
 g.box((0,0,2.68),(3.4,8.3,.28),6);g.box((0,-4,1.3),(3.4,.25,2.6),6)
 # Smooth-ish layered mound, front arch mouth intentionally left open.
 for side in [-1,1]:
  for j in range(18):
   y0=-5.8+j*.56;y1=y0+.56
   for k in range(7):
    u=k/7;v=(k+1)/7
    def p(y,t):
     taper=max(.2,1-((y+.5)/7)**2);x=side*(1.6+4.3*t);z=3.55*(1-t*t)*taper
     return (x,y,max(-.5,z))
    g.face([p(y0,u),p(y0,v),p(y1,v),p(y1,u)],2 if k%3 else 0)
 for j in range(16):
  y=-5.7+j*.56
  if y>3.7:continue
  g.face([(-1.65,y,3.25),(1.65,y,3.25),(1.65,y+.56,3.35),(-1.65,y+.56,3.35)],2)
 # Sandbag entrance wings and a small roof that shelters the stairs.
 for side in [-1,1]:
  for row in range(4):
   for j in range(4):g.box((side*(1.85+j*.65),3.8,.25+row*.37),(.72,.65,.38),5)
 g.box((0,4.0,2.8),(3.8,1.7,.18),6)
 # Bunk, supply shelving and a map board readable from the spawn.
 g.box((-1,-1.9,.45),(.65,2.1,.12),6)
 for z in [.7,1.5]:g.box((1.1,-2,z),(.6,2,.10),6)
 g.box((0,-3.84,1.6),(1.55,.05,.9),1)
 return g
export(bunker().mesh('Bunker_Entrance'))

# Art review layout, separated from runtime local origins.
for i,ob in enumerate(bpy.context.scene.objects):ob.location=(i%5*15,i//5*18,0)
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/Map01_Forest_Bridge_Bunker.blend')
with gzip.open(OUT+'/Sept22.meshdata.json.gz','wt',encoding='utf8') as f:json.dump(payload,f,separators=(',',':'))
with open(OUT+'/mesh-budget.json','w') as f:json.dump(counts,f,indent=2)
print('BUNDLE_COMPLETE',counts,flush=True)
