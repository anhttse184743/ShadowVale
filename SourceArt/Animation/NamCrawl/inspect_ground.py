import bpy,json,numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'Nam_Crawl_Mixamo_Review_v4.blend'))
scene=bpy.data.scenes['Mixamo rifle'];bpy.context.window.scene=scene;scene.frame_set(18)
rig=next(o for o in scene.objects if o.type=='ARMATURE' and not o.name.startswith('SOURCE_Mixamo'))
graph=bpy.context.evaluated_depsgraph_get();report=[]
for ob in scene.objects:
 if ob.type!='MESH' or not any(m.type=='ARMATURE' and m.object==rig for m in ob.modifiers):continue
 ev=ob.evaluated_get(graph);mesh=ev.to_mesh()
 try:
  coords=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('co',coords);coords=coords.reshape(-1,3)
  matrix=np.asarray(ev.matrix_world,dtype=np.float32);zs=coords@matrix[2,:3]+matrix[2,3]
  ids=np.empty(len(mesh.loops),dtype=np.int32);mesh.loops.foreach_get('vertex_index',ids)
  report.append({'object':ob.name,'vertices':len(zs),'face_vertices':len(np.unique(ids)),'all_min':float(zs.min()),'surface_min':float(zs[ids].min())})
 finally:ev.to_mesh_clear()
print('GROUND_REPORT',json.dumps(report),flush=True)
