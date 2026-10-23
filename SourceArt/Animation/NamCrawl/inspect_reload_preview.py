import bpy,json
from pathlib import Path
from mathutils import Vector
here=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(here/'Nam_Prone_Reload_Review_v1.blend'))
scene=bpy.data.scenes['Nam prone reload review'];bpy.context.window.scene=scene
actor=next(o for o in scene.objects if o.name=='Nam_Crawl_Review')
rows=[]
source=json.loads((here.parents[2]/'Logs/AnimationPreview/NamCrawl/ReloadPreview/validation.json').read_text())['samples']
max_error=0;body=[];lowest=100
for f in [0,24,48,64,86,126]:
    scene.frame_set(f);bpy.context.view_layer.update()
    mags=[o for o in scene.objects if o.type=='MESH' and 'magazine' in o.name.lower() and not o.hide_render]
    row={'frame':f,'hand':list(actor.matrix_world@actor.pose.bones['LeftHand'].head),'mag':[(o.name,list(sum((o.matrix_world@Vector(c) for c in o.bound_box),Vector())/8)) for o in mags]}
    assert len(mags)==5,(f,len(mags))
    expected=Vector(source[f]['new_mag'] if f/30>=1.95 else source[f]['old_mag'])
    actual=sum((Vector(v) for _,v in row['mag']),Vector())/len(mags)
    error=(actual-expected).length;max_error=max(max_error,error)
    assert error<.001,(f,'magazine detached',error)
    assert (Vector(row['hand'])-Vector(source[f]['left_hand'])).length<.001
    body.append((actor.matrix_world@actor.pose.bones['Hips'].head).z)
    graph=bpy.context.evaluated_depsgraph_get()
    for ob in scene.objects:
        if ob.type!='MESH' or not any(m.type=='ARMATURE' and m.object==actor for m in ob.modifiers):continue
        ev=ob.evaluated_get(graph);mesh=ev.to_mesh()
        try:lowest=min(lowest,min((ev.matrix_world@v.co).z for v in mesh.vertices))
        finally:ev.to_mesh_clear()
    rows.append(row)
(here/'reload-saved-inspection.json').write_text(json.dumps(rows,indent=2))
assert max(body)-min(body)<.015,body
assert lowest>-.03,lowest
out=here.parents[2]/'Logs/AnimationPreview/NamCrawl/ReloadPreview/saved-validation.json'
out.write_text(json.dumps({'passed':True,'magazine_max_error_m':max_error,'hip_span_m':max(body)-min(body),'skin_lowest_m':lowest,'gameplay_changed':False,'new_mixamo_reload_downloaded':False},indent=2))
print('SAVED_RELOAD_VERIFIED',flush=True)
