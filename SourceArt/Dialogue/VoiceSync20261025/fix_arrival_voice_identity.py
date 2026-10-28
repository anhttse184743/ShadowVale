import json,shutil,csv
from full_game_audio import BASE,ROOT
out=BASE/'output/VoiceSync_BothMaps_20261025'
ids={'m2_moor_order','m2_ashore_order'}
for rel,key in [('Assets/Resources/Dialogue/voice-catalog.json','entries'),('SourceArt/Dialogue/voice-catalog.json','entries'),('SourceArt/Dialogue/FullGameFree20261023/integrated-manifest.json','lines')]:
    p=ROOT/rel;d=json.loads(p.read_text(encoding='utf-8-sig'))
    for x in d[key]:
        if x['id'] in ids:
            x['speaker']='Anh Thành';x['caption']='Anh Thành: '+x['text'];x['match']=x['caption']
    p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
folder=BASE/'output/Voice_Preparation_20261024'
p=folder/'voice-queue.json';d=json.loads(p.read_text(encoding='utf-8'))
for x in d['lines']:
    if x['id'] in ids:x['speaker']='Anh Thành'
p.write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8')
for p in folder.glob('*.csv'):
    with p.open(encoding='utf-8-sig',newline='') as f:
        reader=csv.DictReader(f);fields=reader.fieldnames;rows=list(reader)
    for x in rows:
        if x['id'] in ids:x['speaker']='Anh Thành'
    with p.open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.DictWriter(f,fields);writer.writeheader();writer.writerows(rows)
for p in [folder/'voice-queue.json',*folder.glob('*.csv')]:shutil.copy2(p,ROOT/'Docs/Audio/VoicePreparation_20261024'/p.name)
shutil.copy2(__file__,ROOT/'SourceArt/Dialogue/VoiceSync20261025/fix_arrival_voice_identity.py')
print('Corrected only speaker/caption/match for 2 arrival cues; no WAV changed.')
