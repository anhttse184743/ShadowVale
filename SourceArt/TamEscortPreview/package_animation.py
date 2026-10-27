"""Package real Blender renders for review, preserving their timing."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json
OUT=Path(__file__).resolve().parent/'Animation_v4'
data=json.loads((OUT/'validation.json').read_text())
assert data['foot_max_error_m']<.005,data['foot_max_error_m']
assert data['shoulder_max_error_m']<.005,data['shoulder_max_error_m']
assert data['waist_max_error_m']<.02,data['waist_max_error_m']
frames=[]
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',20)
for i in range(1,data['loop_frames']+1):
    with Image.open(OUT/'frames'/f'{i:03}.png') as image:im=image.convert('RGB')
    draw=ImageDraw.Draw(im)
    draw.rectangle((0,0,800,42),fill=(21,30,26))
    draw.text((16,8),'Nam dìu Tâm · Shoulder assist · 7 km/h · Blender preview',font=font,fill=(233,237,227))
    frames.append(im)
timing=[30,30,40]*(data['loop_frames']//3)
frames[0].save(OUT/'Nam_Tam_ShoulderAssist.gif',save_all=True,append_images=frames[1:],duration=timing,loop=0,disposal=2,optimize=False)
sheet=Image.new('RGB',(1200,830),(21,30,26));draw=ImageDraw.Draw(sheet)
for column,i in enumerate((0,4,9)):
    sheet.paste(frames[i].resize((400,400)),(column*400,0))
for column,name in enumerate(('side','back')):
    with Image.open(OUT/(name+'.png')) as image:sheet.paste(image.convert('RGB').resize((400,400)),(column*400,420))
draw.text((816,455),'Vòng bước: 0,6 giây\n30 fps · 18 khung hình\n\nTâm khoác vai Nam\nNam đỡ hông trái Tâm\nTay phải giữ K54\n\nBlender preview',font=font,fill=(233,237,227))
sheet.save(OUT/'Contact_Sheet.png')
with Image.open(OUT/'Nam_Tam_ShoulderAssist.gif') as check:
    assert check.n_frames==data['loop_frames']
    total=0
    for i in range(check.n_frames):check.seek(i);total+=check.info['duration']
    assert total==600,total
data['preview_gif_frames']=data['loop_frames'];data['preview_gif_duration_ms']=total
(OUT/'validation.json').write_text(json.dumps(data,indent=2))
print(f"PASS: {data['loop_frames']} rendered frames, {total} ms loop, contact and foot target tolerances verified.")
