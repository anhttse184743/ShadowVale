from pathlib import Path
import json,subprocess
import numpy as np,soundfile as sf
from scipy.signal import butter,sosfilt
from full_game_audio import OUT,FF
sr=48000;rng=np.random.default_rng(231023);rows=[]
def save(name,a,description):
 a=np.asarray(a);a*=.7/max(abs(a).max(),.001);sf.write(OUT/(name+'.wav'),a,sr,subtype='PCM_16');rows.append(dict(name=name,source='Original procedural synthesis',license='CC0-1.0',description=description))
def noise(n,cut=900):return sosfilt(butter(2,cut,fs=sr,output='sos'),rng.normal(0,1,n))
t=np.arange(sr*24)/sr
save('bamboo',noise(len(t),1500)*(.25+.1*np.sin(t*.7)),'Soft wind/foliage rustle; synthesized, not a field recording.')
t=np.arange(sr*14)/sr;fade=np.sin(np.pi*t/t[-1])**2;phase=2*np.pi*np.cumsum(76+2*np.sin(t*.4))/sr
save('plane',fade*(.4*np.sin(phase)+.2*np.sin(phase*3)+noise(len(t),450)),'Distant propeller-like engine, synthesized; no historical aircraft identity claimed.')
t=np.arange(sr*2)/sr;env=sum(np.exp(-((t-p)/.035)**2) for p in [.1,.23,.38,.57,.8])
save('duck',noise(len(t),1800)*env,'Short wing-flap foley; synthesized rustling, no duck vocal imitation.')
t=np.arange(sr)/sr;save('crate',(np.sin(t*2*np.pi*96)*np.exp(-t*20)+noise(len(t),2200)*np.exp(-t*32)),'Wooden crate contact foley, synthesized impact.')
# Sparse original plucked-string melody, reserved for the promise scene.
a=np.zeros(sr*96)
for start,midi in [(0,60),(3,64),(7,67),(12,69),(18,67),(23,64),(30,62),(36,60),(44,64),(50,67),(58,69),(64,72),(72,69),(79,67),(86,64),(91,60)]:
 tt=np.arange(sr*4)/sr;f=440*2**((midi-69)/12);note=sum(np.sin(2*np.pi*f*k*tt)/k**2 for k in [1,2,3,4]);note*=np.exp(-tt*1.5)*(1-np.exp(-tt*80));i=round(start*sr);a[i:i+len(note)]+=note
save('hung_theme',a,'Original sparse solo plucked-string theme; procedural composition, not an existing song or authentic instrument recording.')
(OUT/'original-sfx.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
print('Original effects ready:',[x['name'] for x in rows])
