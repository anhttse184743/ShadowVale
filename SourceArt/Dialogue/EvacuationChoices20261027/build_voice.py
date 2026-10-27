"""Fill the eight new evacuation radio lines using the project's existing offline cast."""
from pathlib import Path
import hashlib,json,os,random,re,subprocess,sys,uuid,csv

ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
RUNTIME=ROOT/'Tools/TamVoiceRuntime'
sys.path.insert(0,str(RUNTIME/'site'))
os.environ.update(HF_HOME=str(RUNTIME/'model-cache'),HF_HUB_OFFLINE='1',TRANSFORMERS_OFFLINE='1',HF_HUB_DISABLE_TELEMETRY='1')
import numpy as np
import soundfile as sf
import imageio_ffmpeg

bank_path=ROOT/'Assets/Resources/Dialogue/voice-catalog.json'
bank=json.loads(bank_path.read_text(encoding='utf-8-sig'))
chapter=json.loads((ROOT/'Assets/_Project/Map01/Resources/Story/Map02Chapter.json').read_text(encoding='utf-8-sig'))
lines=[e for e in chapter['entries'] if e['id'].startswith('m2v3_evac_')]
assert len(lines)==8
ids={e['id'] for e in lines}
before={e['id']:hashlib.sha256((ROOT/'Assets/Resources'/(e['resource']+'.wav')).read_bytes()).hexdigest()
        for e in bank['entries'] if e['id'] not in ids and (ROOT/'Assets/Resources'/(e.get('resource','')+'.wav')).exists()}
backup=HERE/'voice-catalog-before.json'
if not backup.exists():backup.write_bytes(bank_path.read_bytes())
ff=imageio_ffmpeg.get_ffmpeg_exe()
template=(ROOT/'Assets/Resources/Dialogue/VI/m2v3_scoutescort_warning_01_radio.wav.meta').read_text(encoding='utf-8')
tts=None
records=[]

def measure(path):
    audio,sr=sf.read(path,dtype='float32')
    assert audio.ndim==1 and sr==48000 and len(audio) and np.isfinite(audio).all()
    peak=float(np.max(np.abs(audio)));rms=float(np.sqrt(np.mean(audio*audio)))
    assert peak<.99 and rms>.002,(path,peak,rms)
    return dict(duration=len(audio)/sr,sampleRate=sr,channels=1,peak=peak,rms=rms,sha256=hashlib.sha256(path.read_bytes()).hexdigest())

for index,line in enumerate(lines):
    ident=line['id'];voice='Adam' if line['speaker']=='Nam' else 'Minh Đức'
    tempo,pitch=(1,1) if voice=='Adam' else (.92,.94)
    raw=HERE/(ident+'_raw.wav');clean=HERE/(ident+'_clean.wav');radio=HERE/(ident+'_radio.wav')
    reuse=None
    if line['text'].casefold().strip('.!')=='rõ':
        original='m2v3_attack_03' if '!' in line['text'] else 'm1_v2_rescue_approach_03'
        entry=next(e for e in bank['entries'] if e['id']==original)
        clean.write_bytes((ROOT/'Assets/Resources'/(entry['resource']+'.wav')).read_bytes());reuse=original
    elif not clean.exists():
        if not raw.exists():
            if tts is None:
                from vieneu import Vieneu
                print('Loading cached VieNeu v3 Turbo ONNX, offline',flush=True)
                tts=Vieneu(backend='onnx',precision='fp32',threads=2)
            random.seed(20261027+index);np.random.seed(20261027+index)
            audio=np.asarray(tts.infer(line['text'],voice=voice,temperature=.65),dtype=np.float32)
            assert len(audio) and np.isfinite(audio).all() and np.sqrt(np.mean(audio*audio))>.002
            audio*=min(.8/max(abs(audio)),.105/max(float(np.sqrt(np.mean(audio*audio))),1e-8))
            sf.write(raw,audio,48000,subtype='PCM_24')
        filters=f'rubberband=tempo={tempo}:pitch={pitch},highpass=f=65,lowpass=f=7200,alimiter=limit=0.9:level=0'
        subprocess.run([ff,'-v','error','-y','-i',str(raw),'-af',filters,'-ar','48000','-ac','1','-c:a','pcm_s16le',str(clean)],check=True)
    subprocess.run([ff,'-v','error','-y','-i',str(clean),'-af','highpass=f=320,lowpass=f=3200','-ar','48000','-ac','1','-c:a','pcm_s16le',str(radio)],check=True)
    stats=measure(radio)
    output=ROOT/'Assets/Resources/Dialogue/VI'/(ident+'_radio.wav');output.write_bytes(radio.read_bytes())
    meta=Path(str(output)+'.meta')
    if not meta.exists():meta.write_text(re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,template),encoding='utf-8')
    caption=line['speaker']+' (bộ đàm): '+line['text']
    record=dict(line,caption=caption,match=caption,resource='Dialogue/VI/'+ident+'_radio',language='vi',voice=voice,
                provider='VieNeu offline',model='v3 Turbo',tempo=tempo,pitch=pitch,cost=0,
                castingApproved=voice=='Minh Đức',userApproved=False,status='integrated_existing_cast_pending_line_listening',
                voiceRevision='EvacuationChoices_20261027',reusedRecording=reuse,
                source='https://huggingface.co/pnnbao-ump/VieNeu-TTS-v3-Turbo',license='Apache-2.0 (model and bundled presets)',**stats)
    records.append(record)
    (HERE/'rendered.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('READY',ident,voice,round(stats['duration'],3),'seconds',flush=True)

bank['entries']=[e for e in bank['entries'] if e['id'] not in ids]+records
for rel in ['Assets/Resources/Dialogue/voice-catalog.json','SourceArt/Dialogue/voice-catalog.json']:
    (ROOT/rel).write_text(json.dumps(bank,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
after={e['id']:hashlib.sha256((ROOT/'Assets/Resources'/(e['resource']+'.wav')).read_bytes()).hexdigest()
       for e in bank['entries'] if e['id'] in before}
assert before==after,'An unrelated recording changed'
missing=[]
by={e['id']:e for e in bank['entries']}
for line in chapter['entries']:
    if not line.get('text','').strip():continue
    entry=by.get(line.get('recording') or line['id'])
    caption=line['speaker']+(' (bộ đàm): ' if line.get('radio') else ': ')+line['text']
    if not entry or entry['caption']!=caption or entry.get('radio',False)!=line.get('radio',False) or not (ROOT/'Assets/Resources'/(entry['resource']+'.wav')).exists():missing.append(line['id'])
assert not missing,missing
queue=ROOT/'Docs/Audio/VoicePreparation_20261024/03_Map2_Ngay2_Dem2.csv'
with queue.open(encoding='utf-8-sig',newline='') as file:
    reader=csv.DictReader(file);columns=reader.fieldnames;rows=[row for row in reader if row['id'] not in ids]
for record in records:
    rows.append(dict(batch='03_Map2_Ngay2_Dem2',id=record['id'],scene=record['conversation'],speaker=record['speaker'],text=record['text'],radio='True',voice=record['voice'],performance='existing cast; radio report',resource=record['resource'],status='Đã tích hợp 27/10/2026; chờ nghe duyệt từng câu',approved='False'))
with queue.open('w',encoding='utf-8',newline='') as file:
    writer=csv.DictWriter(file,fieldnames=columns);writer.writeheader();writer.writerows(rows)
summary=dict(integrated=len(records),missingMap2VoicedLines=missing,unrelatedRecordingsPreserved=len(before),cost=0,records=records)
(HERE/'integration.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('INTEGRATED',len(records),'lines; unrelated recordings preserved:',len(before),flush=True)
