"""Record the latest result of each Unity regression case and the approved asset hashes."""
from pathlib import Path
import hashlib,json,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
work=root/'Docs/Reviews/AnimationRefresh_Unity_20261022'
cases={};runs=[]
for name in ('regression-tests.xml','final-tests.xml','rig-tests.xml'):
    run=ET.parse(work/name).getroot()
    runs.append({'file':name,**{k:run.attrib[k] for k in ('total','passed','failed')}})
    for case in run.iter('test-case'):
        cases[case.attrib['fullname']]={'name':case.attrib['fullname'],'result':case.attrib['result'],'run':name}
assert len(cases)==22 and all(c['result']=='Passed' for c in cases.values()),cases
def sha(path):return hashlib.sha256((root/path).read_bytes()).hexdigest()
source='SourceArt/AnimationRefresh_20261022/Nam_AnimationRefresh_2026-10-22_v3.blend'
assert sha(source)=='77727ffeab59bf989dfd9fdd568fbe859ce35541ae6df2a92e6341a32c59ac2c'
paths=[source,'Assets/_Project/Map01/Resources/Characters/NamMotionV3.asset','Assets/_Project/Art/NamMotionV3/Nam_AnimationRefresh_v3.fbx',
       *['Assets/_Project/Map01/Runtime/'+f+'.cs' for f in ('NamMotionData','NamMotionRefresh','Map01Pistol','Map01Mosin','Map01NamActions','Map01Mission')],
       'Assets/_Project/Scripts/Gameplay/Player/PlayerController.cs','Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller']
receipt={'approvedOn':'2026-10-22','approval':'oke thêm vào game cho tôi','unity':'6000.3.24f1',
         'scope':'Nam / Map 1; incompatible Hùng skeleton keeps its own motions; Map 2 mission not wired in this change',
         'clips':47,'frames':3208,'fps':60,'bones':62,'props':5,
         'latestDistinctTests':len(cases),'passed':len(cases),'failed':0,'runs':runs,
         'cases':[cases[n] for n in sorted(cases)],'sha256':{p:sha(p) for p in paths},
         'liveUnityStills':sorted(p.name for p in work.glob('[0-9][0-9]-*.png')),
         'committed':False,'pushed':False}
(work/'integration_receipt.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8')
print('NAM_UNITY_INTEGRATION_VERIFIED',len(cases),'tests',len(receipt['liveUnityStills']),'live stills')
