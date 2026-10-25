import os,json,hashlib,subprocess,shutil,re,sys,csv
from pathlib import Path
from collections import Counter
import numpy as np
import soundfile as sf
from full_game_audio import BASE,ROOT,FF
OUT=BASE/'output/Commander_B_Integration_20261025';OUT.mkdir(parents=True,exist_ok=True)
ROLES={'Chỉ huy','Anh Thành'}
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def dump(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def res(r):return ROOT/'Assets/Resources'/Path(r+'.wav')
def backup(p):
    target=OUT/'before'/p.relative_to(ROOT)
    if not target.exists():target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
def audit():
    bank=load(ROOT/'Assets/Resources/Dialogue/voice-catalog.json')
    protected=[]
    for x in bank['entries']:
        if x['speaker'] not in ROLES:
            for r in {x['resource'],x.get('cleanResource',x['resource'])}:
                p=res(r);assert p.exists(),p;protected.append(dict(id=x['id'],file=str(p),sha256=sha(p)))
    dump(OUT/'catalog-before.json',bank)
    rows=[x for x in bank['entries'] if x['speaker'] in ROLES]
    dump(OUT/'plan.json',dict(date='2026-10-25',cost=0,approvedVoice='Minh Đức',approvedSample='ChiHuy_B',tempo=.92,pitch=.94,lines=rows,protected=protected))
    p=BASE/'output/Commander_Elder_Auditions_20261025/manifest.json';d=load(p)
    d['status']='User approved commander sample B on 2026-10-25'
    for x in d['samples']:x['approved']=x['name']=='ChiHuy_B'
    dump(p,d)
    print(json.dumps(dict(cues=len(rows),roles=Counter(x['speaker'] for x in rows)),ensure_ascii=True),flush=True)
def render():
    os.environ.update(HF_HOME=str(BASE/'work/vieneu-cache'),HF_HUB_OFFLINE='1',TRANSFORMERS_OFFLINE='1')
    from vieneu import Vieneu
    tts=Vieneu(backend='onnx',precision='fp32',threads=2)
    plan=load(OUT/'plan.json');done=[]
    for i,x in enumerate(plan['lines'],1):
        key=hashlib.sha256(x['text'].encode()).hexdigest();raw=OUT/(key+'-raw.wav');clean=OUT/(key+'-clean.wav');radio=OUT/(key+'-radio.wav')
        if not raw.exists():
            a=np.asarray(tts.infer(x['text'],voice='Minh Đức',temperature=.65),dtype=np.float32)
            assert len(a) and np.isfinite(a).all(),x['id']
            a*=min(.8/max(abs(a)),.105/max(float(np.sqrt(np.mean(a*a))),1e-8));sf.write(raw,a,48000,subtype='PCM_24')
        if not clean.exists():subprocess.run([str(FF),'-v','error','-y','-i',str(raw),'-af','rubberband=tempo=0.92:pitch=0.94,highpass=f=65,lowpass=f=7200,alimiter=limit=0.9:level=0','-c:a','pcm_s16le',str(clean)],check=True)
        actual=clean
        if x.get('radio'):
            if not radio.exists():subprocess.run([str(FF),'-v','error','-y','-i',str(clean),'-af','highpass=f=320,lowpass=f=3200','-c:a','pcm_s16le',str(radio)],check=True)
            actual=radio
        a,sr=sf.read(actual);assert sr==48000 and np.isfinite(a).all() and max(abs(a))<.99 and np.sqrt(np.mean(a*a))>.002,x['id']
        done.append(dict(x,voice='Minh Đức',file=str(actual),cleanFile=str(clean),duration=len(a)/sr,sha256=sha(actual),tempo=.92,pitch=.94))
        dump(OUT/'rendered.json',dict(cost=0,lines=done));print(f'{i}/{len(plan["lines"])} {x["id"]}',flush=True)
def install():
    plan=load(OUT/'plan.json');done=load(OUT/'rendered.json')['lines'];assert len(done)==len(plan['lines'])
    bank=load(ROOT/'Assets/Resources/Dialogue/voice-catalog.json');assert bank==load(OUT/'catalog-before.json'),'Catalog changed during rendering'
    lookup={x['id']:x for x in bank['entries']}
    for x in plan['protected']:assert sha(Path(x['file']))==x['sha256'],x['id']
    for x in done:
        cue=lookup[x['id']];target=res(cue['resource']);backup(target);shutil.copy2(x['file'],target)
        if cue.get('cleanResource') and cue['cleanResource']!=cue['resource']:
            target=res(cue['cleanResource']);backup(target);shutil.copy2(x['cleanFile'],target)
        cue.update(voice='Minh Đức',duration=x['duration'],sha256=x['sha256'],provider='VieNeu offline',model='v3 Turbo',cost=0,castingApproved=True,castingApproval='Commander B approved by user 2026-10-25',userApproved=False,status='integrated_approved_cast_pending_line_review',voiceRevision='CommanderB_20261025')
        cue.pop('approvalDate',None)
    enemies=[x for x in bank['entries'] if x.get('provider')=='Runway']
    assert len(enemies)==15
    for x in enemies:x.update(userApproved=True,approvalDate='2026-10-25',status='original_Runway_enemy_reconfirmed_by_user')
    # Opening prefab uses one direct concatenated clip rather than catalog playback.
    code=(ROOT/'Assets/_Project/Map01/Runtime/Map01OpeningCutscene.cs').read_text(encoding='utf-8-sig')
    captions=list(dict.fromkeys(re.findall(r'"(Chỉ huy: [^"\n]+)"',code)))
    assert len(captions)==3,captions
    starts=[];parts=[];offset=0
    for cap in captions:
        x=next(x for x in done if x['caption']==cap);a,sr=sf.read(x['cleanFile']);starts.append(offset/48000);parts.extend([a,np.zeros(12000)]);offset+=len(a)+12000
    prefab=ROOT/'Assets/_Project/Map01/Resources/Cutscenes/Map01Opening.prefab';s=prefab.read_text(encoding='utf-8-sig');guid=re.search(r'commanderVoice: \{fileID: 8300000, guid: (\w+)',s).group(1)
    metas=[p for p in (ROOT/'Assets').rglob('m1_open_commander.wav.meta') if 'guid: '+guid in p.read_text(encoding='utf-8-sig')];assert len(metas)==1
    p=Path(str(metas[0])[:-5]);backup(p);sf.write(p,np.concatenate(parts),48000,subtype='PCM_16')
    backup(prefab);s,n=re.subn(r'commanderSubtitleStarts:\n(?:  - [^\n]+\n){3}','commanderSubtitleStarts:\n'+''.join('  - '+str(t)+'\n' for t in starts),s);assert n==1
    prefab.write_text(s,encoding='utf-8',newline='\n')
    for rel in ['Assets/Resources/Dialogue/voice-catalog.json','SourceArt/Dialogue/voice-catalog.json']:
        p=ROOT/rel;backup(p);dump(p,bank)
    measured=[]
    for x in bank['entries']:
        p=res(x['resource']);a,sr=sf.read(p);assert sr==48000 and abs(len(a)/sr-x['duration'])<.04,x['id']
        measured.append(dict(x,sha256=sha(p),duration=len(a)/sr,peak=float(max(abs(a))),rms=float(np.sqrt(np.mean(a*a)))))
    p=ROOT/'SourceArt/Dialogue/FullGameFree20261023/integrated-manifest.json';backup(p);dump(p,dict(date='2026-10-25',cues=len(measured),note='Original Runway enemies; free allied voices; approved commander B',lines=measured))
    for rel in ['Assets/_Project/Map01/Tests/Map01StoryVoiceSupplementTests.cs']:
        p=ROOT/rel;backup(p);p.write_text(p.read_text(encoding='utf-8-sig').replace('Minh Triết','Minh Đức'),encoding='utf-8')
    p=ROOT/'Assets/_Project/Map01/Tests/FullGameAudioTests.cs';backup(p);s=p.read_text(encoding='utf-8-sig')
    marker='            foreach(var e in bank.Where(e=>e.speaker=="Hùng"))'
    if 'e.speaker=="Chỉ huy"||e.speaker=="Anh Thành"' not in s:
        assert marker in s;s=s.replace(marker,'            foreach(var e in bank.Where(e=>e.speaker=="Chỉ huy"||e.speaker=="Anh Thành"))Assert.AreEqual("Minh Đức",e.voice,e.id);\n'+marker)
    marker='        [UnityTest,Timeout(600000)] public IEnumerator EveryCatalogCueCompletesPlaybackAndPauseStopsTheClock()'
    if 'ApprovedCommanderRecordingsCompletePlayback' not in s:
        assert marker in s
        method='''        [UnityTest,Timeout(240000)] public IEnumerator ApprovedCommanderRecordingsCompletePlayback()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            var host=new GameObject("Approved commander playback audit");host.AddComponent<AudioListener>();var voice=DialogueVoice.For(host);voice.CanSpeak=()=>true;var source=host.GetComponent<AudioSource>();source.pitch=3;
            var lines=Read().Where(e=>e.speaker=="Chỉ huy"||e.speaker=="Anh Thành").ToArray();Assert.AreEqual(49,lines.Length);
            foreach(var line in lines)
            {
                voice.Play(line.id);Assert.AreEqual(line.id,voice.CurrentId,line.id);float end=Time.realtimeSinceStartup+source.clip.length/3+3;
                while(voice.HasSpeech&&Time.realtimeSinceStartup<end)yield return null;
                Assert.False(voice.HasSpeech,"Stalled commander cue: "+line.id);
            }
            voice.Stop();UnityEngine.Object.Destroy(host);yield return new ExitPlayMode();
        }
'''
        s=s.replace(marker,method+marker)
    p.write_text(s,encoding='utf-8')
    for x in plan['protected']:assert sha(Path(x['file']))==x['sha256'],x['id']
    report=dict(date='2026-10-25',cost=0,updatedCommanderCues=len(done),casting=dict(Counter(x['speaker'] for x in done)),originalRunwayEnemies=15,enemyFilesChanged=False,openingSubtitleStarts=starts,tests='pending')
    dump(OUT/'integration-report.json',report)
    source=ROOT/'SourceArt/Dialogue/CommanderB20261025';source.mkdir(parents=True,exist_ok=True)
    for p in [OUT/'plan.json',OUT/'rendered.json',OUT/'integration-report.json',Path(__file__),BASE/'full_game_audio.py']:shutil.copy2(p,source/p.name)
    qfolder=BASE/'output/Voice_Preparation_20261024';p=qfolder/'voice-queue.json';q=load(p)
    for x in q['lines']:
        if x['speaker'] in ROLES:x.update(voice='Minh Đức',castingApproved=True,approved=False,status='Đã tích hợp giọng chỉ huy B, người dùng duyệt bộ giọng 25/10/2026')
    q['commanderVoiceDecision']=dict(sample='B',voice='Minh Đức',tempo=.92,pitch=.94,approved=True,date='2026-10-25');dump(p,q)
    for p in qfolder.glob('*.csv'):
        with p.open(encoding='utf-8-sig',newline='') as f:reader=csv.DictReader(f);fields=reader.fieldnames;items=list(reader)
        for x in items:
            if x['speaker'] in ROLES:x.update(voice='Minh Đức',status='Đã tích hợp chỉ huy B')
        with p.open('w',encoding='utf-8-sig',newline='') as f:writer=csv.DictWriter(f,fields);writer.writeheader();writer.writerows(items)
    for p in [qfolder/'voice-queue.json',*qfolder.glob('*.csv')]:shutil.copy2(p,ROOT/'Docs/Audio/VoicePreparation_20261024'/p.name)
    print(json.dumps(report,ensure_ascii=True),flush=True)
if __name__=='__main__':{'audit':audit,'render':render,'install':install}[sys.argv[1]]()
