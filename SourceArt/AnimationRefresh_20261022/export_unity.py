"""Export the approved rig/props and a bind-pose witness. Never save or replace the Blender source."""
import bpy,json,hashlib,math
from pathlib import Path
from mathutils import Vector,Matrix
R=Path(__file__).resolve().parents[2];O=Path(__file__).parent/'UnityExport';O.mkdir(exist_ok=True)
p=json.loads((R/'Docs/Reviews/AnimationRefresh_20261022/polish_receipt.json').read_text(encoding='utf-8-sig'))
source=Path(bpy.data.filepath);sha=hashlib.sha256(source.read_bytes()).hexdigest()
assert sha=='77727ffeab59bf989dfd9fdd568fbe859ce35541ae6df2a92e6341a32c59ac2c'
s=bpy.context.scene;r=bpy.data.objects['Player_Rig'];vl=bpy.context.view_layer
roots={k:bpy.data.objects[n] for k,n in [('Pistol','K54_Player'),('Knife','Nam_Dao_KaBar'),('F1','DEMO_F1'),('Stone','DEMO_Stone_10cm'),('Smoke','DEMO_Smoke')]}
objects=list(dict.fromkeys(c for root in roots.values() for c in [root]+list(root.children_recursive)))
for o in bpy.data.objects:
    if o.type=='MESH' and o not in objects:
        o.hide_set(True)
        for mod in o.modifiers:
            if mod.type=='ARMATURE':mod.show_viewport=False
props=[{'key':k,'root':root.name,'parts':[o.name for o in [root]+list(root.children_recursive) if o.type=='MESH']} for k,root in roots.items()]
visibility={o.name:[] for o in objects if o.type=='MESH'}
rootinv=r.data.bones['root'].matrix_local.to_3x3().inverted()@r.matrix_world.inverted().to_3x3()
s.frame_set(1);vl.update();origin=(r.matrix_world@r.pose.bones['root'].head).copy();rootloc=[];prop_root_locations={o.name:[] for o in objects if o.parent not in objects};takes={f:t for t in p['timeline'] for f in range(t['start'],t['end']+1)}
for f in range(1,p['frames']+1):
    s.frame_set(f);vl.update()
    for o in objects:
        if o.name in visibility:visibility[o.name].append(not o.hide_render)
    t=takes[f];k=f-t['start'];height=0
    if 'NHAY' in t['label'] and 8<=k<=53:
        seconds=p['jump_flight_seconds']*(k-8)/45
        height=max(0,math.sqrt(56)*seconds-10*seconds*seconds)
    at=r.matrix_world@r.pose.bones['root'].head
    delta=Vector((origin.x-at.x,origin.y-at.y,-height))
    rootloc.append(r.pose.bones['root'].location+rootinv@delta)
    for name,values in prop_root_locations.items():
        o=bpy.data.objects[name];M=o.matrix_world.copy();M.translation+=delta
        if o.parent:M=o.matrix_parent_inverse.inverted()@o.parent.matrix_world.inverted()@M
        values.append(M.to_translation())
    if f%400==0:print('SAMPLED',f,flush=True)
def curves(a):return [fc for l in a.layers for st in l.strips for bag in st.channelbags for fc in bag.fcurves]
r.animation_data.action=r.animation_data.action.copy();r.animation_data.action_slot=r.animation_data.action.slots[0]
for fc in curves(r.animation_data.action):
    if fc.data_path=='pose.bones["root"].location':
        fc.keyframe_points.clear();fc.keyframe_points.add(p['frames']);fc.keyframe_points.foreach_set('co',[v for f,q in enumerate(rootloc,1) for v in (f,q[fc.array_index])])
        for key in fc.keyframe_points:key.interpolation='LINEAR'
        fc.update()
for o in objects:
    if o.animation_data and o.animation_data.action:
        a=o.animation_data.action.copy();o.animation_data.action=a;o.animation_data.action_slot=a.slots[0]
        for layer in a.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for fc in list(bag.fcurves):
                        if fc.data_path in ('hide_render','hide_viewport'):bag.fcurves.remove(fc)
        if o.name in prop_root_locations:
            for fc in curves(a):
                if fc.data_path=='location':
                    values=prop_root_locations[o.name];fc.keyframe_points.clear();fc.keyframe_points.add(p['frames']);fc.keyframe_points.foreach_set('co',[v for f,q in enumerate(values,1) for v in (f,q[fc.array_index])])
                    for key in fc.keyframe_points:key.interpolation='LINEAR'
                    fc.update()
    o.hide_render=False;o.hide_viewport=False;o.hide_set(False)
# A tiny synthetic skin supplies all 62 bind matrices to the importer; it is never used by the game.
verts=[];faces=[]
for b in r.data.bones:
    x=r.matrix_world@b.head_local;i=len(verts);verts.extend([x+Vector((-.001,0,0)),x+Vector((.001,0,0)),x+Vector((0,.001,0))]);faces.append((i,i+1,i+2))
mesh=bpy.data.meshes.new('BindWitness');mesh.from_pydata(verts,[],faces);mesh.update()
witness=bpy.data.objects.new('EXPORT_BIND_WITNESS_DO_NOT_RENDER',mesh);s.collection.objects.link(witness)
for i,b in enumerate(r.data.bones):witness.vertex_groups.new(name=b.name).add([i*3,i*3+1,i*3+2],1,'REPLACE')
mod=witness.modifiers.new('Existing rig bind matrices','ARMATURE');mod.object=r
selected={r,witness,*objects}
for o in list(selected):
    parent=o.parent
    while parent is not None:selected.add(parent);parent=parent.parent
for o in vl.objects:o.select_set(False)
for o in selected:o.hide_set(False);o.hide_viewport=False;o.select_set(True)
vl.objects.active=r;s.frame_start=1;s.frame_end=p['frames'];s.render.fps=60;s.render.fps_base=1;s.frame_set(1)
fbx=O/'Nam_AnimationRefresh_v3.fbx'
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},use_mesh_modifiers=True,add_leaf_bones=False,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='AUTO',embed_textures=False)
receipt={'source':str(source),'sourceHash':sha,'fbxHash':hashlib.sha256(fbx.read_bytes()).hexdigest(),'frames':p['frames'],'fps':60,'bones':len(r.data.bones),'clips':p['timeline'],'props':props,'visibility':[{'name':n,'visible':v} for n,v in visibility.items()],'rootFlightRemoved':True,'rootXZRemoved':True,'bindWitnessOnly':True}
(O/'export.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8')
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
print('UNITY_EXPORT_READY',fbx.stat().st_size,len(objects),flush=True)
