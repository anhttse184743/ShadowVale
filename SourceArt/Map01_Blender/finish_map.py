import bpy, math, os, json
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=os.path.abspath('outputs/ShadowVale_Map01.blend'))
def height(x,y):
    rx=8+12*math.sin(y*.041)+4*math.sin(y*.105)
    hills=14*math.exp(-((x+57)**2/440+(y-48)**2/520))+7*math.exp(-((x-68)**2/650+(y-57)**2/700))
    raw=2.7+.7*math.sin(x*.11)*math.cos(y*.09)+.4*math.sin(y*.22+x*.1)+hills
    return -.8+(raw+.8)*min(1,max(0,(abs(x-rx)-3)/8))
for o,(x,y,r) in zip(sorted([o for o in bpy.data.objects if o.name.startswith('Trampled clearing')],key=lambda o:o.name),[(-62,-53,13),(-27,-9,12),(45,34,17),(-55,46,9),(57,-5,5)]):
    v=[(x,y,height(x,y)+.13)];f=[];n=96;rings=32
    for j in range(1,rings+1):
        for i in range(n):
            a=i*math.tau/n;xx=x+r*j/rings*math.cos(a);yy=y+r*.8*j/rings*math.sin(a);v.append((xx,yy,height(xx,yy)+.13))
        if j==1:f.extend([(0,1+i,1+(i+1)%n) for i in range(n)])
        else:
            b=1+(j-2)*n;c=1+(j-1)*n;f.extend([(b+i,c+i,c+(i+1)%n,b+(i+1)%n) for i in range(n)])
    me=bpy.data.meshes.new('Terrain conforming clearing');me.from_pydata(v,[],f);me.materials.append(bpy.data.materials['Worn ochre trail']);o.data=me
scene=bpy.context.scene;scene.camera=bpy.data.objects['CAM_01 • Isometric overview']
detail=bpy.data.objects['CAM_03 • Abandoned base'];detail.location=(58,12,110);detail.rotation_euler=(Vector((46,31,2))-detail.location).to_track_quat('-Z','Y').to_euler();detail.data.ortho_scale=47
bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath('outputs/ShadowVale_Map01.blend'))
report={'objects':len(scene.objects),'forest_instances':sum(o.name.startswith('Forest canopy') for o in scene.objects),'triangles_evaluated_without_modifiers':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in scene.objects if o.type=='MESH' and not o.hide_render),'terrain_meters':[180,160],'cameras':3,'gameplay_implemented':False}
with open('outputs/Map01_validation.json','w') as f:json.dump(report,f,indent=2)
for cam,name,w,h in [('CAM_01 • Isometric overview','Map01_Isometric',1900,1500),('CAM_02 • Top down layout','Map01_TopDown',1600,1400),('CAM_03 • Abandoned base','Map01_AbandonedBase',1500,1100)]:
    scene.camera=bpy.data.objects[cam];scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=os.path.abspath('outputs/'+name+'.png');bpy.ops.render.render(write_still=True)
print('FINAL_COMPLETE',report,flush=True)
