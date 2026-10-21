"""Resolve reviewed branch conflicts only in the disposable integration worktree."""
from pathlib import Path
import subprocess,re,json

root=Path(r'C:/Users/Admin/.codex/worktrees/merge-dev-thinh-20261020/ShadowVale')
report=Path(r'D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020')
def git(*args):return subprocess.check_output(['git','-c','core.autocrlf=false',*args],cwd=root)
rx=re.compile(r'^<<<<<<< .*?\n(.*?)^=======\n(.*?)^>>>>>>> .*?\n',re.M|re.S)
conflicts=git('diff','--name-only','--diff-filter=U').decode().splitlines()
for rel in conflicts:
 p=root/rel;s=p.read_text(encoding='utf-8-sig')
 if rel.endswith('BossRelocation.cs'):
  def pick(m):
   return m[2].replace('Vector3? men = null, bool fallingBack = false)', 'Vector3? men = null, bool fallingBack = false, Func<Vector3, bool> allowed = null)').replace('        public static bool TryPick', '        /// <param name="allowed">Map-specific legal cover positions, including the C3 fence.</param>\n        public static bool TryPick')
 elif rel.endswith('BossBehaviour.cs'):
  def pick(m):return m[2].replace('men, fallingBack)', 'men, fallingBack, allowed: AllowedPosition)')
 elif rel.endswith('Map01Story.cs'):
  def pick(m):return '                    Progress.circuitSeed=0;Progress.circuitRound=0;Progress.circuitRotations=null;\n'+m[1]
 elif rel.endswith('BossIntegrationTests.cs'):
  def pick(m):return m[1]
 else:
  def pick(m):return m[2]
 s=rx.sub(pick,s)
 assert not re.search(r'^(<<<<<<<|=======|>>>>>>>)',s,re.M),rel
 if rel.endswith('EnemyFeedback.cs'):
  assert 'GunshotClip' in s and 'private bool Cast(' in s
  s=s.replace('PlayAudio(gunshot, .85f,', 'PlayAudio(GunshotClip != null ? new AudioSlice(GunshotClip, 0f, 0f, 1f) : gunshot, .85f,')
  assert 'PlayAudio(GunshotClip != null ? new AudioSlice(GunshotClip' in s
 if rel.endswith('.json'):json.loads(s)
 p.write_text(s,encoding='utf-8',newline='\n')
 git('add','--',rel)
print(json.dumps({'resolved':conflicts,'remaining':git('diff','--name-only','--diff-filter=U').decode().splitlines()},indent=2))
