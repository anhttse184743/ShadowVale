from pathlib import Path
import bpy
HERE=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'Revision2/Quartermaster_Proposal.blend'))
S=bpy.context.scene;S.render.engine='CYCLES';S.cycles.samples=1
S.render.bake.use_pass_direct=False;S.render.bake.use_pass_indirect=False;S.render.bake.use_pass_color=True
wood=bpy.data.materials['Walnut brown timber'].copy()
bpy.ops.mesh.primitive_plane_add(size=1);o=bpy.context.object;o.data.materials.append(wood)
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
img=bpy.data.images.new('WalnutTimber_Tile',width=1024,height=1024,alpha=False)
tex=wood.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img;wood.node_tree.nodes.active=tex
bpy.ops.object.bake(type='DIFFUSE')
img.filepath_raw=str(HERE.parents[1]/'Assets/_Project/Art/Environment/CommandBunker/WalnutTimber_Tile.png');img.file_format='PNG';img.save()
print('Walnut tile baked')
