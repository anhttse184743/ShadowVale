"""Export reviewed script without generating or charging for speech."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
source = ROOT / 'Assets/_Project/Map01/Runtime/Map01OperationLines.cs'
lines = []
for speaker, text, radio, cue in re.findall(r'new L\("([^"]+)","([^"]+)",(true|false),"([^"]+)"\)', source.read_text(encoding='utf-8-sig')):
    lines.append(dict(id=cue, speaker=speaker, text=text, radio=radio == 'true'))
for cue, speaker, text in [
    ('m1_ops_radio_alarm', 'Lính phản ứng', 'Báo động ở trạm đài! Tới kiểm tra ngay!'),
    ('m1_ops_enemy_spotted', 'Lính địch', 'Có người ở mé rừng! Bên kia!'),
    ('m1_ops_enemy_order', 'Sĩ quan địch', 'Tản ra! Chặn đường hắn!'),
]:
    lines.append(dict(id=cue, speaker=speaker, text=text, radio=False))
for line in lines:
    line.update(voiceStatus='pending_native_vietnamese_audition', delivery='remote_light_radio' if line['radio'] else 'direct_clean')
    line['caption']=line['speaker']+(' (bộ đàm): ' if line['radio'] else ': ')+line['text']
    line['cleanResource']='Dialogue/VI/'+line['id']+'_clean'
    line['resource']='Dialogue/VI/'+line['id']+('_radio' if line['radio'] else '_clean')
manifest = dict(scriptApproved=True, newVoiceApproved=False, paidGenerationAuthorized=False, creditsSpent=0, lines=lines)
folder = Path(__file__).parent
existing_path = folder / 'voice-manifest.json'
if existing_path.exists():
    existing = json.loads(existing_path.read_text(encoding='utf-8-sig'))
    if existing.get('paidGenerationAuthorized'):
        assert {e['id']:e['text'] for e in existing['lines']} == {e['id']:e['text'] for e in lines}, 'Script changed: review before replacing installed speech'
        manifest = existing
(folder / 'voice-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
rows = ['# Map 1 — Thoại ba điểm nhiệm vụ', '', 'Kịch bản đã duyệt; trạng thái thu âm và ngân sách xem voice-manifest.json. Nam nói tại chỗ dùng bản sạch; chỉ huy từ đầu đàm mới lọc bộ đàm. Không phát đồng thời hai bản.', '']
for line in lines:
    channel = ' · bộ đàm' if line['radio'] else ''
    rows.extend([f"**{line['speaker']}{channel}** (`{line['id']}`)", '', line['text'], ''])
(folder / 'SCRIPT.md').write_text('\n'.join(rows), encoding='utf-8')
print(f'{len(lines)} cues exported; no speech generated.')
