import bpy,json,os
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),"../.."))
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT,'SourceArt/Map01_Realism/Map01_RefinedEnvironment.blend'))
vertices=[];names=[]
for o in bpy.context.scene.objects:
 if o.type!='MESH' or not ('jetty' in o.name.lower() or o.name.startswith(('Moored wooden canoe','Canoe seat','Mooring rope'))):continue
 names.append(o.name)
 ev=o.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ev.to_mesh()
 for v in m.vertices:
  p=o.matrix_world@v.co;vertices.extend([round(p.x,4),round(p.z,4),round(p.y,4)])
 ev.to_mesh_clear()
with open(os.path.join(ROOT,'SourceArt/Map01_RiverDetails/JettyOriginalVertices.json'),'w') as f:json.dump({'vertices':vertices,'objects':names},f)
print('EXPORTED',len(names),'source objects',len(vertices)//3,'vertices')

