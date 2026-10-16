"""Rebuild only the rescue introduction's three Vietnamese radio cues from Raw MP3s."""
from pathlib import Path
import json,subprocess,hashlib,uuid
import numpy as np,soundfile as sf,imageio_ffmpeg
ART=Path(__file__).resolve().parent
ROOT=ART.parents[1];RAW=ART/'Raw';OUT=ROOT/'Assets/Resources/Dialogue/VI'
RATE=48000
LINES=[
 ('m1_rescue_radio_report','Nam','Brodie','Chỉ huy, tôi đã tìm thấy Hùng ở bến tàu. Địch ở đây rất đông, tôi không thể đánh trực diện. Xin chỉ thị.','8302fa7d-03ae-4c65-a715-8682a2d0bc30'),
 ('m1_rescue_radio_instruction','Chỉ huy','Tom','Nam, tuyệt đối không nổ súng. Dùng đá đánh lạc hướng, vòng ra sau hạ lính giám sát rồi cứu Hùng.','3080192e-a025-4934-bf8b-f5c026e238ae'),
 ('m1_rescue_radio_ack','Nam','Brodie','Rõ. Tôi sẽ đánh lạc hướng rồi tiếp cận từ phía sau.','c7608e10-666b-47d4-b190-a0ba4b0f6c83')]
def decode(path,band=False):
 args=[imageio_ffmpeg.get_ffmpeg_exe(),'-v','error','-i',str(path),'-ac','1','-ar',str(RATE)]
 if band:args+=['-af','highpass=f=350,lowpass=f=3000']
 return np.frombuffer(subprocess.check_output(args+['-f','f32le','pipe:1']),dtype=np.float32).copy()
def normalize(x):
 x*=min(.105/max(float(np.sqrt(np.mean(x*x))),1e-6),.78/max(float(np.max(np.abs(x))),1e-6))
 fade=min(720,len(x)//2);x[:fade]*=np.linspace(0,1,fade);x[-fade:]*=np.linspace(1,0,fade)
 return x
catalog=json.loads((ART/'voice-catalog.json').read_text(encoding='utf-8-sig'))
entries={e['id']:e for e in catalog['entries'] if e['id']!='m1_rescue_intro_nam'}
report=[];preview=[]
for id,speaker,voice,text,task in LINES:
 path=RAW/(id+'.mp3');dry=decode(path)
 blocks=len(dry)//480;rms=np.sqrt(np.mean(dry[:blocks*480].reshape(blocks,480)**2,axis=1))
 active=np.flatnonzero(rms>max(.0008,float(rms.max())*.02));assert len(active)
 start=max(0,(active[0]-10)*480);end=min(len(dry),(active[-1]+17)*480)
 sf.write(RAW/(id+'_dry.wav'),normalize(dry[start:end].copy()),RATE,subtype='PCM_16')
 band=normalize(decode(path,True)[start:end]);band=np.tanh(band*1.6)/1.6
 noise=np.random.default_rng(1401).normal(0,.0026,len(band)).astype(np.float32)
 energy=np.convolve(np.abs(band),np.ones(480)/480,mode='same')
 x=band+noise*np.clip(.2+energy*6,.2,1);x[:720]*=np.linspace(0,1,720);x[-720:]*=np.linspace(1,0,720)
 radio=speaker!='Nam'
 if not radio:x=normalize(dry[start:end].copy())
 output=OUT/(id+'.wav');sf.write(output,x,RATE,subtype='PCM_16')
 meta=Path(str(output)+'.meta')
 if not meta.exists():
  template=(OUT/'m1_rescue_intro_nam.wav.meta').read_text(encoding='utf-8')
  import re
  meta.write_text(re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,template),encoding='utf-8')
 entries[id]=dict(id=id,speaker=speaker,voice=voice,text=text,caption=speaker+(' (bộ đàm): ' if radio else ': ')+text,match='',language='vi',radio=radio,
                 taskId=task,model='eleven_v3',duration=round(len(x)/RATE,5),reportedDuration=round(len(dry)/RATE,5),resource='Dialogue/VI/'+id)
 assert np.max(np.abs(x))<.99 and np.sqrt(np.mean(x*x))>.01
 report.append(dict(id=id,radio=radio,duration=entries[id]['duration'],peak=float(np.max(np.abs(x))),rms=float(np.sqrt(np.mean(x*x))),sha256=hashlib.sha256(output.read_bytes()).hexdigest(),taskId=task))
 preview.extend([x,np.zeros(round(RATE*.25),dtype=np.float32)])
catalog['entries']=list(entries.values())
for path in [ART/'voice-catalog.json',ROOT/'Assets/Resources/Dialogue/voice-catalog.json']:path.write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8')
sf.write(ART/'rescue-radio-preview.wav',np.concatenate(preview),RATE,subtype='PCM_16')
(ART/'rescue-radio-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('Rescue radio:',[(r['id'],r['duration']) for r in report])
