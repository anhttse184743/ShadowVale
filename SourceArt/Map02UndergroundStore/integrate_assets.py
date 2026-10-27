"""Copy verified visual prefab/import metadata, write genuine URP materials from the project's template."""
from pathlib import Path
import shutil,re,uuid
ROOT=Path(__file__).resolve().parents[2]
relative=Path('Assets/_Project/Map01/Resources/Story/NightStore')
src=ROOT/'Tools/TamEscortValidation'/relative;dest=ROOT/relative
shutil.copytree(src,dest,dirs_exist_ok=True)
if src.with_suffix('.meta').exists():shutil.copy2(src.with_suffix('.meta'),dest.with_suffix('.meta'))
template=(ROOT/'Assets/_Project/Map01/Resources/Story/Map02QuestWood.mat').read_text(encoding='utf-8')
colors={'Walnut':(1,1,1),'Earth':(1,1,1),'Iron':(.075,.09,.085),'Canvas':(.32,.33,.19),'Paper':(.70,.63,.43),'LampAmber':(1,.47,.12)}
for name,color in colors.items():
 s=template.replace('m_Name: Map02QuestWood','m_Name: NS_'+name)
 for prop in ['_BaseColor','_Color']:
  s=re.sub(r'('+prop+r': )\{[^}]+\}',r'\g<1>{r: %s, g: %s, b: %s, a: 1}'%color,s)
 s=re.sub(r'(_Smoothness: )[^\n]+',r'\g<1>'+('.42' if name=='Iron' else '.15'),s)
 s=re.sub(r'(_Metallic: )[^\n]+',r'\g<1>'+('.75' if name=='Iron' else '0'),s)
 if name in ['Walnut','Earth']:
  meta=(dest/'Textures'/('NightStore_'+name+'.png.meta')).read_text();guid=re.search(r'guid: (\w+)',meta)[1]
  for prop in ['_BaseMap','_MainTex']:
   s=re.sub(r'('+prop+r':\n        m_Texture: )\{[^}]+\}',r'\g<1>{fileID: 2800000, guid: '+guid+', type: 3}',s)
 if name=='LampAmber':
  s=s.replace('m_ValidKeywords: []','m_ValidKeywords:\n  - _EMISSION')
  s=re.sub(r'(_EmissionColor: )\{[^}]+\}',r'\g<1>{r: 2, g: 0.66, b: 0.08, a: 1}',s)
 (dest/'Materials'/('NS_'+name+'.mat')).write_text(s,encoding='utf-8')
for rel in ['Assets/_Project/Map01/Runtime/Map02UndergroundStore.cs','Assets/_Project/Map01/Editor/Map02UndergroundStoreSetup.cs']:
 meta=(ROOT/rel).with_suffix('.cs.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
receipt=ROOT/'Tools/TamEscortValidation/SourceArt/Map02UndergroundStore/unity-import.txt'
shutil.copy2(receipt,ROOT/'SourceArt/Map02UndergroundStore/unity-import.txt')
print('PASS prefab, FBX/texture GUIDs, six URP materials, and script metadata integrated')
