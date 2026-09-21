"""Re-export static Blender chunks, removing obsolete palms and refining timber walls."""
import bpy, os, json, gzip, math
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'));OUT=os.path.join(ROOT,'SourceArt/Map01_Realism')
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT,'SourceArt/Map01_Optimized/ShadowVale_Map01_Optimized.blend'))
removed=[];refined=0
for ob in list(bpy.context.scene.objects):
 if ob.name.startswith(('Palm trunk','Palm frond','Forest fern','Stream ripple')):
  removed.append(ob.name);bpy.data.objects.remove(ob,do_unlink=True)
# Replace flat side-wall slabs with narrow horizontal timber boards and central windows.
for ob in list(bpy.context.scene.objects):
 if not ob.name.startswith('Side wall'):continue
 me=ob.data;mi=list(me.materials);lo=[min(v.co[j] for v in me.vertices) for j in range(3)];hi=[max(v.co[j] for v in me.vertices) for j in range(3)];v=[];f=[]
 def box(x0,x1,y0,y1,z0,z1):
  n=len(v);v.extend([(x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0),(x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)])
  f.extend([tuple(n+j for j in face) for face in [(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]])
 for j in range(15):
  z0=lo[2]+(hi[2]-lo[2])*j/15;z1=lo[2]+(hi[2]-lo[2])*(j+1)/15-.014/max(.01,ob.scale.z)
  if 6<=j<=11:
   mid=(lo[1]+hi[1])/2;w=(hi[1]-lo[1])*.2;box(lo[0],hi[0],lo[1],mid-w,z0,z1);box(lo[0],hi[0],mid+w,hi[1],z0,z1)
  else:box(lo[0],hi[0],lo[1],hi[1],z0,z1)
 new=bpy.data.meshes.new('Weathered horizontal timber with window');new.from_pydata(v,[],f)
 for mat in mi:new.materials.append(mat)
 ob.data=new;refined+=1
chunks={};deps=bpy.context.evaluated_depsgraph_get()
def unity(p):return [round(p.x,4),round(p.z,4),round(p.y,4)]
for ob in bpy.context.scene.objects:
 if ob.type!='MESH' or ob.hide_render or ob.name.startswith(('Forest canopy','Supply crate','Rusted empty fuel drum','Rounded river boulder','Mossy rock','Base scattered rubble')) or ob.name=='Diorama earth foundation':continue
 ev=ob.evaluated_get(deps);me=ev.to_mesh();world=ob.matrix_world;nm=world.to_3x3().inverted().transposed()
 for poly in me.polygons:
  center=world@poly.center;kind='water' if ob.name.startswith('Continuous stream') else 'opaque';key=(int(math.floor(center.x/24)),int(math.floor(center.y/24)),kind)
  if key not in chunks:chunks[key]={'id':'Chunk_%d_%d_%s'%key,'kind':kind,'vertices':[],'normals':[],'colors':[],'triangles':[],'cache':{}}
  dst=chunks[key];color=list(me.materials[poly.material_index].diffuse_color) if len(me.materials)>poly.material_index else [.3,.35,.2,1]
  if ob.name.startswith('MAP01'):
   factor=.86+.16*math.sin(center.x*.5)*math.cos(center.y*.37);color=[c*factor for c in color[:3]]+[1]
  color=[round(c,4) for c in color];ids=[]
  for idx in poly.vertices:
   p=unity(world@me.vertices[idx].co);n=unity((nm@(me.vertices[idx].normal if poly.use_smooth else poly.normal)).normalized());vk=tuple(p+n+color)
   index=dst['cache'].get(vk)
   if index is None:index=len(dst['vertices'])//3;dst['cache'][vk]=index;dst['vertices'].extend(p);dst['normals'].extend(n);dst['colors'].extend(color)
   ids.append(index)
  for j in range(1,len(ids)-1):dst['triangles'].extend([ids[0],ids[j+1],ids[j]])
 ev.to_mesh_clear()
for mesh in chunks.values():mesh.pop('cache')
with gzip.open(os.path.join(OUT,'StaticRefinement.meshdata.json.gz'),'wt',encoding='utf8') as file:json.dump({'meshes':list(chunks.values())},file,separators=(',',':'))
bpy.context.scene['refinement']='Obsolete inland palms removed; side walls rebuilt with timber courses and windows; water ripple shader replaces baked ripple strips'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Map01_RefinedEnvironment.blend'))
with open(os.path.join(OUT,'static-refinement.json'),'w') as file:json.dump({'removed_objects':len(removed),'removed_palm_parts':sum(n.startswith('Palm') for n in removed),'refined_side_walls':refined,'chunks':len(chunks)},file,indent=2)
print('STATIC_REFINEMENT_COMPLETE',refined,len(removed),len(chunks),flush=True)
