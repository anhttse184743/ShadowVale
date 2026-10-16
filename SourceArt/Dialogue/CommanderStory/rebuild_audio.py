"""Prepare approved generated speech for Unity. Preserve MP3s and clean masters."""
from pathlib import Path
import array, concurrent.futures, hashlib, json, math, os, subprocess, sys, unicodedata, urllib.request

HERE=Path(__file__).resolve().parent
if os.environ.get('SHADOWVALE_AUDIO_DEPS'):sys.path.insert(0,os.environ['SHADOWVALE_AUDIO_DEPS'])
import numpy as np
import soundfile as sf
import imageio_ffmpeg

RATE=48000
ROOT=HERE.parents[2]
raw=HERE/'Raw';output=ROOT/'Assets/Resources/Dialogue/VI';output.mkdir(parents=True,exist_ok=True)
receipt=json.loads((HERE/'voice-manifest.json').read_text(encoding='utf-8'))
jobs=receipt['entries'];assert len(jobs)==22 and receipt['creditsSpent']==35
for e in jobs:
    source=raw/(e['id']+'.mp3')
    assert source.exists() and hashlib.sha256(source.read_bytes()).hexdigest()==e['sourceSha256'],'Original source missing or altered: '+e['id']
ffmpeg=imageio_ffmpeg.get_ffmpeg_exe()
report=[];previews={}
for e in jobs:
    source=raw/(e['id']+'.mp3')
    x=np.frombuffer(subprocess.check_output([ffmpeg,'-v','error','-i',str(source),'-ac','1','-ar',str(RATE),'-f','f32le','pipe:1']),dtype=np.float32).copy()
    assert len(x)>RATE*.3 and np.isfinite(x).all()
    rms=np.sqrt(np.mean(x[:len(x)//480*480].reshape(-1,480)**2,axis=1))
    active=np.flatnonzero(rms>max(.0008,float(rms.max())*.02));assert len(active)
    start=max(0,(active[0]-10)*480);end=min(len(x),(active[-1]+18)*480);x=x[start:end]
    x*=min(.105/max(1e-6,float(np.sqrt(np.mean(x*x)))),.78/max(1e-6,float(np.max(np.abs(x)))))
    fade=min(720,len(x)//2);x[:fade]*=np.linspace(0,1,fade);x[-fade:]*=np.linspace(1,0,fade)
    clean=output/(e['id']+'_clean.wav');sf.write(clean,x,RATE,subtype='PCM_16')
    sf.write(raw/(e['id']+'_dry.wav'),x,RATE,subtype='PCM_16')
    final=x
    if e['radio']:
        # Match existing radio colour, keeping consonants and tones clear.
        band=np.frombuffer(subprocess.check_output([ffmpeg,'-v','error','-i',str(clean),'-af','highpass=f=350,lowpass=f=3000','-f','f32le','pipe:1']),dtype=np.float32).copy()
        band*=min(.105/max(1e-6,float(np.sqrt(np.mean(band*band)))),.78/max(1e-6,float(np.max(np.abs(band)))))
        band=np.tanh(band*1.5)/1.5
        energy=np.convolve(np.abs(band),np.ones(480)/480,mode='same')
        noise=np.random.default_rng(1401).normal(0,.0021,len(band))
        final=band+noise*np.clip(.2+energy*6,.2,1)
        final[:fade]*=np.linspace(0,1,fade);final[-fade:]*=np.linspace(1,0,fade)
        sf.write(output/(e['id']+'_radio.wav'),final,RATE,subtype='PCM_16')
    final_path=output/(e['id']+('_radio' if e['radio'] else '_clean')+'.wav')
    assert np.isfinite(final).all() and .01<float(np.sqrt(np.mean(final*final)))<.2 and np.max(np.abs(final))<.99
    assert e['text']==unicodedata.normalize('NFC',e['text'])
    row={k:v for k,v in e.items() if k not in ('url','lastFetch','status','creditsRemaining')}
    row.update(duration=round(len(final)/RATE,5),sampleRate=RATE,channels=1,peak=float(np.max(np.abs(final))),
        rms=float(np.sqrt(np.mean(final*final))),sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
        cleanSha256=hashlib.sha256(clean.read_bytes()).hexdigest(),sha256=hashlib.sha256(final_path.read_bytes()).hexdigest(),
        language='vi',match='',status='validated',provider='Runway',provenance='Generated in the user-controlled workspace for this approved game task; original MP3 and clean PCM master retained.')
    report.append(row)
    previews.setdefault(e['group'],[]).extend([final,np.zeros(round(RATE*.35),dtype=np.float32)])
for group,parts in previews.items():sf.write(HERE/(group+'-preview.wav'),np.concatenate(parts),RATE,subtype='PCM_16')
receipt.update(entries=report)
(HERE/'voice-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(lines=len(report),runtimeFiles=len(list(output.glob('*.wav'))),seconds=round(sum(e['duration'] for e in report),2),radioLines=sum(e['radio'] for e in report),creditsSpent=35)))
