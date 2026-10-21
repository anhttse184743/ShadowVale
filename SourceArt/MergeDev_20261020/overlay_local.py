"""Three-way overlay of verified pre-existing local work into the trial merge."""
from pathlib import Path
import subprocess,json,shutil,hashlib
main=Path(r'D:/DO_AN_GAME/ShadowVale')
root=Path(r'C:/Users/Admin/.codex/worktrees/merge-dev-thinh-20261020/ShadowVale')
backup=main/'Temp/MergeDev_20261020_Before'
report=main/'Docs/Reviews/MergeDev_20261020'
manifest=json.loads((backup/'manifest.json').read_text(encoding='utf-8'))
temp=root/'Temp/LocalOverlay';temp.mkdir(parents=True,exist_ok=True)
def git(*args):return subprocess.check_output(['git','-c','core.autocrlf=false',*args],cwd=root)
rows=[]
for row in manifest['files']:
 rel=row['path'];src=backup/'Files'/rel;dst=root/rel
 if rel=='Assets/_Project/Scenes/Maps/Map 1.unity':continue # scene decision handled separately
 if row.get('deleted'):
  rows.append({'path':rel,'result':'deleted-needs-review'});continue
 assert hashlib.sha256(src.read_bytes()).hexdigest()==row['sha256'],rel
 dst.parent.mkdir(parents=True,exist_ok=True)
 if not row.get('tracked'):
  if dst.exists() and dst.read_bytes()!=src.read_bytes():
   rows.append({'path':rel,'result':'untracked-collision'});continue
  shutil.copy2(src,dst);continue
 base=git('show',manifest['head']+':'+rel)
 if dst.read_bytes().replace(b'\r\n',b'\n')==base.replace(b'\r\n',b'\n'):
  shutil.copy2(src,dst);rows.append({'path':rel,'result':'local-only'});continue
 (temp/'ours').write_bytes(src.read_bytes());(temp/'base').write_bytes(base);(temp/'theirs').write_bytes(dst.read_bytes())
 run=subprocess.run(['git','-c','core.autocrlf=false','merge-file','-p','--diff3','--ignore-space-change',str(temp/'ours'),str(temp/'base'),str(temp/'theirs')],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
 # merge-file has no whitespace option on older Git builds; normalize all inputs instead.
 if run.returncode==129:
  for name in ['ours','base','theirs']:
   p=temp/name;p.write_bytes(p.read_bytes().replace(b'\r\n',b'\n'))
  run=subprocess.run(['git','merge-file','-p','--diff3',str(temp/'ours'),str(temp/'base'),str(temp/'theirs')],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
 assert run.returncode>=0 and run.returncode<128,(rel,run.stderr.decode(errors='replace'))
 dst.write_bytes(run.stdout)
 rows.append({'path':rel,'result':'conflict' if run.returncode else 'merged','blocks':run.returncode})
(report/'local_overlay.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(rows,ensure_ascii=False,indent=2))
