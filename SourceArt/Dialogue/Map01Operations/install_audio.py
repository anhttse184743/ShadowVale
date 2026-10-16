"""Download approved jobs, process clean/radio takes, and install exact-caption cues.
No generation calls. Run with --deps pointing to local numpy/soundfile/imageio_ffmpeg.
"""
from pathlib import Path
import argparse, concurrent.futures, hashlib, json, shutil, subprocess, sys
p=argparse.ArgumentParser();p.add_argument('--deps',required=True);p.add_argument('--results',type=Path);args=p.parse_args();sys.path.insert(0,args.deps)
import numpy as np
import soundfile as sf
import imageio_ffmpeg
H=Path(__file__).resolve().parent;ROOT=H.parents[2]
request=json.loads((H/'approved-request-20261016.json').read_text(encoding='utf-8-sig'))
results={r['id']:r for r in json.loads((args.results or H/'results-20261016.json').read_text(encoding='utf-8-sig'))['results']}
assert sum(e['estimatedCredits'] for e in request['lines'])<=request['authorizedCeiling']==30
for sub in ('Raw','VI'):(H/sub).mkdir(exist_ok=True)
runtime=ROOT/'Assets/Resources/Dialogue/VI';runtime.mkdir(parents=True,exist_ok=True)
def download(e):
    r=results[e['id']];assert r['kind']=='audio'
    out=H/'Raw'/(e['id']+'.mp3')
    if not out.exists() or out.stat().st_size<2000:
        if not r.get('url'):raise RuntimeError('Missing original MP3. Refresh the signed download receipt by task ID and pass --results; do not generate again.')
        try:subprocess.run(['curl.exe','--fail','--location','--silent','--show-error','--tlsv1.2','--tls-max','1.2','--http1.1','--retry','4','--retry-all-errors','-o',str(out),r['url']],check=True)
        except subprocess.CalledProcessError:raise RuntimeError('Download failed for '+e['id']) from None
    assert out.stat().st_size>2000
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:list(pool.map(download,request['lines']))
previous=json.loads((H/'voice-manifest.json').read_text(encoding='utf-8-sig')) if (H/'voice-manifest.json').exists() else {}
reviews={e['id']:e for e in previous.get('lines',[])}
rate=48000;entries=[];waves={};checks=[]
for e in request['lines']:
    src=H/'Raw'/(e['id']+'.mp3')
    x=np.frombuffer(subprocess.check_output([imageio_ffmpeg.get_ffmpeg_exe(),'-v','error','-i',str(src),'-ac','1','-ar',str(rate),'-f','f32le','pipe:1']),dtype=np.float32).copy()
    assert len(x)>rate*.3 and np.isfinite(x).all()
    rms=float(np.sqrt(np.mean(x*x)));assert rms>.0001
    x*=min(.105/rms,.78/max(1e-6,float(np.max(np.abs(x)))))
    n=min(480,len(x)//2);x[:n]*=np.linspace(0,1,n);x[-n:]*=np.linspace(1,0,n)
    sf.write(H/'VI'/(e['id']+'_clean.wav'),x,rate,subtype='PCM_16')
    y=x.copy()
    if e['radio']:
        # Wide gentle radio band preserves consonants and Vietnamese tonal cues.
        freq=np.fft.rfftfreq(len(x),1/rate)
        gain=(freq/np.sqrt(freq*freq+220**2))*(4200/np.sqrt(freq*freq+4200**2))
        y=np.fft.irfft(np.fft.rfft(x)*gain,n=len(x)).astype(np.float32)
        y=np.tanh(y*1.15)/1.15;y*=.105/max(1e-6,float(np.sqrt(np.mean(y*y))))
        y+=np.random.default_rng(241).normal(0,.001,len(y)).astype(np.float32)
        y=np.clip(y,-.88,.88);y[:n]*=np.linspace(0,1,n);y[-n:]*=np.linspace(1,0,n)
        sf.write(H/'VI'/(e['id']+'_radio.wav'),y,rate,subtype='PCM_16')
    for wav in (H/'VI').glob(e['id']+'_*.wav'):shutil.copyfile(wav,runtime/wav.name)
    row=dict(e);row.update(language='vi',match='',model='eleven_v3',speed=.9,
        taskId=results[e['id']]['taskId'],duration=round(len(x)/rate,5),sampleRate=rate,channels=1,
        provider='Runway',sourceSha256=hashlib.sha256(src.read_bytes()).hexdigest(),
        sha256=hashlib.sha256((H/'VI'/(Path(e['resource']).name+'.wav')).read_bytes()).hexdigest(),
        voiceStatus='installed_existing_cast',pronunciationReview='pending human listening review')
    if e['id'] in reviews and reviews[e['id']].get('text')==e['text'] and 'user approved' in reviews[e['id']].get('pronunciationReview',''):
        row['pronunciationReview']=reviews[e['id']]['pronunciationReview']
    entries.append(row);waves[e['id']]=y
    checks.append(dict(id=e['id'],duration=row['duration'],peak=float(np.max(np.abs(y))),rms=float(np.sqrt(np.mean(y*y))),caption=e['caption']))
for path in [ROOT/'Assets/Resources/Dialogue/voice-catalog.json',ROOT/'SourceArt/Dialogue/voice-catalog.json']:
    catalog=json.loads(path.read_text(encoding='utf-8-sig'));ids={e['id'] for e in entries}
    backup=H/(path.parent.name+'-catalog-before-20261016.json')
    if not backup.exists():shutil.copyfile(path,backup)
    catalog['entries']=[e for e in catalog['entries'] if e['id'] not in ids]+entries
    path.write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8')
groups={'supply-order':['m1_ops_supply_order_01','m1_ops_supply_order_02','m1_ops_supply_order_03'],
        'handoff':['m1_ops_handoff_01','m1_ops_handoff_02','m1_ops_handoff_03','m1_ops_handoff_04'],
        'radio-report':['m1_ops_radio_report_01','m1_ops_radio_report_02'],
        'officer-report':['m1_ops_commander_report_01','m1_ops_commander_report_02'],
        'retreat':['m1_ops_retreat_01','m1_ops_retreat_02','m1_ops_retreat_03']}
for name,ids in groups.items():
    sf.write(H/(name+'-preview.wav'),np.concatenate([part for cue in ids for part in [waves[cue],np.zeros(16800,dtype=np.float32)]]),rate,subtype='PCM_16')
(H/'voice-manifest.json').write_text(json.dumps(dict(scriptApproved=True,newVoiceApproved=False,existingCastApproved=True,
    paidGenerationAuthorized=True,approvedCeiling=30,creditsSpent=30,pronunciationApproval=previous.get('pronunciationApproval',{}),lines=entries),ensure_ascii=False,indent=2),encoding='utf-8')
(H/'audio-validation.json').write_text(json.dumps(dict(cues=len(entries),checks=checks,pronunciation='Awaiting human listening approval; signal checks are not pronunciation checks.'),ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(installed=len(entries),credits=30,seconds=round(sum(e['duration'] for e in entries),2))))
