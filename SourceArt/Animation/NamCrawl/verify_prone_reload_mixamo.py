import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
here=Path(__file__).resolve().parent;out=here.parents[2]/'Logs/AnimationPreview/NamCrawl/ReloadMixamoPreview'
bpy.ops.wm.open_mainfile(filepath=str(here/'Nam_Prone_Reload_Mixamo_Review_v2.blend'))
scene=bpy.data.scenes['Nam Mixamo prone reload review'];bpy.context.window.scene=scene
rig=scene.objects['Nam_Crawl_Review'];source=scene.objects['SOURCE_Mixamo_Prone_Reloading']
assert 'Prone_Reloading' in source.animation_data.action.name
records=json.loads((out/'validation.json').read_text())
assert records['actual_mixamo_reload'] and records['frames']==193
max_error=0;lowest=100
for f in range(0,193,6):
    scene.frame_set(f);bpy.context.view_layer.update()
    hand=rig.matrix_world@rig.pose.bones['LeftHand'].head
    error=(hand-Vector(records['samples'][f]['left_hand'])).length
    max_error=max(max_error,error);assert error<.001,(f,error)
    visible=[o for o in scene.objects if o.type=='MESH' and 'magazine' in o.name.lower() and not o.hide_render]
    assert len(visible)==(0 if 43<=f<46 else 5),(f,len(visible))
    graph=bpy.context.evaluated_depsgraph_get()
    for ob in scene.objects:
        if ob.type!='MESH' or not any(m.type=='ARMATURE' and m.object==rig for m in ob.modifiers):continue
        ev=ob.evaluated_get(graph);mesh=ev.to_mesh()
        try:lowest=min(lowest,min((ev.matrix_world@v.co).z for v in mesh.vertices))
        finally:ev.to_mesh_clear()
assert lowest>-.04,lowest
report={'passed':True,'actual_mixamo_reload':True,'source_sha256':hashlib.sha256((here/'MixamoSource/Prone_Reloading_XBot.fbx').read_bytes()).hexdigest(),'keys':193,'seconds':6.4,'max_saved_hand_error_m':max_error,'skin_lowest_m':lowest,'gameplay_changed':False}
(out/'saved-validation.json').write_text(json.dumps(report,indent=2))
print('ACTUAL_MIXAMO_RELOAD_SAVED_VERIFIED',flush=True)
