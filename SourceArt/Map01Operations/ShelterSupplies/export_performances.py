import bpy,sys,json,hashlib
from pathlib import Path
from mathutils import Vector
import argparse
p=argparse.ArgumentParser();p.add_argument('--source-dir',required=True);p.add_argument('--output-dir',required=True);p.add_argument('--include-hung',action='store_true')
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);O=Path(a.source_dir).resolve();D=Path(a.output_dir).resolve();D.mkdir(parents=True,exist_ok=True)
receipt=[]
for label in ['Collect','Handoff']+(['HungRest'] if a.include_hung else []):
 src=O/'Nam_Backpack_Reach_v9_20261019'/f'Nam_Backpack_{label}_Reach_v9_20261019.blend' if label!='HungRest' else O/'Hung_Stretcher_Complete_PREVIEW_20261019.blend'
 bpy.ops.wm.open_mainfile(filepath=str(src));s=bpy.context.scene;r=bpy.data.objects['Nam'] if label!='HungRest' else next(o for o in s.objects if o.type=='ARMATURE')
 s.render.fps=30;s.frame_set(1);r.name='Armature'
 selected=[r]+[o for o in s.objects if o.type=='MESH' and o.find_armature()==r]
 cloth=[]
 if label!='HungRest':
  props=[o for o in s.objects if o.name.startswith('Interaction_Backpack_') or o.name=='SupplyBag_Interaction' or o.parent and o.parent.name=='SupplyBag_Interaction']
  props=[o for o in props if o.name!='Interaction_Backpack_Handle' and 'BagHandle' not in o.name]
  selected+=props
  for name in ['Backpack_TopHandle_v5','Backpack_SideHandle_v5','SupplyBag_ClothHandle_v5']:
   ob=bpy.data.objects[name];frames=[];me0=None
   for f in range(1,482):
    s.frame_set(f);ob.hide_viewport=False;ob.hide_render=False;bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();ev=ob.evaluated_get(dg);me=bpy.data.meshes.new_from_object(ev,depsgraph=dg)
    if me0 is None:me0=me.copy()
    frames.extend(round(float(x),6) for v in me.vertices for x in v.co);bpy.data.meshes.remove(me)
   me0.calc_loop_triangles();assert len(frames)==481*len(me0.vertices)*3 and len(me0.vertices)>0
   cloth.append(dict(name=name,vertices=len(me0.vertices),xyz=frames,triangles=[i for tri in me0.loop_triangles for i in tri.vertices]))
   repl=bpy.data.objects.new(name+'_Baked',me0);s.collection.objects.link(repl);selected.append(repl)
  (D/f'{label}_Cloth.json').write_text(json.dumps(dict(fps=30,frames=481,straps=cloth),separators=(',',':')))
 # Conversion probes let the Unity importer derive the exact model coordinate basis.
 for name,loc in [('Basis_X',(1,0,0)),('Basis_Y',(0,1,0)),('Basis_Z',(0,0,1))]:
  ob=bpy.data.objects.new(name,None);s.collection.objects.link(ob);ob.location=loc;selected.append(ob)
 for ob in selected:
  if ob.animation_data and ob.animation_data.action:
   for layer in ob.animation_data.action.layers:
    for strip in layer.strips:
     for cb in strip.channelbags:
      for curve in list(cb.fcurves):
       if curve.data_path in ['hide_viewport','hide_render']:cb.fcurves.remove(curve)
  ob.hide_render=False;ob.hide_viewport=False;ob.hide_set(False)
 s.frame_set(1);bpy.ops.object.select_all(action='DESELECT')
 for ob in selected:ob.select_set(True)
 bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=str(D/f'{label}.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)
 receipt.append(dict(name=label,source=str(src),source_sha256=hashlib.sha256(src.read_bytes()).hexdigest(),bones=len(r.data.bones),file_sha256=hashlib.sha256((D/f'{label}.fbx').read_bytes()).hexdigest(),frames=s.frame_end))
(D/'export-receipt.json').write_text(json.dumps(receipt,indent=2));print('UNITY_EXPORT_COMPLETE')
