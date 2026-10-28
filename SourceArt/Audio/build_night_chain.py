"""Deterministic distant metal-track cue; no speech or third-party recording."""
from pathlib import Path
import wave
import numpy as np

rate=24000
duration=3.8
t=np.arange(int(rate*duration))/rate
rng=np.random.default_rng(214)
y=np.zeros_like(t)
for onset in np.arange(.12,3.3,.19):
    age=t-onset
    env=np.where(age>=0,np.exp(-np.maximum(age,0)/.19),0)
    ring=sum(np.sin(2*np.pi*f*age+rng.uniform(0,6.28))*gain for f,gain in [(173,.3),(367,.24),(641,.18),(1103,.09)])
    y+=env*(ring+.12*rng.standard_normal(len(t)))
scrape=rng.standard_normal(len(t))
scrape=np.convolve(scrape,np.ones(17)/17,mode='same')
y+=scrape*.65*(.6+.4*np.sin(2*np.pi*5.3*t))
fade=np.minimum(1,t/.35)*np.minimum(1,(duration-t)/.65)
y*=fade
y=np.convolve(y,np.ones(5)/5,mode='same')
y=y/max(1,np.max(np.abs(y)))*.7
output=Path(__file__).resolve().parents[2]/'Assets/Resources/OperationsAudio/night2_chain.wav'
output.parent.mkdir(parents=True,exist_ok=True)
with wave.open(str(output),'wb') as f:
    f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes((y*32767).astype('<i2').tobytes())
print(f'{output.name}: {duration:.1f}s, mono PCM16, {rate} Hz')
