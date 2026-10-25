import os,json,re,hashlib,random,subprocess,sys
from pathlib import Path
BASE=Path(__file__).resolve().parent
ROOT=Path('G:/game/Capstone/ShadowVale')
OUT=BASE/'work/full-game-audio-20261023';OUT.mkdir(parents=True,exist_ok=True)
FF=BASE/'work/voice-deps/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
CAST={
'Nam':('Adam','young adult, healthy, energetic',1.0,1.0),
'Hùng':('Anh Khôi','middle aged, right arm injured; restrained pain, not elderly; user selected sample A',.97,.97),
'Chỉ huy':('Minh Đức','older commander, experienced and authoritative; user approved sample B',.92,.94),
'Anh Thành':('Minh Đức','older commander, experienced and authoritative; user approved sample B',.92,.94),
'Anh Lực':('Đức Trí','adult guerrilla, direct, alert',1,1),
'Chị Lành':('Thục Đoan','adult woman, caring, southern',1,1),
'Ông Sáu':('Thiền Tâm Đức','elderly man, frail; short breath; user selected sample A',.87,.94),
'Chú Bảy':('Đức Trí','older man, sturdy boatman; distinct from frail Sau',.94,.97),
'Tâm':('Mạnh Dũng','young adult scout, injured leg; breath restrained',.97,1),
'Tổ gác':('Phạm Tuyên','adult guard, healthy and alert',1,1),
'Người trinh sát':('Mạnh Dũng','young adult scout',.97,1),
'Bé Tí':('Mỹ Duyên','child boy; proxy preset, requires listening review',1.06,1.12),
}
ENEMY_ROLES={'Lính địch','Lính','Giám sát','Lính phản ứng','Sĩ quan địch','Lính gác cổng','Lính tuần tra','Chỉ huy địch','Địch'}
def casting(speaker):
 s=re.sub(r'\s*\(.*?\)','',speaker).strip()
 s={'Phụ trách địa phương':'Anh Lực','Người chăm cha':'Chị Lành','Trinh sát địa phương':'Người trinh sát'}.get(s,s)
 if s in CAST:return CAST[s]
 if s in ['Chỉ huy địch','Sĩ quan địch','Giám sát']:return ('Quang Sơn','middle aged enemy officer',1,.98)
 if 'Lính' in s or s=='Địch':return ('Xuân Vĩnh','young adult soldier, alert',1.02,1)
 raise ValueError('Unclassified speaker '+repr(s))
def plan():
 old=json.loads((ROOT/'Assets/Resources/Dialogue/voice-catalog.json').read_text(encoding='utf-8-sig'))
 rows={}
 for x in old['entries']:
  caption=x.get('caption','');speaker=x.get('speaker') or caption.split(':',1)[0]
  text=x.get('text') or caption.split(':',1)[-1].strip()
  if text:rows[x['id']]=dict(id=x['id'],speaker=speaker,text=text,caption=caption,radio=x.get('radio',False),origin='catalog')
 for rel in ['Assets/_Project/Map01/Resources/Story/Map02Chapter.json','Assets/_Project/Map01/Resources/Dialogue/Map02Story/script.json']:
  for x in json.loads((ROOT/rel).read_text(encoding='utf-8-sig'))['entries']:
   if not x.get('text','').strip():continue
   rows[x['id']]=dict(x,caption=x['speaker']+(' (bộ đàm): ' if x.get('radio') else ': ')+x['text'],origin=rel)
 for p in (ROOT/'Assets/_Project/Map01/Runtime').glob('*.cs'):
  # Spoken subtitle literals, including newly added enemy commander combat barks.
  for speaker,text in re.findall(r'"(Nam|Hùng|Địch|Chỉ huy(?: địch)?|Lính địch|Trinh sát|Chị Lành|Ông Sáu)(?: \(bộ đàm\))?: ([^"\n]+)"',p.read_text(encoding='utf-8-sig')):
   if '{' in text or '\\' in text or any(x['text']==text and x['speaker']==speaker for x in rows.values()):continue
   ident='free_literal_'+hashlib.sha256((speaker+text).encode()).hexdigest()[:12]
   rows[ident]=dict(id=ident,speaker='Người trinh sát' if speaker=='Trinh sát' else speaker,text=text,caption=speaker+': '+text,radio=False,origin=p.name)
 p=ROOT/'Assets/_Project/Map01/Runtime/Map01CommanderCallouts.cs'
 for kind,text in re.findall(r'"(\w+)" => "([^"\n]+)"',p.read_text(encoding='utf-8-sig')):
  ident='free_boss_'+kind;rows[ident]=dict(id=ident,speaker='Chỉ huy địch',text=text,caption='Chỉ huy địch: '+text,radio=False,origin=p.name)
 lines=[]
 installed={x['id']:x for x in old['entries']}
 for x in rows.values():
  if x['speaker'] in ENEMY_ROLES:
   existing=installed.get(x['id'])
   # User wants original Runway enemies only. Missing retakes stay subtitle-only.
   if not existing or existing.get('provider')!='Runway':continue
   lines.append(dict(existing,preserveExisting=True));continue
  voice,brief,tempo,pitch=casting(x['speaker']);x.update(voice=voice,performance=brief,tempo=tempo,pitch=pitch)
  lines.append(x)
 result=dict(cost=0,provider='VieNeu-TTS v3 Turbo offline',source='https://huggingface.co/pnnbao-ump/VieNeu-TTS-v3-Turbo',license='Apache-2.0',lines=lines,total=len(lines),cast=CAST)
 (OUT/'plan.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
 print('PLAN',len(lines),'speakers',sorted({x['speaker'] for x in lines}),flush=True)
def render():
 os.environ['HF_HOME']=str(BASE/'work/vieneu-cache');os.environ['HF_HUB_OFFLINE']='1';os.environ['TRANSFORMERS_OFFLINE']='1'
 import numpy as np,soundfile as sf
 from vieneu import Vieneu
 tts=Vieneu(backend='onnx',precision='fp32',threads=2)
 data=json.loads((OUT/'plan.json').read_text(encoding='utf-8'));cache={};done=[]
 for i,x in enumerate(data['lines']):
  if x.get('preserveExisting'):
   path=ROOT/'Assets/Resources'/Path(x['resource']+'.wav')
   a,sr=sf.read(path)
   x.update(file=str(path),duration=len(a)/sr,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),peak=float(max(abs(a))),rms=float(np.sqrt(np.mean(a*a))))
   done.append(x);(OUT/'manifest.json').write_text(json.dumps(dict(data,lines=done,rendered=len(done)),ensure_ascii=False,indent=2),encoding='utf-8')
   print('Preserved original Runway',x['id'],flush=True);continue
  key=hashlib.sha256(json.dumps([x['text'],x['voice'],x['tempo'],x['pitch'],x.get('radio')],ensure_ascii=False).encode()).hexdigest()
  path=OUT/(key+'.wav')
  if not path.exists():
   raw=OUT/(key+'-raw.wav');random.seed(12000+i);np.random.seed(12000+i)
   a=np.asarray(tts.infer(x['text'],voice=x['voice'],temperature=.7),dtype=np.float32)
   if not len(a) or np.sqrt(np.mean(a*a))<.002:raise RuntimeError('Silent '+x['id'])
   a*=min(.85/max(abs(a)),.11/max(np.sqrt(np.mean(a*a)),1e-8));sf.write(raw,a,48000,subtype='PCM_24')
   filters=f"rubberband=tempo={x['tempo']}:pitch={x['pitch']},highpass=f=75,lowpass=f=8500"
   if x.get('radio'):filters+=',highpass=f=320,lowpass=f=3200'
   subprocess.run([str(FF),'-v','error','-y','-i',str(raw),'-af',filters,'-ar','48000','-c:a','pcm_s16le',str(path)],check=True)
  a,sr=sf.read(path);x.update(file=str(path),duration=len(a)/sr,peak=float(max(abs(a))),rms=float(np.sqrt(np.mean(a*a))),sha256=hashlib.sha256(path.read_bytes()).hexdigest())
  done.append(x);(OUT/'manifest.json').write_text(json.dumps(dict(data,lines=done,rendered=len(done)),ensure_ascii=False,indent=2),encoding='utf-8')
  print(f"{i+1}/{len(data['lines'])} {x['id']} {x['speaker']} {x['duration']:.2f}s",flush=True)
if __name__=='__main__':
 if '--render' in sys.argv:render()
 else:plan()
