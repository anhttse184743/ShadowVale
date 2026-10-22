import bpy
from pathlib import Path
from mathutils import Matrix
root=Path.cwd()
bpy.ops.wm.open_mainfile(filepath=str(root/'SourceArt/TamInjuredPreview/Tam_Injured_Proposal.blend'))
a=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)]
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:
 deps=bpy.context.evaluated_depsgraph_get();mesh=bpy.data.meshes.new_from_object(o.evaluated_get(deps),depsgraph=deps)
 mesh.transform(o.matrix_world)
 baked=bpy.data.objects.new('Tam_Injured',mesh);bpy.context.collection.objects.link(baked);baked.select_set(True)
 baked.data.materials.clear()
 bpy.context.view_layer.objects.active=baked
out=root/'Assets/_Project/Map01/Resources/Characters/Map02Roles/TamInjured.fbx'
bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)
