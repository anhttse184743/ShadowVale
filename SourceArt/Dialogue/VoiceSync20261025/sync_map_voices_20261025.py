import os,json,hashlib,subprocess,shutil,sys
from pathlib import Path
from collections import Counter
import numpy as np
import soundfile as sf
from full_game_audio import BASE,ROOT,FF,casting

OUT=BASE/'output/VoiceSync_BothMaps_20261025'
OUT.mkdir(parents=True,exist_ok=True)
ENEMY={'Lính địch','Lính','Giám sát','Lính phản ứng','Sĩ quan địch','Lính gác cổng','Lính tuần tra','Chỉ huy địch','Địch'}
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def dump(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def resource(r):
    candidates=[ROOT/'Assets/Resources'/Path(r+ext) for ext in ['.wav','.ogg','.mp3']]
    candidates=[p for p in candidates if p.exists()]
    if not candidates:candidates=[p for p in (ROOT/'Assets').glob('**/Resources/'+r+'.*') if p.suffix.lower() in ['.wav','.ogg','.mp3']]
    assert len(candidates)==1,(r,[str(p) for p in candidates])
    return candidates[0]
def backup(p):
    target=OUT/'before'/p.relative_to(ROOT)
    if not target.exists():target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
def audit():
    bank=load(ROOT/'Assets/Resources/Dialogue/voice-catalog.json');lookup={x['id']:x for x in bank['entries']}
    errors=[];sources=[]
    for rel in ['Assets/_Project/Map01/Resources/Story/Map02Chapter.json','Assets/_Project/Map01/Resources/Dialogue/Map02Story/script.json']:
        d=load(ROOT/rel);sources.append(dict(path=rel,sha256=sha(ROOT/rel)))
        for line in d['entries']:
            if not line.get('text','').strip():continue
            cue=lookup.get(line.get('recording') or line['id'])
            caption=line['speaker']+(' (bộ đàm): ' if line.get('radio') else ': ')+line['text']
            if not cue or cue['caption']!=caption or bool(cue.get('radio'))!=bool(line.get('radio')):errors.append(dict(id=line['id'],caption=caption,current=cue))
    rows=[];preserved=[]
    for x in bank['entries']:
        p=resource(x['resource'])
        if x['speaker'] in ENEMY:
            preserved.append(dict(id=x['id'],resource=x['resource'],file=str(p),sha256=sha(p),voice=x['voice']))
        desired,brief,tempo,pitch=casting(x['speaker'])
        if x['speaker'] in ['Hùng','Ông Sáu']:
            rows.append(dict(x,voice=desired,performance=brief,tempo=tempo,pitch=pitch,oldSha256=sha(p)))
        elif x['speaker'] not in ENEMY and x['voice']!=desired:
            errors.append(dict(id=x['id'],reason='Unexpected voice mismatch',current=x['voice'],desired=desired))
    assert not errors,errors
    dump(OUT/'catalog-before.json',bank)
    dump(OUT/'plan.json',dict(date='2026-10-25',cost=0,dev=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),scope='Same approved Hung A and Sau A across both maps; all enemy clips retained byte-for-byte',sources=sources,lines=rows,preservedEnemies=preserved))
    print(json.dumps(dict(catalog=len(bank['entries']),recast=Counter(x['speaker'] for x in rows),preservedEnemyCues=len(preserved)),ensure_ascii=True),flush=True)
def render():
    os.environ.update(HF_HOME=str(BASE/'work/vieneu-cache'),HF_HUB_OFFLINE='1',TRANSFORMERS_OFFLINE='1')
    from vieneu import Vieneu
    tts=Vieneu(backend='onnx',precision='fp32',threads=2)
    plan=load(OUT/'plan.json');rows=[]
    selected=load(BASE/'output/Voice_Recast_Hung_Sau_20261024/manifest.json')['samples']
    for i,x in enumerate(plan['lines']):
        key=hashlib.sha256(json.dumps([x['text'],x['voice'],x['tempo'],x['pitch']],ensure_ascii=False).encode()).hexdigest()
        raw=OUT/(key+'-raw.wav');clean=OUT/(key+'-clean.wav');radio=OUT/(key+'-radio.wav')
        if not raw.exists():
            sample=next((s for s in selected if s['approved'] and s['voice']==x['voice'] and s['text']==x['text']),None)
            if sample:shutil.copy2(Path(sample['file']).with_name(sample['name']+'_raw.wav'),raw)
            else:
                a=np.asarray(tts.infer(x['text'],voice=x['voice'],temperature=.65),dtype=np.float32)
                assert len(a) and np.isfinite(a).all(),x['id']
                a*=min(.8/max(abs(a)),.105/max(float(np.sqrt(np.mean(a*a))),1e-8))
                sf.write(raw,a,48000,subtype='PCM_24')
        if not clean.exists():
            subprocess.run([str(FF),'-v','error','-y','-i',str(raw),'-af',f"rubberband=tempo={x['tempo']}:pitch={x['pitch']},highpass=f=70,lowpass=f=7500,alimiter=limit=0.9:level=0",'-c:a','pcm_s16le',str(clean)],check=True)
        actual=clean
        if x.get('radio'):
            if not radio.exists():subprocess.run([str(FF),'-v','error','-y','-i',str(clean),'-af','highpass=f=320,lowpass=f=3200','-c:a','pcm_s16le',str(radio)],check=True)
            actual=radio
        a,sr=sf.read(actual)
        assert sr==48000 and np.isfinite(a).all() and max(abs(a))<.99 and np.sqrt(np.mean(a*a))>.002,x['id']
        rows.append(dict(x,file=str(actual),cleanFile=str(clean),duration=len(a)/sr,sha256=sha(actual),peak=float(max(abs(a)))))
        dump(OUT/'rendered.json',dict(cost=0,lines=rows))
        print(f"{i+1}/{len(plan['lines'])} {x['id']} {len(a)/sr:.2f}s",flush=True)
def install():
    plan=load(OUT/'plan.json');rows=load(OUT/'rendered.json')['lines']
    assert len(rows)==len(plan['lines'])
    bank=load(ROOT/'Assets/Resources/Dialogue/voice-catalog.json');mapping={x['id']:x for x in bank['entries']}
    assert bank==load(OUT/'catalog-before.json'),'Catalog changed during voice rendering; audit again before installing'
    for x in plan['preservedEnemies']:assert sha(Path(x['file']))==x['sha256'],x['id']
    changes=[]
    for x in rows:
        cue=mapping[x['id']];p=resource(cue['resource']);backup(p);shutil.copy2(x['file'],p);changes.append(str(p.relative_to(ROOT)))
        cleanResource=cue.get('cleanResource')
        if cleanResource and cleanResource!=cue['resource']:
            clean=resource(cleanResource);backup(clean);shutil.copy2(x['cleanFile'],clean);changes.append(str(clean.relative_to(ROOT)))
        cue.update(voice=x['voice'],duration=x['duration'],sha256=x['sha256'],sampleRate=48000,channels=1,cost=0,castingApproved=True,castingApproval='User chose Hùng A / ông Sáu A on 2026-10-24',userApproved=False,status='integrated_approved_cast_pending_line_review',voiceRevision='20261025')
    # The invitation is also exported separately for the finale flashback.
    invitation=next(x for x in rows if x['id']=='m2v3_promise_05')
    p=ROOT/'Assets/Resources/Map02DayOne/hung_invitation.wav';backup(p);shutil.copy2(invitation['cleanFile'],p);changes.append(str(p.relative_to(ROOT)))
    sources=ROOT/'SourceArt/Dialogue/FullGameFree20261023/sfx-sources.json';backup(sources);sourceRows=load(sources)
    for item in sourceRows:
        if item['name']=='hung_invitation':item.update(source='VieNeu-TTS, Anh Khôi preset; user approved Hung A',sha256=sha(p))
    dump(sources,sourceRows)
    for rel in ['Assets/Resources/Dialogue/voice-catalog.json','SourceArt/Dialogue/voice-catalog.json']:
        p=ROOT/rel;backup(p);dump(p,bank);changes.append(rel)
    measured=[]
    for x in bank['entries']:
        p=resource(x['resource']);a,sr=sf.read(p)
        assert sr==48000 and np.isfinite(a).all() and max(abs(a))<.9999 and np.sqrt(np.mean(a*a))>.002,x['id']
        assert abs(len(a)/sr-x['duration'])<.04,x['id']
        measured.append(dict(x,sha256=sha(p),duration=len(a)/sr,peak=float(max(abs(a))),rms=float(np.sqrt(np.mean(a*a)))))
    p=ROOT/'SourceArt/Dialogue/FullGameFree20261023/integrated-manifest.json';backup(p)
    dump(p,dict(cost=0,date='2026-10-25',dev=plan['dev'],cues=len(measured),lines=measured))
    # Keep test casting expectations aligned with the user's chosen identities.
    for rel in ['Assets/_Project/Map01/Tests/FullGameAudioTests.cs','Assets/_Project/Map01/Tests/Map01StoryVoiceSupplementTests.cs']:
        p=ROOT/rel;backup(p);s=p.read_text(encoding='utf-8-sig').replace('Thanh Bình','Anh Khôi').replace('Thái Sơn','Thiền Tâm Đức');p.write_text(s,encoding='utf-8');changes.append(rel)
    for x in plan['preservedEnemies']:assert sha(Path(x['file']))==x['sha256'],x['id']
    report=dict(cost=0,totalCues=len(measured),updatedCues=len(rows),casting=Counter(x['speaker'] for x in rows),enemyCuesUnchanged=len(plan['preservedEnemies']),changedFiles=changes,unityTests='pending')
    dump(OUT/'integration-report.json',report)
    source=ROOT/'SourceArt/Dialogue/VoiceSync20261025';source.mkdir(parents=True,exist_ok=True)
    for name in ['plan.json','rendered.json','integration-report.json']:shutil.copy2(OUT/name,source/name)
    shutil.copy2(Path(__file__),source/Path(__file__).name)
    shutil.copy2(BASE/'full_game_audio.py',source/'full_game_audio.py')
    print(json.dumps(report,ensure_ascii=True),flush=True)
if __name__=='__main__':{'audit':audit,'render':render,'install':install}[sys.argv[1]]()
