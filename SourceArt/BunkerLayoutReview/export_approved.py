"""Export only approved meeting furniture and resized shelf; no review room geometry."""
from pathlib import Path
HERE=Path(__file__).resolve().parent
base=(HERE.parent/'CommandBunkerPreview/export_unity.py').read_text(encoding='utf-8')
base=base.replace("Path(__file__).with_name('CommandBunker_Proposal.blend')","Path(__file__).parent/'Revision2/BunkerLayout_Revision2.blend'")
start=base.index("chair=bpy.data.objects[")
end=base.index("result={'models':[]}",start)
base=base[:start]+'''meeting=list(bpy.data.collections['06 Meeting table - NEW PROPOSAL'].all_objects)
benches=[o for o in meeting if o.name.startswith(('Long bench','Bench broad','Bench underseat'))]
table=[o for o in meeting if o not in benches and not o.name.startswith(('Olive pith helmet','Pith helmet brim'))]
shelf=bpy.data.objects['02 Quartermaster shelf']
groups=[('MeetingTableV2',table,Matrix.Translation((-.65,.75,0))),
 ('MeetingBenchesV2',benches,Matrix.Translation((-.65,.75,0))),
 ('EquipmentShelfHeadSizedFinal',[o for o in shelf.children if not o.name.startswith('COLLIDER_')],shelf.matrix_world.inverted())]
''' + base[end:]
base=base.replace("ApprovedBunker.meshdata.json","ApprovedFurnitureLayout.meshdata.json")
exec(compile(base,str(HERE/'export_approved.py'),'exec'))
