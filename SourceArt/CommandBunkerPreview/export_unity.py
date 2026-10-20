"""Export the approved Blender geometry and bake its procedural wood for URP."""
from pathlib import Path
import bpy, json, re
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/_Project/Art/Environment/CommandBunker'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(Path(__file__).with_name('CommandBunker_Proposal.blend')))
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=1
scene.render.bake.use_pass_direct=False;scene.render.bake.use_pass_indirect=False;scene.render.bake.use_pass_color=True
scene.render.bake.margin=8
scene.render.image_settings.file_format='PNG'
deps=bpy.context.evaluated_depsgraph_get()
chair=bpy.data.objects['Chair 01 | front toward table']
groups=[('Workbench',list(bpy.data.collections['01 Crafting workbench'].all_objects),Matrix.Translation((0,.4,0))),
        ('MissionBoard',list(bpy.data.collections['02 Mission board'].all_objects),Matrix.Translation((0,-1.94,0))),
        ('Chair',list(chair.children),chair.matrix_world.inverted())]
result={'models':[]}
for name,objects,pivot in groups:
    batches={}
    for source in objects:
        if source.type not in {'MESH','CURVE','FONT'}:continue
        evaluated=source.evaluated_get(deps)
        mesh=bpy.data.meshes.new_from_object(evaluated,depsgraph=deps)
        mesh.calc_loop_triangles()
        if not mesh.vertices:continue
        lo=Vector(tuple(min(v.co[k] for v in mesh.vertices) for k in range(3)))
        hi=Vector(tuple(max(v.co[k] for v in mesh.vertices) for k in range(3)))
        for tri in mesh.loop_triangles:
            mat=mesh.materials[tri.material_index] if mesh.materials else source.active_material
            if mat is None:continue
            batch=batches.setdefault(mat.name,{'material':mat,'v':[],'f':[],'g':[]})
            indices=[]
            for vi in tri.vertices:
                co=mesh.vertices[vi].co
                indices.append(len(batch['v']))
                batch['v'].append(tuple(pivot@source.matrix_world@co))
                batch['g'].append(tuple((co[k]-lo[k])/max(hi[k]-lo[k],.000001) for k in range(3)))
            batch['f'].append(indices)
        bpy.data.meshes.remove(mesh)
    model={'name':name,'parts':[]}
    for index,(matname,batch) in enumerate(batches.items()):
        key=name+'_'+str(index).zfill(2)
        mesh=bpy.data.meshes.new(key);mesh.from_pydata(batch['v'],[],batch['f']);mesh.update()
        ob=bpy.data.objects.new(key,mesh);scene.collection.objects.link(ob)
        bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
        # Weld triangle corners before smooth shading; preserve the beveled silhouettes.
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.000001);bpy.ops.object.mode_set(mode='OBJECT')
        mat=batch['material'].copy();mesh.materials.append(mat)
        bs=mat.node_tree.nodes.get('Principled BSDF')
        base=list(bs.inputs['Base Color'].default_value);metal=bs.inputs['Metallic'].default_value;rough=bs.inputs['Roughness'].default_value
        wood=bool(bs.inputs['Base Color'].is_linked)
        texture=''
        if wood:
            # Use object-space plank coordinates, independent of the combined mesh bounds.
            # A small bake proxy keeps the approved grain direction on every plank.
            generated=mesh.attributes.new('plankCoordinates','FLOAT_VECTOR','POINT')
            # Reconstruct normalized local plank coordinates by closest original vertex.
            # Welding preserves the first matching coordinate at seams.
            lookup={tuple(round(x,6) for x in co):g for co,g in zip(batch['v'],batch['g'])}
            for v in mesh.vertices:
                generated.data[v.index].vector=lookup.get(tuple(round(x,6) for x in v.co),(0,0,0))
            nodes=mat.node_tree.nodes;links=mat.node_tree.links
            attr=nodes.new('ShaderNodeAttribute');attr.attribute_name='plankCoordinates'
            for link in list(links):
                if link.from_node.type=='TEX_COORD' and link.from_socket.name=='Generated':
                    target=link.to_socket;links.remove(link);links.new(attr.outputs['Vector'],target)
            bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.1519,island_margin=.008);bpy.ops.object.mode_set(mode='OBJECT')
            size=1024 if name!='Chair' else 512
            img=bpy.data.images.new(key+'_Albedo',width=size,height=size,alpha=False)
            tex=nodes.new('ShaderNodeTexImage');tex.image=img;nodes.active=tex
            bpy.ops.object.bake(type='DIFFUSE')
            texture=key+'_Albedo.png';img.filepath_raw=str(OUT/texture);img.save()
        mesh.calc_loop_triangles()
        p={'name':key,'material':matname,'base':base,'metallic':metal,'roughness':rough,'texture':texture,'vertices':[],'normals':[],'uv':[],'triangles':[]}
        for tri in mesh.loop_triangles:
            start=len(p['vertices'])//3
            for li in tri.loops:
                loop=mesh.loops[li];v=mesh.vertices[loop.vertex_index];n=loop.normal
                p['vertices'].extend([v.co.x,v.co.z,v.co.y]);p['normals'].extend([n.x,n.z,n.y])
                uv=mesh.uv_layers.active.data[li].uv if mesh.uv_layers.active else (0,0)
                p['uv'].extend(uv)
            p['triangles'].extend([start,start+2,start+1])
        model['parts'].append(p)
        print('EXPORTED',key,len(p['triangles'])//3,texture,flush=True)
    result['models'].append(model)
(OUT/'ApprovedBunker.meshdata.json').write_text(json.dumps(result,separators=(',',':')),encoding='utf-8')
print('EXPORT COMPLETE',OUT,flush=True)
