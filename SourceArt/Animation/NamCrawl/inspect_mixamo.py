import bpy,json
from pathlib import Path
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
report={}
for key,path in [('Nam',ROOT/'Assets/_Project/Art/Characters/Player/Player.fbx'),('Mixamo',HERE/'MixamoSource/Prone_Forward_InPlace_XBot.fbx')]:
    before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(path));rig=next(o for o in set(bpy.data.objects)-before if o.type=='ARMATURE')
    action=rig.animation_data.action if rig.animation_data else None
    points={}
    for frame in [0,20,40,70]:
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
        points[str(frame)]={p.name:list(rig.matrix_world@p.head) for p in rig.pose.bones if any(s in p.name for s in ['Hips','Head','Arm','Leg','Foot','Spine']) and 'Hand' not in p.name}
    report[key]={'rig':rig.name,'matrix':[list(row) for row in rig.matrix_world],'action':action.name if action else None,'frames':list(action.frame_range) if action else None,
        'bones':[{ 'name':b.name,'parent':b.parent.name if b.parent else None,'matrix':[list(row) for row in b.matrix_local]} for b in rig.data.bones],'poses':points}
(ROOT/'Logs/AnimationPreview/NamCrawl/mixamo-source-inspection.json').write_text(json.dumps(report,indent=2))
print('MIXAMO_INSPECTED',flush=True)
