"""Reuse approved clean masters for local Nam. No TTS job or third-party dependency."""
from pathlib import Path
import array, hashlib, json, shutil, wave

art=Path(__file__).resolve().parent
root=art.parents[1]
report=[]
for catalog_path in (art/'voice-catalog.json',root/'Assets/Resources/Dialogue/voice-catalog.json'):
    catalog=json.loads(catalog_path.read_text(encoding='utf-8-sig'))
    for entry in catalog['entries']:
        if entry['id'] not in ('m1_rescue_radio_report','m1_rescue_radio_ack'):continue
        master=art/'Raw'/(entry['id']+'_dry.wav')
        output=root/'Assets/Resources/Dialogue/VI'/(entry['id']+'.wav')
        shutil.copyfile(master,output)
        with wave.open(str(output),'rb') as source:
            samples=array.array('h',source.readframes(source.getnframes()))
            seconds=len(samples)/source.getframerate()/source.getnchannels()
        entry['radio']=False;entry['caption']='Nam: '+entry['text'];entry['duration']=round(seconds,5)
        if catalog_path.parent==art:
            report.append(dict(id=entry['id'],radio=False,master=str(master.relative_to(root)),
                sha256=hashlib.sha256(output.read_bytes()).hexdigest(),duration=seconds,
                peak=max(abs(x) for x in samples)/32768))
    catalog_path.write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8')
(art/'rescue-local-voice-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('Local Nam uses clean masters; remote commander keeps the existing filtered cue.')
