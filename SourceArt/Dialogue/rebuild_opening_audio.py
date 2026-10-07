"""Rebuild the opening with the same Tom/Brodie voices and processing as later dialogue."""
from pathlib import Path
import json,subprocess,hashlib
import numpy as np,soundfile as sf,imageio_ffmpeg
ART=Path(__file__).resolve().parent
ROOT=ART.parents[1];RAW=ART/'Raw';OUT=ROOT/'Assets/Resources/Dialogue/VI'
RATE=48000;GAP=.25
LINES=[
 ('m1_open_order','Tom','Đồng chí ra bến tàu phía Bắc nhận tiếp tế cho đơn vị.'),
 ('m1_open_supplies','Tom','Kiểm tra lương thực, thuốc men và trang bị. Phối hợp bốc dỡ, đưa hàng về điểm tập kết.'),
 ('m1_open_hung','Tom','Hùng chưa trở về từ bến tàu. Nếu gặp anh ấy, đưa về căn cứ an toàn. Có trở ngại, báo ngay cho tôi!'),
 ('m1_open_reply','Brodie','Rõ! Tôi sẽ hoàn thành nhiệm vụ!')]
def process(path):
 x=np.frombuffer(subprocess.check_output([imageio_ffmpeg.get_ffmpeg_exe(),'-v','error','-i',str(path),'-ac','1','-ar',str(RATE),'-f','f32le','pipe:1']),dtype=np.float32).copy()
 block=480;n=len(x)//block;rms=np.sqrt(np.mean(x[:n*block].reshape(n,block)**2,axis=1))
 active=np.flatnonzero(rms>max(.0008,float(np.max(rms))*.02));assert len(active)>0
 x=x[max(0,(active[0]-10)*block):min(len(x),(active[-1]+17)*block)]
 rms=float(np.sqrt(np.mean(x*x)));peak=float(np.max(np.abs(x)))
 x*=min(.105/max(rms,.000001),.78/max(peak,.000001))
 fade=min(720,len(x)//2);x[:fade]*=np.linspace(0,1,fade);x[-fade:]*=np.linspace(1,0,fade)
 return x
OUT.mkdir(parents=True,exist_ok=True)
waves=[];entries=[]
for id,voice,text in LINES:
 x=process(RAW/(id+'.mp3'));waves.append(x)
 sf.write(OUT/(id+'.wav'),x,RATE,subtype='PCM_16')
 sf.write(RAW/(id+'_dry.wav'),x,RATE,subtype='PCM_16')
 entries.append(dict(id=id,voice=voice,text=text,language='vi',model='eleven_v3',speed=.9 if id=='m1_open_reply' else 1,radio=False,duration=len(x)/RATE,
  resource='Dialogue/VI/'+id,peak=float(np.max(np.abs(x))),rms=float(np.sqrt(np.mean(x*x))),sha256=hashlib.sha256((OUT/(id+'.wav')).read_bytes()).hexdigest()))
starts=[0,len(waves[0])/RATE+GAP,(len(waves[0])+len(waves[1]))/RATE+2*GAP]
commander=np.concatenate([waves[0],np.zeros(round(RATE*GAP)),waves[1],np.zeros(round(RATE*GAP)),waves[2]])
sf.write(OUT/'m1_open_commander.wav',commander,RATE,subtype='PCM_16')
sf.write(ART/'opening-voice-preview.wav',np.concatenate([commander,np.zeros(round(RATE*.5)),waves[3]]),RATE,subtype='PCM_16')
(ART/'opening-voice-timing.json').write_text(json.dumps(dict(commanderSubtitleStarts=starts,commanderDuration=len(commander)/RATE,replyDuration=len(waves[3])/RATE,entries=entries),ensure_ascii=False,indent=2),encoding='utf-8')
print('Opening ready:',len(commander)/RATE,'seconds commander;',len(waves[3])/RATE,'seconds Nam; subtitle starts',starts)
