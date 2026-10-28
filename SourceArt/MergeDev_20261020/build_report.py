from pathlib import Path
import xml.etree.ElementTree as ET
import subprocess,json
main=Path(r'D:/DO_AN_GAME/ShadowVale');out=main/'Docs/Reviews/MergeDev_20261020'
candidate=Path(r'C:/Users/Admin/.codex/worktrees/merge-dev-thinh-20261020/ShadowVale')
runs=[];latest={}
def run_time(path):
 r=ET.parse(path).getroot();return r.get('end-time') or r.get('start-time') or ''
for p in sorted(out.glob('*.xml'),key=run_time):
 r=ET.parse(p).getroot();runs.append((p.name,r.get('passed'),r.get('failed'),r.get('total')))
 if p.name.startswith(('candidate-','main-')):
  for c in r.iter('test-case'):latest[c.get('fullname')]={'result':c.get('result'),'run':p.name}
failed={k:v for k,v in latest.items() if v['result']!='Passed'}
def git(*args):return subprocess.check_output(['git',*args],cwd=main).decode().strip()
head=git('rev-parse','HEAD')
applied=(out/'apply_result.json').exists()
sources={
 'Giữ biên C3 cùng chiến thuật lùi sau đồng đội':('Assets/_Project/Scripts/AI/Boss/BossRelocation.cs','public static bool TryPick'),
 'Chuyển tiếp body C3 sang giao diện mới':('Assets/_Project/Map01/Runtime/Map01ConfinedBody.cs','public Vector3 Velocity'),
 'Bố trí lại tiếp xúc tay theo mesh súng và tầm với':('Assets/_Project/Map01/Runtime/Map01Extraction.cs','private void EnsureRifle'),
 'Nhả bộ điều khiển vũ khí của Map 1 trước chuyển cảnh':('Assets/_Project/Map01/Runtime/Map01Mission.cs','public void ReleaseForNextMap'),
 'Ghi nhận vũ khí thực sự của viên đạn chờ xử lý':('Assets/_Project/Scripts/Gameplay/Combat/Health.cs','internal void TakeBulletDamageFromWeapon'),
 'Chặn batch editor ghi vào save thật khi fixture thoát':('Assets/_Project/Map01/Runtime/ForestSaveSlots.cs','if (Application.isBatchMode)'),
}
lines=['# Kết quả ghép dev vào thinh — 20/10/2026','',
 'Dev đã kiểm: `a1e2b9a9b0e71b42316fd9ecf9174c279c6304d1` (remote đã kiểm lại cùng SHA).',
 'Thinh trước ghép: `e44dab1e4be14f14d3524dc731486508b8677242`. HEAD tại lần kiểm tra này: `'+head+'`.','',
 ('Mã ghép đã áp dụng trên dự án chính.' if applied else 'Mã ghép đang kiểm tra trong worktree riêng; chưa áp dụng vào dự án chính.'),
 'Không push. Việc commit tuân theo lựa chọn của người dùng ở cuối lượt ghép.','',
 '## Nội dung đã chốt','',
 '- Lấy AI và cân bằng chiến đấu mới của dev; giữ hàng rào C3, lực lượng hiện có và mức gọi quân 3 người.',
 '- Nhận scene dev; 2.583 block của cụm nhà C3 đã duyệt được đối chiếu không đổi.',
 '- Giữ K54 thường, Mosin/PU/nạp từng viên, cơ chế bắn xa không tự lộ, dao cạnh giường, ngủ/thức, E nhận lệnh và việc đang làm với vật ném.',
 '- Bảo toàn các thay đổi chưa commit bằng bản sao SHA256 trước ghép. Xem `Temp/MergeDev_20261020_Before/manifest.json`.','',
 '## Code chính','']
for label,(rel,marker) in sources.items():
 p=(main if applied else candidate)/rel
 if not p.exists():continue
 matches=[i for i,l in enumerate(p.read_text(encoding='utf-8-sig').splitlines(),1) if marker in l]
 if matches:lines.append(f'- {label}: [{Path(rel).name}:{matches[0]}]({(main/rel).as_posix()}:{matches[0]}).')
lines+=['','## Kiểm thử thật','',f'Kết quả mới nhất của **{len(latest)} ca riêng biệt** trên bản ghép/dự án chính: **{len(latest)-len(failed)} đạt, {len(failed)} chưa đạt**. Các lượt lặp không cộng trùng ca.','',
 '| File kết quả | Đạt | Lỗi | Tổng |','|---|---:|---:|---:|']
for name,passed,fail,total in runs:lines.append(f'| [{name}]({(out/name).as_posix()}) | {passed} | {fail} | {total} |')
lines+=['','Các lượt đỏ ban đầu vẫn được giữ để kiểm toán; xem `PHAT_HIEN_VA_CACH_SUA.md` về nguyên nhân và ca kiểm lại tương ứng. `test_results.json` chứa tên từng test và stack lỗi gốc.','']
if failed:
 lines+=['### Ca còn lỗi','']+[f'- `{n}` — {v["run"]}' for n,v in failed.items()]+['']
lines+=['## Save, hình ảnh và giới hạn','',
 '- Hash ô thủ công 0/1 khớp snapshot. Ô tự động bị một đường thoát test ghi nhầm đã được khôi phục; cả ba file lưu tiếp tục khớp sau 5 ca kiểm tra trên dự án chính (`save_verification.json`).',
 '- Ảnh đo tiếp xúc tay: `grip-before.png`, `grip-after.png`; không dựng lại mesh/rig hay diễn xuất ngón tay trong lượt ghép.',
 '- Chưa benchmark 1080p/60 FPS hay kiểm thử bản player phát hành. Kết quả trên là kiểm thử chức năng trong Unity Editor.',
 '- Các cửa sổ Blender đang mở không bị điều khiển. Các file recovery, scene test sinh lại và ảnh/video cũ được giữ ngoài phần stage phát hành.','']
(out/'KET_QUA.md').write_text('\n'.join(lines),encoding='utf-8',newline='\n')
(out/'latest_case_results.json').write_text(json.dumps({'unique':len(latest),'passed':len(latest)-len(failed),'failed':failed,'cases':latest},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'applied':applied,'unique':len(latest),'passed':len(latest)-len(failed),'failed':list(failed)},ensure_ascii=False))
