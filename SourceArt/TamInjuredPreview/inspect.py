import bpy
from pathlib import Path
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(Path.cwd()/'Assets/_Project/Art/Characters/NPCs/Anh Tâm.fbx'))
a=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
for b in a.data.bones: print(b.name,tuple(round(x,3) for x in (a.matrix_world@b.head_local)),tuple(round(x,3) for x in (a.matrix_world@b.tail_local)))
