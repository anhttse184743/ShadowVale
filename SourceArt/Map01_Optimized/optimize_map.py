"""Author low-cost shared tree LODs and export Unity-native mesh source data.
Run from the project root with Blender 5.2 --background --python this_file.
"""
import bpy,bmesh,math,random,json,gzip,os
from mathutils import Vector, Matrix
random.seed(416)
ROOT=os.environ.get('SHADOWVALE_PROJECT',r'G:\game\ShadowVale')
OUT=os.path.join(ROOT,'SourceArt','Map01_Optimized')
os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT,'ShadowVale_Map01_Master.blend'))
scene=bpy.context.scene
original=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in scene.objects if o.type=='MESH' and not o.hide_render)
trees=[o for o in scene.objects if o.name.startswith('Forest canopy')]
palette=[bpy.data.materials['Aged teak']]
for i,col in enumerate([(.12,.23,.07,1),(.19,.31,.095,1),(.26,.37,.12,1),(.16,.285,.085,1),(.32,.42,.15,1)]):
    mat=bpy.data.materials.new('Leaf jade %d'%i);mat.diffuse_color=col;mat.use_nodes=True;mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=col;mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.8;palette.append(mat)
lodcol=bpy.data.collections.new('12_Unity_LOD_library');scene.collection.children.link(lodcol)
def make_tree(t,lod):
    rng=random.Random(814+t)
    v=[];f=[];mi=[]
    def rod(a,b,r,sides=5):
        axis=Vector(b)-Vector(a);q=axis.to_track_quat('Z','Y').to_matrix();start=len(v)
        for z,rr in [(0,r),(axis.length,r*.3)]:
            for j in range(sides):v.append(tuple(Vector(a)+q@Vector((rr*math.cos(j*math.tau/sides),rr*math.sin(j*math.tau/sides),z))))
        for j in range(sides):f.append((start+j,start+(j+1)%sides,start+sides+(j+1)%sides,start+sides+j));mi.append(0)
    def leaf(p,angle,length,width,tilt,col):
        along=Vector((math.cos(angle),math.sin(angle),tilt)).normalized()*length
        side=Vector((-math.sin(angle),math.cos(angle),0))*width
        mid=p+along*.45;ridge=mid+Vector((0,0,width*.32))
        start=len(v);v.extend(map(tuple,[p,mid-side,p+along,mid+side,ridge]))
        for aa,bb in [(0,1),(1,2),(2,3),(3,0)]:f.append((start+aa,start+bb,start+4));mi.append(col)
    rod((0,0,0),(.15,-.1,8.7),.27,6 if lod==0 else 4)
    clusters=[(2.0*math.cos(a*math.tau/6+t*.7),2.0*math.sin(a*math.tau/6+t*.7),7.1+.8*math.sin(a*2+t)) for a in range(6)]+[(.1,0,9)]
    for c,center in enumerate(clusters):
        center=Vector(center);rod((.1,0,4.7),center,.10,4)
        fronds=[8,6,4][lod];pairs=[3,2,1][lod]
        for j in range(fronds):
            angle=j*math.tau/fronds+c*.77+rng.uniform(-.17,.17)
            axis=Vector((math.cos(angle),math.sin(angle),rng.uniform(-.15,.2)))
            end=center+axis*1.8
            if lod==0:rod(center,end,.026,3)
            for k in range(pairs):
                p=center+axis*(.2+1.25*k/max(1,pairs-1))
                for sign in [-1,1]:
                    leaf(p,angle+sign*rng.uniform(.65,1.05),rng.uniform(.8,1.15)*(1+.25*lod),rng.uniform(.22,.34)*(1+.5*lod),rng.uniform(-.35,.3),1+(c+j+k+t)%5)
            leaf(end-axis*.25,angle,1.0+.2*lod,.3+.10*lod,-.24,1+(c+j+t)%5)
    me=bpy.data.meshes.new('Tree_%d_LOD%d'%(t,lod));me.from_pydata(v,[],f);me.update()
    for m in palette:me.materials.append(m)
    for poly,idx in zip(me.polygons,mi):poly.material_index=idx;poly.use_smooth=(idx==0)
    o=bpy.data.objects.new(me.name,me);lodcol.objects.link(o);o.hide_render=True;o.hide_viewport=True
    return me

tree_types={};tree_meshes={}
for t in range(5):
    old=bpy.data.objects['Canopy master %d'%t].data
    for lod in range(3):tree_meshes[(t,lod)]=make_tree(t,lod)
    for o in trees:
        if o.data==old:tree_types[o.name]=t;o.data=tree_meshes[(t,0)]
for o in list(bpy.data.objects):
    if o.name.startswith('Canopy master'):bpy.data.objects.remove(o,do_unlink=True)
# Merge the visually identical stump meshes through Blender's linked data;
# export static dressing in spatial chunks with baked vertex colours.
payload={'version':2,'meshes':[],'trees':[],'props':[],'boxes':[],'routes':[],'points':[]}
def unity(v):return [round(v[0],4),round(v[2],4),round(v[1],4)]
def newmesh(name,kind):return {'id':name,'kind':kind,'vertices':[],'normals':[],'colors':[],'triangles':[],'_cache':{}}
def append_mesh(target,me,matrix=Matrix.Identity(4)):
    nm=matrix.to_3x3().inverted().transposed()
    for poly in me.polygons:
        col=me.materials[poly.material_index].diffuse_color if len(me.materials)>poly.material_index and me.materials[poly.material_index] else (.3,.35,.2,1)
        color=[round(float(x),4) for x in col];normal=unity((nm@poly.normal).normalized());ids=[]
        for vi in poly.vertices:
            if poly.use_smooth:normal=unity((nm@me.vertices[vi].normal).normalized())
            pos=unity(matrix@me.vertices[vi].co);key=tuple(pos+normal+color)
            index=target['_cache'].get(key)
            if index is None:
                index=len(target['vertices'])//3;target['_cache'][key]=index;target['vertices'].extend(pos);target['normals'].extend(normal);target['colors'].extend(color)
            ids.append(index)
        for j in range(1,len(ids)-1):target['triangles'].extend([ids[0],ids[j+1],ids[j]])
for (t,lod),me in tree_meshes.items():
    data=newmesh('Tree_%d_LOD%d'%(t,lod),'tree');append_mesh(data,me);payload['meshes'].append(data)
for o in trees:
    payload['trees'].append({'type':tree_types[o.name],'position':unity(o.location),'scale':unity(o.scale),'yaw':round(-math.degrees(o.rotation_euler.z),4)})
chunks={};collision_chunks={}
prop_prototypes={}
depsgraph=bpy.context.evaluated_depsgraph_get()
walk_prefix=('MAP01','Bridge decking','Bridge approach ramp','Floor board','Entry step','Intelligence shelter floor','Observation platform')
block_prefix=('Cover','Supply crate','Tall rock','Fallen trunk','Rounded river boulder')
for o in list(scene.objects):
    if o.type!='MESH' or o.hide_render or o in trees:continue
    if o.name=='Diorama earth foundation' or o.name.startswith(('Natural woodland footpath','Trampled clearing','New rest clearing')):continue
    ev=o.evaluated_get(depsgraph);me=ev.to_mesh();me.calc_loop_triangles()
    prop=('Grass'+o.name.split('_')[1]) if o.name.startswith('Groundcover_') else 'Crate' if o.name.startswith('Supply crate') else 'Drum' if o.name.startswith('Rusted empty fuel drum') else 'Rock' if o.name.startswith(('Rounded river boulder','Mossy rock','Base scattered rubble')) else None
    if prop:
        dims=[max(v.co[i] for v in me.vertices)-min(v.co[i] for v in me.vertices) for i in range(3)]
        if prop not in prop_prototypes:
            pm=newmesh('Prop_'+prop,'prop');append_mesh(pm,me);prop_prototypes[prop]=(pm,dims);payload['meshes'].append(pm)
        pm,base_dims=prop_prototypes[prop]
        scale=[o.scale[i]*dims[i]/max(.001,base_dims[i]) for i in range(3)]
        payload['props'].append({'id':'Prop_'+prop,'position':unity(o.location),'scale':unity(scale),'yaw':round(-math.degrees(o.rotation_euler.z),4)})
        ev.to_mesh_clear();continue
    # Ground is split by face centre so culling is useful for a large terrain.
    water=o.name.startswith(('Continuous stream','Stream ripple'))
    walk=o.name.startswith(walk_prefix);block=o.name.startswith(block_prefix)
    world=o.matrix_world
    for poly in me.polygons:
        center=world@poly.center;cx=int(math.floor(center.x/24));cy=int(math.floor(center.y/24));key=(cx,cy,'water' if water else 'opaque')
        if key not in chunks:chunks[key]=newmesh('Chunk_%d_%d_%s'%key,key[2])
        dest=chunks[key]
        col=me.materials[poly.material_index].diffuse_color if len(me.materials)>poly.material_index and me.materials[poly.material_index] else (.3,.35,.2,1)
        color=[round(float(x),4) for x in col]
        if o.name.startswith('MAP01'):
            factor=.86+.16*math.sin(center.x*.5)*math.cos(center.y*.37)
            color=[round(c*factor,4) for c in color[:3]]+[1]
        nm=world.to_3x3().inverted().transposed();normal=unity((nm@poly.normal).normalized());ids=[]
        for vi in poly.vertices:
            if poly.use_smooth:normal=unity((nm@me.vertices[vi].normal).normalized())
            pos=unity(world@me.vertices[vi].co);vk=tuple(pos+normal+color);idx=dest['_cache'].get(vk)
            if idx is None:idx=len(dest['vertices'])//3;dest['_cache'][vk]=idx;dest['vertices'].extend(pos);dest['normals'].extend(normal);dest['colors'].extend(color)
            ids.append(idx)
        for j in range(1,len(ids)-1):dest['triangles'].extend([ids[0],ids[j+1],ids[j]])
    # Collision excludes leaves, tiny decorative ribs, papers and ferns.
    if walk or block:
        k=('Walk' if walk else 'Block',int(o.location.x//24),int(o.location.y//24))
        if k not in collision_chunks:collision_chunks[k]=newmesh('Collision_%s_%d_%d'%k,'walk' if walk else 'block')
        append_mesh(collision_chunks[k],me,world)
    ev.to_mesh_clear()
extra_boxes=[]
for o in scene.objects:
    if o.type!='MESH' or not o.name.startswith(('Rear timber wall','Side wall','Front wall','Back wall','Bamboo fence','Fence rail','Bridge handrail','Watch railing','Map table','Old camp bench')):continue
    pts=[o.matrix_world@Vector(c) for c in o.bound_box];lo=[min(p[i] for p in pts) for i in range(3)];hi=[max(p[i] for p in pts) for i in range(3)]
    if o.name.startswith(('Bridge handrail','Fence rail','Watch railing')):lo[2]=hi[2]-1.15
    if o.name.startswith(('Map table','Old camp bench')):lo[2]-=.8
    extra_boxes.append({'name':'Solid '+o.name,'position':unity(Vector([(lo[i]+hi[i])/2 for i in range(3)])),'size':unity(Vector([max(.18,hi[i]-lo[i]) for i in range(3)]))})
payload['boxes'].extend(extra_boxes)
payload['meshes'].extend(chunks.values());payload['meshes'].extend(collision_chunks.values())
for m in payload['meshes']:m.pop('_cache')
def rx(y):return 8+12*math.sin(y*.041)+4*math.sin(y*.105)
def height(x,y):
    hills=14*math.exp(-((x+57)**2/440+(y-48)**2/520))+7*math.exp(-((x-68)**2/650+(y-57)**2/700))
    raw=2.7+.7*math.sin(x*.11)*math.cos(y*.09)+.4*math.sin(y*.22+x*.1)+hills
    return -.8+(raw+.8)*min(1,max(0,(abs(x-rx(y))-3)/8))
for y in []: # The shallow stream is traversable; no invisible wall along its banks.
    if abs(y)<3:continue
    payload['boxes'].append({'name':'Stream boundary','position':[rx(y),1,y],'size':[6,4,2.25]})
for x,z,sx,sz in [(-110,0,1,200),(110,0,1,200),(0,-100,220,1),(0,100,220,1)]:payload['boxes'].append({'name':'Map boundary','position':[x,10,z],'size':[sx,30,sz]})
routes=[('main',[(-63,-55),(-56,-49),(-47,-34),(-37,-22),(-27,-10),(-13,0),(-5,0),(20,0),(25,1),(35,14),(44,26),(51,25),(44,34),(57,44),(47,57),(36,67)]),('stealth',[(-47,-34),(-60,-22),(-59,-5),(-46,9),(-29,15),(-13,10),(-5,1)]),('diversion',[(-37,-22),(-23,-26),(-10,-18),(-9,-7),(-13,0)]),('base_flank',[(25,1),(39,-4),(58,3),(61,18),(54,25)]),('overlook',[(-46,9),(-54,24),(-55,38)])]
routes[0][1].extend([(35,77),(26,83),(21,85)])
routes.append(('eastern_loop',[(58,3),(77,13),(83,31),(81,49),(70,61),(57,44)]))
for name,points in routes:payload['routes'].append({'id':name,'positions':[c for x,y in points for c in [x,height(x,y)+.2,y]]})
for name,x,y,kind in [('supplies',-62,-57,0),('tutorial_loot',-59,-47,1),('workbench',-59,-49,2),('documents',51,24.3,3),('river_exit',21,85,4),('hide_west',-60,-15,5),('hide_east',-15,-23,5),('cover_west',-30,-13,6),('cover_east',-19,-6,6),('cover_base',43,19,6)]:
    payload['points'].append({'id':name,'kind':kind,'position':[x,height(x,y)+.15,y]})
payload['spawn']=[-63,height(-63,-55)+.2,-55];payload['hung']=[-61,height(-61,-54)+.2,-54];payload['encounterExit']=[25,height(25,1)+.2,1]
payload['patrols']=[{'id':'patrol_0','positions':[c for x,y in [(-27,-9),(-24,-10),(-24,-5),(-28,-5)] for c in [x,height(x,y)+.15,y]]},{'id':'patrol_1','positions':[c for x,y in [(-33,-6),(-30,-3),(-25,-1),(-29,-7)] for c in [x,height(x,y)+.15,y]]},{'id':'patrol_2','positions':[c for x,y in [(-19,-12),(-16,-8),(-13,-3),(-20,-7)] for c in [x,height(x,y)+.15,y]]}]
with gzip.open(os.path.join(OUT,'Map01.meshdata.json.gz'),'wt',encoding='utf8') as f:json.dump(payload,f,separators=(',',':'))
tree_tri=[sum(len(p.vertices)-2 for p in tree_meshes[(0,l)].polygons) for l in range(3)]
static_tri=sum(len(m['triangles'])//3 for m in chunks.values())
prop_tri=sum(len(prop_prototypes[p['id'][5:]][0]['triangles'])//3 for p in payload['props'])
report={'before_triangles':original,'tree_instances':len(trees),'prop_instances':len(payload['props']),'prefab_variants':5+len(prop_prototypes),'shared_tree_meshes':15,'tree_triangles_per_lod':tree_tri,'static_triangles':static_tri,'maximum_lod0_triangles':static_tri+prop_tri+len(trees)*tree_tri[0],'all_lod1_triangles':static_tri+prop_tri+len(trees)*tree_tri[1],'all_lod2_triangles':static_tri+prop_tri+len(trees)*tree_tri[2],'render_chunks':len(chunks),'collision_meshes':len(collision_chunks),'materials_in_unity':2,'extent_meters':[220,200]}
report['reduction_lod0_percent']=round(100*(1-report['maximum_lod0_triangles']/original),1)
with open(os.path.join(OUT,'optimization.json'),'w') as f:json.dump(report,f,indent=2)
scene['Optimization']='Shared tree meshes, 3 LODs, 24m static chunks, vertex colour palette, simplified collision source'
scene.camera=bpy.data.objects['CAM_01 • Isometric overview'];scene.render.resolution_x=1600;scene.render.resolution_y=1260;scene.cycles.samples=20;scene.render.threads_mode='FIXED';scene.render.threads=8
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'ShadowVale_Map01_Optimized.blend'))
scene.render.filepath=os.path.abspath('outputs/Map01_Optimized/Blender_Optimized.png')
print('OPTIMIZED',report,flush=True)



