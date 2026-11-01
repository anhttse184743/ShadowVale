import os,json,hashlib,subprocess,shutil,random,re,sys,uuid
from pathlib import Path
import numpy as np,soundfile as sf
BASE=Path(os.environ.get('SVL_AUDIO_WORKSPACE','C:/Users/dangk/Documents/Codex/2026-09-20/du'))
ROOT=Path(os.environ.get('SVL_GAME_ROOT','G:/game/Capstone/ShadowVale'))
OUT=BASE/'output/VoiceCompletion_20261028';OUT.mkdir(parents=True,exist_ok=True)
FF=BASE/'work/voice-deps/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
HERE=ROOT/'SourceArt/Dialogue/VoiceCompletion20261028'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def dump(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def audio(resource):return ROOT/'Assets/Resources'/(resource+'.wav')
def measure(p):
 a,s=sf.read(p,dtype='float32');assert s==48000 and a.ndim==1 and np.isfinite(a).all() and len(a)/s>.15 and np.sqrt(np.mean(a*a))>.002 and max(abs(a))<.99,p
 return dict(duration=len(a)/s,sampleRate=s,channels=1,peak=float(max(abs(a))),rms=float(np.sqrt(np.mean(a*a))),sha256=sha(p))
def prepare():
 bank=read(ROOT/'Assets/Resources/Dialogue/voice-catalog.json');chapter=read(ROOT/'Assets/_Project/Map01/Resources/Story/Map02Chapter.json');rows=[]
 for e in bank['entries']:
  if e.get('speaker')=='Nam' and e.get('voice')=='Adam':
   assert e['id'].startswith('m2v3_evac_');rows.append(dict(e,voice='Minh Quân',reuse='m2v3_attack_03' if e['text'].strip('.!').casefold()=='rõ' and '!' in e['text'] else 'm2v3_day1report_03' if e['text'].strip('.!').casefold()=='rõ' else None))
 assert len(rows)==5
 for id,speaker,text,voice in [
 ('m2v3_day3report_01','Nam','Vật tư đã chuyển xuống bến. Hai thuyền đã vá xong, các nhà cũng biết đường sơ tán rồi.','Minh Quân'),
 ('m2v3_day3report_02','Anh Thành','Được. Tối nay cứ theo phân công. Có động thì báo ngay.','Minh Đức')]:
  assert not any(e['id']==id for e in bank['entries']);rows.append(dict(id=id,speaker=speaker,text=text,voice=voice,radio=False,resource='Dialogue/VI/'+id+'_clean',reuse=None))
 protected={str(audio(e['resource'])):sha(audio(e['resource'])) for e in bank['entries'] if e['id'] not in {x['id'] for x in rows}}
 dump(OUT/'plan.json',dict(lines=rows,protected=protected,bankSha=sha(ROOT/'Assets/Resources/Dialogue/voice-catalog.json')))
 for rel in ['Assets/Resources/Dialogue/voice-catalog.json','SourceArt/Dialogue/voice-catalog.json','SourceArt/Dialogue/FullGameFree20261023/integrated-manifest.json','Assets/_Project/Map01/Resources/Story/Map02Chapter.json','Assets/_Project/Map01/Runtime/Map02ChapterNightPatrol.cs']:
  p=ROOT/rel;dest=OUT/'before'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
 for x in rows[:5]:
  p=audio(x['resource']);dest=OUT/'before'/p.relative_to(ROOT);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
 print('Prepared',len(rows),'audio outputs; 5 unique syntheses, 2 exact reuses; all other catalog audio protected.',flush=True)
def render():
 os.environ.update(HF_HOME=str(BASE/'work/vieneu-cache'),HF_HUB_OFFLINE='1',TRANSFORMERS_OFFLINE='1',HF_HUB_DISABLE_TELEMETRY='1')
 plan=read(OUT/'plan.json');bank=read(ROOT/'Assets/Resources/Dialogue/voice-catalog.json');lookup={e['id']:e for e in bank['entries']};tts=None;done=[]
 for x in plan['lines']:
  id=x['id'];raw=OUT/(id+'_raw.wav');clean=OUT/(id+'_clean.wav');target=OUT/(id+'_radio.wav') if x['radio'] else clean
  if x.get('reuse'):
   origin=lookup[x['reuse']];assert origin['voice']=='Minh Quân' and not origin.get('radio');shutil.copy2(audio(origin['resource']),clean)
  elif not clean.exists():
   if not raw.exists():
    if tts is None:
     from vieneu import Vieneu
     tts=Vieneu(backend='onnx',precision='fp32',threads=2)
    seed=int(hashlib.sha256(id.encode()).hexdigest()[:8],16);random.seed(seed);np.random.seed(seed)
    a=np.asarray(tts.infer(x['text'],voice=x['voice'],temperature=.7 if x['voice']=='Minh Quân' else .65),dtype=np.float32);assert np.isfinite(a).all() and len(a)
    sf.write(raw,a,48000,subtype='PCM_24')
   filt='atempo=1.12,highpass=f=80,lowpass=f=8000,acompressor=threshold=0.14:ratio=2:attack=8:release=90,alimiter=limit=0.90:level=0' if x['voice']=='Minh Quân' else 'rubberband=tempo=0.92:pitch=0.94,highpass=f=65,lowpass=f=7200,alimiter=limit=0.9:level=0'
   subprocess.run([str(FF),'-v','error','-y','-i',str(raw),'-af',filt,'-ar','48000','-ac','1','-c:a','pcm_s16le',str(clean)],check=True)
   a,s=sf.read(clean);a*=min(.88/max(abs(a)),.11/max(float(np.sqrt(np.mean(a*a))),1e-8));sf.write(clean,a,s,subtype='PCM_16')
  if x['radio']:subprocess.run([str(FF),'-v','error','-y','-i',str(clean),'-af','highpass=f=320,lowpass=f=3200','-ar','48000','-ac','1','-c:a','pcm_s16le',str(target)],check=True)
  done.append(dict(x,file=str(target),cleanFile=str(clean),**measure(target)));dump(OUT/'rendered.json',done);print('READY',id,x['voice'],round(done[-1]['duration'],2),flush=True)
def install():
 plan=read(OUT/'plan.json');done=read(OUT/'rendered.json');assert len(done)==7;bankpath=ROOT/'Assets/Resources/Dialogue/voice-catalog.json';assert sha(bankpath)==plan['bankSha'];bank=read(bankpath);lookup={e['id']:e for e in bank['entries']}
 for p,h in plan['protected'].items():assert sha(Path(p))==h
 HERE.mkdir(parents=True,exist_ok=True)
 template=next((ROOT/'Assets/Resources/Dialogue/VI').glob('*.wav.meta')).read_text()
 for x in done:
  target=audio(x['resource']);shutil.copy2(x['file'],target)
  if not Path(str(target)+'.meta').exists():Path(str(target)+'.meta').write_text(re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,template),encoding='utf-8')
  e=lookup.get(x['id']);
  if e is None:e=dict(id=x['id']);bank['entries'].append(e)
  caption=x['speaker']+(' (bộ đàm): ' if x['radio'] else ': ')+x['text']
  e.update(speaker=x['speaker'],text=x['text'],caption=caption,match=caption,radio=x['radio'],language='vi',resource=x['resource'],voice=x['voice'],provider='VieNeu offline',model='v3 Turbo',castingApproved=True,userApproved=False,status='approved cast; synchronized offline; individual performance not user-reviewed',voiceRevision='Nam_C_20261026' if x['speaker']=='Nam' else 'Commander_B_20261025',integrationRevision='VoiceCompletion_20261028',reusedRecording=x.get('reuse'),cost=0,**measure(target))
  for suffix in ['_raw.wav','_clean.wav','_radio.wav']:
   p=OUT/(x['id']+suffix)
   if p.exists():shutil.copy2(p,HERE/p.name)
 chapter=read(ROOT/'Assets/_Project/Map01/Resources/Story/Map02Chapter.json');task=next(t for t in chapter['tasks'] if t['id']=='D3_06');assert not task['scene'];task['scene']='day3report'
 extra=[dict(id=x['id'],conversation='m2v3_day3report',speaker=x['speaker'],text=x['text'],radio=False) for x in done if x['id'].startswith('m2v3_day3report')]
 extra.append(dict(id='m2v3_day3report_03',conversation='m2v3_day3report',speaker='Nam',text='Rõ.',radio=False,recording='m2v3_day1report_03'))
 chapter['entries'].extend(extra);dump(ROOT/'Assets/_Project/Map01/Resources/Story/Map02Chapter.json',chapter)
 enemy=[]
 for name,id in [('night2_patrol_warning','m1_ops_enemy_spotted'),('night2_patrol_fire_order','m1_ops_enemy_order')]:
  e=lookup[id];assert e['provider']=='Runway' and e['userApproved'];target=ROOT/'Assets/Resources/OperationsAudio'/(name+'.wav');old=OUT/'before'/target.relative_to(ROOT);old.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(target,old);shutil.copy2(audio(e['resource']),target);assert sha(target)==sha(audio(e['resource']));enemy.append(dict(resource='OperationsAudio/'+name,sourceId=id,text=e['text'],sha256=sha(target),provider='Runway original, exact file reused',cost=0))
 p=ROOT/'Assets/_Project/Map01/Runtime/Map02ChapterNightPatrol.cs';s=p.read_text();s=s.replace('I think something moved. Keep a close watch.',lookup['m1_ops_enemy_spotted']['text']).replace("There's a little rat out there. Squad, open fire on that position!",lookup['m1_ops_enemy_order']['text']);p.write_text(s,encoding='utf-8')
 for p in [bankpath,ROOT/'SourceArt/Dialogue/voice-catalog.json']:dump(p,bank)
 measured=[]
 for e in bank['entries']:measured.append(dict(e,**measure(audio(e['resource']))))
 dump(ROOT/'SourceArt/Dialogue/FullGameFree20261023/integrated-manifest.json',dict(cues=len(measured),date='2026-10-28',lines=measured))
 for p,h in plan['protected'].items():assert sha(Path(p))==h,'Unrelated audio changed: '+p
 dump(HERE/'integration.json',dict(date='2026-10-28',cost=0,updatedAudio=[dict(id=x['id'],voice=x['voice'],resource=x['resource'],reused=x.get('reuse'),sha256=x['sha256']) for x in done],newConversation=extra,enemy=enemy,unchangedAudio=len(plan['protected']),userPerformanceApproval=False))
 print('INSTALLED; protected audio unchanged; cost 0',flush=True)
if __name__=='__main__':{'prepare':prepare,'render':render,'install':install}[sys.argv[1]]()
