"""Read prop surface values from the approved source without changing it."""
import bpy, json
from pathlib import Path
names=('K54_Player','Nam_Dao_KaBar','DEMO_F1','DEMO_Stone_10cm','DEMO_Smoke')
materials={}
for name in names:
    root=bpy.data.objects[name]
    for obj in [root,*root.children_recursive]:
        if obj.type!='MESH':continue
        for mat in obj.data.materials:
            if mat is None or mat.name in materials:continue
            base=list(mat.diffuse_color);metal=mat.metallic;rough=mat.roughness
            if mat.use_nodes:
                node=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
                if node:
                    base=list(node.inputs['Base Color'].default_value)
                    metal=node.inputs['Metallic'].default_value
                    rough=node.inputs['Roughness'].default_value
            materials[mat.name]={'color':base,'viewportColor':list(mat.diffuse_color),'metallic':metal,'roughness':rough,'nodes':[
                {'type':n.type,'name':n.name,'colors':{i.name:list(i.default_value) for i in n.inputs if i.type=='RGBA' and not i.is_linked}}
                for n in mat.node_tree.nodes] if mat.node_tree else []}
(Path(__file__).resolve().parent/'UnityExport/materials.json').write_text(json.dumps(materials,indent=2),encoding='utf-8')
print('READ_MATERIALS',len(materials),flush=True)
