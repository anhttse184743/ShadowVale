"""Inspect empty-hand curl on the actual skinned Nam model."""
import ast
from pathlib import Path
builder=Path(__file__).with_name('build_mixamo_preview.py')
tree=ast.parse(builder.read_text())
stop=next(i for i,n in enumerate(tree.body) if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='validation' for t in n.targets))
exec(compile(ast.Module(body=tree.body[:stop],type_ignores=[]),str(builder),'exec'),globals())
pose_mixamo(.25,'unarmed')
camera()
cam=scene.camera
target=(pos('LeftHand')+pos('RightHand'))*.5
cam.location=target+Vector((-1,-1,1))
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.ortho_scale=.7
scene.render.filepath=str(OUT/'fists-closeup.png')
bpy.ops.render.render(write_still=True)
print('FIST_PREVIEW_COMPLETE',flush=True)
