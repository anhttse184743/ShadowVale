"""Re-import the exported FBX bytes; do not trust the author's success message."""
from pathlib import Path
import bpy,json,math,hashlib
HERE=Path(__file__).resolve().parent
rows=[]
for path in sorted((HERE/'exports').glob('*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    rigs=[o for o in bpy.data.objects if o.type=='ARMATURE'];meshes=[o for o in bpy.data.objects if o.type=='MESH']
    assert len(rigs)==1 and len(meshes)==1,(path.name,len(rigs),len(meshes))
    rig=rigs[0];mesh=meshes[0]
    assert len(rig.data.bones)==62,(path.name,len(rig.data.bones))
    assert all(rig.data.bones.get(side+'Hand'+finger+'3') is not None for side in ['Left','Right'] for finger in ['Thumb','Index','Middle','Ring','Pinky'])
    unweighted=0;bad_weights=0
    for v in mesh.data.vertices:
        ws=sum(g.weight for g in v.groups)
        if ws<.001:unweighted+=1
        if abs(ws-1)>.001:bad_weights+=1
        assert all(math.isfinite(c) for c in v.co)
    assert unweighted==0 and bad_weights==0,(path.name,unweighted,bad_weights)
    tris=sum(len(p.vertices)-2 for p in mesh.data.polygons);assert tris<30000,(path.name,tris)
    rows.append({'file':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bones':len(rig.data.bones),'triangles':tris,'unweighted_vertices':unweighted,'non_normalized_weights':bad_weights})
assert len(rows)==12,len(rows)
(HERE/'validation-report.json').write_text(json.dumps({'result':'PASS','scope':'Blender FBX round trip and numeric mesh/skin checks only; not Unity animation approval','characters':rows},indent=2),encoding='utf8')
print('FBX_ROUNDTRIP_PASS',len(rows))
