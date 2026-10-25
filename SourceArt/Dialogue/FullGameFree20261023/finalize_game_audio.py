from full_game_audio import *
import shutil,uuid,numpy as np,soundfile as sf
# Run only after the editor test process exits.
src=ROOT/'SourceArt/Dialogue/FullGameFree20261023'
catpath=ROOT/'Assets/Resources/Dialogue/voice-catalog.json';cat=json.loads(catpath.read_text(encoding='utf-8'))
entry=next(x for x in cat['entries'] if x['id']=='m2v3_farewell_01')
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'));line=next(x for x in manifest['lines'] if x['id']==entry['id']);p=Path(line['file']);raw=p.with_name(p.stem+'-raw.wav')
filters=f"rubberband=tempo={line['tempo']}:pitch={line['pitch']},highpass=f=75,lowpass=f=8500,alimiter=limit=0.9:level=0:latency=1"
subprocess.run([str(FF),'-v','error','-y','-i',str(raw),'-af',filters,'-ar','48000','-c:a','pcm_s16le',str(p)],check=True)
shutil.copy2(p,ROOT/'Assets/Resources'/Path(entry['resource']+'.wav'))
a,sr=sf.read(p);entry['duration']=len(a)/sr;entry['sha256']=hashlib.sha256(p.read_bytes()).hexdigest()
for x in cat['entries']:
 for key in ['taskId','reportedDuration','spokenText','languageCode','sourceSha256','pendingVoice']:
  x.pop(key,None)
 x['cost']=0;x['license']='Apache-2.0 (model and bundled presets)';x['source']='https://huggingface.co/pnnbao-ump/VieNeu-TTS-v3-Turbo'
for rel in ['Assets/Resources/Dialogue/voice-catalog.json','SourceArt/Dialogue/voice-catalog.json']:
 (ROOT/rel).write_text(json.dumps(cat,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
dest=ROOT/'Assets/Resources/Map02DayOne/tank_tracks.wav';shutil.copy2(OUT/'tank_tracks.wav',dest)
meta=Path(str(dest)+'.meta')
if not meta.exists():
 template=(ROOT/'Assets/Resources/Map02DayOne/plane.wav.meta').read_text(encoding='utf-8');template=re.sub(r'guid: \w+','guid: '+uuid.uuid4().hex,template,count=1);meta.write_text(template,encoding='utf-8',newline='\n')
tankguid=re.search(r'guid: (\w+)',meta.read_text()).group(1)
blast=ROOT/'Assets/_Project/Map01/Resources/Audio/Explosion_Medium_Blast.wav.meta';blastguid=re.search(r'guid: (\w+)',blast.read_text()).group(1)
layout=ROOT/'Assets/_Project/Map01/Resources/Story/Map02ChapterLayout.asset';s=layout.read_text(encoding='utf-8');s=s.replace('tankTracksClip: {fileID: 0}',f'tankTracksClip: {{fileID: 8300000, guid: {tankguid}, type: 3}}').replace('artilleryClip: {fileID: 0}',f'artilleryClip: {{fileID: 8300000, guid: {blastguid}, type: 3}}');layout.write_text(s,encoding='utf-8',newline='\n')
p=ROOT/'Assets/_Project/Map01/Runtime/TutorialCaptureCutscene.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('bool wasPlaying = Playing;','bool wasPlaying = Playing;\n            if(wasPlaying&&mission!=null)mission.GetComponent<DialogueVoice>()?.Stop();');p.write_text(s,encoding='utf-8',newline='\n')
p=ROOT/'Assets/_Project/Map01/Resources/Cutscenes/Map01Opening.prefab';p.write_bytes(p.read_bytes().replace(b'\r\n',b'\n'))
# Complete asset provenance and actual imported-file metrics, including aliases.
allfiles=[]
for x in cat['entries']:
 p=ROOT/'Assets/Resources'/Path(x['resource']+'.wav');a,sr=sf.read(p)
 allfiles.append(dict(id=x['id'],resource=x['resource'],caption=x['caption'],speaker=x['speaker'],voice=x['voice'],radio=x['radio'],duration=len(a)/sr,peak=float(np.max(np.abs(a))),rms=float(np.sqrt(np.mean(a*a))),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
assert all(x['peak']<.9999 and x['rms']>.002 for x in allfiles)
(src/'integrated-manifest.json').write_text(json.dumps(dict(cost=0,cues=len(allfiles),lines=allfiles),ensure_ascii=False,indent=2),encoding='utf-8')
rows=json.loads((OUT/'sfx-sources.json').read_text(encoding='utf-8'))
rows+= [dict(name='cough',page='https://commons.wikimedia.org/wiki/File:Cough_2.ogg',author='ezwa',license='Public domain'),dict(name='night_river',page='https://swanson.run/frogs/',author='Michael G. Swanson',license='CC0-1.0',changes='First short excerpt mixed with FreethinkerAnon crickets; generic environment substitute, not Vietnam field recording'),dict(name='hung_laugh',page='https://opengameart.org/content/do-you-remember-laughter',author='Supergeek',license='CC0-1.0',changes='Short excerpt; mild pitch adjustment, requires perceptual review')]
rows+=json.loads((OUT/'original-sfx.json').read_text(encoding='utf-8'))
rows+=[dict(name='tank_tracks',source='Original procedural synthesis',license='CC0-1.0'),dict(name='children',source='VieNeu-TTS preset, original environment call: Chờ với! Qua đây nè!',license='Apache-2.0 (model/presets)'),dict(name='hung_invitation',source='VieNeu-TTS, Thanh Bình preset',license='Apache-2.0 (model/presets)')]
for x in rows:
 p=ROOT/'Assets/Resources/Map02DayOne'/Path(x['name']+'.wav');assert p.exists(),p;x['sha256']=hashlib.sha256(p.read_bytes()).hexdigest()
(src/'sfx-sources.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
for name in ['MODEL_CARD.md','LICENSE.txt']:
 p=ROOT/'SourceArt/Dialogue/Map02DayOneFree'/name
 if p.exists():shutil.copy2(p,src/name)
for name in ['full_game_audio.py','make_original_sfx.py','fetch_free_ambience.py','finalize_game_audio.py']:
 shutil.copy2(BASE/name,src/name)
review=BASE/'output/GameAudio_Free_20261023/BANG_GIONG_VA_NGUON.md';shutil.copy2(review,src/'README.md')
print('FINAL:',len(allfiles),'cues;',len(rows),'asset source records; peak and silence audit passed.')
