import shutil,json,hashlib
from pathlib import Path
from full_game_audio import BASE,ROOT
out=BASE/'output/Rescue_Clarity_20261025';rows=[]
for name in ['Map01Rescue.cs','Map01RescueCinematic.cs','Map01OpeningCutscene.cs','Map01Extraction.cs','Map01SaveSystem.cs','RescueRadioIntroTests.cs','RescuePresentationTests.cs','RescueBushTests.cs']:
    rel=Path('Assets/_Project/Map01')/('Tests' if name.endswith('Tests.cs') else 'Runtime')/name
    target=ROOT/rel;source=out/'source'/name
    save=out/'before'/rel
    if target.exists() and not save.exists():save.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(target,save)
    target.write_text(source.read_text(encoding='utf-8-sig'),encoding='utf-8',newline='\n')
    rows.append(dict(file=str(rel),sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
(out/'publish.json').write_text(json.dumps(rows,indent=2),encoding='utf-8');print('Published',len(rows),'files')
