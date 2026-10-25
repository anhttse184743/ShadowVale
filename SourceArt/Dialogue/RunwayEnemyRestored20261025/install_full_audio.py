import json,shutil,hashlib,subprocess
from pathlib import Path
from full_game_audio import ROOT,OUT,FF
catalog=ROOT/'Assets/Resources/Dialogue/voice-catalog.json'
old=json.loads(catalog.read_text(encoding='utf-8-sig'));mapping={x['id']:x for x in old['entries']}
backup=OUT/'before-install';backup.mkdir(exist_ok=True)
if not (backup/'voice-catalog.json').exists():shutil.copy2(catalog,backup/'voice-catalog.json')
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
assert manifest['rendered']==manifest['total']
for x in manifest['lines']:
 if x.get('preserveExisting'):
  assert hashlib.sha256((ROOT/'Assets/Resources'/Path(x['resource']+'.wav')).read_bytes()).hexdigest()==x['sha256'],x['id']
  continue
 previous=mapping.get(x['id'],{})
 resource=previous.get('resource') or 'Dialogue/FullGameFree20261023/'+x['id']
 target=ROOT/'Assets/Resources'/Path(resource+'.wav');target.parent.mkdir(parents=True,exist_ok=True)
 if target.exists():
  save=backup/target.relative_to(ROOT);save.parent.mkdir(parents=True,exist_ok=True)
  if not save.exists():shutil.copy2(target,save)
 if not target.exists() or hashlib.sha256(target.read_bytes()).hexdigest()!=x['sha256']:shutil.copy2(x['file'],target)
 # If the old resource was mp3/ogg, retaining both names would make Resources ambiguous.
 assert not any(target.with_suffix(ext).exists() for ext in ['.mp3','.ogg']),resource
 entry=dict(previous,id=x['id'],caption=x['caption'],match=x['caption'],speaker=x['speaker'],text=x['text'],resource=resource,radio=x.get('radio',False),language='vi',duration=x['duration'],voice=x['voice'],provider='VieNeu offline',model='v3 Turbo',userApproved=False,status='integrated_for_listening_review',sha256=x['sha256'])
 if previous.get('cleanResource') and x.get('radio'):
  clean=ROOT/'Assets/Resources'/Path(previous['cleanResource']+'.wav');clean.parent.mkdir(parents=True,exist_ok=True)
  save=backup/clean.relative_to(ROOT);save.parent.mkdir(parents=True,exist_ok=True)
  if clean.exists() and not save.exists():shutil.copy2(clean,save)
  raw=Path(x['file']).with_name(Path(x['file']).stem+'-raw.wav')
  subprocess.run([str(FF),'-v','error','-y','-i',str(raw),'-af',f"rubberband=tempo={x['tempo']}:pitch={x['pitch']},highpass=f=75,lowpass=f=8500",'-ar','48000','-c:a','pcm_s16le',str(clean)],check=True)
 if x['id'].startswith('free_boss_'):entry['match']=x['speaker']+': "'+x['text']+'"'
 mapping[x['id']]=entry;x['resource']=resource
old['entries']=list(mapping.values())
for rel in ['Assets/Resources/Dialogue/voice-catalog.json','SourceArt/Dialogue/voice-catalog.json']:
 p=ROOT/rel;p.write_text(json.dumps(old,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
source=ROOT/'SourceArt/Dialogue/FullGameFree20261023';source.mkdir(parents=True,exist_ok=True)
(source/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('Installed',len(manifest['lines']),'voice cues. Originals backed up:',backup)
