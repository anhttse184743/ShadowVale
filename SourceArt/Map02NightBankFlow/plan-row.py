from pathlib import Path
import re,json,sys
sys.path.insert(0,str(Path('Tools/TamVoiceRuntime/site').resolve()))
import numpy as np
from scipy.optimize import least_squares
from scipy.spatial import cKDTree
from PIL import Image

scene=Path('Assets/_Project/Scenes/Maps/Map 2.unity').read_text(encoding='utf-8')
def bank(name):
    s=re.search(name+r':\n((?:  - \{x:.*\n)+)',scene)[1]
    return np.array([[float(x),float(z)] for x,z in re.findall(r'x: ([^,]+), y: [^,]+, z: ([^}]+)',s)])
shore=bank('rightBank');a=shore[:-1];v=shore[1:]-a
def shore_distance(p):
    t=np.clip(np.sum((p-a)*v,axis=1)/np.sum(v*v,axis=1),0,1)
    return float(np.min(np.linalg.norm(a+t[:,None]*v-p,axis=1)))
mask=np.array(Image.open('Assets/_Project/Art/Environment/Map02_Village/Roadside_Rounded_Lanes.png'))
rows,cols=np.where(mask[:,:,0]>127)
road=np.column_stack((cols*220/(mask.shape[1]-1)-110,100-rows*200/(mask.shape[0]-1)))
road_tree=cKDTree(road)
def points(q):return q[:2]+np.arange(3)[:,None]*np.array([np.cos(q[2]),np.sin(q[2])])*10
def error(q):
    p=points(q)
    return [shore_distance(p[0])-40,shore_distance(p[2])-40,road_tree.query(p[0])[0]-20]
solutions=[]
for x in [30,40,50]:
 for z in [-12,-20,-30]:
  for theta in [-1.8,-1.5,-1.2]:
   r=least_squares(error,[x,z,theta],bounds=([15,-40,-2.5],[70,0,-.5]),diff_step=.0001,max_nfev=150)
   solutions.append((np.linalg.norm(r.fun),r.x))
score,q=min(solutions,key=lambda r:r[0]);posts=points(q)
print('row residual',score,'posts',posts.tolist(),'shore',[shore_distance(p) for p in posts],'road',road_tree.query(posts[0])[0])

blocks=re.split(r'(?=--- !u!)',scene);trs={};go_to_tr={}
def vec(b,key,keys):
    m=re.search(r'  '+key+r': \{([^}]+)\}',b);d=dict((a,float(c)) for a,c in re.findall(r'(\w+): ([^,}]+)',m[1]));return [d[k] for k in keys]
for b in blocks:
 if not re.match(r'--- !u!4 &\d+\s*\nTransform:',b):continue
 try:
  ident=int(re.match(r'--- !u!4 &(\d+)',b)[1]);go=int(re.search(r'm_GameObject: \{fileID: (\d+)',b)[1]);parent=int(re.search(r'm_Father: \{fileID: (\d+)',b)[1])
  pos=vec(b,'m_LocalPosition','xyz');x,y,z,w=vec(b,'m_LocalRotation','xyzw');scale=vec(b,'m_LocalScale','xyz')
  rot=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
  m=np.eye(4);m[:3,:3]=rot@np.diag(scale);m[:3,3]=pos;trs[ident]=(parent,m);go_to_tr[go]=ident
 except (AttributeError,TypeError):pass
worlds={}
def world(i):
 if i not in trs:return np.eye(4)
 if i not in worlds:
  parent,m=trs[i];worlds[i]=world(parent)@m
 return worlds[i]
young_guids=[]
for p in Path('Assets/_Project/Art/Environment').rglob('*Rice_Young_LOD0.asset.meta'):
 young_guids.append(re.search(r'guid: (\w+)',p.read_text())[1])
young=[]
for b in blocks:
 if 'guid: 345aefe532ceacf439538f83ef380ddb' not in b:continue
 mesh=re.search(r'detailedMesh: \{[^}]*guid: (\w+)',b)
 if mesh is None or mesh[1] not in young_guids:continue
 go=int(re.search(r'm_GameObject: \{fileID: (\d+)',b)[1]);m=world(go_to_tr.get(go,0))
 for x,y,z in re.findall(r'- \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w:',b):
  p=m@np.array([float(x),float(y),float(z),1]);young.append(p[:3])
young=np.array(young);old=np.array([-3.971,-21.858]);candidates=[]
for p in young:
 d=p[[0,2]]-old
 if d[1]>-15:continue
 projected=old+d/np.linalg.norm(d)*30
 if shore_distance(projected)<4:continue
 nearest=np.min(np.linalg.norm(young[:,[0,2]]-projected,axis=1))
 if nearest>.3:continue
 # Prefer continuing south along the bank rather than turning deep into the field.
 candidates.append((abs(projected[0]-old[0])+nearest,projected,p))
_,hatch,_=min(candidates,key=lambda c:c[0]);clump=young[np.argmin(np.linalg.norm(young[:,[0,2]]-hatch,axis=1))]
print('young plants',len(young),'hatch',hatch.tolist(),'move',np.linalg.norm(hatch-old),'rice clump',clump.tolist(),'shore',shore_distance(hatch))
result={'postsXZ':posts.tolist(),'rowDirectionXZ':(posts[1]-posts[0]).tolist(),'shoreDistances':[shore_distance(p) for p in posts],'firstRoadEdgeDistance':float(road_tree.query(posts[0])[0]),'hatchXZ':hatch.tolist(),'oldHatchXZ':old.tolist(),'hatchMoveMeters':float(np.linalg.norm(hatch-old)),'youngRiceClump':clump.tolist()}
Path('SourceArt/Map02NightBankFlow/row-proposal.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
