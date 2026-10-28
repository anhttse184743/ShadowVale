import sys, pathlib, subprocess, math, random, wave, array
root=pathlib.Path(__file__).parent
sys.path.insert(0,str(root/'.deps'))
import imageio_ffmpeg
ff=imageio_ffmpeg.get_ffmpeg_exe()
random.seed(17)
sr=48000
samples=array.array('h')
wind=0.0
for i in range(sr*6):
 t=i/sr
 wind=0.995*wind+0.005*random.uniform(-1,1)
 v=wind*0.18
 for start,freq in [(0.35,2500),(0.65,2900),(1.7,2200),(2.0,2700),(4.6,2400),(5.1,2800)]:
  u=t-start
  if 0<u<0.15:
   v+=0.013*math.sin(math.pi*u/0.15)**2*math.sin(2*math.pi*(freq*u+900*u*u))
 if 2.1<t<2.32:
  u=t-2.1
  v+=0.006*math.sin(math.pi*u/0.22)**2*(math.sin(2*math.pi*120*t)+random.uniform(-0.4,0.4))
 # A quiet receiver contact and fabric rustle, aligned after visual inspection.
 if 3.4<t<3.5:
  u=t-3.4
  v+=0.04*math.exp(-u*65)*(0.6*math.sin(2*math.pi*370*u)+0.4*random.uniform(-1,1))
 fade=min(1,t/0.2,(6-t)/0.3)
 samples.append(int(max(-1,min(1,v*fade))*32767))
with wave.open(str(root/'morning_ambience_v2.wav'),'wb') as w:
 w.setnchannels(1);w.setsampwidth(2);w.setframerate(sr);w.writeframes(samples.tobytes())
# 81 source frames at 16 fps; retime to 6 seconds and deliver exactly 144 frames.
vf="setpts=PTS*1.185185185,scale=1920:1108:flags=lanczos,crop=1920:1080,setsar=1,fps=24,zoompan=z='1+0.16*(1-pow(1-min(on/48,1),3))':x='(iw-iw/zoom)*0.65':y='(ih-ih/zoom)*0.35':d=1:s=1920x1080:fps=24,tpad=stop_mode=clone:stop_duration=1"
subprocess.run([ff,'-y','-i',str(root/'shot01_v2_raw.mp4'),'-i',str(root/'morning_ambience_v2.wav'),'-vf',vf,'-frames:v','144','-t','6','-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-c:a','aac','-b:a','160k','-movflags','+faststart',str(root/'shot01_v2_1080p_24fps.mp4')],check=True)
subprocess.run([ff,'-y','-i',str(root/'shot01_v2_1080p_24fps.mp4'),'-vf',"select=eq(n\\,143)",'-frames:v','1',str(root/'shot01_v2_final_frame.png')],check=True)
