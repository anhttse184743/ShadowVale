from pathlib import Path
import bpy, math, json
from mathutils import Vector,Matrix
HERE=Path(__file__).resolve().parent;OUT=HERE/'Revision2';OUT.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(HERE/'BunkerLayout_Proposal.blend'))
S=bpy.context.scene
# Remove meeting chairs, retain the two crafting chairs.
for o in [x for x in bpy.data.objects if x.type=='EMPTY' and x.name.startswith('Chair ')]:
 if int(o.name.split()[1])>=10:
  for ch in list(o.children):bpy.data.objects.remove(ch,do_unlink=True)
  bpy.data.objects.remove(o,do_unlink=True)
wood=bpy.data.materials['Weathered teak | fine grain'];light=bpy.data.materials['Worn plank edges']
room=bpy.data.collections['04 Preview room'];meeting=bpy.data.collections['06 Meeting table - NEW PROPOSAL']
def box(n,p,d,mat,col=meeting):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.name=n;o.dimensions=d;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 for c in list(o.users_collection):c.objects.unlink(o)
 col.objects.link(o);o.data.materials.append(mat);mod=o.modifiers.new('Worn edge','BEVEL');mod.width=.008;mod.segments=3
 return o
# Two plain slab benches with panel legs, exactly as long as the table.
for x in [.08,2.22]:
 box('Long bench seat 3.30m',(x,-.2,.455),(.40,3.30,.07),light)
 for y in [-1.49,1.09]:box('Bench broad slab leg',(x,y,.21),(.34,.105,.42),wood)
 box('Bench underseat rail',(x,-.2,.365),(.08,2.7,.10),wood)
# Long table axis now X: horizontal across the shelter, matching the original table.
pivot=Vector((1.15,-.2,0));new=Vector((.65,-.75,0));rot=Matrix.Rotation(math.pi/2,4,'Z')
for o in meeting.objects:o.matrix_world=Matrix.Translation(new)@rot@Matrix.Translation(-pivot)@o.matrix_world
# Workbench stays beside entrance on the cot side; cots provide explicit visual context.
cotpath=HERE.parent/'QuartermasterPreview/CotsSuppliesPreview/BeddingRevision2/FieldCots_ArmyBedding_Proposal.blend'
with bpy.data.libraries.load(str(cotpath),link=False) as (src,dst):
 dst.objects=[n for n in src.objects]
loaded=[o for o in dst.objects if o]
cotroot=next(o for o in loaded if o.name.startswith('FieldCot - head'))
def below(o,root):
 while o:
  if o==root:return True
  o=o.parent
 return False
cotobs=[o for o in loaded if below(o,cotroot)]
for o in cotobs:room.objects.link(o)
for o in loaded:
 if o not in cotobs:bpy.data.objects.remove(o,do_unlink=True)
cotroot.location=(-4.1,-1.5,0);cotroot.rotation_euler.z=-math.pi/2
# Actual quartermaster shelf, preserving authored crate and helmet detail.
with bpy.data.libraries.load(str(HERE.parent/'QuartermasterPreview/Revision2/Quartermaster_Proposal.blend'),link=False) as (src,dst):dst.objects=list(src.objects)
loaded=[o for o in dst.objects if o];shelf=next(o for o in loaded if o.name.startswith('02 Quartermaster shelf'))
shelfobs=[o for o in loaded if below(o,shelf)]
for o in shelfobs:room.objects.link(o)
for o in loaded:
 if o not in shelfobs:bpy.data.objects.remove(o,do_unlink=True)
helmetnames=['Green pith helmet crown','Downturned wide green brim','Crown fabric top button','Dark chinstrap across front brim','Buckle vertical edge','Buckle horizontal edge','Gold badge rim','Red circular insignia','Gold five-pointed star']
helmets=[o for o in shelf.children if any(o.name.startswith(n) for n in helmetnames)]
measure=json.loads((HERE/'head_measurements.json').read_text())[0]
target=measure['size'][0];factor=target/(2*.239)
for o in helmets:
 centerx=min([-.53,0,.53],key=lambda x:abs((sum(v[0] for v in o.bound_box)/8+o.location.x)-x))
 center=Vector((centerx,0,1.60));o.matrix_basis=Matrix.Translation(center)@Matrix.Scale(factor,4)@Matrix.Translation(-center)@o.matrix_basis
shelf.location=(3.5,2.35,0)
bpy.context.view_layer.update()
# Additional crop: actual player's head geometry beside resized store helmets.
before=set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=str(HERE.parents[1]/'Assets/_Project/Art/Characters/Player/Player.fbx'))
imported=set(bpy.data.objects)-before
headcol=bpy.data.collections.new('Head comparison only - hidden in layout');S.collection.children.link(headcol)
for o in imported:
 if o.type=='MESH':
  groups={g.index for g in o.vertex_groups if g.name in ['Head','Head_end']}
  keep=[v.index for v in o.data.vertices if any(g.group in groups and g.weight>.5 for g in v.groups)]
  import bmesh
  bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index not in set(keep)],context='VERTS');bm.to_mesh(o.data);bm.free()
  world=o.matrix_world.copy();o.modifiers.clear();o.parent=None;o.matrix_world=world
  o.location+=Vector((3.5,1.5,-.06))
  for c in list(o.users_collection):c.objects.unlink(o)
  headcol.objects.link(o)
for o in imported:
 if o.name in bpy.data.objects and o.type!='MESH':bpy.data.objects.remove(o,do_unlink=True)
S.render.resolution_x=1500;S.render.resolution_y=1050;S.cycles.samples=20
headcol.hide_render=True
cam=S.camera
views=[('01_layout',(-8.5,-11,8),(-.5,.2,.6),43),('02_horizontal_meeting',(4,-6.6,3.5),(.6,-.75,.65),50),('03_cot_side_workbench',(-7,-4,4),(-3.4,.8,.65),43)]
for name,pos,target,lens in views:
 cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=lens;S.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
headcol.hide_render=False
cam.location=(4.8,-.6,2.7);cam.rotation_euler=(Vector((3.5,2.2,1.5))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=58
S.render.filepath=str(OUT/'04_helmet_scale.png');bpy.ops.render.render(write_still=True)
headcol.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'BunkerLayout_Revision2.blend'))
(OUT/'validation.json').write_text(json.dumps({'preview_only':True,'meeting_long_axis':'X - horizontal','table_length_m':3.3,'bench_count':2,'bench_lengths_m':[3.3,3.3],'craft_chairs':2,'workbench_wall_gap_m':.03,'head_reference':'Player.fbx Head weighted vertices >0.5; includes original head equipment','head_reference_width_m':measure['size'][0],'helmet_scale_factor':factor,'helmet_brim_width_m':target,'location':'Doorway on field-cot side, conceptual context; exact Unity alignment pending approval'},indent=2))
