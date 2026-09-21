import bpy,json,gzip,os,math
ROOT=r'G:\game\ShadowVale';SOURCE=os.path.join(ROOT,'SourceArt','Map01_Optimized')
with gzip.open(os.path.join(SOURCE,'Map01.meshdata.json.gz'),'rt',encoding='utf8') as f:data=json.load(f)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def material(name):
    m=bpy.data.materials.new(name);m.use_nodes=True
    n=m.node_tree.nodes.new('ShaderNodeVertexColor');n.layer_name='Col';m.node_tree.links.new(n.outputs['Color'],m.node_tree.nodes.get('Principled BSDF').inputs['Base Color']);return m
opaque=material('MapPalette');water=material('StreamPalette');meshes={}
def vec(a,i=0):return (a[i],a[i+2],a[i+1])
for src in data['meshes']:
    if src['kind'] in ['walk','block']:continue
    me=bpy.data.meshes.new(src['id']);v=[vec(src['vertices'],i) for i in range(0,len(src['vertices']),3)];t=src['triangles'];faces=[(t[i],t[i+2],t[i+1]) for i in range(0,len(t),3)];me.from_pydata(v,[],faces);me.update();me.materials.append(water if src['kind']=='water' else opaque)
    colors=me.color_attributes.new(name='Col',type='FLOAT_COLOR',domain='POINT')
    for i,c in enumerate(colors.data):c.color=src['colors'][i*4:i*4+4]
    for p in me.polygons:p.use_smooth=True
    meshes[src['id']]=me
    if src['kind'] in ['opaque','water']:
        o=bpy.data.objects.new(src['id'],me);bpy.context.scene.collection.objects.link(o);o.select_set(True)
for idx,src in enumerate(data['trees']):
    o=bpy.data.objects.new('Tree_%04d'%idx,meshes['Tree_%d_LOD0'%src['type']]);bpy.context.scene.collection.objects.link(o);o.location=vec(src['position']);o.scale=vec(src['scale']);o.rotation_euler.z=-math.radians(src['yaw']);o.select_set(True)
for idx,src in enumerate(data['props']):
    o=bpy.data.objects.new(src['id']+'_%04d'%idx,meshes[src['id']]);bpy.context.scene.collection.objects.link(o);o.location=vec(src['position']);o.scale=vec(src['scale']);o.rotation_euler.z=-math.radians(src['yaw']);o.select_set(True)
path=os.path.join(ROOT,'Assets','_Project','Art','Environment','Map01_Blender','Map01_Environment.fbx')
bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,use_mesh_modifiers=False,mesh_smooth_type='FACE',colors_type='LINEAR')
print('FBX_COMPLETE',os.path.getsize(path),flush=True)
