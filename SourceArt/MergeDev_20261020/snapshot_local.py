"""Copy existing local work before integration; never removes or alters a source file."""
from pathlib import Path
import subprocess,json,hashlib,shutil,datetime

root=Path('D:/DO_AN_GAME/ShadowVale')
out=root/'Temp/MergeDev_20261020_Before'
assert not out.exists(),f'Snapshot already exists: {out}'
out.mkdir(parents=True)
def git(*args):return subprocess.check_output(['git',*args],cwd=root)
def paths(*args):return [p for p in git(*args).decode('utf-8').split('\0') if p]
def digest(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
 return h.hexdigest()

tracked=set(paths('diff','--name-only','-z'))|set(paths('diff','--cached','--name-only','-z'))
untracked=set(paths('ls-files','--others','--exclude-standard','-z'))
protected={'Assets/_Project/Scenes/Maps/Map 1.unity'}
rows=[]
for i,rel in enumerate(sorted(tracked|untracked|protected)):
 src=root/rel
 if not src.exists():rows.append({'path':rel,'deleted':True});continue
 dst=out/'Files'/rel;dst.parent.mkdir(parents=True,exist_ok=True)
 before=digest(src);shutil.copy2(src,dst)
 assert before==digest(dst),f'Copy differs: {rel}'
 assert before==digest(src),f'Source changed during snapshot: {rel}'
 rows.append({'path':rel,'sha256':before,'bytes':src.stat().st_size,'mtime_ns':src.stat().st_mtime_ns,'tracked':rel in tracked})
 if i%500==0:print('SNAPSHOT',i,flush=True)
(out/'tracked.patch').write_bytes(git('diff','--binary'))
(out/'index.patch').write_bytes(git('diff','--cached','--binary'))
observed=[]
save_root=Path('C:/Users/Admin/AppData/LocalLow/DefaultCompany/ShadowVale')
for name in ('shadowvale-slot-0.json','shadowvale-slot-1.json','shadowvale-slot-4.json','shadowvale-map01-checkpoint.json'):
 src=save_root/name
 if src.exists():
  dst=out/'ObservedPlayerSaves'/name;dst.parent.mkdir(exist_ok=True);shutil.copy2(src,dst)
  observed.append({'path':str(src),'sha256':digest(src),'mtime_ns':src.stat().st_mtime_ns})
manifest={'time':datetime.datetime.now().isoformat(),'head':git('rev-parse','HEAD').decode().strip(),
 'dev':git('rev-parse','origin/dev').decode().strip(),'files':rows,'observed_player_saves':observed}
(out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'snapshot':str(out),'files':len(rows),'bytes':sum(r.get('bytes',0) for r in rows),'saves':len(observed)},indent=2),flush=True)
