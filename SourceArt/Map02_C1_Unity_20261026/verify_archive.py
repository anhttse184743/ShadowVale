"""Read-only geometry, bind rig and active animation fingerprints for the portable source."""
import bpy, hashlib, json, struct, sys
from pathlib import Path
def digest(rows):
    return hashlib.sha256(json.dumps(rows,separators=(',',':')).encode()).hexdigest()
result={}
for name in ['Nam_Than','Player_LeftHand','Player_RightHand','Hung_Than']:
    ob=bpy.data.objects[name]; mesh=ob.data
    result[name]={
        'geometry':digest([[list(v.co) for v in mesh.vertices],[list(p.vertices) for p in mesh.polygons]]),
        'weights':digest([[[g.group,g.weight] for g in v.groups] for v in mesh.vertices]),
        'groups':[g.name for g in ob.vertex_groups],
    }
for name in ['Player_Rig','Hung_Rig']:
    ob=bpy.data.objects[name]; action=ob.animation_data.action
    curves=[]
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                curves.extend([[f.data_path,f.array_index,[[list(k.co),k.interpolation,list(k.handle_left),list(k.handle_right)] for k in f.keyframe_points]] for f in bag.fcurves])
    result[name]={
        'bind':digest([[b.name,b.parent.name if b.parent else None,[list(row) for row in b.matrix_local]] for b in ob.data.bones]),
        'animation':digest(curves),'bones':len(ob.data.bones),'action':action.name,
    }
out=Path(sys.argv[sys.argv.index('--')+1]);out.write_text(json.dumps(result,indent=2),encoding='utf-8')
print('C1_ARCHIVE_FINGERPRINT',out,flush=True)
