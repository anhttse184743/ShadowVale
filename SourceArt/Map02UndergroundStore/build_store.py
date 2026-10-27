"""Blender-authored metre-scale Map 2 store. Unity coordinates mapped to Blender (x,-z,y)."""
import bpy, math, random, json
import numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
random.seed(54)
ROOT=Path(__file__).resolve().parents[2]; HERE=Path(__file__).resolve().parent
OUT=ROOT/'Assets/_Project/Map01/Resources/Story/NightStore';TEX=OUT/'Textures'
TEX.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
def xyz(p):return (-p[0],-p[2],p[1])
def texture(name,wood):
 n=1024;y,x=np.mgrid[0:n,0:n]/n;rng=np.random.default_rng(15 if wood else 71)
 if wood:
  grain=np.sin(x*360+np.sin(y*18)*1.7+np.sin(y*53)*.4)*.5+np.sin(x*810+np.sin(y*9)*4)*.2
  knots=np.sin(np.sqrt(((x-.37)*7)**2+((y-.6)*.6)**2)*90)*np.exp(-(((x-.37)*8)**2+((y-.6)*2)**2))
  value=.80+.11*grain+.13*knots+rng.normal(0,.025,(n,n));base=np.array([.37,.22,.115])
 else:
  value=np.full((n,n),.85)
  for k in range(20):
   angle=rng.uniform(0,math.tau);freq=rng.uniform(30,280);value+=np.sin((x*math.cos(angle)+y*math.sin(angle))*freq+rng.uniform(0,math.tau))*.018
  value+=rng.normal(0,.045,(n,n));base=np.array([.34,.245,.145])
 rgb=np.clip(value[:,:,None]*base[None,None,:],0,1);rgba=np.dstack([rgb,np.ones((n,n))]).astype('float32')
 im=bpy.data.images.new(name,width=n,height=n);im.pixels.foreach_set(rgba.ravel());im.filepath_raw=str(TEX/(name+'.png'));im.file_format='PNG';im.save();return im
woodtex=texture('NightStore_Walnut',True);earthtex=texture('NightStore_Earth',False)
def material(name,color,tex=None,metal=0,rough=.8):
 m=bpy.data.materials.new('NS_'+name);m.diffuse_color=(*color,1);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 if tex:
  node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=tex;m.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color']);bump=m.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.16;bump.inputs['Distance'].default_value=.025;m.node_tree.links.new(node.outputs['Color'],bump.inputs['Height']);m.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal'])
 return m
wood=material('Walnut',(.34,.19,.085),woodtex);earth=material('Earth',(.34,.25,.15),earthtex);metal=material('Iron',(.075,.09,.085),metal=.75,rough=.52);cloth=material('Canvas',(.32,.33,.19));label=material('Paper',(.70,.63,.43));glass=material('LampAmber',(.9,.38,.07),metal=.15,rough=.18)
glass.node_tree.nodes.get('Principled BSDF').inputs['Emission Color'].default_value=(1,.3,.045,1);glass.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'].default_value=3
parts={};lid=[]
def remember(o,group):parts.setdefault(group,[]).append(o);return o
def box(name,p,size,mat,group,bevel=.015):
 bpy.ops.mesh.primitive_cube_add(size=1,location=xyz(p));o=bpy.context.object;o.name=name;o.dimensions=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mat)
 if bevel:
  mod=o.modifiers.new('Worn softened edges','BEVEL');mod.width=bevel;mod.segments=2;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 return remember(o,group)
def rod(name,a,b,r,mat,group,verts=10):
 a=Vector(xyz(a));b=Vector(xyz(b));d=b-a;bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=r,depth=d.length,location=(a+b)*.5);o=bpy.context.object;o.name=name;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();o.data.materials.append(mat);return remember(o,group)
def ring(name,p,major,minor,mat,group,rot=None):
 bpy.ops.mesh.primitive_torus_add(major_segments=20,minor_segments=6,location=xyz(p),major_radius=major,minor_radius=minor);o=bpy.context.object;o.name=name
 if rot:o.rotation_euler=rot
 o.data.materials.append(mat);return remember(o,group)
def dirt_surface(name,xmin,xmax,zmin,zmax,y,ceiling=False):
 nx=max(2,round((xmax-xmin)*5));nz=max(2,round((zmax-zmin)*5));verts=[];faces=[]
 for j in range(nz+1):
  for i in range(nx+1):
   x=xmin+(xmax-xmin)*i/nx;z=zmin+(zmax-zmin)*j/nz;w=0 if i in [0,nx] or j in [0,nz] else random.uniform(-.018,.018);verts.append(xyz((x,y+w,z)))
 for j in range(nz):
  for i in range(nx):
   a=j*(nx+1)+i;f=(a,a+1,a+nx+2,a+nx+1);faces.append(tuple(reversed(f)) if ceiling else f)
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);scene.collection.objects.link(o);o.data.materials.append(earth);uv=mesh.uv_layers.new()
 for polygon in mesh.polygons:polygon.use_smooth=True
 for face in mesh.polygons:
  for li in face.loop_indices:
   v=mesh.vertices[mesh.loops[li].vertex_index].co;uv.data[li].uv=(v.x*.45,v.y*.45)
 remember(o,name);return o
dirt_surface('EarthFloor',-1,7,-1.5,3.5,-7)
dirt_surface('EarthCeilingBack',-1,7,1.5,3.5,-3,True);dirt_surface('EarthCeilingMain',1,7,-1.5,1.5,-3,True)
def boards(group,axis,at,lo,hi,ylo,yhi):
 count=math.ceil((hi-lo)/.25)
 for i in range(count):
  t=lo+(i+.5)*(hi-lo)/count;h=yhi-ylo;v=random.uniform(.97,1.02)
  p=(at,(ylo+yhi)*.5,t) if axis=='x' else (t,(ylo+yhi)*.5,at);s=(.16,h,(hi-lo)/count-.006) if axis=='x' else ((hi-lo)/count-.006,h,.16)
  box(group+' plank '+str(i),p,s,wood,group,.008)
  for y in [ylo+.16,yhi-.16]:
   a=(at+(.09 if at<0 else -.09),y,t) if axis=='x' else (t,y,at+(.09 if at<0 else -.09));b=(a[0]+(.014 if axis=='x' else 0),a[1],a[2]+(.014 if axis=='z' else 0));rod('Hand forged nail',a,b,.011,metal,group,6)
boards('WallWest','x',-1.09,-1.5,1.5,-7,.02);boards('WallWestBack','x',-1.09,1.5,3.5,-7,-3)
boards('WallEast','x',7.09,-1.5,3.5,-7,-3)
boards('WallFront','z',-1.59,-1,7,-7,-3);boards('WallBack','z',3.59,-1,7,-7,-3)
boards('ShaftEast','x',1.09,-1.5,1.5,-3,.02);boards('ShaftFront','z',-1.59,-1,1,-3,.02);boards('ShaftBack','z',1.59,-1,1,-3,.02)
for x in [-.86,1.1,3.1,5.1,6.86]:
 for z in [-1.32,3.32]:box('Upright walnut support',(x,-5,z),(.18,4,.18),wood,'Supports',.025)
for z in [-1.3,3.3]:box('Wall crown rail',(3,-3.14,z),(8,.22,.18),wood,'Supports');box('Wall foot rail',(3,-6.88,z),(8,.22,.18),wood,'Supports')
for x in [-1.12,1.12]:box('Hatch frame side',(x,.08,0),(.24,.20,3.45),wood,'HatchFrame',.025)
for z in [-1.62,1.62]:box('Hatch frame end',(0,.08,z),(2,.20,.24),wood,'HatchFrame',.025)
for i in range(8):box('Hatch plank '+str(i),(-.875+i*.25,.14,0),(.247,.12,2.98),wood,'HatchLeaf',.012)
for z in [-1.15,1.15]:box('Hatch iron strap',(0,.214,z),(1.92,.035,.09),metal,'HatchLeaf',.01)
for z in [-1.13,1.13]:rod('Hinge barrel',(1.015,.18,z-.14),(1.015,.18,z+.14),.055,metal,'HatchLeaf')
rod('Knob stem',(-.72,.21,0),(-.72,.30,0),.028,metal,'HatchLeaf');bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=.065,location=xyz((-.72,.32,0)));o=bpy.context.object;o.name='Rounded pull knob';o.data.materials.append(wood);remember(o,'HatchLeaf')
for z in [-.36,.36]:box('Ladder rail',(-.80,-3.40,z),(.095,6.9,.095),wood,'Ladder',.018)
for i in range(24):rod('Ladder rung '+str(i),(-.74,-.06-i*.29,-.36),(-.74,-.06-i*.29,.36),.035,wood,'Ladder',12)
def shelf(name,center,length,rotate=False):
 # Shelf depth along local z, length along local x. Rotate the far-wall unit ninety degrees.
 def tr(p):return (center[0]+(p[2] if rotate else p[0]),center[1]+p[1],center[2]+(-p[0] if rotate else p[2]))
 def dims(s):return (s[2],s[1],s[0]) if rotate else s
 for x in [-length/2+.07,length/2-.07]:
  for z in [-.27,.27]:box(name+' post',tr((x,1.05,z)),dims((.10,2.1,.10)),wood,name)
 for y in [.18,.85,1.52,2.12]:box(name+' shelf board',tr((0,y,0)),dims((length,.085,.66)),wood,name)
 for tier,y in enumerate([.25,.92,1.59]):
  for k,x in enumerate(np.linspace(-length*.34,length*.34,3)):
   if tier==0 and k==1:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=10,location=xyz(tr((float(x),y+.20,0))));sack=bpy.context.object;sack.name='Canvas supply sack';sack.scale=(.32,.23,.22) if not rotate else (.23,.32,.22);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);sack.data.materials.append(cloth);remember(sack,name)
    rod('Tied sack neck',tr((float(x),y+.35,0)),tr((float(x),y+.47,0)),.055,cloth,name,10)
    continue
   if tier==1 and k==2:
    for dx in [-.19,0,.19]:rod('Metal supply tin',tr((float(x)+dx,y,0)),tr((float(x)+dx,y+.31,0)),.075,metal,name,16)
    continue
   p=tr((float(x),y+.19,0));box('Supply crate',p,dims((.62,.38,.42)),wood,name,.02)
   for delta in [-.22,.22]:box('Crate reinforcing band',tr((float(x)+delta,y+.19,-.219)),dims((.045,.37,.018)),metal,name,.004)
   box('Inventory paper label',tr((float(x),y+.19,-.23)),dims((.22,.12,.008)),label,name,.001)
   if tier==1 and k==1:
    for j in range(2):box('Folded field cloth',tr((float(x),y+.44+j*.08,.02)),dims((.55,.07,.34)),cloth,name,.025)
shelf('ShelfFront',(3.1,-7,-1.08),3.8);shelf('ShelfBack',(3.1,-7,3.08),3.8);shelf('ShelfEast',(6.58,-7,1),2.8,True)
for index,x in enumerate([2.2,5.2]):
 g='OilLamp'+str(index+1);rod('Ceiling hanging chain',(x,-3.02,1),(x,-3.38,1),.012,metal,g,8)
 ring('Hanger loop',(x,-3.40,1),.06,.012,metal,g,(math.pi/2,0,0));bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=10,location=xyz((x,-3.65,1)));o=bpy.context.object;o.name='Rounded oil lamp glass';o.scale=(.075,.075,.16);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(glass);remember(o,g)
 rod('Oil reservoir',(x,-3.94,1),(x,-3.81,1),.115,metal,g,16)
 for y in [-3.83,-3.45]:rod('Lamp metal cap',(x,y-.028,1),(x,y+.028,1),.11,metal,g,16)
 for z in [.90,1.10]:rod('Lamp protective upright',(x,-3.8,z),(x,-3.48,z),.012,metal,g,8)
 # Render-only lighting; Unity creates exactly two matching Point lights.
 light=bpy.data.lights.new('Preview lantern light '+str(index),'POINT');light.energy=190;light.color=(1,.48,.16);light.shadow_soft_size=.22;ob=bpy.data.objects.new(light.name,light);ob.location=xyz((x,-3.65,1));scene.collection.objects.link(ob)
# Consolidate by architectural section; keep hatch pivot independent for the game animation.
models=[]
for name,objects in parts.items():
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=name;models.append(o)
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
pivot=bpy.data.objects.new('HatchPivot',None);scene.collection.objects.link(pivot);pivot.location=xyz((1,.14,0));leaf=next(o for o in models if o.name=='HatchLeaf')
for vertex in leaf.data.vertices:vertex.co-=pivot.location
leaf.parent=pivot;leaf.matrix_parent_inverse=Matrix.Identity(4);leaf.matrix_basis=Matrix.Identity(4);models.append(pivot)
bpy.ops.object.select_all(action='DESELECT')
for o in models:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'NightStore.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
(HERE/'model-receipt.json').write_text(json.dumps({'roomInterior':[8,5,4],'surfaceToFloor':7,'hatch':[2,3],'shelves':3,'lamps':2,'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in models if o.type=='MESH'),'blenderVersion':bpy.app.version_string},indent=2))
# Actual authored Blender preview, front two walls hidden as an architectural cutaway.
for name in ['WallFront','ShaftFront','WallWest','WallWestBack','EarthCeilingMain','EarthCeilingBack']:bpy.data.objects[name].hide_render=True
pivot.rotation_euler[1]=math.radians(-105)
world=bpy.data.worlds.new('Store preview world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.13,.15,1);world.node_tree.nodes['Background'].inputs[1].default_value=.4;scene.world=world
def area(name,p,target,energy,size):
 l=bpy.data.lights.new(name,'AREA');l.energy=energy;l.shape='DISK';l.size=size;o=bpy.data.objects.new(name,l);scene.collection.objects.link(o);o.location=xyz(p);o.rotation_euler=(Vector(xyz(target))-o.location).to_track_quat('-Z','Y').to_euler()
area('Cutaway softbox',(-4,3,-7),(2,-3,1),1500,9);area('Fill',(7,0,7),(3,-4,1),900,7)
cam=bpy.data.objects.new('Preview camera',bpy.data.cameras.new('Preview camera'));scene.collection.objects.link(cam);cam.location=xyz((-11,8,-13));cam.rotation_euler=(Vector(xyz((2,-3,1)))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=15;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=40;scene.cycles.use_denoising=True;scene.render.resolution_x=1400;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(HERE/'blender-cutaway.png');bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'NightStore.blend'));bpy.ops.render.render(write_still=True)
for o in models:o.hide_render=False
pivot.rotation_euler[1]=0;cam.data.type='PERSP';cam.data.lens=20;cam.location=xyz((0,-5.2,1));cam.rotation_euler=(Vector(xyz((3.5,-4.8,1)))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(HERE/'blender-interior.png');bpy.ops.render.render(write_still=True)
print('NIGHT_STORE_BLENDER_COMPLETE',OUT)
