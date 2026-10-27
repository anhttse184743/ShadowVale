from pathlib import Path
import urllib.request,re,json,hashlib,subprocess,wave,uuid,os
import numpy as np
ROOT=Path(os.environ.get('SVL_GAME_ROOT',Path(__file__).resolve().parents[3]))
WORK=Path(os.environ.get('SVL_AUDIO_WORKSPACE',Path.cwd()))
FF=Path(os.environ.get('SVL_FFMPEG',WORK/'work/voice-deps/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'))
OUT=ROOT/'SourceArt/Audio/Map01_Ambience_20261026'
RES=ROOT/'Assets/Resources/Map01Ambience'
OUT.mkdir(parents=True,exist_ok=True);RES.mkdir(parents=True,exist_ok=True)
records=[]
for name,user,num in [('wind','Yoyodaman234',335891),('birds','Lizardhood',427040)]:
    page=f'https://freesound.org/people/{user}/sounds/{num}/'
    html=urllib.request.urlopen(urllib.request.Request(page,headers={'User-Agent':'Mozilla/5.0'}),timeout=60).read().decode()
    assert 'creativecommons.org/publicdomain/zero/1.0' in html
    (OUT/f'{name}-license.html').write_text(html,encoding='utf-8')
    url=re.search(r'https://cdn\.freesound\.org/previews/[^\s"<>]+-hq\.mp3',html).group(0)
    dest=OUT/f'{name}-source.mp3'
    if not dest.exists():dest.write_bytes(urllib.request.urlopen(url,timeout=60).read())
    records.append(dict(name=name,creator=user,page=page,download=url,license='CC0 1.0',licenseUrl='https://creativecommons.org/publicdomain/zero/1.0/',sha256=hashlib.sha256(dest.read_bytes()).hexdigest()))

def decode(name,start,duration,filters):
    raw=subprocess.check_output([str(FF),'-v','error','-ss',str(start),'-i',str(OUT/f'{name}-source.mp3'),'-t',str(duration),'-af',filters,'-ar','48000','-ac','1','-f','f32le','-'])
    return np.frombuffer(raw,dtype='<f4').copy()
def write(name,x,rms):
    x=x*(rms/max(float(np.sqrt(np.mean(x*x))),1e-6));x=np.clip(x,-.65,.65)
    with wave.open(str(RES/f'{name}.wav'),'wb') as w:w.setparams((1,2,48000,0,'NONE','not compressed'));w.writeframes((x*32767).astype('<i2').tobytes())
    return dict(file=name+'.wav',seconds=len(x)/48000,rms=float(np.sqrt(np.mean(x*x))),peak=float(np.max(np.abs(x))),sha256=hashlib.sha256((RES/f'{name}.wav').read_bytes()).hexdigest())
x=decode('wind',38,48,'highpass=f=170,lowpass=f=5500,acompressor=threshold=0.08:ratio=3:attack=40:release=350')
n=48000*3;t=np.linspace(0,1,n,dtype=np.float32)
x=np.concatenate([x[n:-n],x[-n:]*(1-t)+x[:n]*t])
assets=[write('wind_leaves',x,.085)]
for i,start in enumerate([1,8,15]):
    x=decode('birds',start,5,'highpass=f=1000,lowpass=f=8500');n=12000;x[:n]*=np.linspace(0,1,n);x[-n:]*=np.linspace(1,0,n)
    assets.append(write(f'birds_{i+1}',x,.10))
(OUT/'sources.json').write_text(json.dumps(dict(sources=records,assets=assets,processing='48 kHz mono PCM16; filtered, quiet RMS; wind end/start overlapped 3 seconds; bird excerpts faded. Species/location not asserted.',checked='2026-10-26'),ensure_ascii=False,indent=2),encoding='utf-8')
