import sys, pathlib, json, shutil
root=pathlib.Path(__file__).parent
sys.path.insert(0,str(root.parent/'Shot01'/'.deps'))
from gradio_client import Client, handle_file
prompt=('Continuous hand-drawn 2D animated film, clean stable linework, natural proportions and subtle cel shading. '
 'Start exactly at the supplied first frame, finish at the supplied last frame. No cuts or mirroring. '
 '0 to 2 seconds: Nam enters naturally from OFFSCREEN LEFT beside the empty chair. Nam is the man seen on the LEFT in the target frame: angular clean-shaven Vietnamese tan face, rounded net-covered green steel helmet, dark olive long sleeved uniform with two flap chest pockets, simple belt, green trousers and olive lace-up shoes. No scarf or vest on Nam. '
 'He holds a small olive canteen, steps up, grips it naturally, sets it upright on the clear LEFT side of the desk far from the radio, and releases his fingers after contact. '
 '2 to 5 seconds: Nam remains STANDING screen left and looks at Hung with a familiar considerate expression, mouth making small conversational movements for one short question. Hung, seated RIGHT, first shifts his eyes to Nam then gently turns his head. '
 '5 to 8 seconds: Nam stops speaking, Hung replies quietly with small mouth movements and tired concerned eyes, looking at Nam. '
 'Hung retains pith helmet chinstrap, red-white checked scarf, rolled sleeves, front canvas pouches and black slide sandals. '
 'Both remain on their respective sides. Gentle continuous camera adjustment, do not cross dialogue axis. Empty chair stays empty, canteen stays on desk at end. Stable shelter, lamp, radio, bag, morning light, subtle leaves and breathing. Exactly two men. No subtitles, text, weapons, narration or added props.')
(root/'motion_prompt.txt').write_text(prompt,encoding='utf-8')
client=Client('amisima/Wan_2.2_I2V_Studio',download_files=str(root/'download'))
result=client.predict(input_image=handle_file(str(root/'shot02_start.png')),
 last_image=handle_file(str(root/'shot02_target.png')),prompt=prompt,
 duration_seconds=8.0,steps=6,seed=20217,randomize_seed=False,
 negative_prompt='live action, 3D, photo, text, subtitles, logo, watermark, duplicate men, extra fingers, deformed hands, morphing clothes, scarf on left man, sitting left man, mirror, cuts, weapons',
 my1_repo='',my2_repo='',my3_repo='',my4_repo='',my5_repo='',identity_ref=None,api_name='/generate_video')
print('RESULT',repr(result),flush=True)
(root/'generation_result.json').write_text(json.dumps(result,ensure_ascii=False,default=str),encoding='utf-8')
def files(x):
 if isinstance(x,str) and pathlib.Path(x).is_file() and x.lower().endswith('.mp4'): yield x
 elif isinstance(x,(tuple,list)):
  for y in x: yield from files(y)
 elif isinstance(x,dict):
  for y in x.values(): yield from files(y)
paths=list(files(result))
if not paths: raise RuntimeError('No output video returned')
shutil.copyfile(paths[0],root/'shot02_raw.mp4')


