import bpy,math,random,json,gzip,os
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'));OUT=os.path.join(ROOT,'SourceArt/Map01_Optimized');os.makedirs(OUT,exist_ok=True)
source=open(ROOT+'/SourceArt/Map01_Realism/build_assets.py',encoding='utf-8-sig').read()
# Reuse the project's Blender geometry helpers, without rebuilding other assets.
exec(source[source.index('random.seed(921)'):source.index('def forest(')])
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
random.seed(524)
def house(lod):
 g=Geo();g.box((0,-.8,.10),(6.4,6.8,.20))
 # Floor boards and a deep, open front veranda.
 if lod==0:
  for j in range(32):g.box((-3.1+j*.2,-.8,.215),(.188,6.7,.03),6 if j%4 else 1)
 for x in [-2.9,0,2.9]:
  for y in ([-4,2.35] if x!=0 else [2.35]):g.rod([(x,y,-.2),(x,y,2.7)],[.105,.085],6,6)
 for x in [-2.9,2.9]:g.rod([(x,-2,0),(x,-2,3)],[.12,.1],6,6)
 # Woven bamboo walls; wide front doorway stays open.
 for z in [(.3+i*.11) for i in range(24 if lod==0 else 12)]:
  if lod: z=.3+(z-.3)*2
  g.box((0,2.38,z),(5.8,.10,.10 if lod==0 else .21),6 if int(z*9)%3 else 1)
  for side in [-1,1]:
   g.box((side*2.92,.18,z),(.10,4.3,.10 if lod==0 else .21),6 if int(z*9)%3 else 1)
   g.box((side*1.95,-2.05,z),(1.9,.10,.10 if lod==0 else .21),6 if int(z*9)%3 else 1)
 if lod==0:
  for x in [-2.8+i*.35 for i in range(17)]:g.box((x,2.30,1.55),(.035,.025,2.5),1)
  for side in [-1,1]:
   for y in [-1.9+i*.35 for i in range(12)]:g.box((side*2.84,y,1.55),(.025,.035,2.5),1)
 # Broad palm roof, ridge along X, sagged overlapping thatch courses.
 for side,extent in [(-1,4.5),(1,2.9)]:
  # Solid dark underside gives the layered palm roof thickness and shade.
  g.face([(-3.62,0,4.48),(3.62,0,4.48),(3.62,side*extent,2.64),(-3.62,side*extent,2.64)],5)
  rows=12 if lod==0 else 6;columns=95 if lod==0 else 40
  for row in range(rows):
   t0=row/rows;t1=min(1.035,(row+1.45)/rows)
   for j in range(columns):
    x=-3.6+j*7.2/columns;w=7.2/columns*.985;tip=t1+random.uniform(-.012,.012)
    def z(t):return 4.6-1.85*t-.16*math.sin(t*math.pi)+.025*(rows-row)
    a=Vector((x,side*extent*t0,z(t0)));b=Vector((x+w,side*extent*t0,z(t0)));c=Vector((x+w*.88,side*extent*tip,z(tip)));d=Vector((x+w*.12,side*extent*tip,z(tip)))
    g.face([a,b,c,d],7 if (j+row)%5 else 5)
    if lod==0:g.face([a+Vector((w*.42,0,.012)),a+Vector((w*.53,0,.022)),d+Vector((w*.36,0,.015)),d+Vector((w*.27,0,.01))],5)
  # Ragged, downward hanging dry-palm fringe, rather than a straight roof edge.
 for side,extent in [(-1,4.5),(1,2.9)]:
  for j in range(110 if lod==0 else 48):
   x=-3.6+j*7.2/(110 if lod==0 else 48);w=.075 if lod==0 else .16;drop=random.uniform(.12,.29)
   g.face([(x,side*extent,2.8),(x+w,side*extent,2.8),(x+w*.7,side*(extent+.07),2.7-drop),(x+w*.18,side*(extent+.05),2.72-drop)],7 if j%3 else 5)
 g.rod([(-3.65,0,4.94),(3.65,0,4.94)],[.13,.13],7,6)
 for y in [-4.05,2.35]:g.rod([(-3.1,y,2.67),(3.1,y,2.67)],[.10,.10],6,6)
 # Exposed roof frame and veranda rails leave the middle entrance open.
 for x in [-2.9,2.9]:
  g.rod([(x,-4.05,2.7),(x,0,4.45),(x,2.45,2.9)],[.075,.075,.075],6,5)
  g.box((x,-3.0,.65),(.1,1.9,.10),6)
 for side in [-1,1]:
  g.box((side*2.1,-3.7,.50),(1.4,.48,.1),6)
  for x in [side*1.6,side*2.6]:g.box((x,-3.7,.28),(.08,.36,.45),6)
 # Split bamboo shutters and hanging fabric beside the open doorway.
 g.face([(-.95,-2.15,2.8),(-.45,-2.17,2.7),(-.58,-2.17,1.05),(-.93,-2.15,.9)],5)
 for i in range(3):g.box((0,-4.3-i*.36,-.02-i*.15),(1.9,.40,.16),6)
 return g
payload={'meshes':[]};counts={}
export_source=source[source.index('def export(ob):'):source.index('for variant in range(5):')]
exec(export_source)
for lod in range(2):export(house(lod).mesh('House_LOD'+str(lod)))
for o in bpy.context.scene.objects:o.hide_render=o.name.endswith('LOD1')
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/Map01_PalmHouse.blend')
with gzip.open(OUT+'/House.meshdata.json.gz','wt',encoding='utf8') as f:json.dump(payload,f,separators=(',',':'))
print('HOUSE_COMPLETE',counts,flush=True)



