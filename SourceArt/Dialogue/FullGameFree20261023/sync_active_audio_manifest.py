from full_game_audio import *
import numpy as np,soundfile as sf,shutil
catalog=ROOT/'Assets/Resources/Dialogue/voice-catalog.json';d=json.loads(catalog.read_text(encoding='utf-8'));rows=[]
for x in d['entries']:
 for k in ['taskId','reportedDuration','spokenText','languageCode','sourceSha256','pendingVoice','estimatedCredits']:x.pop(k,None)
 x['cost']=0;x['license']='Apache-2.0 (model and bundled presets)';x['source']='https://huggingface.co/pnnbao-ump/VieNeu-TTS-v3-Turbo'
 p=ROOT/'Assets/Resources'/Path(x['resource']+'.wav');a,sr=sf.read(p);assert sr==48000;assert max(abs(a))<.9999,x['id'];assert np.sqrt(np.mean(a*a))>.002,x['id']
 row=dict(x,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),duration=len(a)/sr,peak=float(max(abs(a))),rms=float(np.sqrt(np.mean(a*a))));rows.append(row)
for rel in ['Assets/Resources/Dialogue/voice-catalog.json','SourceArt/Dialogue/voice-catalog.json']:(ROOT/rel).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
folder=ROOT/'SourceArt/Dialogue/FullGameFree20261023';(folder/'integrated-manifest.json').write_text(json.dumps(dict(cost=0,dev='473a7940',cues=len(rows),lines=rows),ensure_ascii=False,indent=2),encoding='utf-8')
p=folder/'sfx-sources.json';sources=json.loads(p.read_text(encoding='utf-8'))
for x in sources:
 if x['name']=='crickets':x['name']='village_crickets'
if not any(x['name']=='crickets' for x in sources):sources.append(dict(name='crickets',page='https://opengameart.org/content/crickets-ambient-noise-loopable',author='Wolfgang_ / Ted Kerr',license='CC0-1.0',file='Assets/Resources/Map02DayOne/crickets.mp3',sha256=hashlib.sha256((ROOT/'Assets/Resources/Map02DayOne/crickets.mp3').read_bytes()).hexdigest(),usage='Preserved dev final supper hold; other night layer fades out during meal.'))
p.write_text(json.dumps(sources,ensure_ascii=False,indent=2),encoding='utf-8')
for name in ['full_game_audio.py','install_full_audio.py','sync_active_audio_manifest.py']:
 shutil.copy2(BASE/name,folder/name)
report=BASE/'output/GameAudio_Free_20261023/BANG_GIONG_VA_NGUON.md'
if report.exists():
 s=report.read_text(encoding='utf-8').replace('`7abc29af`','`473a7940`');s+='\n\nCập nhật dev tiếp theo: 473a7940. Giữ cách dàn cảnh ăn cơm/chốt bờ tre của dev; thu lại Hùng 05, Hùng 05b, chị Lành 06 và vai Tổ gác bằng giọng mới. Tổ gác: Phạm Tuyên, nam trưởng thành khỏe và cảnh giác. Giữ dế cuối cảnh của Wolfgang_/Ted Kerr từ dev; lớp nền mới dùng tên village_crickets để không trùng resource.\n';report.write_text(s,encoding='utf-8');shutil.copy2(report,folder/'README.md')
print('Audited',len(rows),'cues; non-silent, unclipped, 48 kHz; dev 473a7940.')
