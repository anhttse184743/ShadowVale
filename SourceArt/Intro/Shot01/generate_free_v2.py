import base64, json, pathlib, urllib.request

root = pathlib.Path(__file__).parent
host = 'https://prithivmlmods-wan2-2-fast.hf.space'
prompt = ('An expressive fluid hand-drawn 2D animated film shot, continuous natural character animation. '
 'Camera physically dollies forward into the shelter, foreground leaves sway across the left edge faster than the background, strong gentle depth parallax. '
 'The man seated on the right actively listens to the telephone receiver, blinks once, shifts his eyes toward the radio and tilts his head slightly in concentration. His chest rises with a quiet breath. '
 'After two seconds he smoothly moves his hand and receiver away from his ear, leans forward a little and places the receiver on the wooden desk beside the radio. His fingers release it only after contact. '
 'He withdraws his hand onto his thigh, exhales and settles his shoulders, remaining seated. In the last second he rests quietly looking toward the empty left chair, mouth closed. '
 'Leaves outside flutter gently in morning breeze, reflected light shifts softly, the small duty lamp flame flickers subtly. '
 'Keep the same lean Vietnamese adult face, pith helmet, checked red-white scarf, rolled green sleeves, front utility pouches, green trousers and black sandals absolutely consistent. '
 'Left chair stays empty, open space on left. No other person. Restrained acting, stable anatomy, no talking, no cuts, no camera shake.')
data = ['data:image/png;base64,' + base64.b64encode((root/'shot01_start.png').read_bytes()).decode(), prompt, 6,
 '3D, live action, slideshow, text, subtitles, watermark, extra fingers, deformed hands, morphing face, costume changes, weapons, extra people, camera cuts',
 5.0, 1.0, 1.0, 10229, False]
(root/'motion_prompt_v2.txt').write_text(prompt,encoding='utf-8')
req=urllib.request.Request(host+'/gradio_api/call/generate_video', data=json.dumps({'data':data}).encode(), headers={'Content-Type':'application/json'})
with urllib.request.urlopen(req,timeout=60) as r: result=json.load(r)
print('Submission:',result,flush=True)
with urllib.request.urlopen(host+'/gradio_api/call/generate_video/'+result['event_id'],timeout=300) as r:
 for raw in r:
  line=raw.decode().strip()
  if line.startswith('data:'):
   payload=json.loads(line[5:].strip())
   if isinstance(payload,list) and payload and isinstance(payload[0],dict) and 'video' in payload[0]:
    (root/'shot01_v2_raw.mp4').write_bytes(base64.b64decode(payload[0]['video'].split(',',1)[1]))
    print('Saved shot01_v2_raw.mp4',flush=True)
   else: print(str(payload)[:2000],flush=True)
  elif line: print(line,flush=True)
