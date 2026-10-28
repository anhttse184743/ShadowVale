import base64, json, pathlib, urllib.request

root = pathlib.Path(__file__).parent
host = 'https://prithivmlmods-wan2-2-fast.hf.space'
prompt = ('Continuous cinematic hand-drawn 2D animation with stable clean outlines and subtle cel shading. '
 'The seated man stays on screen right. First third: slow camera push inward past the foreground leaf and wooden post, gentle multiplane parallax. '
 'Middle: he listens carefully to the receiver, small eye movement, quietly breathes, then lowers the receiver naturally and sets it firmly on the wooden desk. '
 'Last: his hand releases the receiver, shoulders relax subtly, hold three-quarter composition. Keep left chair empty and left side open. '
 'Preserve the exact face, pith helmet, red white checked scarf, rolled sleeves, utility pouches and sandals. '
 'No other people enter. Natural anatomy, believable hand contact, subtle foliage motion, warm lamp, soft morning light.')
data = ['data:image/png;base64,' + base64.b64encode((root/'shot01_start.png').read_bytes()).decode(), prompt, 4,
 '3D, live action, slideshow, text, subtitles, watermark, extra fingers, deformed hands, morphing face, costume changes, weapons, extra people, camera cuts',
 5.0, 1.0, 1.0, 10217, False]
(root/'motion_prompt.txt').write_text(prompt,encoding='utf-8')
req=urllib.request.Request(host+'/gradio_api/call/generate_video', data=json.dumps({'data':data}).encode(), headers={'Content-Type':'application/json'})
with urllib.request.urlopen(req,timeout=60) as r: result=json.load(r)
print('Submission:',result,flush=True)
with urllib.request.urlopen(host+'/gradio_api/call/generate_video/'+result['event_id'],timeout=300) as r:
 for raw in r:
  line=raw.decode().strip()
  if line.startswith('data:'):
   payload=json.loads(line[5:].strip())
   if isinstance(payload,list) and payload and isinstance(payload[0],dict) and 'video' in payload[0]:
    (root/'shot01_raw.mp4').write_bytes(base64.b64decode(payload[0]['video'].split(',',1)[1]))
    print('Saved shot01_raw.mp4',flush=True)
   else: print(str(payload)[:2000],flush=True)
  elif line: print(line,flush=True)
