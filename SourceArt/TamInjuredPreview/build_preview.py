import bpy, math
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion
OUT=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(Path.cwd()/'Assets/_Project/Art/Characters/NPCs/Anh Tâm.fbx'))
a=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
if a.animation_data:a.animation_data_clear()
for p in a.pose.bones:p.rotation_mode='QUATERNION'
def aim(n,d):
 p=a.pose.bones[n];bpy.context.view_layer.update(); m=p.matrix.copy();w=a.matrix_world@m;current=w.to_3x3().col[1].normalized();q=current.rotation_difference(Vector(d).normalized());w=Matrix.Translation(w.translation)@q.to_matrix().to_4x4()@w.to_3x3().to_4x4();p.matrix=a.matrix_world.inverted()@w;bpy.context.view_layer.update()
def pos(n):return a.matrix_world@a.pose.bones[n].head
# Position hip near the ground, with the torso reclining behind it.
p=a.pose.bones['Hips'];m=p.matrix.copy();m.translation=a.matrix_world.inverted()@Vector((0,0,.23));p.matrix=m;bpy.context.view_layer.update()
aim('Hips',(0,.84,.54))
p=a.pose.bones['Hips'];p.rotation_quaternion=p.rotation_quaternion @ Quaternion((0,1,0), math.radians(-10));bpy.context.view_layer.update()
for n in ['Spine','Spine1','Spine2']:aim(n,(-.10,.94,.32))
aim('Neck',(0,.43,.90));aim('Head',(.08,.30,.95))
aim('LeftUpLeg',(.23,-.93,-.15));aim('LeftLeg',(.05,-.99,-.10));aim('LeftFoot',(.12,-.65,.45))
aim('RightUpLeg',(-.46,-.80,.39));aim('RightLeg',(.05,-.65,-.76));aim('RightFoot',(-.12,-1,0))
# Anatomical left elbow supports the torso; forearm rests forward on ground.
aim('LeftArm',(.48,-.04,-.88));aim('LeftForeArm',(-.12,-1,.02));aim('LeftHand',(-.15,-1,-.08))
# Right elbow bends in front of chest; right hand reaches the left shoulder.
aim('RightArm',(.50,-.65,.57))
shoulder=pos('LeftArm');handtarget=shoulder+Vector((-.04,-.14,.13))
elbow=pos('RightForeArm');direction=(handtarget-elbow).normalized();aim('RightForeArm',direction);aim('RightHand',direction)
a.location.z-=.83
bpy.context.view_layer.update()
# Repair missing FBX image paths using original texture folder.
tex=Path.cwd()/'Assets/_Project/Art/Characters/NPCs/Anh Tâm Textures'
for im in bpy.data.images:
 if im.source=='FILE':
  match=list(tex.rglob(Path(im.filepath.replace('\\','/')).name))
  if match:im.filepath=str(match[0]);im.reload()
# Neutral review floor, separate from the game scene.
def mat(n,c):
 m=bpy.data.materials.new(n);m.diffuse_color=(*c,1);return m
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='Preview floor';floor.data.materials.append(mat('Muted earth',(.20,.23,.19)))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.world=bpy.data.worlds.new('Review world');scene.world.color=(.25,.25,.25)
for loc,power,size in [((2,-3,5),650,4),((-3,1,3),450,3)]:
 bpy.ops.object.light_add(type='AREA',location=loc);l=bpy.context.object;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(Vector((0,0,.4))-l.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1300;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
bpy.ops.object.camera_add(location=(2.5,-3.4,2.6));cam=bpy.context.object;cam.rotation_euler=(Vector((0,-.12,.32))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=2.25;scene.camera=cam
scene.view_settings.view_transform='AgX'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Tam_Injured_Proposal.blend'))
scene.render.filepath=str(OUT/'Tam_Injured_Preview.png');bpy.ops.render.render(write_still=True)






