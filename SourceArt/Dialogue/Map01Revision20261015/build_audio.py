from pathlib import Path
import concurrent.futures, hashlib, json, subprocess, sys
H=Path(__file__).resolve().parent
sys.path.insert(0,str(H.parent/'voice-deps'))
import numpy as np
import soundfile as sf
import imageio_ffmpeg
req=json.loads((H/'request.json').read_text(encoding='utf-8-sig'))
jobs=json.loads((H/'jobs.json').read_text(encoding='utf-8-sig'))['jobs']
retry=json.loads((H/'retry-result.json').read_text(encoding='utf-8-sig'))
entries=[e for e in req['entries'] if e['id']!='m1_v2_rescue_return_04']
for e in entries:
    if e['id']=='m1_v2_radio_report_02':
        e['text']='Chuyển tới điểm ba. Xác nhận tên sĩ quan đang điều quân.'
        e['caption']='Chỉ huy (bộ đàm): '+e['text']
        e['result']=retry
    else:e['result']=next(j for j in jobs if j['id']==e['id'])
(H/'Raw').mkdir(exist_ok=True);(H/'VI').mkdir(exist_ok=True)
def download(e):
    out=H/'Raw'/(e['id']+'.mp3')
    assert e['result']['kind']=='audio'
    if not out.exists():
        subprocess.run(['curl.exe','--fail','--location','--silent','--show-error','--tlsv1.2','--tls-max','1.2','--http1.1','--retry','6','--retry-all-errors','--retry-delay','1','-o',str(out),e['result']['url']],check=True)
    assert out.stat().st_size>2000
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as p:list(p.map(download,entries))
rate=48000; waves={}; manifest=[]
for e in entries:
    src=H/'Raw'/(e['id']+'.mp3')
    x=np.frombuffer(subprocess.check_output([imageio_ffmpeg.get_ffmpeg_exe(),'-v','error','-i',str(src),'-ac','1','-ar',str(rate),'-f','f32le','pipe:1']),dtype=np.float32).copy()
    assert len(x)>rate*.3 and np.isfinite(x).all()
    x*=min(.105/max(1e-6,float(np.sqrt(np.mean(x*x)))),.78/max(1e-6,float(np.max(np.abs(x)))))
    n=min(480,len(x)//2);x[:n]*=np.linspace(0,1,n);x[-n:]*=np.linspace(1,0,n)
    sf.write(H/'Raw'/(e['id']+'_dry.wav'),x,rate,subtype='PCM_16')
    sf.write(H/'VI'/(e['id']+'_clean.wav'),x,rate,subtype='PCM_16')
    y=x
    if e['radio']:
        # Gentle band limiting, soft saturation and low hiss; only this mix plays remotely.
        freq=np.fft.rfftfreq(len(x),1/rate)
        gain=(freq/np.sqrt(freq*freq+350**2))*(3000/np.sqrt(freq*freq+3000**2))
        y=np.fft.irfft(np.fft.rfft(x)*gain,n=len(x)).astype(np.float32)
        y=np.tanh(y*1.35)/1.35
        y*=.105/max(1e-6,float(np.sqrt(np.mean(y*y))))
        rng=np.random.default_rng(312); noise=rng.normal(0,.0021,len(y)).astype(np.float32)
        y=np.clip(y+noise,-.9,.9);y[:n]*=np.linspace(0,1,n);y[-n:]*=np.linspace(1,0,n)
        sf.write(H/'VI'/(e['id']+'_radio.wav'),y,rate,subtype='PCM_16')
    row={k:v for k,v in e.items() if k!='result'}
    row.update(taskId=e['result']['taskId'],duration=round(len(x)/rate,5),sampleRate=rate,channels=1,
               provider='Runway',sourceSha256=hashlib.sha256(src.read_bytes()).hexdigest(),
               sha256=hashlib.sha256((H/'VI'/(Path(e['resource']).name+'.wav')).read_bytes()).hexdigest(),
               pronunciationReview='pending human audition',status='bytes_validated')
    waves[e['id']]=y;manifest.append(row)
for group in sorted({e['group'] for e in entries}):
    parts=[]
    for e in entries:
        if e['group']==group:parts.extend([waves[e['id']],np.zeros(16800,dtype=np.float32)])
    if group in ('rescue_return','commander_confirm'):parts.extend([waves['m1_v2_rescue_approach_03'],np.zeros(16800)])
    sf.write(H/(group+'-preview.wav'),np.concatenate(parts),rate,subtype='PCM_16')
(H/'voice-manifest.json').write_text(json.dumps(dict(approvedCeiling=23,reportedBalanceBefore=360,reportedBalanceAfter=338,creditsUsed=22,entries=manifest,aliases={'m1_v2_rescue_return_04':'m1_v2_rescue_approach_03'}),ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(takes=len(manifest),creditsUsed=22,seconds=round(sum(e['duration'] for e in manifest),2))))

