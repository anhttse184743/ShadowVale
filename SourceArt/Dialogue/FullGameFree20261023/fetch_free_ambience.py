from pathlib import Path
import urllib.request,re,json,hashlib,subprocess
from full_game_audio import OUT,FF
opener=urllib.request.build_opener();opener.addheaders=[('User-Agent','ShadowValeAudioAudit/1.0 (game asset license review)')];urllib.request.install_opener(opener)
SOURCES=[('night_river','felix.blume',466955,30),('bamboo','Yoyodaman234',335891,20),('wings','cosmicembers',160758,3),('plane','Mozfoo',440163,15),('cough','Fabrizio84',457972,3)]
rows=json.loads((OUT/'sfx-sources.json').read_text(encoding='utf-8'))
for name,author,ident,seconds in SOURCES:
 try:
  url=f'https://freesound.org/people/{author}/sounds/{ident}/';response=urllib.request.urlopen(url,timeout=40);page=response.read().decode();url=response.url
  assert 'creativecommons.org/publicdomain/zero' in page,'Not CC0'
  previews=re.findall(r'https://cdn\.freesound\.org/previews/[^\s"<>]+-hq\.mp3',page);assert previews,'No preview'
  audio=OUT/(name+'-source.mp3');audio.write_bytes(urllib.request.urlopen(previews[0],timeout=60).read())
  (OUT/(name+'-license.html')).write_text(page,encoding='utf-8')
  dest=OUT/(name+'.wav');subprocess.run([str(FF),'-v','error','-y','-i',str(audio),'-t',str(seconds),'-af','highpass=f=70,afade=t=in:d=0.08,afade=t=out:st='+str(seconds-.2)+':d=0.2','-ar','48000','-ac','1',str(dest)],check=True)
  rows.append(dict(name=name,page=url,preview=previews[0],license='CC0-1.0',sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),file=str(dest)))
  print(name,'OK',flush=True)
 except Exception as e:print(name,'FAILED',str(e),flush=True)
 (OUT/'sfx-sources.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
