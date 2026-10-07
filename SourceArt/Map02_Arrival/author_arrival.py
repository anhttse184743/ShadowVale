import bpy, math, json, ast
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion
ROOT=Path(__file__).resolve().parents[2];HERE=Path(__file__).resolve().parent
OUT=ROOT/'Assets/_Project/Art/Characters/Animations/Map02Arrival';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'SourceArt/Map01_Extraction/Map01_Extraction.blend'))
scene=bpy.context.scene;scene.render.fps=30
# Hidden source collections stop pose evaluation in Blender. Retarget moving source bones,
# not the last cached pose left before the collection was hidden.
for c in bpy.data.collections:c.hide_viewport=False
for ob in bpy.data.objects:
    if ob.name.startswith('Source_'):ob.hide_viewport=False;ob.hide_set(False);ob.hide_render=True
# Reuse only pure authoring helpers; never execute the original factory reset/export script.
tree=ast.parse((ROOT/'SourceArt/Map01_Extraction/author_extraction.py').read_text())
for node in tree.body:
    if isinstance(node,ast.FunctionDef) and node.name in ['target_pose','solve_leg','set_head']:
        exec(compile(ast.Module(body=[node],type_ignores=[]),'authoring-helper','exec'))
report=[]
for who in ['Nam','Hung']:
    rig=bpy.data.objects[who];rig.location=(0,0,0);rig.rotation_euler=(0,0,0);rig.scale*=1/.89
    for pb in rig.pose.bones:
        for constraint in pb.constraints:constraint.mute=True
    rest={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
    source=bpy.data.objects['Source_Walk']
    for suffix,seconds in [('Boat_Stand',3),('Jetty_StepUp',3.4),('Walk_Ashore',1.2),('Oar_Stow',4),('Standing_Guard',3)]:
        old=bpy.data.actions[who+'_Boat_Turn_And_Sit']
        action=bpy.data.actions.new(who+'_'+suffix);action.use_fake_user=True
        frames=round(seconds*30)
        for f in range(frames+1):
            if suffix=='Standing_Guard':
                rig.animation_data.action=None
                target_pose(rig,bpy.data.objects['Source_Gunplay'],12)
                for bone in ['Spine','Spine1']:
                    rig.pose.bones[bone].rotation_quaternion @= Quaternion((1,0,0),.009*math.sin(f/30*math.pi*2/3))
                bpy.context.view_layer.update()
            elif suffix=='Oar_Stow':
                rig.animation_data.action=bpy.data.actions[who+'_Row_Loop'];scene.frame_set(0)
                start={b.name:(b.location.copy(),b.rotation_quaternion.copy(),b.scale.copy()) for b in rig.pose.bones}
                rig.animation_data.action=bpy.data.actions[who+'_Boat_Seated_Travel'];scene.frame_set(0)
                end={b.name:(b.location.copy(),b.rotation_quaternion.copy(),b.scale.copy()) for b in rig.pose.bones}
                rig.animation_data.action=None
                w=(f/frames)**2*(3-2*f/frames)
                for pb in rig.pose.bones:
                    a=start[pb.name];b=end[pb.name]
                    pb.location=a[0].lerp(b[0],w);pb.rotation_quaternion=a[1].slerp(b[1],w);pb.scale=a[2].lerp(b[2],w)
                bpy.context.view_layer.update()
            elif suffix=='Boat_Stand':
                # Rise forward from the travel pose; do not reverse the turn-and-sit yaw.
                rig.animation_data.action=bpy.data.actions[who+'_Boat_Seated_Travel'];scene.frame_set(0)
                seated={b.name:(b.location.copy(),b.rotation_quaternion.copy(),b.scale.copy()) for b in rig.pose.bones}
                rig.animation_data.action=None;target_pose(rig,bpy.data.objects['Source_Gunplay'],12)
                upright={b.name:(b.location.copy(),b.rotation_quaternion.copy(),b.scale.copy()) for b in rig.pose.bones}
                t=f/frames
                def ease(a,b):
                    x=max(0,min(1,(t-a)/(b-a)));return x*x*x*(10-15*x+6*x*x)
                for pb in rig.pose.bones:
                    w=ease(.20,.91) if pb.name in ['Hips','LeftUpLeg','RightUpLeg','LeftLeg','RightLeg'] else ease(.12,.94)
                    a=seated[pb.name];b=upright[pb.name]
                    pb.location=a[0].lerp(b[0],w);pb.rotation_quaternion=a[1].slerp(b[1],w);pb.scale=a[2].lerp(b[2],w)
                # Lean before lifting the pelvis, then settle slowly into balance.
                lean=.14*math.sin(math.pi*ease(0,.90))
                for name in ['Spine','Spine1']:
                    rig.pose.bones[name].rotation_quaternion @= Quaternion((1,0,0),lean)
                bpy.context.view_layer.update()
            else:
                rig.animation_data.action=None
                target_pose(rig,source,(f/frames)*(source.animation_data.action.frame_range[1]-source.animation_data.action.frame_range[0]) if suffix=="Walk_Ashore" else f*1.05)
                if suffix=='Jetty_StepUp':
                    # Extra toe clearance over the short wooden risers; keep the original arm swing.
                    for side,offset in [('Left',0),('Right',math.pi)]:
                        foot=rig.pose.bones[side+'Foot']
                        q=rig.matrix_world@foot.head
                        q.z+=.12*max(0,math.sin(f/30*math.pi*2/.68+offset))
                        solve_leg(rig,side,q)
            matrices={pb.name:pb.matrix.copy() for pb in rig.pose.bones}
            rig.animation_data.action=None;scene.frame_set(f)
            rig.animation_data.action=action
            for pb in rig.pose.bones:
                parent=matrices[pb.parent.name] if pb.parent else Matrix.Identity(4)
                restparent=pb.parent.bone.matrix_local if pb.parent else Matrix.Identity(4)
                base=parent@restparent.inverted()@pb.bone.matrix_local
                pb.matrix_basis=base.inverted()@matrices[pb.name];pb.rotation_mode='QUATERNION'
                for prop in ['location','rotation_quaternion','scale']:pb.keyframe_insert(prop,frame=f,group=pb.name)
        assert rest=={b.name:[list(row) for row in b.matrix_local] for b in rig.data.bones}
        scene.frame_start=0;scene.frame_end=frames;scene.frame_set(0)
        bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
        bpy.ops.export_scene.fbx(filepath=str(OUT/(who+'_'+suffix+'.fbx')),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,bake_anim_step=1,axis_forward='-Z',axis_up='Y')
        report.append({'clip':who+'_'+suffix,'bones':28,'fps':30,'frames':frames+1,'rest_preserved':True})
    rig.animation_data.action=bpy.data.actions[who+'_Jetty_StepUp'];rig.scale*=.89
    rig.location=(-12.53,84.8+(1 if who=='Hung' else 0),.81);rig.rotation_euler.z=math.radians(-104.1)
# Exact current dock/canal geometry, rather than a generic showcase platform.
collection=bpy.data.collections.new('REFERENCE — Map 2 southern landing');scene.collection.children.link(collection)
geo=json.loads((HERE/'arrival-reference.json').read_text())
for part in geo['parts']:
    if part['name'] not in ['Timber evacuation landing','Winding canal water','South bank reference']:continue
    if part['center']['z']>0:continue
    vertices=[(v['x'],-v['z'],v['y']) for v in part['vertices']]
    faces=[part['triangles'][i:i+3][::-1] for i in range(0,len(part['triangles']),3)]
    mesh=bpy.data.meshes.new(part['name']);mesh.from_pydata(vertices,[],faces);mesh.update()
    ob=bpy.data.objects.new(part['name'],mesh);collection.objects.link(ob)
for i in range(5):
    bpy.ops.mesh.primitive_cube_add(size=1,location=(-12.27-i*.252,84.82+i*.063,.41+i*.21-.08))
    ob=bpy.context.object;ob.name='Arrival tread '+str(i+1);ob.scale=(.28,2.5,.16);ob.rotation_euler.z=math.radians(-14.1)
# The carried Map 1 hull/benches are shown at the measured Map 2 mooring.
map1=json.loads((ROOT/'SourceArt/Map01_Extraction/jetty-reference.json').read_text())
angle=math.radians(-14.1)
for part in map1['parts']:
    if part['name'] not in ['Moored wooden sampan','Boat bench']:continue
    vertices=[]
    for v in part['vertices']:
        x=v['x']-1.55;z=v['z']-87
        wx=-11.346+math.cos(angle)*x+math.sin(angle)*z
        wz=-84.586-math.sin(angle)*x+math.cos(angle)*z
        vertices.append((wx,-wz,v['y']+.24))
    faces=[part['triangles'][i:i+3][::-1] for i in range(0,len(part['triangles']),3)]
    mesh=bpy.data.meshes.new(part['name']+' arrival');mesh.from_pydata(vertices,[],faces);mesh.update()
    ob=bpy.data.objects.new(part['name']+' arrival',mesh);collection.objects.link(ob)
# Hide the Map 1 reference for clean manual editing; its meshes/actions remain available.
for c in bpy.data.collections:
    if c.name.startswith('REFERENCE') and c!=collection:c.hide_viewport=True;c.hide_render=True
scene.frame_start=0;scene.frame_end=102;scene.frame_set(30)
scene.camera.location=(-18,91,5);scene.camera.rotation_euler=(Vector((-13,86,1))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Map02_Arrival.blend'))
(HERE/'animation-validation.json').write_text(json.dumps(report,indent=2))
print('MAP02_AUTHORING_COMPLETE',len(report))
