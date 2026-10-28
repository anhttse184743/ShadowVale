from pathlib import Path
import sys
from PIL import Image,ImageDraw,ImageFont
here=Path(__file__).resolve().parent
actual='--mixamo' in sys.argv
magazine_only='--magazine-only' in sys.argv
folder=here.parents[2]/'Logs/AnimationPreview/NamCrawl'/('ReloadMagazineOnlyPreview' if magazine_only else 'ReloadMixamoPreview' if actual else 'ReloadPreview')
count=36 if magazine_only else 97 if actual else 64
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',21)
small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',15)
def stage(t):
    if magazine_only:return 'Tay về băng đạn' if t<.5 else 'Tháo băng / đổi băng' if t<1.28 else 'Lắp băng mới' if t<2 else 'Trở lại ngắm'
    if actual:return 'Tay về băng đạn' if t<.6 else 'Tháo băng / tay về túi' if t<1.55 else 'Lắp băng mới' if t<2.6 else 'Thao tác trên thân súng' if t<5.5 else 'Trở lại ngắm'
    return 'Chuẩn bị' if t<.4 else 'Tay về băng đạn' if t<.8 else 'Tháo băng' if t<1.25 else 'Đưa tay về túi' if t<2.05 else 'Lắp băng mới' if t<3.1 else 'Hoàn tất thao tác' if t<3.85 else 'Trở lại ngắm'
frames=[]
for i in range(count):
    close=(folder/f'close-{i:03}.png').exists()
    frame=Image.new('RGB',(1200,520) if close else (800,665),(18,24,27))
    if close:
        frame.paste(Image.open(folder/f'frame-{i:03}.png').convert('RGB').resize((600,450)),(0,38))
        frame.paste(Image.open(folder/f'close-{i:03}.png').convert('RGB').resize((600,450)),(600,38))
    else:frame.paste(Image.open(folder/f'frame-{i:03}.png').convert('RGB'),(0,38))
    draw=ImageDraw.Draw(frame)
    draw.text((16,5),'NAM / THAY BĂNG ĐẠN KHI NẰM — 2,4 GIÂY' if magazine_only else 'NAM / MIXAMO PRONE RELOADING' if actual else 'NAM / THAY ĐẠN KHI NẰM — PREVIEW BLENDER',font=font,fill=(239,238,226))
    draw.text((16,495 if close else 640),f'{stage(i/15)} • {i/15:.1f}s   |   Chưa gắn vào game',font=small,fill=(228,222,197))
    frames.append(frame.quantize(colors=192))
durations=([60,70,70]*40)[:count]
if not magazine_only:durations[-1]=500
frames[0].save(folder/'nam-prone-reload-preview.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0,disposal=2)
sheet=Image.new('RGB',(1200,700),(18,24,27));draw=ImageDraw.Draw(sheet)
draw.text((20,9),'THAY ĐẠN KHI NẰM / CÁC MỐC CHUYỂN ĐỘNG',font=font,fill='white')
for cell,i in enumerate([0,8,16,24,30,35] if magazine_only else [0,12,21,36,60,96] if actual else [0,12,24,32,43,63]):
    x=(cell%3)*400;y=48+(cell//3)*320
    sheet.paste(Image.open(folder/f'frame-{i:03}.png').convert('RGB').resize((400,290)),(x,y))
    draw.text((x+12,y+291),f'{i/15:.1f}s — {stage(i/15)}',font=small,fill=(235,228,204))
sheet.save(folder/'nam-prone-reload-steps.png')
print('RELOAD_GIF_PACKAGED',len(frames))
