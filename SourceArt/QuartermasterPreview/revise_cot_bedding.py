"""Preview-only bedding revision. Supplies and all Unity assets remain unchanged."""
from pathlib import Path
import bpy, math
from mathutils import Vector, Matrix
HERE=Path(__file__).resolve().parent
OUT=HERE/'CotsSuppliesPreview/BeddingRevision2';OUT.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(HERE/'CotsSuppliesPreview/FieldCots_RationStores_Proposal.blend'))
S=bpy.context.scene;S.cycles.samples=28;S.render.resolution_x=1400;S.render.resolution_y=1000
def cloth(name,color,camo=False):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;n=m.node_tree.nodes;l=m.node_tree.links
 bs=n.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=.9
 tc=n.new('ShaderNodeTexCoord')
 weave=n.new('ShaderNodeTexNoise');weave.inputs['Scale'].default_value=230;weave.inputs['Detail'].default_value=2;l.new(tc.outputs['Generated'],weave.inputs[0])
 bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.16;bump.inputs['Distance'].default_value=.0006;l.new(weave.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs[0],bs.inputs['Normal'])
 if camo:
  tex=n.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=2.6;tex.inputs['Detail'].default_value=1.0;tex.inputs['Roughness'].default_value=.7;l.new(tc.outputs['Generated'],tex.inputs[0])
  ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.interpolation='CONSTANT'
  colors=[(.25,(.025,.032,.052)),(.43,(.055,.11,.047)),(.54,(.11,.16,.065)),(.64,(.25,.29,.115))]
  for e in list(ramp.color_ramp.elements)[1:]:ramp.color_ramp.elements.remove(e)
  ramp.color_ramp.elements[0].position=colors[0][0];ramp.color_ramp.elements[0].color=(*colors[0][1],1)
  for pos,col in colors[1:]:ramp.color_ramp.elements.new(pos).color=(*col,1)
  l.new(tex.outputs['Fac'],ramp.inputs[0]);l.new(ramp.outputs[0],bs.inputs['Base Color'])
 return m
army=cloth('Deep army green cot canvas',(.010,.030,.007))
inner=cloth('Pressed olive blanket interior',(.045,.085,.025))
camo=cloth('Military camouflage folded cover',(.055,.11,.047),True)
stitch=cloth('Dark olive blanket edge seam',(.025,.045,.014))
def box(name,loc,size,mat,root,bevel=.008):
 bpy.ops.mesh.primitive_cube_add(size=1);o=bpy.context.object;o.name=name;o.parent=root;o.location=loc;o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mat)
 b=o.modifiers.new('Pressed textile edge','BEVEL');b.width=bevel;b.segments=4;o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');return o
cots=[o for o in bpy.data.objects if o.type=='EMPTY' and (o.name=='FieldCot - head at positive Y' or o.name.startswith('FieldCot row '))]
assert len(cots)==5
for root in cots:
 for o in list(root.children_recursive):
  if any(o.name.startswith(n) for n in ['Soft cloth pillow','Pillow stitched perimeter','Folded wool blanket layer','Blanket woven stripe']):bpy.data.objects.remove(o,do_unlink=True)
 for o in root.children_recursive:
  if o.name.startswith('Tensioned canvas sleeping surface'):
   o.data=o.data.copy();o.data.materials.clear();o.data.materials.append(army)
 before=set(root.children)
 # Compact squared military bedding, then turn the exposed green fold toward the side.
 box('Square pressed blanket core',(0,.70,.551),(.505,.385,.128),inner,root,.018)
 box('Camouflage folded bottom flap',(0,.70,.485),(.57,.44,.020),camo,root,.007)
 box('Camouflage square top fold',(0,.70,.625),(.57,.44,.023),camo,root,.009)
 for x in [-.269,.269]:box('Camouflage vertical side fold',(x,.70,.552),(.032,.44,.135),camo,root,.008)
 box('Camouflage rear folded edge',(0,.905,.552),(.54,.026,.135),camo,root,.007)
 box('Foot-facing lower folded hem',(0,.486,.503),(.54,.018,.018),camo,root,.004)
 for x in [-.26,.26]:box('Pressed top stitched edge',(x,.70,.638),(.002,.39,.0015),stitch,root,.0004)
 pivot=Vector((0,.70,.55));turn=Matrix.Rotation(math.pi/2,3,'Z')
 for o in set(root.children)-before:
  o.location=pivot+turn@(o.location-pivot);o.rotation_euler.z+=math.pi/2
 # User layout: the bundle's long edge lies across the bed; exposed core stays on its side.
 stretch=Matrix.Diagonal((.60/.44,.32/.57,1,1))
 for o in set(root.children)-before:
  o.matrix_basis=Matrix.Translation(pivot)@stretch@Matrix.Translation(-pivot)@o.matrix_basis
 root['bedding_open_face']='Right side of bed (+X)'
 root['bedding_location']='Head only, positive Y against wall';root['preview_only']=True
stores=bpy.data.objects['Rations and ammunition on a low pallet']
for o in [stores,*stores.children_recursive]:o.hide_render=True
wall=bpy.data.objects['Walnut wall for five-cot placement']
def visibility(root,show):
 for o in [root,*root.children_recursive]:o.hide_render=not show
def render(name,location,target,scale):
 cam=S.camera;cam.location=location;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
 S.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
hero=bpy.data.objects['FieldCot row 3']
for root in cots:visibility(root,root==hero)
visibility(wall,False)
render('01_dark_army_cot',(2.4,-3,2.5),(0,0,.32),2.85)
render('02_top_view_layout',(0,0,4),(0,0,0),3.35)
for root in cots:visibility(root,True)
visibility(wall,True)
render('03_five_cots_head_bedding',(5,-7,5),(0,0,.55),8.3)
bpy.context.view_layer.update()
head=max((hero.matrix_world@o.matrix_local@Vector(c)).y for o in hero.children if o.type=='MESH' for c in o.bound_box)
assert abs((1.048-head)-.03)<.001
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FieldCots_ArmyBedding_Proposal.blend'))
(OUT/'README.txt').write_text('PREVIEW ONLY. Five dark army-green canvas cots. Square camouflage military bedding at head (+Y) only; no foot blanket or separate pillow. Head rail-to-wall clearance 0.030m. Rations/ammunition retained unchanged in source blend. No Unity assets modified.',encoding='utf-8')
print('BEDDING PREVIEW COMPLETE - Unity unchanged')

