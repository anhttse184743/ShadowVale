import bpy, os, json
ROOT=r'G:\game\ShadowVale'
source=os.path.join(ROOT,'SourceArt','Map01_Blender')
asset=os.path.join(ROOT,'Assets','_Project','Art','Environment','Map01_Blender')
os.makedirs(source,exist_ok=True);os.makedirs(asset,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=os.path.abspath('outputs/ShadowVale_Map01.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
    if o.type=='MESH' and not o.hide_render:
        o.select_set(True)
        # FBX 5.2 exporter mis-registers slots on shared meshes. Isolate export
        # data only; the authored blend retains efficient linked instances.
        if o.data.users>1:o.data=o.data.copy()
bpy.ops.export_scene.fbx(filepath=os.path.join(asset,'Map01_Environment.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',path_mode='AUTO')
palette={m.name:list(m.diffuse_color) for m in bpy.data.materials}
with open(os.path.join(source,'material_palette.json'),'w') as f:json.dump(palette,f,indent=2)
print('EXPORT_COMPLETE')
