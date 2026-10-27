"""One new Thành line, using the project's approved Commander B preset and radio chain."""
from pathlib import Path
import sys,os,json,hashlib,subprocess,random,uuid,re
ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
RUNTIME=ROOT/'Tools/TamVoiceRuntime'
sys.path.insert(0,str(RUNTIME/'site'))
os.environ['HF_HOME']=str(RUNTIME/'model-cache')
os.environ['HF_HUB_DISABLE_TELEMETRY']='1'
os.environ['PYTHONIOENCODING']='utf-8'
import numpy as np,soundfile as sf,imageio_ffmpeg
ID='m2v3_scoutescort_warning_01'
line=next(e for e in json.loads((ROOT/'Assets/_Project/Map01/Resources/Story/Map02Chapter.json').read_text(encoding='utf-8'))['entries'] if e['id']==ID)
caption=line['speaker']+' (bộ đàm): '+line['text']
bankpath=ROOT/'Assets/Resources/Dialogue/voice-catalog.json'
bank=json.loads(bankpath.read_text(encoding='utf-8-sig'))
def measure(path):
 a,sr=sf.read(path,dtype='float32');assert a.ndim==1 and sr==48000 and np.isfinite(a).all()
 return {'duration':len(a)/sr,'sampleRate':sr,'channels':1,'peak':float(np.max(np.abs(a))),'rms':float(np.sqrt(np.mean(a*a))),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
reference=[]
for e in bank['entries']:
 if e.get('speaker')=='Anh Thành' and e.get('radio'):
  p=ROOT/'Assets/Resources'/(e['resource']+'.wav')
  reference.append(dict(id=e['id'],voice=e.get('voice'),resource=e['resource'],**measure(p)))
(HERE/'reference-analysis.json').write_text(json.dumps({'preset':'Minh Đức','tempo':.92,'pitch':.94,'sourceRecipe':'SourceArt/Dialogue/CommanderB20261025/integrate_commander_b.py','cleanFilter':'rubberband=tempo=0.92:pitch=0.94,highpass=f=65,lowpass=f=7200,alimiter=limit=0.9:level=0','radioFilter':'highpass=f=320,lowpass=f=3200','references':reference},ensure_ascii=False,indent=2),encoding='utf-8')
raw=HERE/(ID+'_raw.wav');clean=HERE/(ID+'_clean.wav');radio=HERE/(ID+'_radio.wav')
if not raw.exists():
 from vieneu import Vieneu
 print('Loading same v3 Turbo ONNX preset used by existing Thanh recordings',flush=True)
 tts=Vieneu(backend='onnx',precision='fp32',threads=2)
 random.seed(20261026);np.random.seed(20261026)
 a=np.asarray(tts.infer(line['text'],voice='Minh Đức',temperature=.65),dtype=np.float32)
 assert len(a) and np.isfinite(a).all() and np.sqrt(np.mean(a*a))>.002
 a*=min(.8/max(abs(a)),.105/max(float(np.sqrt(np.mean(a*a))),1e-8))
 sf.write(raw,a,48000,subtype='PCM_24')
ff=imageio_ffmpeg.get_ffmpeg_exe()
for src,dst,filters in [(raw,clean,'rubberband=tempo=0.92:pitch=0.94,highpass=f=65,lowpass=f=7200,alimiter=limit=0.9:level=0'),(clean,radio,'highpass=f=320,lowpass=f=3200')]:
 subprocess.run([ff,'-v','error','-y','-i',str(src),'-af',filters,'-ar','48000','-c:a','pcm_s16le',str(dst)],check=True)
stats=measure(radio);assert stats['peak']<.99 and stats['rms']>.002
out=ROOT/'Assets/Resources/Dialogue/VI'/(ID+'_radio.wav');out.write_bytes(radio.read_bytes())
meta=Path(str(out)+'.meta')
if not meta.exists():
 template=next((ROOT/'Assets/Resources/Dialogue/VI').glob('*.wav.meta')).read_text(encoding='utf-8')
 meta.write_text(re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,template),encoding='utf-8')
record=dict(line,caption=caption,match=caption,resource='Dialogue/VI/'+ID+'_radio',language='vi',voice='Minh Đức',provider='VieNeu offline',model='v3 Turbo',tempo=.92,pitch=.94,cost=0,castingApproved=True,userApproved=False,status='integrated_existing_cast_pending_line_listening',voiceRevision='ScoutEscort_20261026',source='https://huggingface.co/pnnbao-ump/VieNeu-TTS-v3-Turbo',license='Apache-2.0 (model and bundled presets)',**stats)
bank['entries']=[e for e in bank['entries'] if e['id']!=ID]+[record]
for p in [bankpath,ROOT/'SourceArt/Dialogue/voice-catalog.json']:p.write_text(json.dumps(bank,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(HERE/'integration.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print('INTEGRATED',ID,json.dumps(stats),flush=True)
