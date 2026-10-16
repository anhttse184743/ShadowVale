"""Editable, locally authored civilian meshes on a copy of Nam's existing skeleton.
Run: blender --background --python SourceArt/Map02_Civilians/build_civilians.py
No provider calls; original model, rig and animation files are read only.
"""
from pathlib import Path
import bpy, math, json, hashlib
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
OUT=HERE/'exports'; OUT.mkdir(exist_ok=True)
PREVIEW=ROOT/'Logs/Map02_Civilians'; PREVIEW.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene; scene.render.fps=30; scene.unit_settings.system='METRIC'
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/_Project/Art/Characters/Player/Player.fbx'))
source=next(o for o in bpy.data.objects if o.type=='ARMATURE')
source.animation_data_clear()
bpy.context.view_layer.objects.active=source
bpy.ops.object.select_all(action='DESELECT'); source.select_set(True)
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
for o in list(bpy.data.objects):
    if o!=source:bpy.data.objects.remove(o,do_unlink=True)
source.name='SOURCE_SKELETON_DO_NOT_EXPORT'; source.hide_render=True
bone_heads={b.name:b.head_local.copy() for b in source.data.bones}

def material(name,rgb):
    m=bpy.data.materials.new(name);m.diffuse_color=(*rgb,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*rgb,1);bs.inputs['Roughness'].default_value=.88
    return m

skin_mats=[material('Skin_'+str(i),c) for i,c in enumerate([(0.46,.285,.17),(.56,.36,.23),(.62,.405,.27),(.50,.31,.19)])]
hair=material('Hair_charcoal',(.038,.032,.026));gray=material('Hair_salt_and_pepper',(.24,.23,.20))
eye=material('Eyes_dark_brown',(.026,.019,.012));sclera=material('Eyes_warm',(.53,.46,.36));lip=material('Lips_muted',(.32,.16,.115))
seam=material('Seams_dark',(.105,.085,.055));sole=material('Sandal_rubber',(.065,.060,.045));straw=material('Woven_palm',(.47,.37,.19))
cloth_colors=[(.16,.12,.085),(.10,.15,.18),(.25,.22,.14),(.32,.245,.17),(.20,.21,.16),(.16,.18,.20),(.30,.23,.15),(.24,.19,.15),(.20,.24,.23),(.34,.30,.21),(.14,.17,.19),(.29,.23,.19)]
report=[]

class MeshBuilder:
    def __init__(self,name):self.name=name;self.v=[];self.f=[];self.mi=[];self.w=[];self.mats=[]
    def mat(self,m):
        if m not in self.mats:self.mats.append(m)
        return self.mats.index(m)
    def vertex(self,p,weights):self.v.append(tuple(p));self.w.append(weights);return len(self.v)-1
    def face(self,vs,m):self.f.append(vs);self.mi.append(self.mat(m))
    def tube(self,centers,radii,weights,m,sides=12,depths=None):
        rings=[]
        for i,c in enumerate(centers):
            c=Vector(c);d=Vector(centers[min(i+1,len(centers)-1)])-Vector(centers[max(0,i-1)])
            d.normalize();ref=Vector((0,-1,0));a=ref-d*ref.dot(d)
            if a.length<.1:a=Vector((1,0,0))-d*d.x
            a.normalize();b=d.cross(a).normalized()
            rings.append([self.vertex(c+a*math.cos(j*2*math.pi/sides)*(depths[i] if depths else radii[i])+b*math.sin(j*2*math.pi/sides)*radii[i],weights[i]) for j in range(sides)])
        self.face(list(reversed(rings[0])),m);self.face(rings[-1],m)
        for i in range(len(rings)-1):
            for j in range(sides):self.face([rings[i][j],rings[i][(j+1)%sides],rings[i+1][(j+1)%sides],rings[i+1][j]],m)
    def ellipsoid(self,c,scale,weights,m,segments=16,rings=10):
        grid=[]
        for k in range(rings+1):
            lat=-math.pi/2+.015+(math.pi-.03)*k/rings
            grid.append([self.vertex(Vector(c)+Vector((scale[0]*math.cos(lat)*math.cos(j*2*math.pi/segments),scale[1]*math.cos(lat)*math.sin(j*2*math.pi/segments),scale[2]*math.sin(lat))),weights) for j in range(segments)])
        self.face(list(reversed(grid[0])),m);self.face(grid[-1],m)
        for k in range(rings):
            for j in range(segments):self.face([grid[k][j],grid[k][(j+1)%segments],grid[k+1][(j+1)%segments],grid[k+1][j]],m)
    def build(self,rig):
        data=bpy.data.meshes.new(self.name);data.from_pydata(self.v,[],self.f);data.update()
        ob=bpy.data.objects.new(self.name,data);bpy.context.collection.objects.link(ob)
        for m in self.mats:data.materials.append(m)
        for p,idx in zip(data.polygons,self.mi):p.material_index=idx;p.use_smooth=True
        groups={b.name:ob.vertex_groups.new(name=b.name) for b in rig.data.bones}
        for i,weights in enumerate(self.w):
            total=sum(weights.values())
            for name,value in weights.items():groups[name].add([i],value/total,'REPLACE')
        mod=ob.modifiers.new('Original_humanoid_skeleton','ARMATURE');mod.object=rig
        ob.parent=rig
        return ob

def W(name):return {name:1.0}
def torso_weights(z):
    stops=[(.98,'Hips'),(1.13,'Spine'),(1.30,'Spine1'),(1.47,'Spine2')]
    if z<=stops[0][0]:return W('Hips')
    for (a,an),(b,bn) in zip(stops,stops[1:]):
        if z<=b:
            t=(z-a)/(b-a);return {an:1-t,bn:t}
    return W('Spine2')

def continuous_clothes(m,cloth_materials):
    """Join intersecting garment shells in rest pose before skinning: no floating sleeves."""
    selected=[i for i,mat_index in enumerate(m.mi) if m.mats[mat_index] in cloth_materials]
    used=sorted({v for i in selected for v in m.f[i]});remap={v:i for i,v in enumerate(used)}
    data=bpy.data.meshes.new('Garment_union_source');data.from_pydata([m.v[i] for i in used],[],[[remap[v] for v in m.f[i]] for i in selected]);data.update()
    temp=bpy.data.objects.new('Garment_union',data);bpy.context.collection.objects.link(temp)
    kd=KDTree(len(used))
    for i,v in enumerate(used):kd.insert(Vector(m.v[v]),i)
    kd.balance()
    faces_kd=KDTree(len(selected))
    for n,i in enumerate(selected):faces_kd.insert(sum((Vector(m.v[v]) for v in m.f[i]),Vector())/len(m.f[i]),n)
    faces_kd.balance()
    mod=temp.modifiers.new('Continuous_garment_topology','REMESH');mod.mode='VOXEL';mod.voxel_size=.018;mod.use_smooth_shade=True
    smooth=temp.modifiers.new('Relax_garment_surface','SMOOTH');smooth.factor=.55;smooth.iterations=4
    bpy.context.view_layer.update();evaluated=temp.evaluated_get(bpy.context.evaluated_depsgraph_get());result=evaluated.to_mesh()
    out=MeshBuilder(m.name)
    for vertex in result.vertices:
        weights={};near=kd.find_n(vertex.co,4)
        for _,n,distance in near:
            factor=1/max(.00001,distance)**2
            for bone,value in m.w[used[n]].items():weights[bone]=weights.get(bone,0)+value*factor
        total=sum(weights.values());out.vertex(vertex.co,{bone:value/total for bone,value in weights.items()})
    for face in result.polygons:
        _,n,_=faces_kd.find(face.center);out.face(list(face.vertices),m.mats[m.mi[selected[n]]])
    selected_set=set(selected);remaining=[i for i in range(len(m.f)) if i not in selected_set];other_map={}
    for i in remaining:
        newface=[]
        for v in m.f[i]:
            if v not in other_map:other_map[v]=out.vertex(m.v[v],m.w[v])
            newface.append(other_map[v])
        out.face(newface,m.mats[m.mi[i]])
    evaluated.to_mesh_clear();bpy.data.objects.remove(temp,do_unlink=True)
    return out

specs=[
 ('Villager_01_farmer',False,False,1.70,1.02,'short','long'),
 ('Villager_02_boat_worker',False,False,1.73,1.08,'short','short'),
 ('Villager_03_storage_worker',False,False,1.65,1.14,'short','long'),
 ('Villager_04_field_worker',False,False,1.76,.94,'straw','long'),
 ('Villager_05_woman_caregiver',True,False,1.59,1.00,'bun','long'),
 ('Villager_06_woman_farmer',True,False,1.62,1.04,'straw','long'),
 ('Villager_07_woman_storage',True,False,1.57,1.10,'bun','long'),
 ('Villager_08_woman_field',True,False,1.65,.94,'bun','long'),
 ('Villager_09_elder_father',False,True,1.61,.91,'gray','long'),
 ('Villager_10_elder_woman',True,True,1.51,.98,'graybun','long'),
 ('Villager_11_young_worker',False,False,1.68,.91,'short','short'),
 ('Villager_12_woman_boat_worker',True,False,1.61,1.04,'bun','long')]
characters=[]
for idx,(name,female,elder,height,width,hairstyle,pants) in enumerate(specs):
    rig=source.copy();rig.data=source.data.copy();bpy.context.collection.objects.link(rig);rig.name=name+'_Rig';rig.hide_render=False;rig.hide_set(False)
    rig.animation_data_clear()
    for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
    skin=skin_mats[idx%len(skin_mats)];cloth=material(name+'_Shirt',cloth_colors[idx]);trouser=material(name+'_Trousers',tuple(c*.6 for c in cloth_colors[(idx+3)%12]))
    m=MeshBuilder(name+'_Body')
    cx=-.015;cy=.075
    # Tailored torso: hem, waist, ribs, shoulders and a closed neck opening.
    zs=[.92,.98,1.09,1.22,1.36,1.46,1.54,1.59]
    widths=[.175,.18,.17,.18,.205,.235,.20,.065]
    depths=[.108,.115,.115,.122,.126,.113,.097,.064]
    if female:widths=[x*(.94 if i<4 else .91) for i,x in enumerate(widths)]
    ringids=[];sides=20
    for z,rx,ry in zip(zs,widths,depths):
        ringids.append([m.vertex((cx+math.sin(j*2*math.pi/sides)*rx*width,cy+math.cos(j*2*math.pi/sides)*ry,z),torso_weights(z)) for j in range(sides)])
    for i in range(len(ringids)-1):
        for j in range(sides):m.face([ringids[i][j],ringids[i][(j+1)%sides],ringids[i+1][(j+1)%sides],ringids[i+1][j]],cloth)
    m.face(ringids[-1],cloth)
    m.face(list(reversed(ringids[0])),cloth)
    # Shirt placket, small buttons and two pockets, with no military equipment.
    for z in [1.04,1.16,1.28,1.40,1.51]:m.ellipsoid((cx,cy-.126,z),(.008,.005,.008),torso_weights(z),seam,8,4)
    for side in [-1,1]:
        x=cx+side*.10*width
        ids=[m.vertex((x+dx,cy-.133,z),torso_weights(z)) for dx,z in [(-.038,1.39),(.038,1.39),(.038,1.31),(0,1.30),(-.038,1.31)]]
        m.face(ids,cloth)
    m.tube([(cx,cy,1.57),(cx,cy,1.67)],[.055,.052],[W('Neck'),W('Head')],skin,12)
    # Arms have extra elbow rings with blended weights instead of rigid cylinders.
    for side in ['Left','Right']:
        u,e,h=[bone_heads[side+s] for s in ['Arm','ForeArm','Hand']]
        inner=u.lerp(Vector((cx,cy,1.51)),.38)
        cs=[inner,u,u.lerp(e,.35),u.lerp(e,.78),e,e.lerp(h,.20),e.lerp(h,.58),h]
        ws=[W(side+'Shoulder'),{side+'Shoulder':.15,side+'Arm':.85},W(side+'Arm'),{side+'Arm':.8,side+'ForeArm':.2},{side+'Arm':.5,side+'ForeArm':.5},{side+'Arm':.15,side+'ForeArm':.85},W(side+'ForeArm'),{side+'ForeArm':.65,side+'Hand':.35}]
        m.tube(cs,[.056,.072,.069,.060,.056,.053,.045,.031],ws,cloth,14)
        hand=bone_heads[side+'Hand'];mid=bone_heads[side+'HandMiddle1']
        m.tube([hand,hand.lerp(mid,.55)],[.028,.033],[{side+'ForeArm':.3,side+'Hand':.7},W(side+'Hand')],skin,12)
        m.ellipsoid(hand.lerp(mid,.52),(.061,.033,.024),W(side+'Hand'),skin,12,6)
        for finger in ['Thumb','Index','Middle','Ring','Pinky']:
            bones=[rig.data.bones.get(side+'Hand'+finger+str(k)) for k in [1,2,3]]
            if any(b is None for b in bones):continue
            points=[b.head_local.copy() for b in bones]+[bones[-1].tail_local.copy()]
            radii=[.013,.012,.010,.006] if finger=='Thumb' else [.0105,.010,.008,.005]
            m.tube(points,radii,[W(b.name) for b in bones]+[W(bones[-1].name)],skin,8)
        thigh,knee,ankle=[bone_heads[side+s] for s in ['UpLeg','Leg','Foot']]
        if pants=='long':
            points=[thigh,thigh.lerp(knee,.35),thigh.lerp(knee,.85),knee,knee.lerp(ankle,.2),knee.lerp(ankle,.7),ankle+Vector((0,0,-.018))]
            weights=[W(side+'UpLeg'),W(side+'UpLeg'),{side+'UpLeg':.8,side+'Leg':.2},{side+'UpLeg':.5,side+'Leg':.5},{side+'UpLeg':.15,side+'Leg':.85},W(side+'Leg'),W(side+'Leg')]
            m.tube(points,[.106*width,.115*width,.088,.084,.084,.068,.058],weights,trouser,14)
        else:
            m.tube([thigh,thigh.lerp(knee,.65),knee],[.11*width,.102,.087],[W(side+'UpLeg'),W(side+'UpLeg'),{side+'UpLeg':.6,side+'Leg':.4}],trouser,14)
            m.tube([knee,knee.lerp(ankle,.3),knee.lerp(ankle,.7),ankle],[.065,.067,.048,.035],[{side+'UpLeg':.45,side+'Leg':.55},W(side+'Leg'),W(side+'Leg'),W(side+'Foot')],skin,12)
        toe=bone_heads[side+'ToeBase'];footcenter=ankle.lerp(toe,.6)+Vector((0,0,.015))
        m.tube([ankle+Vector((0,0,.035)),ankle,footcenter],[.035,.034,.034],[W(side+'Leg'),{side+'Leg':.4,side+'Foot':.6},W(side+'Foot')],skin,12)
        m.ellipsoid(footcenter,(.063,.11,.045),W(side+'Foot'),skin,14,6)
        m.ellipsoid(footcenter+Vector((0,0,-.033)),(.067,.119,.015),W(side+'Foot'),sole,14,4)
        m.ellipsoid(footcenter+Vector((0,-.012,.035)),(.066,.020,.016),W(side+'Foot'),seam,10,4)
    # A shaped head, restrained eyes/lips, ears and nose: no texture borrowed from soldiers.
    hc=Vector((cx,.050,1.746));hw=.089*(1+(idx%3-1)*.065);hd=.085;hh=.119
    if elder:hw*=.94
    m.ellipsoid(hc,(hw,hd,hh),W('Head'),skin,24,16)
    for s in [-1,1]:
        m.ellipsoid(hc+Vector((s*hw,.007,-.005)),(.018,.014,.033),W('Head'),skin,10,7)
        m.ellipsoid(hc+Vector((s*.034,-.077,.016)),(.014,.005,.004),W('Head'),sclera,12,6)
        m.ellipsoid(hc+Vector((s*.034,-.082,.016)),(.0045,.002,.0038),W('Head'),eye,12,6)
        m.ellipsoid(hc+Vector((s*.034,-.078,.030)),(.018,.004,.0028),W('Head'),gray if elder else hair,12,4)
    m.ellipsoid(hc+Vector((0,-.083,-.005)),(.014,.024,.031),W('Head'),skin,12,8)
    m.ellipsoid(hc+Vector((0,-.100,-.020)),(.019,.013,.012),W('Head'),skin,12,6)
    m.ellipsoid(hc+Vector((0,-.081,-.050)),(.028,.006,.004),W('Head'),lip,14,4)
    # Hair cap follows the upper cranium; buns stay attached to Head.
    hairmat=gray if elder else hair
    cap=[];n=24
    for k in range(7):
        lat=.20+1.34*k/6
        cap.append([m.vertex(hc+Vector((hw*1.025*math.cos(lat)*math.cos(j*2*math.pi/n),hd*1.035*math.cos(lat)*math.sin(j*2*math.pi/n),hh*1.025*math.sin(lat))),W('Head')) for j in range(n)])
    for k in range(6):
        for j in range(n):m.face([cap[k][j],cap[k][(j+1)%n],cap[k+1][(j+1)%n],cap[k+1][j]],hairmat)
    if 'bun' in hairstyle:m.ellipsoid(hc+Vector((0,.088,.024)),(.045,.043,.041),W('Head'),hairmat,14,8)
    if female:
        for s in [-1,1]:m.ellipsoid(hc+Vector((s*.081,.010,.018)),(.012,.047,.063),W('Head'),hairmat,12,7)
    if hairstyle=='straw':
        brim=[]
        for radius,z in [(.205,1.825),(.17,1.84),(.09,1.88),(.012,1.98)]:brim.append([m.vertex((cx+radius*math.cos(j*2*math.pi/32),.05+radius*math.sin(j*2*math.pi/32),z),W('Head')) for j in range(32)])
        for k in range(3):
            for j in range(32):m.face([brim[k][j],brim[k][(j+1)%32],brim[k+1][(j+1)%32],brim[k+1][j]],straw)
    m=continuous_clothes(m,{cloth,trouser})
    ob=m.build(rig)
    # UVs are editable and exportable; no unlicensed photo textures.
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
    scale=height/(1.98 if hairstyle=='straw' else 1.87)
    rig.scale=(scale,scale,scale)
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);ob.select_set(True);bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
    # Review pose: arms lowered with mild elbow bend, no wrist compensation.
    bpy.context.view_layer.update()
    for side,sign in [('Left',1),('Right',-1)]:
        for suffix,direction in [('Arm',Vector((sign*.16,-.04,-1))),('ForeArm',Vector((sign*.08,-.16,-1)))]:
            pb=rig.pose.bones[side+suffix];current=pb.matrix.copy()
            q=(pb.tail-pb.head).normalized().rotation_difference(direction.normalized())@current.to_quaternion()
            pb.matrix=Matrix.LocRotScale(pb.head,q,current.to_scale());bpy.context.view_layer.update()
    rig.location=((idx%6-2.5)*1.30,(idx//6)*2.0,0)
    characters.append((rig,ob))
    bad=sum(1 for ws in m.w if abs(sum(ws.values())-1)>1e-5)
    tris=sum(len(p.vertices)-2 for p in ob.data.polygons)
    report.append({'id':name,'gender_presentation':'female' if female else 'male','elder':elder,'height_m':height,'vertices':len(ob.data.vertices),'triangles':tris,'bones':len(rig.data.bones),'unweighted_vertices':sum(not ws for ws in m.w),'non_normalized_input_weights':bad,'fbx':str(OUT/(name+'.fbx'))})
bpy.data.objects.remove(source,do_unlink=True)
floor_mat=material('Preview_ground',(.14,.17,.135))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.055));floor=bpy.context.object;floor.name='PREVIEW_ONLY_ground';floor.data.materials.append(floor_mat)
camera_data=bpy.data.cameras.new('Review_camera');camera=bpy.data.objects.new('Review_camera',camera_data);bpy.context.collection.objects.link(camera);scene.camera=camera
camera_data.type='ORTHO';camera_data.ortho_scale=9.0;camera.location=(4.2,-11,5.0);target=Vector((0,1, .82));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
scene.world=bpy.data.worlds.new('Review_world')
scene.render.engine='BLENDER_WORKBENCH';sh=scene.display.shading;sh.light='STUDIO';sh.color_type='MATERIAL';sh.show_shadows=True;sh.show_cavity=True;sh.cavity_type='BOTH';sh.curvature_ridge_factor=1.2;sh.curvature_valley_factor=.6;sh.show_specular_highlight=True;sh.background_type='WORLD';scene.world.color=(.12,.14,.115)
scene.render.resolution_x=1800;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
sh.show_specular_highlight=False;sh.curvature_ridge_factor=.7;sh.curvature_valley_factor=.4
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(PREVIEW/'civilian-lineup.png');bpy.ops.render.render(write_still=True)
# Close view of caregiver and father, retaining every original bone.
for i,(rig,ob) in enumerate(characters):ob.hide_render=i not in [4,8]
characters[4][0].location=(-.55,0,0);characters[8][0].location=(.55,0,0)
camera_data.ortho_scale=2.65;camera.location=(2,-6,2.5);camera.rotation_euler=(Vector((0,0,.98))-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1200;scene.render.resolution_y=1000;scene.render.filepath=str(PREVIEW/'family-detail.png');bpy.ops.render.render(write_still=True)
for i,(rig,ob) in enumerate(characters):ob.hide_render=False;rig.location=((i%6-2.5)*1.30,(i//6)*2,0)
camera_data.ortho_scale=9.0;camera.location=(4.2,-11,5);camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
scene['asset_status']='Blender prototype; static review only. Unity skinning and animation review still required.'
scene['source_rig']='Existing Player.fbx skeleton, copied; source file unchanged.'
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Map02_Civilians.blend'))
receipt={'status':'blender_authored_static_review_pending','method':'Local Blender Python mesh authoring, no paid provider','source_rig':str(ROOT/'Assets/_Project/Art/Characters/Player/Player.fbx'),'source_sha256':hashlib.sha256((ROOT/'Assets/_Project/Art/Characters/Player/Player.fbx').read_bytes()).hexdigest(),'characters':report,'limits':{'max_triangles_per_character':30000,'retains_all_62_bones':True},'files':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(OUT.glob('*.fbx'))}}
(HERE/'asset-receipt.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf8')
print('CIVILIANS_COMPLETE',len(report),max(r['triangles'] for r in report))
