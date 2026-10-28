import json,shutil,csv
from pathlib import Path
from full_game_audio import BASE,ROOT,ENEMY_ROLES
out=BASE/'output/RunwayEnemy_Original_20261025'
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def dump(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
removed=[]
for rel,key in [('Assets/Resources/Dialogue/voice-catalog.json','entries'),('SourceArt/Dialogue/voice-catalog.json','entries'),('SourceArt/Dialogue/FullGameFree20261023/integrated-manifest.json','lines')]:
    p=ROOT/rel;d=load(p)
    bad=[x for x in d[key] if x.get('speaker') in ENEMY_ROLES and x.get('provider')!='Runway']
    if not removed:removed=bad
    d[key]=[x for x in d[key] if x not in bad]
    if 'cues' in d:d['cues']=len(d[key])
    dump(p,d)
ids={x['id'] for x in removed};assert len(ids)==11
dump(out/'subtitle-only-enemy-cues.json',dict(reason='User requested original Runway enemies only; no offline substitutes',entries=removed))
source=ROOT/'SourceArt/Dialogue/RunwayEnemyRestored20261025'
shutil.copy2(out/'subtitle-only-enemy-cues.json',source/'subtitle-only-enemy-cues.json')
p=out/'restoration-report.json';report=load(p)
report.update(activeRecordedCues=260,subtitleOnlyEnemyCues=sorted(ids),remainingEnemyPolicy='Only 15 original Runway Martin recordings; 11 later enemy lines retain subtitles with no audio recording assigned')
dump(p,report);shutil.copy2(p,source/p.name)
qfolder=BASE/'output/Voice_Preparation_20261024';p=qfolder/'voice-queue.json';q=load(p)
q['enemyVoiceDecision'].update(missingOriginalAction='subtitle_only_no_offline_voice')
for x in q['lines']:
    if x['id'] in ids:x.update(voice='',resource='',regenerate=False,status='Chỉ phụ đề: chưa có bản Runway gốc; không dùng giọng offline')
dump(p,q)
for p in qfolder.glob('*.csv'):
    with p.open(encoding='utf-8-sig',newline='') as f:
        reader=csv.DictReader(f);fields=reader.fieldnames;rows=list(reader)
    for x in rows:
        if x['id'] in ids:x.update(voice='',resource='',status='Chỉ phụ đề: chưa có bản Runway gốc; không dùng giọng offline')
        elif any(r['id']==x['id'] for r in report['restored']):x.update(voice='Martin',status='Đã phục hồi Runway gốc')
    with p.open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.DictWriter(f,fields);writer.writeheader();writer.writerows(rows)
for p in [qfolder/'voice-queue.json',*qfolder.glob('*.csv')]:shutil.copy2(p,ROOT/'Docs/Audio/VoicePreparation_20261024'/p.name)
for p in [BASE/'full_game_audio.py',BASE/'install_full_audio.py',Path(__file__)]:shutil.copy2(p,source/p.name)
p=ROOT/'Assets/_Project/Map01/Tests/FullGameAudioTests.cs';s=p.read_text(encoding='utf-8-sig').replace('id,resource,caption,speaker,voice;','id,resource,caption,speaker,voice,provider;').replace('Assert.GreaterOrEqual(bank.Length,269)','Assert.GreaterOrEqual(bank.Length,260)')
marker='        [Test] public void EntireCurrentMapTwoScriptHasExactVoicesIncludingFinale()'
assert marker in s
new='''        [Test] public void EnemyVoicesUseOnlyOriginalRunwayRecordings()
        {
            var roles=new[]{"Lính địch","Lính","Giám sát","Lính phản ứng","Sĩ quan địch","Lính gác cổng","Lính tuần tra","Chỉ huy địch","Địch"};
            var enemies=Read().Where(e=>roles.Contains(e.speaker)).ToArray();Assert.AreEqual(15,enemies.Length);
            foreach(var e in enemies){Assert.AreEqual("Runway",e.provider,e.id);Assert.AreEqual("Martin",e.voice,e.id);}
        }
'''
if 'void EnemyVoicesUseOnlyOriginalRunwayRecordings' not in s:s=s.replace(marker,new+marker)
p.write_text(s,encoding='utf-8')
for p in [BASE/'output/VoiceSync_BothMaps_20261025/casting-policy.json',ROOT/'SourceArt/Dialogue/VoiceSync20261025/casting-policy.json']:
    d=load(p);d['enemyPolicy']=report['remainingEnemyPolicy'];dump(p,d)
text=(out/'BANG_DOI_CHIEU.md').read_text(encoding='utf-8').replace('Các file hiện tại của các câu này chưa thay; không giả nhận là giọng Runway.','Các câu này giữ phụ đề, đã bỏ gán giọng offline khỏi catalog. Không dùng sai câu Runway để thay thế.')
(out/'BANG_DOI_CHIEU.md').write_text(text,encoding='utf-8');shutil.copy2(out/'BANG_DOI_CHIEU.md',source/'BANG_DOI_CHIEU.md')
print('15 original Runway enemy cues active; 11 later enemy cues subtitle-only; 245 allied/other cues unchanged.')
