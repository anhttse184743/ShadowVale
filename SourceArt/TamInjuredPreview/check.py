exec(compile(open('SourceArt/TamInjuredPreview/build_preview.py',encoding='utf-8-sig').read().split('# Repair missing')[0],'pose','exec'))
for n in ['Hips','Spine2','LeftArm','LeftForeArm','LeftHand','RightArm','RightForeArm','RightHand']:print(n,tuple(pos(n)))
