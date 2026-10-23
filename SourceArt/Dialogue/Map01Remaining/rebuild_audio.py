"""Rebuild/check approved Map 1 WAVs offline. Never calls a speech provider."""
from pathlib import Path
import argparse, hashlib, io, json, os, subprocess, sys

HERE=Path(__file__).resolve().parent
if os.environ.get('SHADOWVALE_AUDIO_DEPS'):
    sys.path.insert(0,os.environ['SHADOWVALE_AUDIO_DEPS'])
import numpy as np
import soundfile as sf
import imageio_ffmpeg

args=argparse.ArgumentParser();args.add_argument('--check',action='store_true',help='Validate reproduced bytes without writing assets.')
check=args.parse_args().check
ROOT=HERE.parents[2];OUT=ROOT/'Assets/Resources/Dialogue/VI'
manifest=json.loads((HERE/'voice-manifest.json').read_text(encoding='utf-8'))
assert manifest['creditsSpent']==13 and len(manifest['entries'])==8
waves={}
for e in manifest['entries']:
    source=HERE/'Raw'/(e['id']+'.mp3')
    assert hashlib.sha256(source.read_bytes()).hexdigest()==e['sourceSha256'],'Original source changed: '+e['id']
    x=np.frombuffer(subprocess.check_output([imageio_ffmpeg.get_ffmpeg_exe(),'-v','error','-i',str(source),'-ac','1','-ar','48000','-f','f32le','pipe:1']),dtype=np.float32).copy()
    assert len(x)>14400 and np.isfinite(x).all()
    x*=min(.105/max(1e-6,float(np.sqrt(np.mean(x*x)))),.78/max(1e-6,float(np.max(np.abs(x)))))
    fade=min(720,len(x)//2);x[:fade]*=np.linspace(0,1,fade);x[-fade:]*=np.linspace(1,0,fade)
    buffer=io.BytesIO();sf.write(buffer,x,48000,format='WAV',subtype='PCM_16');data=buffer.getvalue()
    assert hashlib.sha256(data).hexdigest()==e['sha256'],'Rebuild differs: '+e['id']
    if check:
        assert hashlib.sha256((OUT/(e['id']+'.wav')).read_bytes()).hexdigest()==e['sha256']
    else:
        (OUT/(e['id']+'.wav')).write_bytes(data)
        (HERE/'Raw'/(e['id']+'_dry.wav')).write_bytes(data)
    waves[e['id']]=x
if not check:
    groups={'map1-rescue-preview.wav':[e['id'] for e in manifest['entries'][:4]],
            'map1-boat-preview.wav':['m1_board_invite_current','m1_depart_destination_question','m1_depart_destination_order','m1_depart_bank_reply'],
            'map1-all-eight-preview.wav':[e['id'] for e in manifest['entries']]}
    for name,ids in groups.items():
        parts=[]
        for id in ids:parts.extend([waves[id],np.zeros(16800,dtype=np.float32)])
        sf.write(HERE/name,np.concatenate(parts),48000,subtype='PCM_16')
print(json.dumps(dict(lines=8,mode='checked' if check else 'rebuilt',paidCalls=0)))
