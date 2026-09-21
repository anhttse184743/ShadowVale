import bpy, math, random, os
from mathutils import Vector
random.seed(99)
bpy.ops.wm.open_mainfile(filepath=os.path.abspath('outputs/ShadowVale_Map01.blend'))
forest=bpy.data.collections['08_Forest']
for o in list(forest.objects):
    if o.name.startswith('Forest canopy'):
        x,y,z=o.location
        # More breathing room beside the stream and in front of the bridge.
        rx=8+12*math.sin(y*.041)+4*math.sin(y*.105)
        remove=(abs(x-rx)<12 and random.random()<.65) or (-9<y<5 and -14<x<30) or random.random()<.16
        if remove:bpy.data.objects.remove(o,do_unlink=True)
        else:o.scale.z*=random.uniform(.9,1.25)
for t in range(5):
    proto=bpy.data.objects['Canopy master %d'%t];old=proto.data
    verts=[tuple(v.co) for v in old.vertices];faces=[];indices=[]
    # Keep wood branches; replace spherical crowns with actual leaf geometry.
    for p in old.polygons:
        if old.materials[p.material_index].name=='Aged teak':faces.append(tuple(p.vertices));indices.append(p.material_index)
    centers=[(2*math.cos(a*2*math.pi/5+t),2*math.sin(a*2*math.pi/5+t),7.8) for a in range(5)]+[(0,0,9)]
    leafslots=[i for i,m in enumerate(old.materials) if m.name.startswith('Canopy')]
    for cx,cy,cz in centers:
        for j in range(210):
            u=random.uniform(-1,1);a=random.random()*math.tau;r=random.random()**.33
            center=Vector((cx+2.25*r*math.sqrt(1-u*u)*math.cos(a),cy+2.1*r*math.sqrt(1-u*u)*math.sin(a),cz+1.9*r*u))
            theta=random.random()*math.tau;length=random.uniform(.28,.60);width=length*random.uniform(.35,.65)
            axis=Vector((math.cos(theta),math.sin(theta),random.uniform(-.65,.65)))*length
            side=Vector((-math.sin(theta),math.cos(theta),random.uniform(-.25,.25)))*width
            idx=len(verts);verts.extend([tuple(center-axis),tuple(center+side),tuple(center+axis),tuple(center-side),tuple(center+Vector((0,0,.08)))])
            faces.extend([(idx,idx+1,idx+4),(idx+1,idx+2,idx+4),(idx+2,idx+3,idx+4),(idx+3,idx,idx+4)])
            indices.extend([random.choice(leafslots)]*4)
    me=bpy.data.meshes.new('Detailed tropical foliage %d'%t);me.from_pydata(verts,[],faces);me.update()
    for m in old.materials:me.materials.append(m)
    for p,mi in zip(me.polygons,indices):p.material_index=mi
    for o in bpy.data.objects:
        if o.type=='MESH' and o.data==old:o.data=me
for m in bpy.data.materials:
    if m.name.startswith('Canopy'):
        c=m.diffuse_color;c=(c[0]*.72,c[1]*.83,c[2]*.65,1);m.diffuse_color=c;m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=c
scene=bpy.context.scene;scene.camera=bpy.data.objects['CAM_01 • Isometric overview'];scene.camera.data.ortho_scale=272
scene.render.resolution_x=1900;scene.render.resolution_y=1500;scene.cycles.samples=24
scene.render.threads_mode='FIXED';scene.render.threads=8
bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath('outputs/ShadowVale_Map01.blend'))
for cam,name,w,h in [('CAM_01 • Isometric overview','Map01_Isometric',1900,1500),('CAM_02 • Top down layout','Map01_TopDown',1600,1400),('CAM_03 • Abandoned base','Map01_AbandonedBase',1500,1100)]:
    scene.camera=bpy.data.objects[cam];scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=os.path.abspath('outputs/'+name+'.png');bpy.ops.render.render(write_still=True)
print('REFINE_COMPLETE',len(bpy.data.objects))
