"""Export approved revision 2 using the existing bunker mesh/URP importer format."""
from pathlib import Path
import sys
HERE=Path(__file__).resolve().parent
template=HERE.parent/'CommandBunkerPreview/export_unity.py'
code=template.read_text(encoding='utf-8')
code=code.replace("ROOT=Path(__file__).resolve().parents[2]",f"ROOT=Path({str(HERE.parents[1])!r})")
quarter='--quartermaster' in sys.argv
source=HERE/'Revision2'/('Quartermaster_Proposal.blend' if quarter else 'CommandBunker_Walnut_Proposal.blend')
code=code.replace("str(Path(__file__).with_name('CommandBunker_Proposal.blend'))",repr(str(source)))
if quarter:
 a=code.index("chair=bpy.data.objects[");b=code.index("result={'models':[]}")
 code=code[:a]+"""groups=[]
for name,key in [('RifleRack','01 AK47 rack - original player mesh'),('EquipmentShelf','02 Quartermaster shelf'),('RadioDesk','03 Information desk and inward facing chair')]:
    root=bpy.data.objects[key]
    groups.append((name,[o for o in root.children_recursive if not o.name.startswith('COLLIDER_')],root.matrix_world.inverted()))
"""+code[b:]
 code=code.replace("'ApprovedBunker.meshdata.json'","'ApprovedQuartermaster.meshdata.json'")
 # Retain imported AK UVs and packed images. Procedural plank materials still bake.
 code=code.replace("'g':[]}","'g':[],'uv':[]}")
 code=code.replace("for vi in tri.vertices:","for vi,loop_index in zip(tri.vertices,tri.loops):")
 code=code.replace("batch['v'].append(tuple(pivot@source.matrix_world@co))","batch['v'].append(tuple(pivot@source.matrix_world@co))\n                batch['uv'].append(tuple(mesh.uv_layers.active.data[loop_index].uv) if mesh.uv_layers.active else (0,0))")
 code=code.replace("bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.000001);bpy.ops.object.mode_set(mode='OBJECT')","# Keep triangle corners to retain source texture UVs.")
 code=code.replace("wood=bool(bs.inputs['Base Color'].is_linked)","""image_nodes=[n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
        wood=bool(bs.inputs['Base Color'].is_linked) and not image_nodes
        if image_nodes:
            uv_layer=mesh.uv_layers.new(name='UVMap')
            for loop in mesh.loops:uv_layer.data[loop.index].uv=batch['uv'][loop.vertex_index]
""")
 code=code.replace("texture=''","""texture=''
        if image_nodes:
            image=image_nodes[0].image
            texture=key+'_Albedo.png'
            image.filepath_raw=str(OUT/texture);image.file_format='PNG';image.save()
            base=[1,1,1,1]
""")
 # Reading newly calculated loop normals in Blender 5 is supported by the existing exporter.
exec(compile(code,str(template),'exec'))
