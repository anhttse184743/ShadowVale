"""Blender-authored floating boat, mossy rocks and buried bunker. Run with Blender --background --python this_file."""
import bpy, math, random, os, json, gzip
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.dirname(__file__)
src=open(ROOT+'/SourceArt/Map01_Realism/build_assets.py',encoding='utf-8-sig').read()
exec(src[src.index('random.seed(921)'):src.index('def forest(')])
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for name,c in [('Stone',(.29,.32,.27,1)),('Soil',(.25,.22,.14,1)),('Moss',(.22,.34,.07,1))]:
 m=bpy.data.materials.new(name);m.diffuse_color=c;mats.append(m)
payload={'meshes':[]};counts={}
exec(src[src.index('def export(ob):'):src.index('for variant in range(5):')])
# Open hull, curved sheer, individual strakes, internal ribs, seats and paddle.
g=Geo()
def hull(y,t):
 u=y/3.5; w=.12+.94*max(0,1-u*u)**.6
 return (w*math.sin(t),y,-.32+.83*(1-math.cos(t))+.32*abs(u)**4)
for side in [-1,1]:
 for j in range(28):
  y=-3.5+j*.25
  for k in range(5):
   a=side*k*math.pi/10;b=side*(k+1)*math.pi/10
   pts=[hull(y,a),hull(y+.25,a),hull(y+.25,b),hull(y,b)]
   g.face(pts,6 if k%2 else 1);g.face(list(reversed([(x*.94,y,z+.06) for x,y,z in pts])),6)
 g.rod([hull(-3.5+j*.25,side*math.pi/2) for j in range(29)],[.065]*29,0,8)
for y in [-2.5,-1.5,0,1.5,2.5]:
 g.rod([tuple(Vector(hull(y,-math.pi/2+j*math.pi/12))+Vector((0,0,.08))) for j in range(13)],[.045]*13,0,6)
for j in range(22):
 y=-2.75+j*.25;g.box((0,y,.015),(1.4*(1-(y/3.55)**2),.245,.075),6)
for y in [-1.8,.2,1.9]:g.box((0,y,.34),(1.65*(1-(y/4)**2),.4,.12),6)
for y in [-3.49,3.49]:g.box((0,y,.26),(.24,.08,1.1),0)
g.rod([(-.75,-1,.62),(.6,1.7,.62)],[.035,.035],1,8);g.box((.72,1.94,.62),(.22,.65,.06),6)
export(g.mesh('Boat_Floating'))
# Three deterministic higher-resolution rocks with patchy moss and trailing vines.
for variant in range(3):
 g=Geo();r=random.Random(410+variant)
 def rock(t,a,offset=0):
  f=1+.07*math.sin(5*a+variant)*math.sin(t*3)+.045*math.cos(a*9+t*7)
  return Vector(((1+offset)*math.sin(t)*math.cos(a)*f,(.86+offset)*math.sin(t)*math.sin(a)*f,(.83+offset)*math.cos(t)*f))
 for j in range(20):
  t=.001+j*(math.pi-.002)/20;tn=.001+(j+1)*(math.pi-.002)/20
  for k in range(32):
   a=k*math.tau/32;b=(k+1)*math.tau/32
   moss=j<9 and (math.sin(a*3+variant)+math.cos(t*8+a*2)>.0)
   g.face([rock(t,a),rock(tn,a),rock(tn,b),rock(t,b)],10 if moss else 8)
 for vine in range(3):
  a=variant+vine*1.9;pts=[rock(.3+j*.085,a+.10*math.sin(j*.4),.025) for j in range(23)]
  g.rod(pts,[.012]*len(pts),2,5)
  for j in range(2,22,2):
   p=pts[j];d=Vector((math.cos(a+1.57),math.sin(a+1.57),.1))*.15*(-1 if j%4 else 1)
   g.leaf([p,p+d*.5,p+d],[0,.055,0],3)
 export(g.mesh('Rock_Moss_'+str(variant)))
# Retain the playable interior footprint; replace the surface with a closed hill and recessed approach.
g=Geo()
g.box((0,0,.05),(3.1,8,.18),9)
for side in [-1,1]:
 g.box((side*1.55,0,1.3),(.22,8,2.6),9)
 for y in [-3.7,-2,-.3,1.4,3.65]:
  g.rod([(side*1.4,y,.15),(side*1.4,y,2.6)],[.13,.13],0,8)
  g.rod([(-1.55,y,2.55),(1.55,y,2.55)],[.14,.14],0,8)
 for z in [.4,.85,1.3,1.75,2.2]:g.rod([(side*1.4,-3.8,z),(side*1.4,3.8,z)],[.10,.10],6,7)
g.box((0,0,2.72),(3.5,8.3,.3),9);g.box((0,-4,1.3),(3.4,.25,2.6),9)
# Keep mound separate so Unity can fit its perimeter to actual terrain.
structure=g;g=Geo()
# Continuous roof and side slopes, front is cut away only for the doorway/trench.
for j in range(48):
 y=-9+j*.36;yn=y+.36
 for k in range(48):
  x=-8+k/3;xn=x+1/3
  if abs(x+1/6)<1.7 and y>=3.8:continue
  def height(x,y):
   core=max(0,1-(max(0,abs(x)-1.65)/6.35)**1.5)*max(0,1-(max(0,abs(y+.4)-4.4)/3.2)**1.5)
   return -.18+3.65*core+.08*math.sin(x*2+y)*core
  g.face([(x,y,height(x,y)),(xn,y,height(xn,y)),(xn,yn,height(xn,yn)),(x,yn,height(x,yn))],10 if (j+k)%7<3 else 9)
# Buried perimeter skirts close the rear and both flanks below uneven terrain.
for j in range(48):
 y=-9+j*.36;yn=y+.36
 for side in [-1,1]:
  x=side*8
  pts=[(x,y,-2),(x,yn,-2),(x,yn,height(x,yn)),(x,y,height(x,y))]
  g.face(pts if side>0 else list(reversed(pts)),9)
for k in range(48):
 x=-8+k/3;xn=x+1/3
 g.face([(x,-9,-2),(xn,-9,-2),(xn,-9,height(xn,-9)),(x,-9,height(x,-9))],9)
export(g.mesh('Bunker_Mound'));g=structure
# The rear retaining earth fills the space behind the room instead of leaving a hollow shell.
g.box((0,-4.35,1.48),(3.5,.5,3.3),9)
g.box((0,3.82,3.1),(3.45,.22,.82),9)
# Recessed trench cheeks: floor remains connected to existing entrance ramp.
for side in [-1,1]:
 g.box((side*1.73,5.4,1.0),(.38,3.1,2.0),8)
 for row in range(3):
  for j in range(5):
   g.box((side*1.78,3.95+j*.67+(row%2)*.12,2.06+row*.22),(.61,.64,.23),5)
 for j in range(16):
  y=4+j*.18;g.rod([(side*1.52,y,1.8),(side*1.51,y+.05,.45)],[.018,.01],2,5)
for row in range(2):
 for j in range(6):g.box((-1.6+j*.63,3.85,2.85+row*.24),(.66,.7,.26),5)
g.box((-1,-1.9,.45),(.65,2.1,.12),6)
for z in [.7,1.5]:g.box((1.1,-2,z),(.6,2,.1),6)
g.box((0,-3.84,1.6),(1.55,.05,.9),1)
export(g.mesh('Bunker_Buried'))
# Independent river jetty: bank approach is fitted to terrain by the Unity importer.
g=Geo()
for j in range(37):g.box((j*.4,0,0),(.385,3.6,.18),6 if j%4 else 1)
for side in [-1,1]:
 g.rod([(0,side*1.6,-.28),(14.4,side*1.6,-.28)],[.14,.14],0,8)
 for x in [.2,4.8,9.6,14.2]:g.rod([(x,side*1.6,-5),(x,side*1.6,1.1)],[.14,.11],0,8)
 g.rod([(.2,side*1.6,1.05),(14.2,side*1.6,1.05)],[.07,.07],6,7)
export(g.mesh('Jetty_Rebuilt'))
for i,ob in enumerate(bpy.context.scene.objects):ob.location=(i*19,0,0)
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/RiverDetails.blend')
with gzip.open(OUT+'/RiverDetails.meshdata.json.gz','wt',encoding='utf8') as f:json.dump(payload,f,separators=(',',':'))
with open(OUT+'/mesh-budget.json','w') as f:json.dump(counts,f,indent=2)
print(counts)





