"""Review actual Blender rifle-rest / shouldered-aim transition renders."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import sys
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Logs/AnimationPreview/NamCrawl'
reference='--reference-fit' in sys.argv
if reference:OUT=OUT/'ProneReference'
if '--mixamo' in sys.argv:OUT=OUT/'MixamoPreview'
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',23)
small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
frames=[]
for frame in range(72):
    t=frame/15
    stage='Giữ súng nằm ngang' if t<.5 or t>=3.9 else 'Đưa súng lên ngắm' if t<1.3 else 'Nằm ngắm bắn' if t<3.1 else 'Hạ súng về tư thế nghỉ'
    panel=Image.new('RGB',(1200,510),(22,29,28));draw=ImageDraw.Draw(panel)
    draw.text((16,6),stage,font=font,fill=(240,243,236))
    for x,prefix in [(0,'frame'),(600,'side')]:
        im=Image.open(OUT/'transition'/f'{prefix}-{frame:03}.png').convert('RGB').resize((600,450),Image.Resampling.LANCZOS)
        panel.paste(im,(x,38))
    draw.text((16,489),'Blender • Preview chuyển tư thế • Chưa gắn vào gameplay',font=small,fill=(196,210,201))
    frames.append(panel)
palette_source=Image.new('RGB',(1200,1020));palette_source.paste(frames[0]);palette_source.paste(frames[36],(0,510))
palette=palette_source.quantize(colors=128)
indexed=[f.quantize(palette=palette,dither=Image.Dither.NONE) for f in frames]
duration=[60 if i%3==0 else 70 for i in range(72)]
indexed[0].save(OUT/'nam-rifle-rest-to-aim-v2.gif',save_all=True,append_images=indexed[1:],duration=duration,loop=0,optimize=False,disposal=2)
comparison=Image.new('RGB',(1200,680),(22,29,28));draw=ImageDraw.Draw(comparison)
for col,(kind,label) in enumerate([('rifle','Giữ súng nằm ngang'),('aim','Nằm ngắm theo ảnh mẫu')]):
    draw.text((col*600+16,5),label,font=font,fill=(240,243,236))
    comparison.paste(Image.open(OUT/kind/'frame-000.png').convert('RGB').resize((600,450),Image.Resampling.LANCZOS),(col*600,38))
    comparison.paste(Image.open(OUT/kind/'side.png').convert('RGB').resize((600,450),Image.Resampling.LANCZOS).crop((0,120,600,310)),(col*600,490))
comparison.save(OUT/'nam-rifle-rest-and-aim-v2.png')
with Image.open(OUT/'nam-rifle-rest-to-aim-v2.gif') as gif:
    # GIF stores repeated hold frames as a longer duration on one frame.
    assert gif.n_frames>=16
    total=0
    for i in range(gif.n_frames):gif.seek(i);total+=gif.info['duration']
    assert total==4800,total
(OUT/'nam-prone-aim-head-raised-forward20cm.png').write_bytes((OUT/'aim'/'side.png').read_bytes())
(OUT/'nam-prone-rifle-forward20cm-transition.gif').write_bytes((OUT/'nam-rifle-rest-to-aim-v2.gif').read_bytes())
print('RIFLE_PREVIEW_PACKAGED 72 frames, 4.8 seconds')
