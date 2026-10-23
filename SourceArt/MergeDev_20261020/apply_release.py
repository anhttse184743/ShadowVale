"""Install the tested candidate tree on thinh, stopping before any commit."""
from pathlib import Path
import subprocess,json,hashlib
main=Path(r'D:/DO_AN_GAME/ShadowVale');report=main/'Docs/Reviews/MergeDev_20261020'
release=json.loads((report/'release_tree.json').read_text(encoding='utf-8'));candidate=Path(release['candidate'])
backup=json.loads((main/'Temp/MergeDev_20261020_Before/manifest.json').read_text(encoding='utf-8'))
baseline={r['path']:r for r in backup['files']}
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def git(*args):return subprocess.check_output(['git','-c','core.autocrlf=false','-c','core.safecrlf=false',*args],cwd=main,stderr=subprocess.STDOUT)
assert git('branch','--show-current').decode().strip()=='thinh'
assert git('rev-parse','HEAD').decode().strip()==release['head'],'Main HEAD changed; inspect before proceeding.'
assert subprocess.run(['git','diff','--cached','--quiet'],cwd=main).returncode==0,'User index changed.'
assert not (main/'.git/MERGE_HEAD').exists()
# A stale, unlocked UnityLockfile can remain from a previous editor session.
# Check the actual process rather than treating the filename as a live editor.
probe=r'''@(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -match '(?i)-projectPath\s+"?D:[\\/]DO_AN_GAME[\\/]ShadowVale(?:"|\s|$)' }).Count'''
running=subprocess.check_output(['powershell','-NoProfile','-Command',probe]).decode().strip()
assert running=='0','Main Unity editor is running. Do not close it without asking.'
assert release['validatedCases']>0 and not release['failures']
changed=[p for p in git('diff','--name-only','-z').decode().split('\0') if p]
assert all(p in baseline for p in changed), 'New local edits appeared: '+str([p for p in changed if p not in baseline])

# Check source work did not change during the long Unity runs, including files kept out of the commit.
for rel,row in baseline.items():
 if row.get('deleted'):continue
 if rel.startswith(('Assets/','Packages/','ProjectSettings/')):
  assert (main/rel).exists() and digest(main/rel)==row['sha256'],'Source changed during tests: '+rel
for row in backup['observed_player_saves']:
 assert digest(Path(row['path']))==row['sha256'],'A player save changed: '+row['path']

for row in release['files']:
 rel=row['path'];dst=(main/rel).resolve();src=candidate/rel
 assert dst.is_relative_to(main.resolve()) and '.git' not in Path(rel).parts
 if not row['deleted']:assert digest(src)==row['sha256'],'Candidate changed after validation: '+rel
 if dst.exists() and rel not in baseline:
  known=subprocess.run(['git','ls-files','--error-unmatch','--',rel],cwd=main,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode==0
  assert known or digest(dst)==row['sha256'],'New untracked collision: '+rel

# The actual three-way merge and conflict resolutions are in the candidate. This
# standard no-commit preparation keeps main's dirty files intact (see install_probe.json).
# Its temporary index tree is then replaced with that exact tested merge tree.
prepared=git('merge','--no-ff','--no-commit','-s','ours',release['dev']).decode(errors='replace')
for row in release['files']:
 dst=(main/row['path']).resolve()
 if row['deleted']:
  if dst.exists():dst.unlink() # individual validated tracked file, never recursive
 else:
  dst.parent.mkdir(parents=True,exist_ok=True);dst.write_bytes((candidate/row['path']).read_bytes())
git('read-tree',release['tree'])
assert git('write-tree').decode().strip()==release['tree']
assert git('rev-parse','MERGE_HEAD').decode().strip()==release['dev']
assert git('rev-parse','HEAD').decode().strip()==release['head']
for row in release['files']:
 dst=main/row['path'];assert not dst.exists() if row['deleted'] else digest(dst)==row['sha256']
message='Merge dev into thinh with existing Map 1 weapon and C3 integration\n\nValidated candidate tree: '+release['tree']+'\nKeep approved C3 buildings, fence and roster. Preserve Mosin, ordinary K54, bed knife and sleep/wake work.\n'
(main/'.git/MERGE_MSG').write_text(message,encoding='utf-8',newline='\n')
result={'headUnchanged':release['head'],'mergeHead':release['dev'],'tree':release['tree'],'filesInstalled':len(release['files']),'prepared':prepared.strip(),'committed':False,'pushed':False}
(report/'apply_result.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
