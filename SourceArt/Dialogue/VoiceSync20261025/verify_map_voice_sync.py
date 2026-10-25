import json,hashlib,shutil
import xml.etree.ElementTree as ET
from pathlib import Path
from full_game_audio import BASE,ROOT
out=BASE/'output/VoiceSync_BothMaps_20261025'
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
plan=load(out/'plan.json');before=load(out/'catalog-before.json')
current=load(ROOT/'Assets/Resources/Dialogue/voice-catalog.json')
lookup={x['id']:x for x in current['entries']}
unchanged=0
for x in before['entries']:
    now=lookup[x['id']]
    if x['speaker'] not in ['Hùng','Ông Sáu']:
        expected=dict(x)
        if x['id'] in ['m2_moor_order','m2_ashore_order']:
            expected.update(speaker='Anh Thành',caption='Anh Thành: '+x['text'],match='Anh Thành: '+x['text'])
        assert expected==now,x['id']+' metadata unexpectedly changed'
        p=ROOT/'Assets/Resources'/Path(x['resource']+'.wav')
        assert sha(p)==x['sha256'],x['id']+' audio unexpectedly changed'
        unchanged+=1
for x in plan['sources']:assert sha(ROOT/x['path'])==x['sha256'],x['path']
assert (ROOT/'Assets/Resources/Dialogue/voice-catalog.json').read_bytes()==(ROOT/'SourceArt/Dialogue/voice-catalog.json').read_bytes()
for x in current['entries']:
    if x['speaker']=='Hùng':assert x['voice']=='Anh Khôi',x['id']
    if x['speaker']=='Ông Sáu':assert x['voice']=='Thiền Tâm Đức',x['id']
results={}
for name in ['Unity_Verified.xml','Unity_Arrival_Retest.xml']:
    xml=ET.parse(out/name).getroot()
    for x in xml.iter('test-case'):results[x.attrib['fullname']]=dict(name=x.attrib['fullname'],result=x.attrib['result'],duration=x.attrib.get('duration'),evidence=name)
cases=list(results.values())
assert cases and all(x['result']=='Passed' for x in cases),cases
report=load(out/'integration-report.json')
report.update(unityTests='Passed',unityTestCount=len(cases),unityTestCases=cases,unchangedOtherAudioCues=unchanged,arrivalSpeakerCorrections=['m2_moor_order','m2_ashore_order'],storyScriptsUnchanged=True,sourceRuntimeCatalogIdentical=True)
(out/'integration-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
source=ROOT/'SourceArt/Dialogue/VoiceSync20261025'
shutil.copy2(out/'integration-report.json',source/'integration-report.json')
shutil.copy2(out/'Unity_Verified.xml',source/'Unity_Verified.xml')
shutil.copy2(out/'Unity_Arrival_Retest.xml',source/'Unity_Arrival_Retest.xml')
shutil.copy2(Path(__file__),source/Path(__file__).name)
p=out/'BAO_CAO.md';text=p.read_text(encoding='utf-8');text+=f'\n## Kết quả Unity\n\n- Import: 271 cue, unresolved=0.\n- {len(cases)}/{len(cases)} kiểm thử Unity đạt theo kết quả mới nhất của từng ca; lần đầu 14/15, sau đó sửa và kiểm tra lại tên vai ở hai câu cập bến.\n- Phát hết toàn bộ 271 cue ở tốc độ 3× trong cảnh test cô lập; kiểm tra pause, chuyển người nói, kênh bộ đàm, metadata thời lượng, thoại Map 1 và toàn bộ thoại hiện tại Map 2.\n- {unchanged} cue ngoài Hùng/ông Sáu giữ nguyên SHA-256, trong đó có 26 cue địch. Hai cue cập bến chỉ đổi speaker/caption/match từ Chỉ huy sang Anh Thành, không thay âm thanh hay lời nói.\n- Hai nguồn cốt truyện Map 2 không thay đổi; không sửa nhiệm vụ.\n- Fixture FullGameAudioTests dùng ForestSceneTestBase để ngăn công cụ Editor chuyển cảnh kiểm thử về Boot và giữ save test tách biệt.\n- Đây là kiểm thử âm thanh và dữ liệu; không phải một lượt chơi toàn bộ cốt truyện hay duyệt phát âm/diễn xuất bằng tai của mọi câu.\n'
p.write_text(text,encoding='utf-8');shutil.copy2(p,source/'README.md')
print(json.dumps(dict(tests=len(cases),result='Passed',unchangedOtherCues=unchanged,updatedCues=report['updatedCues']),ensure_ascii=True))
