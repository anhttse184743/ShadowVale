from pathlib import Path
s=Path('SourceArt/TamEscortPreview/build_preview.py').read_text();exec(s[:s.index('nam.location.x=')])
for r in [nam,tam]:
 print(r.name,[(n,tuple(round(v,3) for v in pos(r,n))) for n in ['Hips','LeftUpLeg','LeftLeg','LeftFoot','LeftToeBase','LeftArm','LeftForeArm','LeftHand'] if any(p.name.split(':')[-1]==n for p in r.pose.bones)])
before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/_Project/Art/Environment/Map01Items/Meshes/Item_pistol_k54.fbx'))
for o in set(bpy.data.objects)-before:
 if o.type=='MESH':print('GUN',o.name,tuple(o.dimensions),[tuple(round(v,3) for v in o.matrix_world@Vector(c)) for c in o.bound_box][:2])
print('TAM_BONES',[p.name for p in tam.pose.bones if 'Hand' in p.name or 'Finger' in p.name])
