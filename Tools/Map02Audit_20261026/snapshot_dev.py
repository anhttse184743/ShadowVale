from pathlib import Path
import subprocess,json,hashlib
root=Path(__file__).resolve().parents[2];out=root/'Docs/Reviews/Map02_Audit_20261026';target=out/'source'
sha=subprocess.check_output(['git','rev-parse','origin/dev'],cwd=root).decode().strip()
files=subprocess.check_output(['git','ls-tree','-r','--name-only',sha],cwd=root).decode().splitlines();selected=[]
for p in files:
 if p.endswith('.cs') and p.startswith(('Assets/_Project/Map01/Runtime/Map02','Assets/_Project/Map01/Tests/Map02','Assets/_Project/Map01/Editor/Map02')):selected.append(p)
 elif p in ['Assets/_Project/Map01/Runtime/Map01CinematicPose.cs','Assets/_Project/Map01/Runtime/ForestSaveSlots.cs','Assets/_Project/Map01/Resources/Story/Map02Chapter.json','Assets/_Project/Map01/Resources/Story/Map02DayOneArt.asset','Assets/_Project/Map01/Resources/Story/Map02ChapterLayout.asset','Docs/Handoff/Map02_Ngay1Dem1_KeHoachSua_2026-10-23.md']:selected.append(p)
manifest=[]
for p in selected:
 data=subprocess.check_output(['git','show',sha+':'+p],cwd=root);dst=target/p;dst.parent.mkdir(parents=True,exist_ok=True);dst.write_bytes(data)
 manifest.append({'path':p,'lines':len(data.splitlines()),'sha256':hashlib.sha256(data).hexdigest()})
(out/'dev-snapshot.json').write_bytes((json.dumps({'commit':sha,'files':manifest},indent=2)+'\n').encode())
chapter=json.loads((target/'Assets/_Project/Map01/Resources/Story/Map02Chapter.json').read_text(encoding='utf-8'))
lines=[]
for t in chapter['tasks']:lines.append(' | '.join(str(t.get(k,'')) for k in ['id','phase','kind','site','requires','count','seconds','scene','title']))
(out/'tasks-summary.txt').write_bytes(('\n'.join(lines)+'\n').encode('utf-8'))
print('DEV_SNAPSHOT',sha,'files',len(manifest),'tasks',len(chapter['tasks']))
