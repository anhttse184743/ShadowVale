from pathlib import Path
import sys,json,urllib.request,concurrent.futures,subprocess,hashlib
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
import numpy as np,soundfile as sf,imageio_ffmpeg
ART=HERE;RAW=ART/'Raw'
OUT=ROOT/'Assets/Resources/Dialogue/VI';OUT.mkdir(parents=True,exist_ok=True)
catalog=json.loads((ART/'voice-catalog.json').read_text(encoding='utf-8'))
FFMPEG=imageio_ffmpeg.get_ffmpeg_exe();RATE=48000
paths=[RAW/(e['id']+'.mp3') for e in catalog['entries']]
def decode(path,radio=False):
 args=[FFMPEG,'-v','error','-i',str(path),'-ac','1','-ar',str(RATE)]
 if radio:args+=['-af','highpass=f=350,lowpass=f=3000']
 return np.frombuffer(subprocess.check_output(args+['-f','f32le','pipe:1']),dtype=np.float32).copy()
def trim(x):
 block=480;n=len(x)//block
 rms=np.sqrt(np.mean(x[:n*block].reshape(n,block)**2,axis=1))
 threshold=max(.0008,float(np.max(rms))*.02)
 active=np.flatnonzero(rms>threshold)
 if len(active)==0:raise ValueError('Silent voice')
 start=max(0,(active[0]-10)*block);end=min(len(x),(active[-1]+17)*block)
 return x[start:end],start,end
def normalize(x):
 rms=float(np.sqrt(np.mean(x*x)));peak=float(np.max(np.abs(x)))
 x=x*min(.105/max(rms,.000001),.78/max(peak,.000001))
 fade=min(720,len(x)//2);x[:fade]*=np.linspace(0,1,fade);x[-fade:]*=np.linspace(1,0,fade)
 return x
clean={};report=[]
for e,path in zip(catalog['entries'],paths):
 x,start,end=trim(decode(path));x=normalize(x);clean[e['id']]=x.copy()
 sf.write(RAW/(e['id']+'_dry.wav'),x,RATE,subtype='PCM_16')
 if e['radio']:
  band=decode(path,True)[start:end];band=normalize(band)
  band=np.tanh(band*1.6)/1.6
  noise=np.random.default_rng(1401).normal(0,.0026,len(band)).astype(np.float32)
  # Slight crackle follows the speech; direct lines never receive this treatment.
  energy=np.convolve(np.abs(band),np.ones(480)/480,mode='same')
  x=band+noise*np.clip(.2+energy*6,.2,1)
  x[:720]*=np.linspace(0,1,720);x[-720:]*=np.linspace(1,0,720)
 sf.write(OUT/(e['id']+'.wav'),x,RATE,subtype='PCM_16')
 e['duration']=round(len(x)/RATE,5);e['resource']='Dialogue/VI/'+e['id']
 report.append({'id':e['id'],'duration':e['duration'],'peak':float(np.max(np.abs(x))),'rms':float(np.sqrt(np.mean(x*x))),'radio':e['radio'],'sha256':hashlib.sha256((OUT/(e['id']+'.wav')).read_bytes()).hexdigest()})
silence=np.zeros(round(RATE*.25),dtype=np.float32)
# Play only the transmitted order and reply, each once.
sf.write(ART/'voice-radio-preview.wav',np.concatenate([sf.read(OUT/'m1_radio_order.wav',dtype='float32')[0],np.zeros(round(RATE*.25)),sf.read(OUT/'m1_radio_reply.wav',dtype='float32')[0]]),RATE,subtype='PCM_16')
for e in catalog['entries']:e.pop('url',None)
(ART/'voice-catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8')
(ART/'audio-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('Processed',len(report),'Vietnamese clips. Radio filtered:',sum(r['radio'] for r in report))
