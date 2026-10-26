"""Portable, compressed copy of the approved scene; never saves the original."""
import bpy,json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];P=Path(__file__).resolve().parent
source=Path(bpy.data.filepath);before=hashlib.sha256(source.read_bytes()).hexdigest()
keep={'Player_Rig','Hung_Rig','Nam_Than','Player_LeftHand','Player_RightHand','Hung_Than','BaLo','Bank','Tread1','Tread2','Tread3',bpy.context.scene.camera.name}
for ob in list(bpy.data.objects):
    if ob.name not in keep and ob.type!='LIGHT':bpy.data.objects.remove(ob,do_unlink=True)
actions={ob.animation_data.action for ob in bpy.data.objects if ob.animation_data and ob.animation_data.action}
for a in list(bpy.data.actions):
    if a not in actions:bpy.data.actions.remove(a)
bpy.data.orphans_purge(do_local_ids=True,do_linked_ids=False,do_recursive=True)
target=P/'C1_Approved_v4.blend';bpy.ops.wm.save_as_mainfile(filepath=str(target),compress=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==before
receipt={'approved_original_sha256':before,'archive':target.name,'archive_sha256':hashlib.sha256(target.read_bytes()).hexdigest(),'bytes':target.stat().st_size,'note':'Compressed portable scene. Removed unused other cast/actions only; active Nam/Hung meshes, rigs, motion, pack, review camera and steps retained. Original file untouched.'}
(P/'source-receipt.json').write_text(json.dumps(receipt,indent=2),encoding='utf-8');print('C1_SOURCE_ARCHIVED',json.dumps(receipt),flush=True)
