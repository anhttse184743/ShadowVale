"""List only the delivered Map 2 work, review evidence and archived voice proposal."""
from pathlib import Path
import subprocess,json,hashlib
project=Path(__file__).resolve().parents[2];review=project/'Docs/Reviews/Map02_Audit_20261026'
def git(*args):return subprocess.check_output(['git',*args],cwd=project).decode().strip('\0').split('\0')
modified=git('diff','HEAD','--name-only','-z');untracked=git('ls-files','--others','--exclude-standard','-z')
chosen=set()
for p in modified:
    if p.startswith('Assets/') or p in ['SourceArt/Dialogue/voice-catalog.json','SourceArt/Dialogue/Map02DayOneFree/voice-manifest.json','Docs/Handoff/C1_Animation_Latest_20261026.md','Docs/Handoff/Map02_Ngay1Dem1_KeHoachSua_2026-10-23.md','Docs/Map02Chapter/DayOne/AUDIO_CREDITS.md','Docs/Reviews/C1_Unity_20261026/KET_QUA.md','Docs/Reviews/ClaudeContinuation_20261023/radio-clearance.txt','Docs/Reviews/ClaudeContinuation_20261023/supper-timeline.txt']:chosen.add(p)
for p in set(untracked)|set(modified):
    if p.startswith(('Assets/_Project/Art/Map02DayOne/Mocap','Assets/Resources/Map02DayOne/','SourceArt/Audio/Map02_DayOne_20261025/','SourceArt/Dialogue/Map01Map2Voice/')):chosen.add(p)
    if p.startswith('Assets/Resources/Dialogue/Map02DayOneFree/m2v3_echo_promise.wav'):chosen.add(p)
    if p.startswith(('Assets/_Project/Map01/Runtime/Map02ChapterDayOne.','Assets/_Project/Map01/Editor/Map02MocapImport.cs','Assets/_Project/Map01/Editor/Map02AudioImport.cs','Assets/_Project/Map01/Tests/Map02DayOneAudioTests.cs','Assets/_Project/Map01/Tests/Map02GroupBTests.cs')):chosen.add(p)
    if p=='Docs/Handoff/Map02_3NgayDem_RaSoat_KeHoach_2026-10-26.md':chosen.add(p)
    if p.startswith('Docs/Reviews/Map02_Audit_20261026/source/'):chosen.add(p)
    if p.startswith('Docs/Reviews/Map02_Audit_20261026/') and Path(p).parent==review.relative_to(project) and Path(p).suffix in ['.md','.xml','.json','.txt','.patch','.png']:chosen.add(p)
    if p.startswith('Tools/Map02Audit_20261026/') and Path(p).name in ['extract_spec.py','snapshot_dev.py','read_code.py','summarize_tests.py','run_tests.ps1','prepare_commit_manifest.py']:chosen.add(p)
# Scratch files and proposals are deliberately outside runtime Resources.
chosen={p for p in chosen if Path(p).is_file() and not p.endswith('commit-manifest.json')}
chosen={p for p in chosen if Path(p).name not in ['runtime-before-complete-catalog.json','source-before-complete-catalog.json'] and not Path(p).name.endswith('-local.txt')}
rows=[{'path':p,'bytes':(project/p).stat().st_size,'sha256':hashlib.sha256((project/p).read_bytes()).hexdigest()} for p in sorted(chosen)]
assert not any(r['path'].startswith(('Assets/_Project/Scenes/','Assets/_Project/Map01/Tests/Generated/','Tools/M16Reports/')) for r in rows)
assert not any(r['bytes']>=95*1024*1024 for r in rows),[(r['path'],r['bytes'])for r in rows if r['bytes']>=95*1024*1024]
(review/'commit-manifest.json').write_text(json.dumps({'files':rows,'count':len(rows),'bytes':sum(r['bytes'] for r in rows)},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'files':len(rows),'MiB':round(sum(r['bytes'] for r in rows)/1024**2,2),'largest':sorted(rows,key=lambda r:r['bytes'],reverse=True)[:4]},indent=2))
