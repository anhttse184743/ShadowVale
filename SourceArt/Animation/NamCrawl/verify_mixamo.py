import bpy,json,math,hashlib
from pathlib import Path
from mathutils import Vector
import numpy as np
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2];OUT=ROOT/'Logs/AnimationPreview/NamCrawl/MixamoPreview'
bpy.ops.wm.open_mainfile(filepath=str(HERE/'Nam_Crawl_Mixamo_Review_v4.blend'))
report={};export=HERE/'MixamoDerivedFBX';export.mkdir(exist_ok=True)
original={b['name']:b['matrix'] for b in json.loads((ROOT/'Logs/AnimationPreview/NamCrawl/mixamo-source-inspection.json').read_text())['Nam']['bones']}
for scene in [s for s in bpy.data.scenes if s.name.startswith('Mixamo ') and s.name!='Mixamo source studio']:
    bpy.context.window.scene=scene
    rig=next(o for o in scene.objects if o.type=='ARMATURE' and not o.name.startswith('SOURCE_Mixamo'))
    assert rig.animation_data and rig.animation_data.action
    assert rig.animation_data.action.name.startswith('Nam_Mixamo_Prone_')
    assert max(abs(b.matrix_local[r][c]-original[b.name][r][c]) for b in rig.data.bones for r in range(4) for c in range(4))<.00001
    end=scene.frame_end+1;points=[];skin_min=[];props=[]
    held=[o for o in scene.objects if o.parent==rig and o.parent_type=='BONE']
    skins=[o for o in scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers)]
    for frame in [0,end//4,end//2,end*3//4,end]:
        scene.frame_set(frame);bpy.context.view_layer.update()
        points.append({n:list(rig.matrix_world@rig.pose.bones[n].head) for n in ['LeftHand','RightHand','LeftFoot','RightFoot','Head']})
        for ob in held:
            hand=rig.matrix_world@rig.pose.bones['RightHand'].matrix
            props.append(list((hand.inverted()@ob.matrix_world).translation))
        low=1000;graph=bpy.context.evaluated_depsgraph_get()
        for ob in skins:
            ev=ob.evaluated_get(graph);mesh=ev.to_mesh()
            try:
                verts=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('co',verts)
                world=np.asarray(ev.matrix_world,dtype=np.float32)
                low=min(low,float((verts.reshape(-1,3)@world[2,:3]+world[2,3]).min()))
            finally:ev.to_mesh_clear()
        skin_min.append(low)
    def dist(a,b):return sum((x-y)**2 for x,y in zip(a,b))**.5
    seam=max(dist(points[0][name],points[-1][name]) for name in points[0])
    assert seam<.0001,(scene.name,seam)
    drift=max((dist(props[0],p) for p in props),default=0)
    assert drift<.001,(scene.name,drift)
    assert min(skin_min)>-.045,(scene.name,skin_min)
    travel=max(dist(points[0]['LeftFoot'],p['LeftFoot']) for p in points)
    if scene.name not in ['Mixamo aim']:assert travel>.02,(scene.name,travel)
    report[scene.name]={'loop_seam_m':seam,'prop_attachment_drift_m':drift,'foot_motion_m':travel,'sampled_skin_lowest_m':skin_min,'rest_preserved':True}
    free=['Left','Right'] if scene.name=='Mixamo unarmed' else ['Left'] if scene.name in ['Mixamo knife','Mixamo grenade','Mixamo stone'] else []
    if free:
        scene.frame_set(0)
        reference={s:rig.pose.bones[s+'Hand'].matrix_basis.to_quaternion().copy() for s in free}
        wrist_error=0
        for frame in range(end+1):
            scene.frame_set(frame)
            for side in free:
                q=rig.pose.bones[side+'Hand'].matrix_basis.to_quaternion()
                wrist_error=max(wrist_error,1-abs(q.dot(reference[side])))
        assert wrist_error<.000001,(scene.name,'wrist rotation changed',wrist_error)
        report[scene.name]['fixed_wrist_max_quaternion_error']=wrist_error
    if scene.name not in ['Mixamo aim','Mixamo transition']:
        roots=[];knees=[]
        for frame in range(end+1):
            scene.frame_set(frame);roots.append(rig.location.z)
            angles=[]
            for side in ['Left','Right']:
                p=[rig.matrix_world@rig.pose.bones[side+n].head for n in ['UpLeg','Leg','Foot']]
                angles.append(math.degrees((p[0]-p[1]).angle(p[2]-p[1])))
            knees.append(angles)
        span=max(roots)-min(roots)
        assert span<.02,(scene.name,'body lift',span)
        report[scene.name]['body_vertical_span_m']=span
    scene.frame_set(end//4)
    if scene.name=='Mixamo aim':
        e=rig.matrix_world@rig.pose.bones['LeftForeArm'].head
        u=rig.matrix_world@rig.pose.bones['LeftArm'].head-e;f=rig.matrix_world@rig.pose.bones['LeftHand'].head-e
        report[scene.name]['elbow_angle']=math.degrees(u.angle(f))
        report[scene.name]['forearm_elevation']=math.degrees(math.asin(abs(f.z)/f.length))
        scene.render.filepath=str(OUT/'saved-aim-playback.png');bpy.ops.render.render(write_still=True)
    # Export selected original Nam skeleton only; source Mixamo rigs and meshes stay private references.
    scene.frame_start=0;scene.frame_end=end;scene.render.fps=30
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(export/(scene.name.replace(' ','_')+'.fbx')),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,bake_anim_step=1,axis_forward='-Z',axis_up='Y')
    print('VERIFIED_AND_EXPORTED',scene.name,flush=True)
assert len(report)==7,len(report)
report['sources']={p.name:{'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in (HERE/'MixamoSource').glob('*.fbx')}
(OUT/'saved-mixamo-validation.json').write_text(json.dumps(report,indent=2))
print('MIXAMO_SAVED_PLAYBACK_VERIFIED',flush=True)
