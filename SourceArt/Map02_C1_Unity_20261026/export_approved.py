"""Export approved C1 v4 only. Never save the .blend or change source rigs.
Model and transform takes share identical FBX axes / bone hierarchy.
"""
import bpy,json,hashlib,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/_Project/Art/Map02ArrivalC1';OUT.mkdir(parents=True,exist_ok=True)
DATA=ROOT/'Assets/Resources/Map02ArrivalC1';DATA.mkdir(parents=True,exist_ok=True)
REPORT=ROOT/'Docs/Reviews/C1_Unity_20261026';REPORT.mkdir(parents=True,exist_ok=True)
sc=bpy.context.scene;sc.render.fps=30;sc.render.fps_base=1
source=Path(bpy.data.filepath);rigs=[bpy.data.objects['Player_Rig'],bpy.data.objects['Hung_Rig']]
jobs=[('Nam',rigs[0],['Nam_Than','Player_LeftHand','Player_RightHand']),('Hung',rigs[1],['Hung_Than'])]
actions={r.name:r.animation_data.action for r in rigs};report={'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'fps':30,'actors':{},'expected':[]}
pack_old=json.loads((DATA/'C1Pack.json').read_text())
pack={k:v for k,v in pack_old.items() if k!='pack'};pack['pack']=[]
for f in range(1,263):
    sc.frame_set(f);bpy.context.view_layer.update();N=rigs[0];q=bpy.data.objects['BaLo'].matrix_world.to_quaternion();ch=N.pose.bones.get('Spine2') or N.pose.bones['Spine1']
    pack['pack'].append({'t':(f-1)/30,'mode':pack_old['pack'][min(f-1,len(pack_old['pack'])-1)]['mode'],'pos':list(bpy.data.objects['BaLo'].matrix_world.translation),'rot':[q.x,q.y,q.z,q.w],'hand':list(N.matrix_world@N.pose.bones['RightHand'].head),'chest':list(N.matrix_world@ch.head)})
    if f in [1,49,64,81,88,114,125,142,176,240,262]:
        row={'frame':f,'time':(f-1)/30}
        for name,rig,_ in jobs:
            row[name]={b:list(rig.matrix_world@rig.pose.bones[b].head) for b in ['Hips','Head','LeftHand','RightHand','LeftForeArm','LeftArm','RightLeg','LeftFoot','RightFoot']}
        report['expected'].append(row)
(DATA/'C1Pack.json').write_text(json.dumps(pack,separators=(',',':')),encoding='utf-8')
(DATA/'ReviewPoses.json').write_text(json.dumps({'frames':report['expected']},separators=(',',':')),encoding='utf-8')
common=dict(use_selection=True,apply_unit_scale=True,global_scale=1.,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,use_armature_deform_only=False,primary_bone_axis='Y',secondary_bone_axis='X')
anchor=bpy.data.objects.new('ClipAnchor',None);sc.collection.objects.link(anchor)
travel={}
for name,rig,meshnames in jobs:
    action=actions[rig.name];meshes=[bpy.data.objects[n] for n in meshnames]
    sc.frame_set(1);bpy.context.view_layer.update()
    original_matrix=rig.matrix_world.copy();relative={o.name:original_matrix.inverted()@o.matrix_world for o in meshes}
    # Remove only planar travel from the take, recording the unchanged trajectory.
    first=(rig.matrix_world@rig.pose.bones['Hips'].head).copy();offs=[]
    for f in range(1,263):
        sc.frame_set(f);bpy.context.view_layer.update();d=(rig.matrix_world@rig.pose.bones['Hips'].head)-first;d.z=0;offs.extend([d.x,d.y])
        root=rig.pose.bones['root'];m=root.matrix.copy();m.translation-=rig.matrix_world.inverted().to_3x3()@d;root.matrix=m;root.keyframe_insert('location',frame=f)
    travel[name.lower()]=offs
    rig.location=(0,0,0);rig.rotation_euler=(0,0,0);bpy.context.view_layer.update()
    for ob in meshes:ob.matrix_world=rig.matrix_world@relative[ob.name]
    oldname=rig.name;rig.name='Armature';sc.frame_start=1;sc.frame_end=262
    def select(obs):
        bpy.ops.object.select_all(action='DESELECT')
        for ob in obs:ob.hide_viewport=False;ob.hide_set(False);ob.select_set(True)
        bpy.context.view_layer.objects.active=rig
    rig.data.pose_position='REST';bpy.context.view_layer.update();select([rig,anchor]+meshes)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'_C1_v4.fbx')),object_types={'ARMATURE','MESH','EMPTY'},bake_anim=False,use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=True,path_mode='STRIP',**common)
    rig.data.pose_position='POSE';rig.animation_data.action=action;select([rig,anchor])
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'_C1_Motion_v4.fbx')),object_types={'ARMATURE','EMPTY'},bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0.,bake_anim_step=1.,**common)
    report['actors'][name]={'bones':len(rig.data.bones),'action':action.name,'frames':[1,262],'meshes':{o.name:len(o.data.vertices) for o in meshes},'source_scale':list(rig.scale)}
    rig.name=oldname
(DATA/'C1Travel.json').write_text(json.dumps(travel,separators=(',',':')),encoding='utf-8')
report['skin_color']=list(bpy.data.objects['Player_LeftHand'].active_material.diffuse_color)
(REPORT/'export.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('C1_APPROVED_EXPORT',json.dumps(report['actors']),flush=True)
