"""Prepare an editable prone reload scene while awaiting the Mixamo source clip."""
import bpy
from pathlib import Path
from mathutils import Matrix,Vector
here=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(here/'Nam_Crawl_Mixamo_Review_v4.blend'))
scene=bpy.data.scenes['Mixamo aim'];bpy.context.window.scene=scene;scene.frame_set(0)
actor=next(o for o in scene.objects if o.type=='ARMATURE' and not o.name.startswith('SOURCE'))
gun=next(o for o in scene.objects if o.parent==actor and o.parent_type=='BONE')
actor.animation_data.action=actor.animation_data.action.copy()
actor.animation_data.action.name='Nam_Prone_Reload_Base_REFERENCE_ONLY'
# The latest game correction lowers only the prop, leaving both hands in place.
gun.matrix_world=Matrix.Translation(Vector((0,0,-.05)))@gun.matrix_world
magazine=bpy.data.objects.new('Reload_Magazine',None);scene.collection.objects.link(magazine)
magazine.parent=gun;magazine.matrix_parent_inverse=Matrix.Identity(4)
parts=[o for o in gun.children_recursive if o.type=='MESH' and 'magazine' in o.name.lower()]
for ob in parts:
    world=ob.matrix_world.copy();ob.parent=magazine;ob.matrix_world=world
assert parts,'AK magazine functional parts not found'
for other in list(bpy.data.scenes):
    if other!=scene:bpy.data.scenes.remove(other)
scene.name='Prone reload - source pending'
note=bpy.data.texts.new('READ ME - reload staging')
note.write('Prepared from approved Nam prone aim / genuine Mixamo Prone Idle. Magazine parts separated for contact animation. Gun lowered 5 cm, hands unchanged. This is staging only: NO new Mixamo reload clip has been downloaded or retargeted. No Unity gameplay changes.\n')
bpy.ops.outliner.orphans_purge(do_recursive=True)
bpy.ops.wm.save_as_mainfile(filepath=str(here/'Nam_Prone_Reload_Working.blend'))
print('PRONE_RELOAD_SCENE_PREPARED',len(parts),flush=True)
