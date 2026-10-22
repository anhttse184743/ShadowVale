"""Compare the authored flow tuning to this task's backup; no game assets are modified."""
from pathlib import Path
import json

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
LOOT = Path('Assets/_Project/Map01/Resources/Loot/Map01Loot.json')
old = json.loads((OUT/'Before'/LOOT).read_text(encoding='utf-8-sig'))
new = json.loads((ROOT/LOOT).read_text(encoding='utf-8-sig'))

def stats(data, code):
    table = next(t for t in data['lootTables'] if t['code'] == code)
    rolls = (table['rollsMin'] + table['rollsMax'])/2
    result = {}
    for item in {e['item'] for e in table['entries']}:
        per_roll, mean = 0, 0
        for e in table['entries']:
            if e['item'] != item: continue
            quantity = (e['min'] + e['max'])/2
            if e['tier'] == 5:
                mean += quantity
            else:
                pool = [v for v in table['entries'] if v['tier'] == e['tier']]
                p = data['tierOdds'][e['tier']-1]/sum(data['tierOdds']) * e['weight']/sum(v['weight'] for v in pool)
                mean += rolls*p*quantity; per_roll += p
        chance = sum(1-(1-per_roll)**n for n in range(table['rollsMin'],table['rollsMax']+1))/(table['rollsMax']-table['rollsMin']+1)
        result[item] = dict(mean_quantity=mean, chance_in_random_rolls=chance)
    return result

result = {code:dict(before=stats(old,code),after=stats(new,code)) for code in ['lt_m1_patrol','lt_m1_camp_guard','lt_m1_operation_guard']}
tuning = json.loads((ROOT/'Assets/_Project/Map01/Map01Balance.json').read_text(encoding='utf-8-sig'))
npc = tuning['guardM16']; cadence = .1
shot_damage = 24 * tuning['guardDamageScale'] * 22 / 30
period = (npc['magazineRounds']//npc['burstRounds'])*(npc['burstRounds']-1)*cadence + (npc['magazineRounds']//npc['burstRounds']-1)*(npc['burstPause']+npc['staggerSeconds']/2)+npc['reloadSeconds']
result['guard_m16'] = dict(shot_damage=shot_damage, old_theoretical_dps=shot_damage/cadence,
    new_theoretical_magazine_cycle_dps=npc['magazineRounds']*shot_damage/period,
    thirty_round_cycle_seconds=period, tuning=npc,
    note='Timing model at authored RPM, average stagger and every shot hitting; excludes frame quantization, group contention and AI hit probability. Player M16 unchanged.')
result['guaranteed_sources'] = {t['code']:{e['item']:e['min'] for e in t['entries'] if e['tier']==5}
    for t in new['lootTables'] if t['code'] in ['lt_m1_base_start','lt_m1_chest_c1','lt_m1_chest_c2','lt_c3_field_approach','lt_c3_field_medic','lt_m1_supply_reward']}
result['store_prices'] = {i['code']:i['baseValue'] for i in new['items'] if i['code'] in ['medkit_small','smoke_bomb','grenade_f1']}
(OUT/'balance-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'medkit_pack_before':result['lt_m1_operation_guard']['before']['medkit_small'],
    'medkit_pack_after':result['lt_m1_operation_guard']['after']['medkit_small'],
    'weapon_parts_mean_before':result['lt_m1_operation_guard']['before']['weapon_parts']['mean_quantity'],
    'weapon_parts_mean_after':result['lt_m1_operation_guard']['after']['weapon_parts']['mean_quantity'],
    'guard_m16':result['guard_m16']},ensure_ascii=False))
