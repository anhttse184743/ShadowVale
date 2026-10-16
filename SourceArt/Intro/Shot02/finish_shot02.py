import sys,pathlib,subprocess,random,math,array,wave
root=pathlib.Path(__file__).parent
sys.path.insert(0,str(root.parent/'Shot01'/'.deps'))
import imageio_ffmpeg as ff
exe=ff.get_ffmpeg_exe()
random.seed(23); sr=48000; data=array.array('h'); wind=0
for i in range(8*sr):
 t=i/sr; wind=.995*wind+.005*random.uniform(-1,1);v=.12*wind
 for start,f in [(.2,2400),(1.1,2700),(3.7,2200),(6.8,2600),(7.1,2300)]:
  u=t-start
  if 0<u<.13:v+=.009*math.sin(math.pi*u/.13)**2*math.sin(2*math.pi*(f*u+600*u*u))
 for start in [.35,.85,1.35]:
  u=t-start
  if 0<u<.12:v+=.022*math.exp(-u*35)*(math.sin(2*math.pi*95*u)+.4*random.uniform(-1,1))
 u=t-1.9
 if 0<u<.12:v+=.025*math.exp(-u*45)*(math.sin(2*math.pi*460*u)+.3*random.uniform(-1,1))
 v*=min(1,t/.15,(8-t)/.2)
 data.append(int(max(-1,min(1,v))*32767))
with wave.open(str(root/'ambience_effects.wav'),'wb') as w:
 w.setnchannels(1);w.setsampwidth(2);w.setframerate(sr);w.writeframes(data.tobytes())
vf='scale=1920:1080:force_original_aspect_ratio=increase:flags=lanczos,crop=1920:1080,setsar=1,fps=24,tpad=stop_mode=clone:stop_duration=1'
out=root/'shot02_1080p_24fps_dubbing_ready.mp4'
subprocess.run([exe,'-hide_banner','-loglevel','error','-y','-i',str(root/'shot02_raw.mp4'),'-i',str(root/'ambience_effects.wav'),'-map','0:v:0','-map','1:a:0','-vf',vf,'-frames:v','192','-t','8','-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-c:a','aac','-b:a','160k','-movflags','+faststart',str(out)],check=True)
subprocess.run([exe,'-hide_banner','-loglevel','error','-y','-i',str(out),'-vf','select=eq(n\\,191)','-frames:v','1','-update','1',str(root/'shot02_final_frame.png')],check=True)
print('Frames and seconds',ff.count_frames_and_secs(str(out)))
r=ff.read_frames(str(out));print(next(r));r.close()
