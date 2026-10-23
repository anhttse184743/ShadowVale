import bpy,json
from pathlib import Path
here=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(here/'Nam_Crawl_Mixamo_Review_v4.blend'))
out={}
for scene in bpy.data.scenes:
    if scene.name not in ['Mixamo unarmed','Mixamo knife','Mixamo rifle']:continue
    bpy.context.window.scene=scene
    rig=next(o for o in scene.objects if o.type=='ARMATURE' and not o.name.startswith('SOURCE'))
    data=[]
    for frame in range(scene.frame_end+1):
        scene.frame_set(frame)
        row={'frame':frame,'root':rig.location.z}
        for name in ['Hips','Head','LeftForeArm','RightForeArm','LeftFoot','RightFoot']:
            row[name]=list(rig.matrix_world@rig.pose.bones[name].head)
        data.append(row)
    out[scene.name]=data
(here/'motion-inspection.json').write_text(json.dumps(out))
for name,data in out.items():
    print(name,'root range',min(r['root'] for r in data),max(r['root'] for r in data),'max root step',max(abs(a['root']-b['root']) for a,b in zip(data,data[1:])),flush=True)
