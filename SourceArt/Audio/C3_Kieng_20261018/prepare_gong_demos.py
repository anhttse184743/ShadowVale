from pathlib import Path
import subprocess,json,hashlib,wave
import numpy as np
p=Path(__file__).resolve().parent;out=p/'GongAuditions';out.mkdir(exist_ok=True)
ff=Path(r'C:\Users\dangk\Documents\Codex\2026-09-20\du\work\voice-deps\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe')
rate=48000
sources=[{'file':'Gong_547296_public_preview.mp3','creator':'Department64','title':'MetalGong 12.wav','page':'https://freesound.org/people/Department64/sounds/547296/','download':'https://cdn.freesound.org/previews/547/547296_861714-hq.mp3'}, {'file':'Gong_487837_public_preview.mp3','creator':'BenDrain','title':'Metal_Pipe_04.wav','page':'https://freesound.org/people/BenDrain/sounds/487837/','download':'https://cdn.freesound.org/previews/487/487837_4763661-hq.mp3'}]
def read(s):
    x=np.frombuffer(subprocess.check_output([str(ff),'-v','error','-i',str(p/s['file']),'-ac','1','-ar',str(rate),'-af','highpass=f=70,lowpass=f=8000','-f','f32le','pipe:1']),dtype='<f4').copy()
    assert np.isfinite(x).all() and len(x)>rate*.3
    # Isolate the strongest impact. These are public compressed previews, not the original WAV.
    peak=int(np.argmax(np.abs(x)));start=max(0,peak-int(.015*rate));end=min(len(x),start+int(2.2*rate))
    x=x[start:end];x*=.6/max(float(np.max(np.abs(x))),.00001)
    fade=min(2400,len(x)//2);x[:96]*=np.linspace(0,1,96);x[-fade:]*=np.linspace(1,0,fade)
    s.update(license='CC0 1.0',licenseUrl='https://creativecommons.org/publicdomain/zero/1.0/',sha256=hashlib.sha256((p/s['file']).read_bytes()).hexdigest(),decodedCropSeconds=[start/rate,end/rate],actualListeningPerformed=False,sourceType='public MP3 preview of a recorded metal impact')
    return x
a,b=map(read,sources)
n=max(len(a),len(b));mix=np.zeros(n,dtype=np.float32);mix[:len(a)]+=.75*a;mix[:len(b)]+=.25*b
report=[]
for name,hit in [('A_Kim_loai_tam',a),('B_Ong_kim_loai',b),('C_Phoi_hai_chat_lieu',mix)]:
    x=np.zeros(int(1.3*rate)+len(hit)+int(.2*rate),dtype=np.float32)
    for sec,gain in [(0,1),(.65,.90),(1.3,.95)]:
        start=round(sec*rate);x[start:start+len(hit)]+=hit*gain
    x*=.78/max(float(np.max(np.abs(x))),.00001)
    target=out/(name+'.wav')
    with wave.open(str(target),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes((x*32767).astype('<i2').tobytes())
    report.append({'file':target.name,'duration':len(x)/rate,'strikeScheduleSeconds':[0,.65,1.3],'sampleRate':rate,'channels':1,'peak':float(np.max(np.abs(x))),'sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
(p/'Gong_Provenance.json').write_text(json.dumps({'sources':sources,'processing':'mono 48k; highpass70 lowpass8000; strongest impact crop <=2.2s; three overlapping strikes; peak normalization; no AI generation','demos':report,'notHistoricalVietnameseGongRecording':True,'integrated':False,'userSelection':'pending'},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
