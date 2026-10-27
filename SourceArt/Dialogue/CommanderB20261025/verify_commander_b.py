import json,hashlib,shutil,re
import xml.etree.ElementTree as ET
from pathlib import Path
import soundfile as sf
from full_game_audio import BASE,ROOT
out=BASE/'output/Commander_B_Integration_20261025'
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def dump(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
plan=load(out/'plan.json');bank=load(ROOT/'Assets/Resources/Dialogue/voice-catalog.json');lookup={x['id']:x for x in bank['entries']}
for x in plan['protected']:assert sha(Path(x['file']))==x['sha256'],x['id']
for x in load(out/'catalog-before.json')['entries']:
    assert lookup[x['id']]['text']==x['text'] and lookup[x['id']]['caption']==x['caption'],x['id']
assert (ROOT/'Assets/Resources/Dialogue/voice-catalog.json').read_bytes()==(ROOT/'SourceArt/Dialogue/voice-catalog.json').read_bytes()
chiefs=[x for x in bank['entries'] if x['speaker'] in ['Chỉ huy','Anh Thành']]
assert len(chiefs)==49 and all(x['voice']=='Minh Đức' and x['castingApproved'] for x in chiefs)
enemies=[x for x in bank['entries'] if x.get('provider')=='Runway'];assert len(enemies)==15 and all(x['voice']=='Martin' and x['userApproved'] for x in enemies)
for x in load(BASE/'output/RunwayEnemy_Original_20261025/restoration-report.json')['restored']:assert sha(Path(x['file']))==x['sha256'],x['id']
root=ET.parse(out/'Unity_CommanderB.xml').getroot();assert root.attrib['result']=='Passed',root.attrib
cases=[dict(name=x.attrib['fullname'],result=x.attrib['result']) for x in root.iter('test-case')]
assert all(x['result']=='Passed' for x in cases)
report=load(out/'integration-report.json');report.update(tests='Passed',testCount=len(cases),testCases=cases,protectedClipReferences=len(plan['protected']),unchangedText=True)
dump(out/'integration-report.json',report)
summary=f'''# Chốt giọng địch và chỉ huy B — 25/10/2026

Project `G:\\game\\Capstone\\ShadowVale`, nhánh Khoa.

- Người dùng duyệt bản địch Runway gốc và chỉ huy B.
- Địch: giữ đúng 15 file Martin/Runway gốc; hash trước/sau không thay đổi. 11 câu mới chưa có Runway gốc giữ phụ đề, không gán giọng offline.
- Chỉ huy B: Minh Đức, tempo 0,92, pitch 0,94. Cập nhật 49 cue: 27 Chỉ huy và 22 Anh Thành, áp dụng xuyên Map 1/Map 2, cả dữ liệu nhánh cũ còn trong catalog.
- Giữ nguyên nguyên văn thoại, ID, resource và kênh bộ đàm. Cập nhật WAV, clean master, metadata thời lượng và SHA-256.
- Clip mở đầu Map 1 được dựng lại bằng ba câu chỉ huy mới. Mốc phụ đề: {report['openingSubtitleStarts']} giây. Giọng Nam đáp giữ nguyên.
- Tất cả giọng khác, gồm Hùng A và ông Sáu A, được giữ nguyên qua hash.
- {len(cases)}/{len(cases)} kiểm thử Unity đạt: nhập/độ dài toàn bộ 260 cue có audio, giọng địch chỉ dùng Runway gốc, giọng các vai, script Map 2, thoại Map 1 và clean/bộ đàm, prefab mở đầu, đủ Ngày 1/Đêm 1. Có lượt phát đến hết cả 49 cue chỉ huy ở tốc độ 3× trong cảnh test cô lập.
- Chi phí tạo mới: 0. Dùng VieNeu offline, không gọi Runway hay dịch vụ trả phí.

Kiểm thử kỹ thuật không thay cho nghe duyệt phát âm và diễn xuất từng câu. Mẫu giọng B đã được duyệt; các câu mới được tích hợp theo bộ giọng đó.

`before/` lưu bản sao trước lần tích hợp. Chưa commit/push trong bước này.
'''
(out/'BAO_CAO.md').write_text(summary,encoding='utf-8')
source=ROOT/'SourceArt/Dialogue/CommanderB20261025'
for p in [out/'integration-report.json',out/'Unity_CommanderB.xml',out/'BAO_CAO.md',Path(__file__)]:shutil.copy2(p,source/p.name)
for p in [BASE/'output/VoiceSync_BothMaps_20261025/casting-policy.json',ROOT/'SourceArt/Dialogue/VoiceSync20261025/casting-policy.json']:
    d=load(p)
    for role in ['Chỉ huy','Anh Thành']:d['voices'][role]=dict(voice='Minh Đức',sample='Chỉ huy B',castingApproved=True,tempo=.92,pitch=.94,approvalDate='2026-10-25')
    d['enemyCastingApproved']=True;dump(p,d)
print(json.dumps(dict(commanderCues=49,originalEnemies=15,tests=len(cases),result='Passed',cost=0),ensure_ascii=True))
