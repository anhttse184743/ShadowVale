"""Reproduce the balance calculations and C3 proposal drawing. Does not modify game assets."""
import csv
import json
import math
from pathlib import Path
from collections import defaultdict
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
loot = json.loads((ROOT / 'Assets/_Project/Map01/Resources/Loot/Map01Loot.json').read_text(encoding='utf-8-sig'))
layout = json.loads((ROOT / 'Assets/_Project/Map01/Resources/Missions/Map01C3Layout.json').read_text(encoding='utf-8-sig'))
runtime = json.loads((OUT / 'current-runtime-layout.json').read_text(encoding='utf-8-sig'))
content_path = Path('Assets/_Project/Map01/Generated/Map01Content.json')
before = json.loads((OUT / 'Before' / content_path).read_text(encoding='utf-8-sig'))
after = json.loads((ROOT / content_path).read_text(encoding='utf-8-sig'))
expected = json.loads(json.dumps(before))
next(w for w in expected['weapons'] if w['id'] == 'rifle_m16').update(damage=22, fire_rate=10, range=80)
assert expected == after, 'Only M16 damage/fire_rate/range may change in this task content delta.'

def table_statistics(code, boost=None):
    table = next(t for t in loot['lootTables'] if t['code'] == code)
    entries = table['entries']
    rolls = (table['rollsMin'] + table['rollsMax']) / 2
    mean, probability = defaultdict(float), defaultdict(float)
    for entry in entries:
        if entry['tier'] == 5:
            mean[entry['item']] += (entry['min'] + entry['max']) / 2
    for tier, chance in enumerate(loot['tierOdds'], 1):
        pool = [e for e in entries if e['tier'] == tier]
        assert pool, 'These audited tables use every tier; no fallback needed.'
        total = sum(e['weight'] * (boost or {}).get(e['item'], 1) for e in pool)
        for entry in pool:
            p = chance / sum(loot['tierOdds']) * entry['weight'] * (boost or {}).get(entry['item'], 1) / total
            probability[entry['item']] += p
            mean[entry['item']] += rolls * p * (entry['min'] + entry['max']) / 2
    return {'mean_quantities': dict(mean), 'per_roll_probability': dict(probability),
            'rolls_min': table['rollsMin'], 'rolls_max': table['rollsMax'],
            'notes': 'Before adaptive boosts and medkit pity; non-pistol ammo in guard packs is converted to held rifle caliber.'}

statistics = {t: table_statistics(t) for t in ['lt_m1_operation_guard', 'lt_m1_patrol', 'lt_m1_chest_c3']}
operation = statistics['lt_m1_operation_guard']
rounds = sum(q for item, q in operation['mean_quantities'].items() if item.startswith('ammo_') and item != 'ammo_pistol')
operation['actual_mean_primary_rounds_after_caliber_matching'] = rounds
med_p = operation['per_roll_probability']['medkit_small']
operation['chance_at_least_one_medkit_without_adaptive_or_pity'] = 1 - (1 - med_p) ** 2
statistics['m16'] = {'old_runtime_damage': 27, 'new_runtime_damage': 22,
                     'old_runtime_rpm': 700, 'new_runtime_rpm': 600,
                     'old_runtime_range_m': 120, 'new_runtime_range_m': 80,
                     'old_dps': 27 * 700 / 60, 'new_dps': 22 * 600 / 60,
                     'dps_reduction_fraction': 1 - 220 / 315,
                     'body_hits_100hp_before': math.ceil(100 / 27), 'body_hits_100hp_after': math.ceil(100 / 22),
                     'perfect_ttk_100hp_before_seconds': (math.ceil(100 / 27) - 1) * 60 / 700,
                     'perfect_ttk_100hp_after_seconds': (math.ceil(100 / 22) - 1) * 60 / 600}
statistics['budget_normal_assumption'] = {'guard_hp': 100, 'm16_hit_rate': .45,
    'expected_rounds_per_guard': 5 / .45, 'old_mean_drop_per_operation_guard': rounds,
    'proposed_c3_drop_mean': 6, 'expected_net_rounds_per_kill_after_proposal': 6 - 5 / .45,
    'notes': 'Planning model, not measured player accuracy. Commander assumed killed by one Mosin hit.'}
(OUT / 'loot-and-balance-audit.json').write_text(json.dumps(statistics, ensure_ascii=False, indent=2), encoding='utf-8')

# Candidate coordinates refer to world x/z. Final placement needs walkability, interaction and patrol QA.
points = [
 dict(id='P1', x=106.8, z=22.7, phase='Chuẩn bị', name='Ổ Mosin hiện có', chance=1,
      contents='1 Mosin + 5 đạn nhắm', condition='Từ lúc nhận nhiệm vụ; một lần; giữ vị trí hiện tại'),
 dict(id='P2', x=43, z=32, phase='Chuẩn bị', name='Túi trinh sát đường Tây', chance=1,
      contents='1 bom khói + 8 đạn K54', condition='Một lần; có dấu vải dẫn đường; vị trí mới cần kiểm tra'),
 dict(id='P3', x=57.5, z=60, phase='Xâm nhập', name='Túi y tế ngoài cửa nhà Tây', chance=1,
      contents='1 băng cứu thương (hồi 45 HP)', condition='Tiếp cận được mà không giết lính; tránh vùng diễn hoạt trên giường'),
 dict(id='P4', x=75.8, z=54, phase='Xâm nhập', name='Hòm đạn sân tiếp tế', chance=1,
      contents='15 viên đúng súng chính (M16 hoặc AK)', condition='Khóa loại đạn vào save khi tạo; không tạo cả hai loại'),
 dict(id='P5', x=91, z=63, phase='Nhánh tùy chọn', name='Một cụm đồ ở sườn Đông', chance=.6,
      contents='Chọn đúng 1: 2 đạn Mosin (45%) / 1 khói (35%) / 1 sửa súng (20%)', condition='60% cụm tồn tại; không bắt buộc cho mục tiêu; seed ổn định'),
 dict(id='P6', x=62, z=72, phase='Cứu nguy', name='Hốc cứu nguy cạnh tuyến điện thoại', chance=1,
      contents='Tối đa 1 gói: 1 băng HOẶC 15 viên đúng súng chính', condition='Hốc có từ đầu; chốt và lưu đúng một lần khi mở: HP < 40 và không có băng ưu tiên băng, nếu không thì đạn khi đạn súng trường trong băng + dự trữ < 15; khỏe và đủ đạn thì 1 vải; không quay lại đổi phần thưởng'),
 dict(id='P7', x=71, z=59.05, phase='Sau mục tiêu', name='Hòm C3 hiện có', chance=1,
      contents='1 sửa súng + 2 linh kiện + 3 đạn Mosin; 2 lượt vật liệu', condition='Chỉ huy chết mới mở; hòm không cấp đồ chuẩn bị; giữ +60 hậu cần hiện có, không cộng trùng'),
 dict(id='P8', x=52, z=82, phase='Rút lui', name='Túi khói đường rút', chance=1,
      contents='1 bom khói', condition='Mở từ khi chỉ huy chết; không hồi sinh; tránh tuyến tiếp viện thực tế'),
]
(OUT / 'c3-proposal.json').write_text(json.dumps({'status': 'DESIGN_PROPOSAL_NOT_APPLIED',
    'coordinate_status': 'Candidate world x/z; P1/P7 measured existing, all new points require Unity placement QA.',
    'scope': 'C3 only. Existing terrain, mission objectives, boss AI and other missions unchanged.',
    'points': points}, ensure_ascii=False, indent=2), encoding='utf-8')
with (OUT / 'c3-proposal.csv').open('w', encoding='utf-8-sig', newline='') as f:
    writer = csv.DictWriter(f, fieldnames=points[0].keys()); writer.writeheader(); writer.writerows(points)

im = Image.new('RGB', (2100,1550), '#f4f1e9'); draw = ImageDraw.Draw(im)
def font(size, bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/' + ('arialbd.ttf' if bold else 'arial.ttf'), size)
def label(x,y,text,size=20,color='#343e38',bold=False,anchor=None):
    draw.text((x,y),text,font=font(size,bold),fill=color,anchor=anchor)
scale=14
def xy(x,z): return (100+(x-35)*scale, 1230-(z-15)*scale)
def route(flat, color, width=2):
    if len(flat)>=4: draw.line([xy(x,z) for x,z in zip(flat[::2],flat[1::2])], fill=color, width=width)
def marker(x,z,color,r=9):
    px,py=xy(x,z); draw.ellipse((px-r,py-r,px+r,py+r),fill=color,outline='white',width=2)
for x in range(35,117,5):
    px,_=xy(x,15); draw.line((px,208,px,1230),fill='#dcdacf'); label(px,1245,str(x),16,anchor='mt')
for z in range(15,89,5):
    _,py=xy(35,z); draw.line((100,py,1234,py),fill='#dcdacf'); label(84,py,str(z),16,anchor='rm')
label(1010,1280,'World X (m)',19); label(100,168,'Bắc ↑ / World Z (m)',19)
mn, mx = layout['fenceMin'], layout['fenceMax']
draw.rectangle((*xy(mn[0],mx[1]), *xy(mx[0],mn[1])),fill='#e3dec9',outline='#766d54',width=4)
label(*xy(68,71.8),'HÀNG RÀO C3',17,'#766d54',anchor='mm')
for role in layout['roles']:
    route(role['points'],'#ba8382',2)
    x,z = role['post']; px,py=xy(x,z); draw.polygon([(px,py-10),(px-9,py+7),(px+9,py+7)],fill='#9b4845')
    name = {'tower':'Tháp 30 m','gate':'Cổng','rover':'Tuần sân','porch':'Hiên','patrolSouth':'Tuần Nam','patrolNorth':'Tuần Tây'}[role['role']]
    offsets = {'tower': (1,-2.5),'gate':(2,-2.5),'rover':(-1,2.5),'porch':(-1,2.5),'patrolSouth':(0,-3),'patrolNorth':(-2,3)}
    dx,dz=offsets[role['role']]; label(*xy(x+dx,z+dz),name,16,'#8c4542',anchor='mm')
marker(68,64.6,'#9b4845',12); label(*xy(68,64.6),'B',17,'white',True,'mm'); label(*xy(69.4,66.4),'Chỉ huy',18,'#8c4542')
for s in layout['sleepers']:
    px,py=xy(*s['inside']); draw.rectangle((px-5,py-5,px+5,py+5),fill='#9b4845')
label(*xy(38,83),'2 tiếp viện',18,'#8c4542')
label(*xy(65.3,44.2),'×',29,anchor='mm'); label(*xy(68.5,75.8),'×',29,anchor='mm')
label(*xy(71,77),'Điện thoại',18); label(*xy(61,43),'Kẻng',18)
for p in runtime['points']:
    if p['kind']=='Stones' and 35<p['position']['x']<116 and 15<p['position']['z']<88:
        marker(p['position']['x'],p['position']['z'],'#99978c',4)
colors={'Chuẩn bị':'#34766a','Xâm nhập':'#d08324','Nhánh tùy chọn':'#9f6db2','Cứu nguy':'#ca7660','Sau mục tiêu':'#3c6f9b','Rút lui':'#3c6f9b'}
for p in points:
    marker(p['x'],p['z'],colors[p['phase']],22); label(*xy(p['x'],p['z']),p['id'],19,'white',True,'mm')
label(100,55,'C3 · Đồ tiếp tế theo đường đi và mức rủi ro',42,'#252d2b',True)
label(100,118,'PHƯƠNG ÁN CHƯA ÁP DỤNG · vị trí mới là ứng viên, chưa đo đường đi và tầm nhìn',23,'#805d30')
legend=[('P1 · Chuẩn bị xa trại','Mosin + 5 viên · giữ ổ hiện có'),('P2 · Chuẩn bị đường Tây','1 khói + 8 đạn K54'),
('P3 · Băng y tế','1 băng / 45 HP · cạnh nhà Tây'),('P4 · Đạn trong sân','15 viên đúng súng chính'),
('P5 · Nhánh Đông tùy chọn','60% có cụm · tối đa 1 phần thưởng'),('P6 · Cứu nguy có điều kiện','1 gói / lượt chơi · băng hoặc đạn'),
('P7 · Hòm sau mục tiêu','Sửa súng, linh kiện, 3 đạn nhắm'),('P8 · Hỗ trợ rút lui','1 khói · mở sau khi hạ chỉ huy')]
for i,(title,detail) in enumerate(legend):
    y=212+i*132
    label(1330,y,title,25,colors[points[i]['phase']],True)
    label(1330,y+43,detail,22)
label(100,1340,'▲ Lính gác   B Chỉ huy   ▪ Lính ngủ   × Kẻng / điện thoại   · Đá nhặt hiện có',23,'#5b5b50')
label(100,1400,'Đường đỏ: tuyến tuần tra trong dữ liệu. Đây không phải bản đồ vùng phát hiện và không thể hiện hình dáng nhà.',23,'#5b5b50')
label(100,1460,'Nguồn: Map01C3Layout.json + snapshot Unity. Sơ đồ từ trên xuống; không phải ảnh gameplay.',23,'#5b5b50')
im.save(OUT / 'c3-proposal-layout.png')
print(json.dumps({'mean_operation_guard_primary_rounds':rounds, 'medkit_chance_per_pack':operation['chance_at_least_one_medkit_without_adaptive_or_pity'],
    'm16':statistics['m16'], 'proposal_points':len(points), 'content_delta_verified':True}, ensure_ascii=False))
