"""Author the approved upright escort take; export to UprightPreview before Unity import."""
from pathlib import Path
HERE=Path(__file__).resolve().parent
code=(HERE/'build_animation.py').read_text(encoding='utf-8')
code=code.replace("OUT=HERE/'Animation_v4'", "OUT=HERE/'UprightPreview'")
code=code.replace("FPS=30;N=18;PERIOD=N/FPS;SPEED=7/3.6", "FPS=30;N=18;PERIOD=N/FPS;SPEED=2.5")
# Match physical stature only in this paired review scene; original FBX models are untouched.
code=code.replace("rest={r:", """bpy.context.view_layer.update()
nam_height=pos(nam,'Head').z-pos(nam,'LeftFoot').z
tam_height=pos(tam,'Head').z-pos(tam,'LeftFoot').z
tam.scale*=nam_height/tam_height
bpy.context.view_layer.update()
rest={r:""")
code=code.replace("r.location.z=(-.015 if frame==0 else -.07+.008*math.cos(2*theta))-(.10 if r==nam else 0)", "r.location.z=0")
# Solve two near-extended legs, with root height determined by both foot contacts.
code=code.replace("        for side,offset,sign in", """        # Height follows limb reach; each actor remains grounded with no bone stretching.
        max_heights=[]
        for leg,off,sgn in [('Left',0,1),('Right',.5,-1)]:
            u=(phase+off)%1;travel=SPEED*PERIOD*.50
            if u<.5:yy=-travel/2+travel*u/.5;lift=0
            else:
                swing=(u-.5)/.5;yy=travel/2-travel*(swing*swing*(3-2*swing))
                lift=(.035 if r==tam and leg=='Right' else .07)*math.sin(math.pi*swing)
            if frame==0:yy=0;lift=0
            hp=pos(r,leg+'UpLeg');kn=pos(r,leg+'Leg');ft=pos(r,leg+'Foot')
            reach=(kn-hp).length+(ft-kn).length-.001
            target=Vector((r.location.x+sgn*.105,yy+r.location.y,.105+lift))
            horizontal=(Vector((hp.x,hp.y,0))-Vector((target.x,target.y,0))).length
            max_heights.append(target.z+math.sqrt(max(.001,reach*reach-horizontal*horizontal))-hp.z)
        r.location.z+=min(max_heights)
        bpy.context.view_layer.update()
        for side,offset,sign in""")
code=code.replace("else .095", "else .07")
code=code.replace("aim(r,side+'Foot',(0,-1,-.35 if lift>0 else -.17))", "aim(r,side+'Foot',(0,-1,-.65))")
code=code.replace("'revision':4,'nam_knee_lowering_m':.10", "'revision':5,'nam_knee_lowering_m':0")
code=code.replace("'speed_kmh':7", "'speed_kmh':9")
code=code.replace("scene.frame_set(1)\n# Keep", "scene.frame_set(0)\n# Keep")
exec(compile(code,str(HERE/'build_animation.py'),'exec'))
