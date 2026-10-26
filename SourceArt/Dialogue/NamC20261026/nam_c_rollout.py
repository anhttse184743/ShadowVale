import os,json,hashlib,subprocess,shutil,re,random,sys
from pathlib import Path
import numpy as np,soundfile as sf
BASE=Path(os.environ.get('SVL_AUDIO_WORKSPACE','C:/Users/dangk/Documents/Codex/2026-09-20/du'))
ROOT=Path(os.environ.get('SVL_GAME_ROOT',str(Path(__file__).resolve().parents[3])))
FF=Path(os.environ.get('SVL_FFMPEG',str(BASE/'work/voice-deps/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe')))
OUT=BASE/'output/Nam_C_20261026';OUT.mkdir(parents=True,exist_ok=True)
SAMPLES=Path(__file__).resolve().parent/'approved'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def dump(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def audio(resource):return ROOT/'Assets/Resources'/(resource+'.wav')
def speaker(x):return (x.get('speaker') or x.get('caption','').split(':')[0]).replace(' (bộ đàm)','').strip()
def words(x):return x.get('text') or x.get('caption','').split(':',1)[-1].strip()
def backup(p):
 d=OUT/'before'/p.relative_to(ROOT);d.parent.mkdir(parents=True,exist_ok=True)
 if not d.exists():shutil.copy2(p,d)
def prepare():
 bank=read(ROOT/'Assets/Resources/Dialogue/voice-catalog.json');nam=[x for x in bank['entries'] if speaker(x)=='Nam'];protected={}
 for x in bank['entries']:
  if speaker(x)!='Nam':
   for resource in {x['resource'],x.get('cleanResource',x['resource'])}:
    p=audio(resource);protected[str(p)]=sha(p)
 for x in nam:
  for resource in {x['resource'],x.get('cleanResource',x['resource'])}:
   p=audio(resource);assert str(p) not in protected,(x['id'],'shared with another actor');backup(p)
 for rel in ['Assets/Resources/Dialogue/voice-catalog.json','SourceArt/Dialogue/voice-catalog.json','SourceArt/Dialogue/FullGameFree20261023/integrated-manifest.json','Assets/Resources/Dialogue/VI/m1_open_reply.wav']:backup(ROOT/rel)
 timing=ROOT/'Assets/_Project/Map01/Runtime/Map02ChapterDayOne.Supper.cs';cues=[(a,float(b)) for a,b in re.findall(r'\{"(m2v3_[^"]+)",([0-9.]+)f?\}',timing.read_text(encoding='utf-8-sig'))];windows={a:(cues[i+1][1] if i+1<len(cues) else 70)-t-.12 for i,(a,t) in enumerate(cues)}
 rows=[]
 for x in nam:
  quiet=any(t in x['id'] for t in ['promise','farewell','dawn','wound','alive','duckevent']) or x['text'] in ['Cậu bị thương mà.','Vịt thôi.']
  rows.append(dict(x,text=words(x),performance='quiet' if quiet else 'mission',tempo=1.02 if quiet else 1.12,window=windows.get(x['id'])))
 dump(OUT/'catalog-before.json',bank);dump(OUT/'plan.json',dict(voice='Minh Quân',approval='User selected Nam C on 2026-10-26',cost=0,lines=rows,protected=protected))
 print('Planned',len(rows),'cues,',len({x['text'] for x in rows}),'unique texts, protected',len(protected),'other audio files',flush=True)
def render():
 os.environ.update(HF_HOME=str(BASE/'work/vieneu-cache'),HF_HUB_OFFLINE='1',TRANSFORMERS_OFFLINE='1')
 from vieneu import Vieneu
 tts=Vieneu(backend='onnx',precision='fp32',threads=2);plan=read(OUT/'plan.json');done=[]
 samples={'free_literal_7be0a5941d33':SAMPLES/'C-0.wav','m1_ops_supply_report_01':SAMPLES/'C-1.wav'}
 for i,x in enumerate(plan['lines'],1):
  key=hashlib.sha256(x['text'].encode()).hexdigest();raw=OUT/(key+'-raw.wav');clean=OUT/(key+'-'+x['performance']+'-clean.wav');reused=x['id'] in samples
  if not clean.exists():
   if reused:shutil.copy2(samples[x['id']],clean)
   else:
    if not raw.exists():
     seed=int(key[:8],16);random.seed(seed);np.random.seed(seed)
     a=np.asarray(tts.infer(x['text'],voice='Minh Quân',temperature=.7),dtype=np.float32);assert len(a) and np.isfinite(a).all(),x['id'];sf.write(raw,a,48000,subtype='PCM_24')
    filters=f"atempo={x['tempo']},highpass=f=80,lowpass=f=8000,acompressor=threshold=0.14:ratio=2:attack=8:release=90,alimiter=limit=0.90:level=0"
    subprocess.run([str(FF),'-v','error','-y','-i',str(raw),'-af',filters,'-ar','48000','-ac','1','-c:a','pcm_s16le',str(clean)],check=True)
   a,sr=sf.read(clean);rms=float(np.sqrt(np.mean(a*a)));a*=min(.88/max(abs(a)),(.095 if x['performance']=='quiet' else .11)/max(rms,1e-8));sf.write(clean,a,sr,subtype='PCM_16')
  a,sr=sf.read(clean)
  if x['window'] and len(a)/sr>x['window']:
   fitted=OUT/(key+'-fitted.wav');rate=(len(a)/sr)/x['window'];assert rate<1.35,(x['id'],'needs delivery review',len(a)/sr,x['window'])
   subprocess.run([str(FF),'-v','error','-y','-i',str(clean),'-af',f'atempo={rate:.6f}','-c:a','pcm_s16le',str(fitted)],check=True);clean=fitted;a,sr=sf.read(clean)
  assert sr==48000 and np.isfinite(a).all() and .1<len(a)/sr and max(abs(a))<.99 and np.sqrt(np.mean(a*a))>.002,x['id']
  target=clean
  if x.get('radio'):
   target=OUT/(key+'-'+x['performance']+'-radio.wav');subprocess.run([str(FF),'-v','error','-y','-i',str(clean),'-af','highpass=f=320,lowpass=f=3200','-c:a','pcm_s16le',str(target)],check=True)
  a,sr=sf.read(target);done.append(dict(x,voice='Minh Quân',file=str(target),cleanFile=str(clean),duration=len(a)/sr,sha256=sha(target),peak=float(max(abs(a))),rms=float(np.sqrt(np.mean(a*a))),reusedApprovedSample=reused))
  dump(OUT/'rendered.json',dict(cost=0,voice='Minh Quân',lines=done));print(f'{i}/{len(plan["lines"])} {x["id"]} {len(a)/sr:.2f}s',flush=True)
def install():
 plan=read(OUT/'plan.json');rendered=read(OUT/'rendered.json')['lines'];assert len(rendered)==len(plan['lines'])
 for p,h in plan['protected'].items():assert sha(Path(p))==h,p
 bank=read(ROOT/'Assets/Resources/Dialogue/voice-catalog.json');assert bank==read(OUT/'catalog-before.json'),'Catalog changed during generation';lookup={x['id']:x for x in bank['entries']}
 installed={}
 for x in rendered:
  cue=lookup[x['id']];dest=audio(cue['resource'])
  h=sha(Path(x['file']))
  if str(dest) in installed:assert installed[str(dest)]==h,('Conflicting shared audio',x['id'])
  shutil.copy2(x['file'],dest);installed[str(dest)]=h
  if cue.get('cleanResource') and cue['cleanResource']!=cue['resource']:shutil.copy2(x['cleanFile'],audio(cue['cleanResource']))
  for field in ['voiceReferenceResources','referenceResources','voiceReferenceFile','reusedExactMap2']:cue.pop(field,None)
  cue.update(voice='Minh Quân',provider='VieNeu offline',model='v3 Turbo',duration=x['duration'],sha256=h,voiceRevision='Nam_C_20261026',castingApproved=True,castingApproval='User selected Nam C — Minh Quân on 2026-10-26',userApproved=x['reusedApprovedSample'],status='Nam C casting approved; generated performance pending individual listening review',performance=x['performance'])
 opening=next(x for x in rendered if x['text']=='Rõ! Tôi sẽ hoàn thành nhiệm vụ!');shutil.copy2(opening['cleanFile'],ROOT/'Assets/Resources/Dialogue/VI/m1_open_reply.wav')
 for rel in ['Assets/Resources/Dialogue/voice-catalog.json','SourceArt/Dialogue/voice-catalog.json']:dump(ROOT/rel,bank)
 measured=[]
 for x in bank['entries']:
  p=audio(x['resource']);a,sr=sf.read(p);assert abs(len(a)/sr-x['duration'])<.04,x['id'];assert not x.get('sha256') or sha(p)==x['sha256'],x['id'];measured.append(dict(x,duration=len(a)/sr,sha256=sha(p),peak=float(max(abs(a))),rms=float(np.sqrt(np.mean(a*a)))))
 dump(ROOT/'SourceArt/Dialogue/FullGameFree20261023/integrated-manifest.json',dict(cues=len(measured),date='2026-10-26',lines=measured))
 for p,h in plan['protected'].items():assert sha(Path(p))==h,p
 dump(OUT/'installation.json',dict(voice='Minh Quân',updatedCues=len(rendered),otherAudioFilesUnchanged=len(plan['protected']),cost=0,tests='pending'))
 print('Installed',len(rendered),'Nam C cues, other characters unchanged',flush=True)
if __name__=='__main__':{'prepare':prepare,'render':render,'install':install}[sys.argv[1]]()
