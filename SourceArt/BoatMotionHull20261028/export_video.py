from pathlib import Path
import csv,subprocess,sys
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('Logs/BoatMotionHull_20261028')
ff=Path(sys.argv[2]) if len(sys.argv)>2 else Path('ffmpeg')
for take in ['Before','After']:
 folder=root/take
 if not (folder/'motion.csv').exists():continue
 cutoff=(folder/'Map1-Approach-0.png').stat().st_mtime-1
 rows=list(csv.DictReader((folder/'motion.csv').open(encoding='utf-8-sig')))
 last={}
 for row in rows:last[int(row['frame'])]=row
 for name,phases in [('Map1',{'Map1-Boarding','Map1-Seating','Map1-Departing'}),('Map2',{'Map2-Approaching','Map2-Mooring'})]:
  chosen=[]
  for image in sorted((folder/'frames').glob('frame-*.png')):
   if image.stat().st_mtime<cutoff:continue
   frame=int(image.stem.split('-')[1]);row=last.get(frame)
   if row is not None and row['phase'] in phases:chosen.append((image,row))
  if not chosen:continue
  lines=[]
  for i,(image,row) in enumerate(chosen):
   duration=.083333
   if i+1<len(chosen) and chosen[i+1][1]['phase']==row['phase']:duration=max(.025,min(.5,float(chosen[i+1][1]['clock'])-float(row['clock'])))
   lines.extend(["file '"+image.as_posix()+"'",f'duration {duration:.6f}'])
  lines.append("file '"+chosen[-1][0].as_posix()+"'")
  lst=folder/(name+'-frames.txt');lst.write_text('\n'.join(lines)+'\n',encoding='utf-8')
  out=folder/(name+'-'+take+'.mp4')
  result=subprocess.run([str(ff),'-y','-v','error','-f','concat','-safe','0','-i',str(lst),'-fps_mode','vfr','-c:v','libx264','-preset','fast','-threads','2','-crf','23','-pix_fmt','yuv420p','-movflags','+faststart',str(out)],capture_output=True)
  print(take,name,len(chosen),'frames',result.returncode,'bytes',out.stat().st_size if out.exists() else 0,result.stderr.decode()[-300:])
