"""Export the approved cot and ration store from the latest Blender source."""
from pathlib import Path
HERE=Path(__file__).resolve().parent
template=HERE.parent/'CommandBunkerPreview/export_unity.py'
code=template.read_text(encoding='utf-8')
code=code.replace("ROOT=Path(__file__).resolve().parents[2]",f"ROOT=Path({str(HERE.parents[1])!r})")
source=HERE/'CotsSuppliesPreview/BeddingRevision2/FieldCots_ArmyBedding_Proposal.blend'
code=code.replace("str(Path(__file__).with_name('CommandBunker_Proposal.blend'))",repr(str(source)))
a=code.index("chair=bpy.data.objects[");b=code.index("result={'models':[]}")
code=code[:a]+"""groups=[]
for name,key in [('FieldCot','FieldCot row 3'),('RationStores','Rations and ammunition on a low pallet')]:
    root=bpy.data.objects[key]
    groups.append((name,list(root.children_recursive),root.matrix_world.inverted()))
"""+code[b:]
code=code.replace("'ApprovedBunker.meshdata.json'","'ApprovedCotsSupplies.meshdata.json'")
exec(compile(code,str(template),'exec'))
