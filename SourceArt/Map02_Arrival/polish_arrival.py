"""Bake arrival-only motion, using unchanged 28-bone rigs and the measured landing.
Run with Blender --background --python polish_arrival.py. Does not rebuild Map 1.
"""
import bpy, ast, json, math
from pathlib import Path
from mathutils import Matrix, Quaternion, Vector

HERE=Path(__file__).resolve().parent; ROOT=HERE.parents[1]
OUT=ROOT/'Assets/_Project/Art/Characters/Animations/Map02Arrival'
bpy.ops.wm.open_mainfile(filepath=str(HERE/'Map02_Arrival.blend'))
scene=bpy.context.scene;scene.render.fps=30;scene.render.fps_base=1
for c in bpy.data.collections:c.hide_viewport=False
for ob in bpy.data.objects:ob.hide_viewport=False;ob.hide_set(False)
tree=ast.parse((ROOT/'SourceArt/Map01_Extraction/author_extraction.py').read_text())
for node in tree.body:
 if isinstance(node,ast.FunctionDef) and node.name in ['target_pose','solve_leg','set_head']:
  exec(compile(ast.Module(body=[node],type_ignores=[]),'original-rig-helpers','exec'))

def ease(x):
 x=max(0,min(1,x));return x*x*x*(10+x*(-15+6*x))
def sample(rig,action,frame):
 rig.animation_data.action=action;scene.frame_set(int(frame));bpy.context.view_layer.update()
 return {b.name:(b.location.copy(),b.rotation_quaternion.copy(),b.scale.copy()) for b in rig.pose.bones}
def apply(rig,pose):
 rig.animation_data.action=None
 for pb in rig.pose.bones:
  pb.location,pb.rotation_quaternion,pb.scale=pose[pb.name]
 bpy.context.view_layer.update()
def mix(a,b,t):
 return {name:(a[name][0].lerp(b[name][0],t),a[name][1].slerp(b[name][1],t),a[name][2].lerp(b[name][2],t)) for name in a}
def gaze(rig,pitch=0,yaw=0):
 # Keep the skull upright in rig space, countering torso lean without twisting the neck.
 for name,weight in [('Neck',.35),('Head',1)]:
  pb=rig.pose.bones[name];head=pb.head.copy()
  q=Quaternion((0,0,1),yaw*weight)@Quaternion((1,0,0),pitch*weight)@pb.bone.matrix_local.to_quaternion()
  pb.matrix=Matrix.LocRotScale(head,q,Vector((1,1,1)));bpy.context.view_layer.update()
def arm(rig,side,target):
 upper=rig.pose.bones[side+'Arm'];lower=rig.pose.bones[side+'ForeArm'];hand=rig.pose.bones[side+'Hand']
 start=upper.head.copy();axis=Vector(target)-start;distance=axis.length;axis.normalize()
 a=(lower.head-upper.head).length;b=(hand.head-lower.head).length
 distance=max(abs(a-b)+.01,min((a+b)*.97,distance))
 pole=Vector((1 if side=='Left' else -1,.4,-.2));pole=(pole-axis*pole.dot(axis)).normalized()
 along=(a*a+distance*distance-b*b)/(2*distance)
 elbow=start+axis*along+pole*math.sqrt(max(0,a*a-along*along))
 wristRotation=hand.matrix.to_quaternion()
 for pb,head,tail in [(upper,start,elbow),(lower,elbow,start+axis*distance)]:
  q=(pb.bone.tail_local-pb.bone.head_local).rotation_difference(tail-head)@pb.bone.matrix_local.to_quaternion()
  pb.matrix=Matrix.LocRotScale(head,q,Vector((1,1,1)));bpy.context.view_layer.update()
 hand.matrix=Matrix.LocRotScale(hand.head,wristRotation,Vector((1,1,1)));bpy.context.view_layer.update()
def foot(rig,side,x,y,z):
 upper=rig.pose.bones[side+'UpLeg'];lower=rig.pose.bones[side+'Leg'];ankle=rig.pose.bones[side+'Foot']
 origin=upper.head.copy();target=Vector((x,y,z));axis=target-origin;distance=axis.length;axis.normalize()
 a=(lower.head-origin).length;b=(ankle.head-lower.head).length
 distance=max(abs(a-b)+.01,min((a+b)*.995,distance));along=(a*a+distance*distance-b*b)/(2*distance)
 pole=Vector((0,-1,0));pole=(pole-axis*pole.dot(axis)).normalized()
 knee=origin+axis*along+pole*math.sqrt(max(0,a*a-along*along))
 rotation=ankle.matrix.to_quaternion();scale=ankle.matrix.to_scale()
 q=(lower.head-origin).rotation_difference(knee-origin)@upper.matrix.to_quaternion()
 upper.matrix=Matrix.LocRotScale(origin,q,upper.matrix.to_scale());bpy.context.view_layer.update()
 q=(ankle.head-lower.head).rotation_difference(origin+axis*distance-lower.head)@lower.matrix.to_quaternion()
 lower.matrix=Matrix.LocRotScale(lower.head,q,lower.matrix.to_scale());bpy.context.view_layer.update()
 ankle.matrix=Matrix.LocRotScale(ankle.head,rotation,scale);bpy.context.view_layer.update()
def plant_pair(rig,targets):
 # Lower the pelvis only as far as needed to keep both contacts inside the original leg reach.
 bpy.context.view_layer.update()
 drop=0
 for side,point in targets.items():
  upper=rig.pose.bones[side+'UpLeg'];lower=rig.pose.bones[side+'Leg'];ankle=rig.pose.bones[side+'Foot']
  reach=((lower.head-upper.head).length+(ankle.head-lower.head).length)*.985
  horizontal=Vector((point[0]-upper.head.x,point[1]-upper.head.y)).length
  allowed=point[2]+math.sqrt(max(.01,reach*reach-horizontal*horizontal))
  drop=max(drop,upper.head.z-allowed)
 if drop>0:
  hip=rig.pose.bones['Hips'];q=hip.head.copy();q.z-=drop+.1;set_head(rig,hip,q)
  bpy.context.view_layer.update()
 for side,point in targets.items():foot(rig,side,*point)
 return max((rig.pose.bones[side+'Foot'].head-Vector(point)).length for side,point in targets.items())

XS=[-55,-95,-121,-147,-173,-199,-235,-235]
YS=[30,32,53,74,95,116,116,116] # cm above the actual interior floor
def stair(progress,side):
 cycle=min(progress,.999999)*8;active=int(cycle);p=ease(cycle-active)
 completed=active-1
 if completed%2!=side:completed-=1
 a=Vector((0,0)) if completed<0 else Vector((XS[completed],YS[completed]))
 if active%2==side:
  b=Vector((XS[active],YS[active]));return a.lerp(b,p)+Vector((0,10*math.sin(math.pi*p)))
 return a

report=[]
for who in ['Nam','Hung']:
 rig=bpy.data.objects[who];saved=(rig.location.copy(),rig.rotation_euler.copy(),rig.scale.copy(),rig.animation_data.action)
 rig.location=(0,0,0);rig.rotation_euler=(0,0,0);rig.scale*=1/.89
 for pb in rig.pose.bones:
  for c in pb.constraints:c.mute=True
 rest={b.name:tuple(v for row in b.matrix_local for v in row) for b in rig.data.bones}
 seated=sample(rig,bpy.data.actions[who+'_Boat_Seated_Travel'],0)
 rowAction=bpy.data.actions[who+'_Row_Loop']
 rig.animation_data.action=None;target_pose(rig,bpy.data.objects['Source_Walk'],0)
 upright={b.name:(b.location.copy(),b.rotation_quaternion.copy(),b.scale.copy()) for b in rig.pose.bones}
 hip=rig.data.bones['Hips'].head_local.copy();floor=rig.data.bones['LeftFoot'].head_local.z
 for suffix,seconds in [('Arrival_Row_Loop',2.4),('Arrival_Travel',3),('Oar_Stow',4),('Boat_Stand',3),('Boat_Turn',.65),('Walk_Ashore',1.2),('Jetty_StepUp',4.4),('Standing_Guard',3)]:
  name=who+'_'+suffix
  if name in bpy.data.actions:bpy.data.actions[name].name=name+'_BeforePolish'
  action=bpy.data.actions.new(name);action.use_fake_user=True;frames=round(seconds*30)
  maxHeadStep=0;maxFootError=0;previousHead=None;feet=[];authored=[]
  for f in range(frames+1):
   t=f/frames;time=f/30
   if suffix=='Arrival_Row_Loop':
    apply(rig,sample(rig,rowAction,t*(rowAction.frame_range[1]-rowAction.frame_range[0])))
    gaze(rig,.035+.012*math.sin(t*math.pi*2),.025*math.sin(t*math.pi*2))
   elif suffix=='Arrival_Travel':
    apply(rig,seated)
    rig.pose.bones['Spine1'].rotation_quaternion@=Quaternion((1,0,0),.009*math.sin(t*math.pi*2))
    bpy.context.view_layer.update();gaze(rig,.035,.02*math.sin(t*math.pi*2))
   elif suffix=='Oar_Stow':
    apply(rig,mix(sample(rig,rowAction,0),seated,ease(t)))
    rig.pose.bones['Spine1'].rotation_quaternion@=Quaternion((1,0,0),.10*math.sin(t*math.pi)**2)
    bpy.context.view_layer.update();gaze(rig,.035+.12*math.sin(t*math.pi)**2,-.14*math.sin(t*math.pi)**2)
   elif suffix=='Boat_Stand':
    w=ease((t-.12)/.78);apply(rig,mix(seated,upright,w))
    set_head(rig,rig.pose.bones['Hips'],(hip.x,hip.y+9*(1-w)-7*math.sin(t*math.pi)**2,42*(1-w)+(hip.z-2)*w))
    rig.pose.bones['Spine1'].rotation_quaternion@=Quaternion((1,0,0),.16*math.sin(t*math.pi)**2)
    bpy.context.view_layer.update()
    maxFootError=max(maxFootError,plant_pair(rig,{side:(sign*12,hip.y-24,floor) for side,sign in [('Left',1),('Right',-1)]}))
    for side,sign in [('Left',1),('Right',-1)]:
     if t<.7:
      bones=[rig.pose.bones[side+s] for s in ['Arm','ForeArm','Hand']]
      old=[(pb.location.copy(),pb.rotation_quaternion.copy()) for pb in bones]
      arm(rig,side,(sign*22,hip.y+12,35))
      if t>.42:
       release=ease((t-.42)/.28)
       for pb,(location,rotation) in zip(bones,old):
        pb.location=pb.location.lerp(location,release);pb.rotation_quaternion=pb.rotation_quaternion.slerp(rotation,release)
       bpy.context.view_layer.update()
    gaze(rig,.035+.09*math.sin(t*math.pi)**2,0)
   else:
    rig.animation_data.action=None
    target_pose(rig,bpy.data.objects['Source_Walk'],t*70 if suffix=='Walk_Ashore' else time*58)
    set_head(rig,rig.pose.bones['Hips'],(hip.x,hip.y,hip.z-(4-1.3*math.cos(t*math.pi*4)) if suffix=='Walk_Ashore' else hip.z-2))
    targets={}
    for side,sign,offset in [('Left',1,0),('Right',-1,.5)]:
     if suffix=='Walk_Ashore':
      phase=(t+offset)%1;stride=100/.89
      if phase<.55:forward=stride*(.275-phase);lift=0
      else:q=(phase-.55)/.45;forward=-stride*.275+stride*.55*ease(q);lift=10*math.sin(math.pi*q)**2
      targets[side]=(sign*12,hip.y-forward,floor+lift)
     elif suffix=='Jetty_StepUp':
      a=stair(t,0);b=stair(t,1);center=(a+b)*.5
      point=stair(t,0 if side=='Left' else 1)
      targets[side]=(sign*12,hip.y+(point.x-center.x)/.89,floor+(point.y-center.y)/.89)
     elif suffix=='Boat_Turn':
      phase=t+offset;lift=6*max(0,math.sin(phase*math.pi*2))**2
      targets[side]=(sign*12,hip.y-24,floor+lift)
     else:targets[side]=(sign*12,hip.y-10,floor)
    maxFootError=max(maxFootError,plant_pair(rig,targets))
    gaze(rig,.04 if suffix=='Walk_Ashore' else .09 if suffix=='Jetty_StepUp' else .035,0)
   evaluated=rig.evaluated_get(bpy.context.evaluated_depsgraph_get());matrices={p.name:evaluated.pose.bones[p.name].matrix.copy() for p in rig.pose.bones}
   head=matrices['Head'].to_quaternion()
   if previousHead:maxHeadStep=max(maxHeadStep,math.degrees(previousHead.rotation_difference(head).angle))
   previousHead=head
   authored.append({name:matrices[name].copy() for name in ['Head','LeftFoot','RightFoot']})
   feet.append([list(matrices[side+'Foot'].translation) for side in ['Left','Right']])
   rig.animation_data.action=None;scene.frame_set(f);rig.animation_data.action=action
   for pb in rig.pose.bones:
    parent=matrices[pb.parent.name] if pb.parent else Matrix.Identity(4)
    restparent=pb.parent.bone.matrix_local if pb.parent else Matrix.Identity(4)
    pb.matrix_basis=(parent@restparent.inverted()@pb.bone.matrix_local).inverted()@matrices[pb.name]
    pb.rotation_mode='QUATERNION'
    for prop in ['location','rotation_quaternion','scale']:pb.keyframe_insert(prop,frame=f,group=pb.name)
  assert rest=={b.name:tuple(v for row in b.matrix_local for v in row) for b in rig.data.bones}
  assert maxHeadStep<2,(name,maxHeadStep)
  assert maxFootError<.15,(name,maxFootError)
  bakedError=0;bakedHeadError=0
  for f in range(frames+1):
   scene.frame_set(f);bpy.context.view_layer.update()
   bakedHeadError=max(bakedHeadError,math.degrees(rig.pose.bones['Head'].matrix.to_quaternion().rotation_difference(authored[f]['Head'].to_quaternion()).angle))
   for bone in ['LeftFoot','RightFoot']:
    bakedError=max(bakedError,(rig.pose.bones[bone].head-authored[f][bone].translation).length)
  assert bakedError<.15 and bakedHeadError<.1,(name,bakedError,bakedHeadError)
  # Keep an unexported neutral frame for manual comparison. Unity's reference
  # avatars are built from the separate unanimated mesh-bind FBXs; a seated
  # take's default nodes must never define its anatomical scale or axes.
  rig.animation_data.action=None
  for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
  bpy.context.view_layer.update();rig.animation_data.action=action
  for pb in rig.pose.bones:
   pb.matrix_basis=Matrix.Identity(4)
   for prop in ['location','rotation_quaternion','scale']:pb.keyframe_insert(prop,frame=-1,group=pb.name)
  scene.frame_start=0;scene.frame_end=frames;scene.frame_set(-1)
  bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
  bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,
   bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,bake_anim_step=1,axis_forward='-Z',axis_up='Y')
  report.append(dict(clip=name,bones=len(rig.data.bones),fps=30,frames=frames+1,rest_preserved=True,max_head_step_degrees=maxHeadStep,max_foot_error_cm=maxFootError,max_baked_foot_error_cm=bakedError,max_baked_head_error_degrees=bakedHeadError,feet_samples=feet[::max(1,frames//8)]))
 rig.location,rig.rotation_euler,rig.scale,_=saved;rig.animation_data.action=bpy.data.actions[who+'_Boat_Stand']
for c in bpy.data.collections:
 if c.name.startswith('REFERENCE') and 'Map 2' not in c.name:c.hide_viewport=True
scene.frame_start=0;scene.frame_end=90;scene.frame_set(45)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Map02_Arrival.blend'))
(HERE/'polish-validation.json').write_text(json.dumps(report,indent=2))
print('ARRIVAL_POLISH_BAKED',len(report))
