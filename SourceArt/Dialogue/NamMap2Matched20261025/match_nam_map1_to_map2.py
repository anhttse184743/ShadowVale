import os,json,hashlib,subprocess,shutil,sys
from pathlib import Path
import numpy as np
import soundfile as sf
from full_game_audio import BASE,ROOT,FF
OUT=BASE/'output/Nam_Map2_Reference_20261025';OUT.mkdir(parents=True,exist_ok=True)
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def dump(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def res(r):return ROOT/'Assets/Resources'/Path(r+'.wav')
def backup(p):
    dest=OUT/'before'/p.relative_to(ROOT)
    if not dest.exists():dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
def prepare():
    bank=load(ROOT/'Assets/Resources/Dialogue/voice-catalog.json');refs=['m2v3_promise_02','m2v3_promise_07','m2v3_promise_09']
    byid={x['id']:x for x in bank['entries']};parts=[]
    for ident in refs:
        x=byid[ident];assert x['speaker']=='Nam';a,sr=sf.read(res(x['resource']));assert sr==48000;parts.extend([a,np.zeros(9600)])
    sf.write(OUT/'Nam_Map2_reference.wav',np.concatenate(parts),48000,subtype='PCM_16')
    rows=[x for x in bank['entries'] if x['speaker']=='Nam' and (x['id'].startswith('m1_') or x['id'].startswith('free_literal_'))]
    protected=[]
    for x in bank['entries']:
        if x not in rows:
            for r in {x['resource'],x.get('cleanResource',x['resource'])}:protected.append(dict(id=x['id'],file=str(res(r)),sha256=sha(res(r))))
    dump(OUT/'catalog-before.json',bank);dump(OUT/'plan.json',dict(cost=0,referenceIds=refs,referenceFile=str(OUT/'Nam_Map2_reference.wav'),lines=rows,protected=protected))
    print('Nam Map1 cues',len(rows),flush=True)
def render():
    os.environ.update(HF_HOME=str(BASE/'work/vieneu-cache'),HF_HUB_OFFLINE='1',TRANSFORMERS_OFFLINE='1')
    from vieneu import Vieneu
    tts=Vieneu(backend='onnx',precision='fp32',threads=2)
    preclean=tts._preclean_reference_audio
    tts._preclean_reference_audio=lambda path:preclean(path,out_path=OUT/'Nam_Map2_reference_trimmed.wav')
    plan=load(OUT/'plan.json');bank=load(OUT/'catalog-before.json')['entries'];done=[]
    # Enrol only the synthetic Nam recordings already installed in Map 2.
    tts.add_voice('Nam_Map2_Matched',plan['referenceFile'],denoise=False,use_ref_codes=True,description='Own synthetic Nam Map2 voice, requested reference',gender='male')
    for i,x in enumerate(plan['lines'],1):
        key=hashlib.sha256(x['text'].encode()).hexdigest();raw=OUT/(key+'-raw.wav');clean=OUT/(key+'-clean.wav')
        same=next((b for b in bank if b['speaker']=='Nam' and b['id'].startswith('m2') and b['text']==x['text'] and not b.get('radio')),None)
        if not clean.exists():
            if same:shutil.copy2(res(same['resource']),clean)
            else:
                if not raw.exists():
                    a=np.asarray(tts.infer(x['text'],voice='Nam_Map2_Matched',temperature=.65),dtype=np.float32)
                    assert len(a) and np.isfinite(a).all(),x['id'];a*=min(.85/max(abs(a)),.11/max(float(np.sqrt(np.mean(a*a))),1e-8));sf.write(raw,a,48000,subtype='PCM_24')
                subprocess.run([str(FF),'-v','error','-y','-i',str(raw),'-af','highpass=f=75,lowpass=f=8500,alimiter=limit=0.9:level=0','-c:a','pcm_s16le',str(clean)],check=True)
        target=clean
        if x.get('radio'):
            target=OUT/(key+'-radio.wav')
            subprocess.run([str(FF),'-v','error','-y','-i',str(clean),'-af','highpass=f=320,lowpass=f=3200','-c:a','pcm_s16le',str(target)],check=True)
        a,sr=sf.read(target);assert sr==48000 and np.isfinite(a).all() and max(abs(a))<.99 and np.sqrt(np.mean(a*a))>.002,x['id']
        done.append(dict(x,file=str(target),cleanFile=str(clean),duration=len(a)/sr,sha256=sha(target),referenceResources=plan['referenceIds'],reusedExactMap2=same['id'] if same else None))
        dump(OUT/'rendered.json',dict(cost=0,lines=done));print(f'{i}/{len(plan["lines"])} {x["id"]}',flush=True)
def install():
    plan=load(OUT/'plan.json');done=load(OUT/'rendered.json')['lines'];assert len(done)==len(plan['lines'])
    bank=load(ROOT/'Assets/Resources/Dialogue/voice-catalog.json');assert bank==load(OUT/'catalog-before.json')
    for x in plan['protected']:assert sha(Path(x['file']))==x['sha256'],x['id']
    lookup={x['id']:x for x in bank['entries']}
    for x in done:
        cue=lookup[x['id']];target=res(cue['resource']);backup(target);shutil.copy2(x['file'],target)
        if cue.get('cleanResource') and cue['cleanResource']!=cue['resource']:
            target=res(cue['cleanResource']);backup(target);shutil.copy2(x['cleanFile'],target)
        cue.update(voice='Adam',duration=x['duration'],sha256=x['sha256'],provider='VieNeu offline',model='v3 Turbo',voiceReferenceResources=plan['referenceIds'],voiceRevision='Nam_Map2_Matched_20261025',status='matched_to_existing_Map2_Nam',userApproved=False)
    # The opening's reply is played from a direct prefab reference.
    reply=next(x for x in done if x['text']=='Rõ! Tôi sẽ hoàn thành nhiệm vụ!')
    target=ROOT/'Assets/Resources/Dialogue/VI/m1_open_reply.wav';backup(target);shutil.copy2(reply['cleanFile'],target)
    for rel in ['Assets/Resources/Dialogue/voice-catalog.json','SourceArt/Dialogue/voice-catalog.json']:
        p=ROOT/rel;backup(p);dump(p,bank)
    measured=[]
    for x in bank['entries']:
        p=res(x['resource']);a,sr=sf.read(p);assert abs(len(a)/sr-x['duration'])<.04,x['id'];measured.append(dict(x,duration=len(a)/sr,sha256=sha(p),peak=float(max(abs(a))),rms=float(np.sqrt(np.mean(a*a)))))
    p=ROOT/'SourceArt/Dialogue/FullGameFree20261023/integrated-manifest.json';backup(p);dump(p,dict(cues=len(measured),date='2026-10-25',lines=measured))
    for x in plan['protected']:assert sha(Path(x['file']))==x['sha256'],x['id']
    report=dict(cost=0,updatedCues=len(done),map2AudioUnchanged=True,enemyAndCommanderAudioUnchanged=True,referenceIds=plan['referenceIds'],tests='pending')
    dump(OUT/'integration-report.json',report)
    source=ROOT/'SourceArt/Dialogue/NamMap2Matched20261025';source.mkdir(parents=True,exist_ok=True)
    for p in [OUT/'plan.json',OUT/'rendered.json',OUT/'integration-report.json',OUT/'Nam_Map2_reference.wav',Path(__file__)]:shutil.copy2(p,source/p.name)
    print(json.dumps(report,ensure_ascii=True),flush=True)
if __name__=='__main__':{'prepare':prepare,'render':render,'install':install}[sys.argv[1]]()
