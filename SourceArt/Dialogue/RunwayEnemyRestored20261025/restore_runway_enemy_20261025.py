import json,shutil,hashlib,subprocess
from pathlib import Path
import numpy as np
import soundfile as sf
from full_game_audio import BASE,ROOT,FF
OUT=BASE/'output/RunwayEnemy_Original_20261025';OUT.mkdir(parents=True,exist_ok=True)
OLD=BASE/'work/full-game-audio-20261023/before-install'
roles={'Lính địch','Lính','Giám sát','Lính phản ứng','Sĩ quan địch','Lính gác cổng','Lính tuần tra','Chỉ huy địch','Địch'}
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def dump(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def backup(p):
    dest=OUT/'before'/p.relative_to(ROOT)
    if not dest.exists():dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
old={x['id']:x for x in load(OLD/'voice-catalog.json')['entries'] if x.get('speaker') in roles}
bank=load(ROOT/'Assets/Resources/Dialogue/voice-catalog.json');rows=[];missing=[];protected=[]
for x in bank['entries']:
    if x['speaker'] not in roles:
        p=ROOT/'Assets/Resources'/Path(x['resource']+'.wav');protected.append(dict(id=x['id'],file=str(p),sha256=sha(p)))
for x in bank['entries']:
    if x['speaker'] not in roles:continue
    if x['id'] not in old:missing.append(dict(id=x['id'],caption=x['caption'],status='No original Runway file found in pre-recast catalog; existing file untouched'));continue
    original=old[x['id']]
    assert original['text']==x['text'] and bool(original.get('radio'))==bool(x.get('radio')),x['id']
    p=OLD/'Assets/Resources'/Path(original['resource']+'.wav');assert p.is_file(),p
    if original.get('sha256'):assert sha(p)==original['sha256'],x['id']
    a,sr=sf.read(p);assert sr==48000 and np.isfinite(a).all() and max(abs(a))<1
    target=ROOT/'Assets/Resources'/Path(x['resource']+'.wav');backup(target);shutil.copy2(p,target)
    if x.get('cleanResource') and x['cleanResource']!=x['resource']:
        clean=OLD/'Assets/Resources'/Path(original['cleanResource']+'.wav');assert clean.is_file(),clean
        dest=ROOT/'Assets/Resources'/Path(x['cleanResource']+'.wav');backup(dest);shutil.copy2(clean,dest)
    restored=dict(original,provider='Runway',model='eleven_v3',voice='Martin',duration=len(a)/sr,sha256=sha(p),sampleRate=sr,channels=1,status='restored_original_Runway_recording_20261025',restorationNote='User requested original enemy voices; no synthesis or paid call performed')
    # Keep original recording approval metadata; no new approval is fabricated.
    x.clear();x.update(restored)
    rows.append(dict(id=x['id'],caption=x['caption'],voice=x['voice'],file=str(target),sha256=sha(target),duration=x['duration']))
for rel in ['Assets/Resources/Dialogue/voice-catalog.json','SourceArt/Dialogue/voice-catalog.json']:
    p=ROOT/rel;backup(p);dump(p,bank)
manifest=[]
for x in bank['entries']:
    p=ROOT/'Assets/Resources'/Path(x['resource']+'.wav');a,sr=sf.read(p)
    manifest.append(dict(x,sha256=sha(p),duration=len(a)/sr,peak=float(max(abs(a))),rms=float(np.sqrt(np.mean(a*a)))))
p=ROOT/'SourceArt/Dialogue/FullGameFree20261023/integrated-manifest.json';backup(p);dump(p,dict(date='2026-10-25',note='Mixed existing Runway enemy audio and free allied character voices; no new paid calls',cues=len(manifest),lines=manifest))
for x in protected:assert sha(Path(x['file']))==x['sha256'],x['id']
cursor=0;index=['# Giọng địch Runway gốc — Martin','15 file đã phục hồi đúng byte từ bản sao trước khi đổi sang giọng offline. Không tạo audio mới, không trả phí.','']
with sf.SoundFile(OUT/'Dich_Runway_Goc_15Cau.wav','w',samplerate=48000,channels=1,subtype='PCM_16') as dest:
    for n,x in enumerate(rows,1):
        a,sr=sf.read(x['file']);x['start']=round(cursor,3);dest.write(a);cursor+=len(a)/sr;dest.write(np.zeros(33600));cursor+=.7
        index.append(f"{n}. {int(x['start']//60):02}:{int(x['start']%60):02} — {x['caption']} (`{x['id']}`)")
for name,src in [('Dich_Runway_Goc_15Cau',OUT/'Dich_Runway_Goc_15Cau.wav'),('Linh_Runway_Goc',Path(rows[0]['file'])),('SiQuan_Runway_Goc',Path(next(x['file'] for x in rows if x['id']=='m1_c3_enemy_call')))]:
    subprocess.run([str(FF),'-v','error','-y','-i',str(src),'-c:a','libmp3lame','-b:a','192k',str(OUT/(name+'.mp3'))],check=True)
report=dict(date='2026-10-25',newCost=0,restored=rows,remainingWithoutOriginal=missing,alliedCuesUnchanged=len(protected),totalReviewDuration=cursor)
dump(OUT/'restoration-report.json',report)
index+=['','## 11 câu chưa có file Runway gốc trong bản sao','Các file hiện tại của các câu này chưa thay; không giả nhận là giọng Runway.']+[f"- {x['caption']} (`{x['id']}`)" for x in missing]
(OUT/'BANG_DOI_CHIEU.md').write_text('\n\n'.join(index),encoding='utf-8')
source=ROOT/'SourceArt/Dialogue/RunwayEnemyRestored20261025';source.mkdir(parents=True,exist_ok=True)
for p in [OUT/'restoration-report.json',OUT/'BANG_DOI_CHIEU.md',Path(__file__)]:shutil.copy2(p,source/p.name)
p=BASE/'output/Voice_Preparation_20261024/voice-queue.json';q=load(p)
q['enemyVoiceDecision']=dict(action='restore_original_Runway_Martin',newPaidGeneration=False,restoredIds=[x['id'] for x in rows],missingOriginalIds=[x['id'] for x in missing],date='2026-10-25')
for x in q['lines']:
    if x['id'] in old:x.update(voice='Martin',preserveCurrentGameAudio=True,regenerate=False,status='Đã phục hồi file Runway gốc, không gen mới')
dump(p,q);shutil.copy2(p,ROOT/'Docs/Audio/VoicePreparation_20261024/voice-queue.json')
print(json.dumps(dict(restored=len(rows),missing=len(missing),alliesUnchanged=len(protected),duration=cursor),ensure_ascii=True))
