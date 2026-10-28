"""Check saved, replayed Blender actions and hand attachments (not just author poses)."""
import bpy,json,sys
from pathlib import Path
from mathutils import Vector
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2];OUT=ROOT/'Logs/AnimationPreview/NamCrawl'
reference='--reference-fit' in sys.argv
v2='--rifle-review' in sys.argv or reference
if reference:OUT=OUT/'ProneReference'
bpy.ops.wm.open_mainfile(filepath=str(HERE/('Nam_Crawl_Prone_Reference_v3.blend' if reference else 'Nam_Crawl_Rifle_Review_v2.blend' if v2 else 'Nam_Crawl_Review.blend')))
result={}
for scene in [s for s in bpy.data.scenes if s.name.startswith('Crawl ')]:
    bpy.context.window.scene=scene
    rig=next(o for o in scene.objects if o.type=='ARMATURE')
    assert rig.animation_data and rig.animation_data.action,scene.name
    samples=[];prop_offsets=[]
    held=[o for o in scene.objects if o.parent==rig and o.parent_type=='BONE']
    end=scene.frame_end+1
    for frame in [0,end//4,end//2,end*3//4,end]:
        scene.frame_set(frame);bpy.context.view_layer.update()
        samples.append({n:list(rig.matrix_world@rig.pose.bones[n].head) for n in ['LeftHand','RightHand','LeftFoot','RightFoot','Head']})
        for prop in held:
            hand=rig.matrix_world@rig.pose.bones['RightHand'].matrix
            prop_offsets.append(list((hand.inverted()@prop.matrix_world).translation))
    def distance(a,b):return sum((x-y)**2 for x,y in zip(a,b))**.5
    seam=max(distance(samples[0][n],samples[-1][n]) for n in samples[0])
    travel=max(distance(samples[0]['LeftFoot'],s['LeftFoot']) for s in samples)
    assert seam<.001,(scene.name,seam)
    if not (v2 and scene.name=='Crawl aim'):assert travel>.15,(scene.name,travel)
    else:assert travel<.001,(scene.name,travel)
    prop_error=max((distance(prop_offsets[0],v) for v in prop_offsets),default=0)
    assert prop_error<.001,(scene.name,prop_error)
    result[scene.name]={'seam_m':seam,'foot_cycle_travel_m':travel,'prop_attachment_drift_m':prop_error}
    if v2 and scene.name in ['Crawl rifle','Crawl aim','Crawl transition']:
        result[scene.name]['elbow_height_m']={side:(rig.matrix_world@rig.pose.bones[side+'ForeArm'].head).z for side in ['Left','Right']}
        if scene.name=='Crawl transition':
            prop=held[0];scene.frame_set(0);q0=prop.matrix_world.to_quaternion()
            scene.frame_set(end//2);q1=prop.matrix_world.to_quaternion()
            angle=q0.rotation_difference(q1).angle
            assert angle>1,angle
            result[scene.name]['weapon_transition_radians']=angle
            if reference:
                lowest={side:1e3 for side in ['Left','Right']}
                for frame in range(end+1):
                    scene.frame_set(frame)
                    for side in lowest:lowest[side]=min(lowest[side],(rig.matrix_world@rig.pose.bones[side+'ForeArm'].head).z)
                result[scene.name]['minimum_elbow_joint_height_over_clip']=lowest
                assert min(lowest.values())>.015,lowest
    if scene.name in ['Crawl aim','Crawl knife']:
        if reference and scene.name=='Crawl aim':
            scene.frame_set(0)
            def bone(name):return rig.matrix_world@rig.pose.bones[name].head
            u=bone('LeftArm')-bone('LeftForeArm');f=bone('LeftHand')-bone('LeftForeArm')
            import math
            measured=math.degrees(u.angle(f));elevation=math.degrees(math.asin(abs(f.z)/f.length))
            assert 80<=measured<=100,(measured,elevation)
            assert 60<=elevation<=75,(measured,elevation)
            result[scene.name]['left_elbow_angle']=measured;result[scene.name]['left_forearm_elevation']=elevation
            body=bone('Spine2')-bone('Hips');flat=Vector((body.x,body.y,0))
            body_angle=math.degrees(flat.angle(Vector((0,-1,0))))
            chest=rig.matrix_world.to_3x3()@(rig.pose.bones['Spine2'].tail-rig.pose.bones['Spine2'].head)
            chest_angle=math.degrees(math.asin(abs(chest.z)/chest.length))
            right=bone('RightForeArm')-bone('RightArm')
            right_angle=math.degrees(Vector((right.x,right.y,0)).angle(flat))
            assert 15<=body_angle<=30,body_angle
            assert 10<=chest_angle<=20,chest_angle
            assert 30<=right_angle<=45,right_angle
            result[scene.name].update(body_heading=body_angle,chest_long_axis_elevation=chest_angle,right_upper_arm_plan_angle=right_angle)
        scene.frame_set(9);scene.render.filepath=str(OUT/(scene.name.replace(' ','-')+'-saved-playback.png'));bpy.ops.render.render(write_still=True)
        if v2 and scene.name=='Crawl aim':
            weapon=held[0]
            result[scene.name]['weapon_world_position']=list(weapon.matrix_world.translation)
            assert abs(weapon.matrix_world.translation.y+.98)<.001
            cam=scene.camera;cam.data.ortho_scale=1.0;cam.location=(-1.6,-2.0,1.0)
            cam.rotation_euler=(Vector((-.13,-1.01,.26))-cam.location).to_track_quat('-Z','Y').to_euler()
            scene.render.filepath=str(OUT/'rifle-v2-hand-detail.png');bpy.ops.render.render(write_still=True)
assert len(result)==(7 if v2 else 6)
(OUT/('rifle-v2-playback-validation.json' if v2 else 'saved-playback-validation.json')).write_text(json.dumps(result,indent=2))
print('SAVED_CRAWL_PLAYBACK_VERIFIED',flush=True)
