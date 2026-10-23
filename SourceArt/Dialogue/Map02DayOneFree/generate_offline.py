# Reproduce the approved built-in voices offline. Python 3.10+, pip install vieneu==3.8.0.
import argparse,hashlib,json,os,random
from pathlib import Path
import numpy as np
import soundfile as sf
from vieneu import Vieneu

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,required=True);args=parser.parse_args();args.out.mkdir(parents=True,exist_ok=True)
 manifest=json.loads(Path(__file__).with_name('voice-manifest.json').read_text(encoding='utf-8'))
 tts=Vieneu(backend='onnx',precision='fp32',threads=2)
 generated=[]
 for cue in manifest['lines']:
  random.seed(cue['seed']);np.random.seed(cue['seed']);audio=np.asarray(tts.infer(cue['text'],voice=cue['voice'],temperature=.8),dtype=np.float32)
  if audio.size==0 or float(np.sqrt(np.mean(audio*audio)))<.002:raise RuntimeError('Empty or silent output: '+cue['id'])
  path=args.out/(cue['id']+'.wav');sf.write(str(path),audio,48000,subtype='PCM_16');generated.append(dict(id=cue['id'],sha256=hashlib.sha256(path.read_bytes()).hexdigest(),duration=len(audio)/48000))
 (args.out/'generation-receipt.json').write_text(json.dumps(generated,indent=2),encoding='utf-8')
 print('Generated',len(generated),'spoken cues; no paid API. Review audio before replacing approved WAVs. The silent shift cue is excluded.')
if __name__=='__main__':main()
