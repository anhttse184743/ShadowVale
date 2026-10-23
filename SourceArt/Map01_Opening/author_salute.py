"""Author a straight-wrist salute on Nam's preserved 28-bone animation rig.
Run Blender --background --python this file. Only the dedicated output is rebuilt.
"""
import bpy,ast,math,json
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[1]
OUT=ROOT/'Assets/_Project/Art/Characters/Animations/Opening';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'SourceArt/Map02_Arrival/Map02_Arrival.blend'))
scene=bpy.context.scene
rig=bpy.data.objects['Nam']
# Keep the existing skinned visual and rig as the editable reference.
keep={rig}|{o for o in bpy.data.objects if any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers)}
for ob in list(bpy.data.objects):
 if ob not in keep:bpy.data.objects.remove(ob,do_unlink=True)
for col in bpy.data.collections:col.hide_viewport=False
for ob in keep:ob.hide_set(False);ob.hide_viewport=False;ob.hide_render=False
rig.location=(0,0,0);rig.rotation_euler=(0,0,0);rig.scale=(.01,.01,.01)
for pb in rig.pose.bones:
 for c in pb.constraints:c.mute=True
rest={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
assert len(rest)==28
before=set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/_Project/Art/cutsence/Salute.fbx'))
source=next(o for o in set(bpy.data.objects)-before if o.type=='ARMATURE');source.name='Source_Salute_Original'
for ob in set(bpy.data.objects)-before:ob.hide_render=True
source.animation_data.action.name='SOURCE_Salute_Original';source.animation_data.action.use_fake_user=True
scene.render.fps=30;scene.render.fps_base=1
tree=ast.parse((ROOT/'SourceArt/Map01_Extraction/author_extraction.py').read_text())
for node in tree.body:
 if isinstance(node,ast.FunctionDef) and node.name=='target_pose':exec(compile(ast.Module(body=[node],type_ignores=[]),'retarget','exec'))
rig.animation_data.action=None;target_pose(rig,source,0)
base={p.name:p.matrix.copy() for p in rig.pose.bones}
head=base['Head'].translation
tip=head+Vector((-13,-11,18))
def smooth(t):return max(0,min(1,t))**2*(3-2*max(0,min(1,t)))
def orient(head,y,normal):
 y=y.normalized();z=(normal-y*normal.dot(y)).normalized();x=y.cross(z).normalized();z=x.cross(y).normalized()
 return Matrix.LocRotScale(head,Matrix((x,y,z)).transposed().to_quaternion(),Vector((1,1,1)))
def arm_pose(tip,amount):
 upper=rig.pose.bones['RightArm'];lower=rig.pose.bones['RightForeArm'];hand=rig.pose.bones['RightHand']
 shoulder=upper.head.copy();a=upper.bone.length;b=lower.bone.length;finger=16.0
 axis=tip-shoulder;d=axis.length;axis.normalize();extended=b+finger
 d=max(abs(a-extended)+.01,min(a+extended-.01,d))
 along=(a*a+d*d-extended*extended)/(2*d)
 pole=Vector((-1,-.35,-.2));pole=(pole-axis*pole.dot(axis)).normalized()
 elbow=shoulder+axis*along+pole*math.sqrt(max(0,a*a-along*along))
 end=shoulder+axis*d;lowerdir=(end-elbow).normalized();wrist=elbow+lowerdir*b
 # Use a common elbow hinge plane for both bones. Independently orienting their
 # rolls can twist the sleeve even when the wrist-to-fingertip line looks straight.
 upperdir=(elbow-shoulder).normalized();hinge=-upperdir.cross(lowerdir).normalized()
 upper.matrix=orient(shoulder,upperdir,hinge.cross(upperdir));bpy.context.view_layer.update()
 lower.matrix=orient(elbow,lowerdir,hinge.cross(lowerdir));bpy.context.view_layer.update()
 hand.matrix=orient(wrist,lowerdir,hinge.cross(lowerdir));bpy.context.view_layer.update()
 return wrist+lowerdir*finger,lowerdir
action=bpy.data.actions.new('Nam_Salute_Briefing');action.use_fake_user=True
max_error=0;max_wrist=0
lower_end=57
def local_pose(matrices):
 result={}
 for p in rig.pose.bones:
  parent=matrices[p.parent.name] if p.parent else Matrix.Identity(4)
  parentrest=p.parent.bone.matrix_local if p.parent else Matrix.Identity(4)
  result[p.name]=(parent@parentrest.inverted()@p.bone.matrix_local).inverted()@matrices[p.name]
 return result

endpoints={}
for p in rig.pose.bones:p.matrix=base[p.name].copy()
bpy.context.view_layer.update();reached,direction=arm_pose(tip,1)
max_error=(tip-reached).length*.01
endpoints['hold']=local_pose({p.name:p.matrix.copy() for p in rig.pose.bones})
upper=rig.pose.bones['RightArm'];lower=rig.pose.bones['RightForeArm'];hand=rig.pose.bones['RightHand']
hinge=-(upper.tail-upper.head).cross(lower.tail-lower.head).normalized()
shoulder=upper.head.copy();a=upper.bone.length;b=lower.bone.length
for p in rig.pose.bones:p.matrix=base[p.name].copy()
bpy.context.view_layer.update()
# Begin and finish with a straight relaxed arm beside the thigh. The previous
# IK rest target was shorter than the full arm, forcing the elbow to stay bent.
down=Vector((-.055,-.025,-1)).normalized()
normal=hinge.cross(down).normalized()
upper.matrix=orient(shoulder,down,normal);bpy.context.view_layer.update()
elbow=shoulder+down*a
lower.matrix=orient(elbow,down,normal);bpy.context.view_layer.update()
wrist=elbow+down*b
hand.matrix=orient(wrist,down,normal);bpy.context.view_layer.update()
start=wrist+down*16
endpoints['rest']=local_pose({p.name:p.matrix.copy() for p in rig.pose.bones})
max_rest_bend=0
bends=[]

for frame in range(lower_end+1):
 rig.animation_data.action=None
 for p in rig.pose.bones:p.matrix=base[p.name].copy()
 bpy.context.view_layer.update()
 # Animate shoulder elevation and elbow flexion together in local joint space.
 # Neither transition uses a fingertip path that can lift the elbow above a
 # downward-pointing forearm. The same straight-arm endpoint owns the idle.
 travel=frame/21 if frame<=21 else 1 if frame<=27 else 1-(frame-27)/(lower_end-27)
 amount=smooth(travel)
 flex=smooth(min(1,travel*1.45))
 for name in ['RightArm','RightForeArm','RightHand']:
  first=endpoints['rest'][name];last=endpoints['hold'][name]
  blend=amount if name=='RightArm' else flex
  rig.pose.bones[name].matrix_basis=Matrix.LocRotScale(first.translation.lerp(last.translation,blend),first.to_quaternion().slerp(last.to_quaternion(),blend),first.to_scale().lerp(last.to_scale(),blend))
 bpy.context.view_layer.update()
 hand=rig.pose.bones['RightHand'];lower=rig.pose.bones['RightForeArm']
 upper=rig.pose.bones['RightArm']
 bend=math.degrees((upper.tail-upper.head).angle(lower.tail-lower.head))
 bends.append(bend)
 if frame in [0,lower_end]:max_rest_bend=max(max_rest_bend,bend)
 max_wrist=max(max_wrist,math.degrees((hand.tail-hand.head).angle(lower.tail-lower.head)))
 matrices={p.name:p.matrix.copy() for p in rig.pose.bones};locals=local_pose(matrices)
 scene.frame_set(frame);rig.animation_data.action=action
 for p in rig.pose.bones:
  p.matrix_basis=locals[p.name]
  p.rotation_mode='QUATERNION'
  for prop in ['location','rotation_quaternion','scale']:p.keyframe_insert(prop,frame=frame,group=p.name)
assert max_error<.01 and max_wrist<2,(max_error,max_wrist)
assert max_rest_bend<2,max_rest_bend
assert rest=={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
for name,pos in [('Salute_Temple_Contact',tip),('Salute_Arm_Rest',start)]:
 ob=bpy.data.objects.new(name,None);scene.collection.objects.link(ob);ob.location=rig.matrix_world@pos;ob.empty_display_type='SPHERE';ob.empty_display_size=.025
scene.frame_start=0;scene.frame_end=lower_end;scene.frame_set(21)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'Nam_Salute_Briefing.fbx'),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,bake_anim_step=1,axis_forward='-Z',axis_up='Y')
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Nam_Salute_Briefing.blend'))
(HERE/'salute-validation.json').write_text(json.dumps(dict(bones=28,fps=30,raise_end=.7,lower_start=.9,length=lower_end/30,raising='shoulder elevation with gradual elbow flexion',lowering='shoulder lowering with gradual elbow extension',max_rest_elbow_bend_degrees=max_rest_bend,elbow_bends_degrees=bends,max_tip_error_m=max_error,max_wrist_bend_degrees=max_wrist,rest_preserved=True),indent=2))
print('SALUTE_AUTHORED',max_error,max_wrist)
