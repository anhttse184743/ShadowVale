import json,hashlib,shutil,csv
import xml.etree.ElementTree as ET
from pathlib import Path
from full_game_audio import BASE,ROOT
out=BASE/'output/Rescue_Clarity_20261025';voice=BASE/'output/Nam_Map2_Reference_20261025'
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
results={}
for name in ['Review_Tests.xml','Final_Tests.xml','Execution_Retest.xml','Retry_Tests.xml']:
    xml=ET.parse(out/name).getroot()
    for x in xml.iter('test-case'):results[x.attrib['fullname']]=dict(name=x.attrib['fullname'],result=x.attrib['result'],source=name)
assert all(x['result']=='Passed' for x in results.values()),results
plan=load(voice/'plan.json')
for x in plan['protected']:assert sha(Path(x['file']))==x['sha256'],x['id']
bank=load(ROOT/'Assets/Resources/Dialogue/voice-catalog.json');byid={x['id']:x for x in bank['entries']}
for x in plan['lines']:
    cue=byid[x['id']];assert cue['voiceRevision']=='Nam_Map2_Matched_20261025' and cue['caption']==x['caption'],x['id']
    assert sha(ROOT/'Assets/Resources'/Path(cue['resource']+'.wav'))==cue['sha256'],x['id']
assert (ROOT/'Assets/Resources/Dialogue/voice-catalog.json').read_bytes()==(ROOT/'SourceArt/Dialogue/voice-catalog.json').read_bytes()
for x in load(BASE/'output/RunwayEnemy_Original_20261025/restoration-report.json')['restored']:assert sha(Path(x['file']))==x['sha256'],x['id']
report=dict(date='2026-10-25',project=str(ROOT),cost=0,testCount=len(results),tests=list(results.values()),namMap1Cues=32,map2VoicesUnchanged=True,originalRunwayEnemyFilesUnchanged=True,commanderBFilesUnchanged=True)
(out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
for p in (ROOT/'Logs/RescueClarity').glob('*'):
    if p.is_file():shutil.copy2(p,out/p.name)
if (out/'commander-seat.png').exists():shutil.copy2(out/'commander-seat.png',out/'commander-seat-test-angle.png')
shutil.copy2(ROOT/'Logs/Extraction/seated-allies.png',out/'commander-seat.png')
for name in ['natural','skip']:
    p=ROOT/'Logs/RescueRadio'/(name+'.txt')
    if p.exists():shutil.copy2(p,out/('radio-'+p.name))
summary=f'''# Cứu Hùng — camera, đi lén, thuyền và giọng Nam

Ngày 25/10/2026. Project `G:\\game\\Capstone\\ShadowVale`, nhánh Khoa.

## Thay đổi

- Intro tiếp cận vẫn kích hoạt khi Nam vào bán kính 40 m sau khi nhận nhiệm vụ. Sau khung hình Nam, cắt ngắn sang Hùng quỳ bị trói và lính canh đang giữ súng, rồi trở về Nam; chọn lens có kiểm tra vật cản và cả hai nhân vật trong khung. Không dịch chuyển nhân vật hoặc bay camera xuyên rừng.
- Kết thúc hoặc bỏ qua intro đều bật đi lén, xóa đà chạy/sprint và hiển thị hướng dẫn phím C. Có 1 giây bảo vệ tiếp cận khi trả điều khiển. Cancel để load save phục hồi tư thế cũ, không áp dụng kết quả của cutscene.
- Snapshot rescue revision 5: save radio-only cũ được xem shot mới một lần khi khu giam giữ còn yên và đủ lính; save đang giao tranh/hộ tống không bị chèn intro. Retry từ checkpoint cũ nâng phiên bản khi chép cờ đã xem, tránh chiếu lại sau mỗi lần làm lại.
- Chỉ huy trên thuyền: hệ thống briefing nhả căn nền hầm sau khi trao actor cho extraction. Tư thế chân được căn vào trong mạn theo xương chân thực tế, giữ skeleton/clip và cao độ ghế.
- 32 cue Nam Map 1 thu lại theo ba đoạn Nam hiện có ở Map 2 (promise_02, promise_07, promise_09). Mẫu là giọng tổng hợp của chính project; không tải audio lên dịch vụ ngoài. Từ trùng nguyên văn dùng lại đúng file Map 2. Cập nhật cả reply trực tiếp trong prefab mở đầu.
- Giữ nguyên giọng Nam Map 2, Hùng A, ông Sáu A, chỉ huy B và 15 file địch Runway gốc; xác nhận bằng SHA-256. 11 câu địch chưa có Runway vẫn chỉ có phụ đề theo yêu cầu trước.

## Kiểm thử

{len(results)}/{len(results)} ca liên quan đạt theo kết quả mới nhất từng ca trong các XML kèm theo. Có camera thật/ảnh Unity, natural/skip/held input, pause, cancel/load, save cũ/retry, preempt phát hiện, ghế/chân chỉ huy, toàn bộ boarding/departure, cutscene thất bại và kiểm tra clip/giọng. Bộ ghi hình cảnh execution được sửa để không đọc pose đã dispose khi director kết thúc giữa Update và callback LateUpdate.

Các ảnh captive-shot.png và commander-seat.png được chụp trong Unity. Đây là kiểm thử các phân đoạn liên quan, không phải tuyên bố toàn bộ game không còn lỗi. Bộ giọng mới vẫn cần người dùng nghe đánh giá sắc thái/phát âm từng câu.

Chi phí: 0; dùng VieNeu offline. Chưa commit/push ở bước này.
'''
(out/'BAO_CAO.md').write_text(summary,encoding='utf-8')
source=ROOT/'SourceArt/Map01RescueClarity20261025';source.mkdir(parents=True,exist_ok=True)
for p in [out/'report.json',out/'BAO_CAO.md',out/'Review_Tests.xml',out/'Final_Tests.xml',out/'Execution_Retest.xml',out/'Retry_Tests.xml',BASE/'publish_rescue_clarity.py',Path(__file__)]:shutil.copy2(p,source/p.name)
for p in (out/'source').glob('*.cs'):shutil.copy2(p,source/p.name)
target=ROOT/'SourceArt/Dialogue/NamMap2Matched20261025'
for p in [BASE/'full_game_audio.py',voice/'Nam_Map1_Matched_Preview.mp3']:shutil.copy2(p,target/p.name)
p=voice/'integration-report.json';d=load(p);d['tests']='Passed';d['evidence']=str(out/'report.json');p.write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8');shutil.copy2(p,target/p.name)
queueFolder=BASE/'output/Voice_Preparation_20261024';p=queueFolder/'voice-queue.json';q=load(p);matchedIds={x['id'] for x in plan['lines']}
q['namMap1VoiceDecision']=dict(date='2026-10-25',request='Match installed Nam Map2 voice',referenceIds=plan['referenceIds'],updatedCues=32,newCost=0)
for x in q['lines']:
    if x['id'] in matchedIds:x.update(status='Đã khớp mẫu Nam Map 2 theo yêu cầu 25/10/2026',voiceReferenceResources=plan['referenceIds'])
p.write_text(json.dumps(q,ensure_ascii=False,indent=2),encoding='utf-8')
for p in queueFolder.glob('*.csv'):
    with p.open(encoding='utf-8-sig',newline='') as f:reader=csv.DictReader(f);fields=reader.fieldnames;rows=list(reader)
    for x in rows:
        if x['id'] in matchedIds:x['status']='Đã khớp mẫu Nam Map 2'
    with p.open('w',encoding='utf-8-sig',newline='') as f:writer=csv.DictWriter(f,fields);writer.writeheader();writer.writerows(rows)
for p in [queueFolder/'voice-queue.json',*queueFolder.glob('*.csv')]:shutil.copy2(p,ROOT/'Docs/Audio/VoicePreparation_20261024'/p.name)
print(json.dumps(dict(tests=len(results),result='Passed',namCues=32,enemyFilesUnchanged=True),ensure_ascii=True))
