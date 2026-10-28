"""Pose fitting for the fictional Nam animation. Metrics are rig QA, not real-world training validation.
Executed in the author's Blender context to retain its original skeleton and prop geometry.
"""
def reference_measure():
    shoulder=pos('LeftArm');elbow=pos('LeftForeArm');wrist=pos('LeftHand')
    u=shoulder-elbow;f=wrist-elbow
    body=pos('Spine2')-pos('Hips');flat=Vector((body.x,body.y,0))
    chest=rig.matrix_world.to_3x3()@(rig.pose.bones['Spine2'].tail-rig.pose.bones['Spine2'].head)
    rarm=pos('RightForeArm')-pos('RightArm');rflat=Vector((rarm.x,rarm.y,0))
    fflat=Vector((f.x,f.y,0))
    def angle(a,b):return math.degrees(a.angle(b)) if a.length*b.length>1e-8 else 0
    desired_left=gun.location+foregrip+Vector((.035,.075,-.045))
    desired_right=gun.location+Vector((-.015,.065,-.005))
    return {'left_elbow_degrees':angle(u,f),'left_forearm_elevation_degrees':math.degrees(math.asin(abs(f.z)/f.length)),
        'body_heading_degrees':angle(flat,Vector((0,-1,0))),
        'chest_long_axis_elevation_degrees':math.degrees(math.asin(abs(chest.z)/chest.length)),
        'left_elbow_z':elbow.z,'right_elbow_z':pos('RightForeArm').z,
        'head_z':pos('Head').z,'left_elbow_axis_offset':elbow.x-gun.location.x,
        'head_axis_offset':pos('Head').x-gun.location.x,
        'left_forearm_plan_heading_degrees':angle(fflat,Vector((0,-1,0))),
        'right_upper_arm_plan_opening_degrees':angle(rflat,flat),
        'left_wrist_contact_error':(wrist-desired_left).length,
        'right_wrist_contact_error':(pos('RightHand')-desired_right).length}

def reference_score(m):
    # Target ranges supplied by the user, applied as animation art constraints.
    value=((m['left_elbow_degrees']-90)/10)**2
    value+=5*(max(0,80-m['left_elbow_degrees'],m['left_elbow_degrees']-100)/5)**2
    value+=((m['left_forearm_elevation_degrees']-67.5)/7.5)**2
    value+=((m['body_heading_degrees']-25)/5)**2
    value+=((m['chest_long_axis_elevation_degrees']-15)/5)**2
    value+=2*((m['left_elbow_z']-.045)/.025)**2
    value+=2*((m['right_elbow_z']-.045)/.025)**2
    value+=.3*((m['head_z']-.347)/.035)**2
    value+=6*((m['head_axis_offset']-.04)/.05)**2
    value+=.3*(m['left_forearm_plan_heading_degrees']/15)**2
    value+=1.5*((m['right_upper_arm_plan_opening_degrees']-37.5)/12)**2
    value+=.2*((m['left_elbow_axis_offset']-.025)/.07)**2
    value+=20*(m['left_wrist_contact_error']/.008)**2+20*(m['right_wrist_contact_error']/.008)**2
    for key in ['left_elbow_z','right_elbow_z']:
        value+=100*(max(0,.025-m[key])/.01)**2
    value+=20*(max(0,.32-m['head_z'])/.02)**2
    return value

def fit_reference():
    global FIT
    bounds={'body_yaw':(-30,-15,5),'body_x':(-.05,.4,.06),'body_y':(-.35,.1,.06),
        'clavicle_forward':(0,60,10),'clavicle_down':(0,50,10),'spine1':(-8,15,5),'spine2':(-28,-3,5),
        'gun_z':(.22,.32,.02),'left_pole':(-.25,.35,.1),'right_pole':(0,.6,.1)}
    def evaluate():
        pose(0,'aim');m=reference_measure();return reference_score(m),m
    for key,(low,high,_) in bounds.items():FIT[key]=max(low,min(high,FIT[key]))
    score,metrics=evaluate();evaluations=1
    print('REFERENCE_FIT_START',score,json.dumps(metrics),flush=True)
    for sweep in range(20):
        scale=1 if sweep<6 else .5 if sweep<12 else .25
        for key,(low,high,step) in bounds.items():
            base=FIT[key];best=base;best_score=score;best_metrics=metrics
            for sign in [-1,1]:
                FIT[key]=max(low,min(high,base+sign*step*scale))
                candidate,measurement=evaluate();evaluations+=1
                if candidate<best_score:best_score=candidate;best=FIT[key];best_metrics=measurement
            FIT[key]=best;score=best_score;metrics=best_metrics
        print('REFERENCE_FIT_SWEEP',sweep,score,json.dumps(metrics),flush=True)
    score,metrics=evaluate()
    payload={'parameters':FIT,'rig_measurements':metrics,'score':score,'evaluations':evaluations,
        'scope':'Fictional character animation art constraints; no source rest changes or gameplay bindings.'}
    (HERE/'prone_reference_fit.json').write_text(json.dumps(payload,indent=2))
    print('REFERENCE_FIT_COMPLETE',json.dumps(payload),flush=True)
