"""Assemble actual Blender render frames into local review GIFs/contact sheet."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json
import sys
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Logs/AnimationPreview/NamCrawl'
if '--mixamo' in sys.argv:OUT=OUT/'MixamoPreview'
KINDS=['unarmed','rifle','aim','knife','grenade','stone']
LABELS=['Tay không','Cầm súng','Ngắm súng','Cầm dao','Cầm lựu đạn','Cầm đá']
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',22)
small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
overview=[]
for frame in range(36):
    panel=Image.new('RGB',(1200,700),(22,29,28));draw=ImageDraw.Draw(panel)
    for i,(kind,label) in enumerate(zip(KINDS,LABELS)):
        path=OUT/kind/f'frame-{frame:03}.png'
        image=Image.open(path).convert('RGB').resize((400,300),Image.Resampling.LANCZOS)
        x=(i%3)*400;y=(i//3)*340
        panel.paste(image,(x,y+36));draw.text((x+14,y+4),label,font=font,fill=(240,243,236))
    draw.text((14,679),'Blender • Chuyển động nền Mixamo • Chưa gắn vào gameplay' if '--mixamo' in sys.argv else 'Bản nháp Blender • Model Nam • Trườn tại chỗ • Chưa gắn vào gameplay',font=small,fill=(196,210,201))
    overview.append(panel)
durations=[60 if i%3==0 else 70 for i in range(36)]
overview[0].save(OUT/'nam-crawl-six-poses.gif',save_all=True,append_images=overview[1:],duration=durations,loop=0,optimize=False,disposal=2)
for kind in KINDS:
    frames=[Image.open(OUT/kind/f'frame-{i:03}.png').convert('RGB') for i in range(36)]
    frames[0].save(OUT/f'nam-crawl-{kind}.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False,disposal=2)
side=Image.new('RGB',(1200,680),(22,29,28));draw=ImageDraw.Draw(side)
for i,(kind,label) in enumerate(zip(KINDS,LABELS)):
    x=(i%3)*400;y=(i//3)*340
    side.paste(Image.open(OUT/kind/'side.png').convert('RGB').resize((400,300),Image.Resampling.LANCZOS),(x,y+36))
    draw.text((x+14,y+4),label,font=font,fill=(240,243,236))
side.save(OUT/'nam-crawl-side-poses.png')
with Image.open(OUT/'nam-crawl-six-poses.gif') as gif:
    assert gif.n_frames==36
    total=0
    for i in range(gif.n_frames):gif.seek(i);total+=gif.info['duration']
    assert total==2400,total
print(json.dumps({'gif_frames':36,'duration_ms':total,'variants':6,'output':str(OUT)},ensure_ascii=False))
