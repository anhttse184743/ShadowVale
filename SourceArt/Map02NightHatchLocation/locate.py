from pathlib import Path
import re,json,sys,math
sys.path.insert(0,str(Path('Tools/TamVoiceRuntime/site').resolve()))
import numpy as np
from PIL import Image
root=Path('.')
mask=np.array(Image.open('Assets/_Project/Art/Environment/Map02_Village/Roadside_Rounded_Lanes.png'))
rows,cols=np.where(mask[:,:,0]>127)
road=np.column_stack((cols*220/(mask.shape[1]-1)-110,100-rows*200/(mask.shape[0]-1)))
def dist(p):
 d=road-np.array(p);i=np.argmin(np.sum(d*d,axis=1));return float(np.linalg.norm(d[i])),road[i]
def from_pixel(x,y):
 px=260+x*1080/720;py=133+y*830/550
 # Saved camera: (0,205,-130), looking at origin, orthographic half-height112.
 sin=205/math.hypot(205,130);cos=130/math.hypot(205,130)
 return ((px-800)*224/1100,((550-py)*224/1100-.8*cos)/sin)
red=from_pixel(459,309);green=from_pixel(589,297)
print('rough red',red,'rough green',green,'road distance',dist(green))
s=Path('Assets/_Project/Scenes/Maps/Map 2.unity').read_text(encoding='utf-8')
blocks=re.split(r'(?=--- !u!)',s);trs={};go_to_tr={}
def vec(b,key,keys):
 m=re.search(r'  '+key+r': \{([^}]+)\}',b);d=dict((a,float(c)) for a,c in re.findall(r'(\w+): ([^,}]+)',m[1]));return [d[k] for k in keys]
for b in blocks:
 if not re.match(r'--- !u!4 &\d+\s*\nTransform:',b):continue
 try:
  ident=int(re.match(r'--- !u!4 &(\d+)',b)[1]);go=int(re.search(r'm_GameObject: \{fileID: (\d+)',b)[1]);parent=int(re.search(r'm_Father: \{fileID: (\d+)',b)[1])
  pos=vec(b,'m_LocalPosition','xyz');q=vec(b,'m_LocalRotation','xyzw');scale=vec(b,'m_LocalScale','xyz');x,y,z,w=q
  rot=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
  m=np.eye(4);m[:3,:3]=rot@np.diag(scale);m[:3,3]=pos;trs[ident]=(parent,m);go_to_tr[go]=ident
 except (AttributeError,TypeError):pass
worlds={}
def world(i):
 if i not in trs:return np.eye(4)
 if i not in worlds:
  parent,m=trs[i];worlds[i]=world(parent)@m
 return worlds[i]
meta={}
for p in (root/'Assets/_Project/Art/Environment').rglob('*.asset.meta'):
 t=p.read_text(encoding='utf-8-sig');g=re.search(r'guid: (\w+)',t)
 if g:meta[g[1]]=Path(str(p)[:-5])
rice=[]
for b in blocks:
 if 'guid: 345aefe532ceacf439538f83ef380ddb' not in b:continue
 mesh=re.search(r'detailedMesh: \{[^}]*guid: (\w+)',b)
 if not mesh or mesh[1] not in meta:continue
 text=meta[mesh[1]].read_text(encoding='utf-8-sig',errors='ignore')[:700]
 name=re.search(r'm_Name: (.+)',text)
 if not name or 'Rice_' not in name[1]:continue
 go=int(re.search(r'm_GameObject: \{fileID: (\d+)',b)[1]);m=world(go_to_tr.get(go,0))
 for x,y,z in re.findall(r'- \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w:',b):
  p=m@np.array([float(x),float(y),float(z),1]);
  if abs(p[0]-green[0])<10 and abs(p[2]-green[1])<20:rice.append(p[:3])
rice=np.array(rice);print('nearby actual rice clumps',len(rice))
best=None
for p in rice:
 d,edge=dist(p[[0,2]])
 if abs(d-10)>.25:continue
 score=np.linalg.norm(p[[0,2]]-np.array(green))
 if best is None or score<best[0]:best=(score,p,d,edge)
if best is None:raise RuntimeError('No planted rice position at 10 m from road found')
_,hatch,d,edge=best
hatch=hatch.copy()
for _ in range(3):
 d,edge=dist(hatch[[0,2]])
 hatch[[0,2]]=edge+(hatch[[0,2]]-edge)*(10/d)
d,edge=dist(hatch[[0,2]])
assert np.min(np.linalg.norm(rice[:,[0,2]]-hatch[[0,2]],axis=1))<.45
print('hatch',hatch,'distance from road',d,'nearest road',edge)
result={'status':'location proposal only; no scene or gameplay changes','mapImage':'Tools/Map02Reports/map02-field-trees-overview.png','patrolApproxXZ':red,'markedHatchApproxXZ':green,'hatchPosition':hatch.tolist(),'nearestRoadEdgeXZ':edge.tolist(),'roadEdgeDistanceMeters':d,'riceVerifiedFromSavedScene':True,'distanceAssumption':'10 metres from nearest road edge, not from patrol'}
Path('SourceArt/Map02NightHatchLocation/location-proposal.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
