"""Local, original UI symbols and short prop/device sounds; no speech/provider calls."""
from pathlib import Path
from PIL import Image, ImageDraw
import math, random, wave, struct, json, hashlib
ROOT=Path(__file__).resolve().parents[2]
UI=ROOT/'Assets/Resources/OperationsUI';SFX=ROOT/'Assets/Resources/OperationsAudio'
UI.mkdir(parents=True,exist_ok=True);SFX.mkdir(parents=True,exist_ok=True)
for kind in ['key','cargo']:
    im=Image.new('RGBA',(512,512));d=ImageDraw.Draw(im);gold=(246,215,139,255)
    if kind=='key':
        d.ellipse((60,55,255,250),outline=gold,width=30);d.line((218,220,431,433),fill=gold,width=35)
        d.line((337,338,391,284),fill=gold,width=34);d.line((387,389,441,335),fill=gold,width=34)
    else:
        d.rounded_rectangle((77,165,435,425),radius=35,outline=gold,width=24);d.arc((177,68,335,245),180,360,fill=gold,width=23)
        d.rounded_rectangle((160,242,352,370),radius=16,outline=gold,width=15);d.line((131,169,131,420),fill=gold,width=12);d.line((383,169,383,420),fill=gold,width=12)
    im.resize((128,128),Image.Resampling.LANCZOS).save(UI/(kind+'.png'))
random.seed(241);rate=24000
def write(name,duration,fn):
    n=int(duration*rate);data=[]
    for i in range(n):
        t=i/rate;fade=min(1,t/.008,(duration-t)/.035);v=max(-.9,min(.9,fn(t)*fade));data.append(int(v*32767))
    with wave.open(str(SFX/(name+'.wav')),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes(struct.pack('<'+'h'*n,*data))
write('lock_turn',.32,lambda t:(random.random()*2-1)*.18*math.exp(-24*(t%.08))+.09*math.sin(2*math.pi*1120*t)*math.exp(-35*t))
write('door_open',.65,lambda t:(random.random()*2-1)*.055+.045*math.sin(2*math.pi*(120+40*t)*t))
write('chest_open',.65,lambda t:.035*math.sin(2*math.pi*(170+30*t)*t)+(random.random()*2-1)*.05*math.sin(math.pi*t/.65)**2)
write('bag_cloth',.6,lambda t:(random.random()*2-1)*.08*math.sin(math.pi*t/.6)**2)
write('radio_alarm',1.3,lambda t:.12*math.sin(2*math.pi*(740+120*math.sin(2*math.pi*3*t))*t)*(1 if t%.35<.23 else 0))
write('radio_off',.45,lambda t:(random.random()*2-1)*.07*(1-t/.45)+.04*math.sin(2*math.pi*(500-350*t)*t))
files=list(UI.glob('*.png'))+list(SFX.glob('*.wav'))+list((ROOT/'Assets/_Project/Art/Environment/Map01Operations').glob('*.fbx'))
(Path(__file__).parent/'asset-receipt.json').write_text(json.dumps({'source':'Original local procedural authoring; no paid provider','units':'meters','props_blend':'SourceArt/Map01Operations/Map01Operations.blend','character_rigs_changed':False,'files':[{'path':str(p.relative_to(ROOT)).replace('\\','/'),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in files]},ensure_ascii=False,indent=2),encoding='utf-8')
print('UI, SFX and asset receipt generated.')
