import bpy, json, os
bpy.ops.wm.read_factory_settings(use_empty=True)
root=os.path.abspath('.')
bpy.ops.import_scene.fbx(filepath=os.path.join(root,'Assets/_Project/Art/Characters/Player/Player.fbx'))
data=[]
for o in bpy.context.scene.objects:
    entry={'name':o.name,'type':o.type,'scale':list(o.scale),'dimensions':list(o.dimensions)}
    if o.type=='ARMATURE':
        entry['bones']=[{'name':b.name,'head':list(o.matrix_world@b.head_local),'tail':list(o.matrix_world@b.tail_local)} for b in o.data.bones]
    if o.type=='MESH':entry['materials']=[m.name if m else None for m in o.data.materials]
    data.append(entry)
with open('Logs/AnimationPreview/NamCrawl/model-info.json','w') as f:json.dump(data,f,indent=2)
print('INSPECT_COMPLETE',flush=True)
