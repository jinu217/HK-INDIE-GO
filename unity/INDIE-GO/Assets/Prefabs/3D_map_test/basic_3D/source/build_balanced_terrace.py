import bpy, math, json, ast, hashlib, shutil, sys
from pathlib import Path
from mathutils import Vector, Matrix
from bpy_extras.object_utils import world_to_camera_view

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT
WIDTH=19.5; DEPTH=15.0; DELTA=(WIDTH-14)/2
SOURCE=ROOT/'source/Base_Approved.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene=bpy.context.scene; source_camera=bpy.data.objects['Camera_Source_Match'];scene.camera=source_camera
PI=math.pi
gold=bpy.data.materials['Champagne_Gold'];gold_light=bpy.data.materials['Pale_Gold']
ivory=bpy.data.materials['Carved_Ivory_Marble'];blue=bpy.data.materials['Lapis_Inlay']
def collection(name):
    c=bpy.data.collections.get(name)
    if c is None:c=bpy.data.collections.new(name);scene.collection.children.link(c)
    return c
tree=ast.parse((ROOT/'source/construction_functions.py').read_text(encoding='utf-8'))
names={'mesh','bevel','cube','curve','bezier_pts','lathe','railing'}
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'original_construction','exec'))
def center(o):return sum((o.matrix_world@Vector(v) for v in o.bound_box),Vector())/8
def signature(o):
    data={'v':[list(v.co) for v in o.data.vertices],'uv':[list(u.uv) for u in o.data.uv_layers.active.data],'matrix':[list(r) for r in o.matrix_world]}
    return hashlib.sha256(json.dumps(data).encode()).hexdigest()

# Restore the approved FIRST model, including its original central artwork.
for im in bpy.data.images:
    if im.source=='FILE':
        filename=Path(im.filepath.replace('\\','/')).name
        target=OUT/'textures'/filename
        if target.is_file():
            if im.packed_file:im.unpack(method='REMOVE')
            im.filepath=str(target);im.reload()
floor=bpy.data.objects['Central_Floor_Flat'];floor_hash=signature(floor)
preserved_groups=['02_Pillars','04_Central_Crest','05_Planters','06_Foliage','07_White_Flowers']
before={n:len(collection(n).objects) for n in preserved_groups}
original_prop_names={n:sorted(o.name for o in collection(n).objects) for n in preserved_groups}

# Remove only optional distant 3D skyline from the scene; the actual map details stay.
bg_old=bpy.data.collections.get('90_Distant_Palace_Optional')
if bg_old:
    for o in list(bg_old.objects):bpy.data.objects.remove(o,do_unlink=True)
    bpy.data.collections.remove(bg_old)

# Base and walkway slabs get their own plain material. Central design is never scaled.
walk=ivory.copy();walk.name='Walkway_Ivory_Marble'
tex=bpy.data.images.load(str(OUT/'textures/walkway_marble.png'))
nt=walk.node_tree
for n in nt.nodes:
    if n.type=='TEX_IMAGE':n.image=tex
for o in list(collection('01_Floor').objects):
    if o.name=='Single_Level_Base' or o.name.startswith(('Walkway_','EndWalk_')):
        bpy.data.objects.remove(o,do_unlink=True)
def planar_uv(o):
    for p in o.data.polygons:
        axes=[i for i in range(3) if i!=max(range(3),key=lambda i:abs(p.normal[i]))]
        for li in p.loop_indices:
            v=o.data.vertices[o.data.loops[li].vertex_index].co
            o.data.uv_layers.active.data[li].uv=(v[axes[0]]/3.2,v[axes[1]]/3.2)
base=cube('Single_Level_Base',(0,-.5,-.095),(WIDTH,DEPTH,.19),walk,'01_Floor',.035);planar_uv(base)
for sign in [-1,1]:
    sw=(WIDTH/2-5)/3
    for i in range(3):
        for j in range(10):
            o=cube(f'Walkway_{sign}_{i}_{j}',(sign*(5+(i+.5)*sw),-7.25+1.5*j,-.008),(sw-.006,1.494,.02),walk,'01_Floor',.004)
            planar_uv(o)
for row,(lo,hi) in enumerate([(-8,-5),(5,7)]):
    for j in range(10):
        o=cube(f'EndWalk_{row}_{j}',(-4.5+j,(lo+hi)/2,-.008),(.994,hi-lo-.006,.02),walk,'01_Floor',.004);planar_uv(o)

# All original pillars move as rigid assemblies, with NO shape stretching.
for o in collection('02_Pillars').objects:
    c=center(o)
    if abs(c.x)>6:o.location.x+=math.copysign(DELTA,c.x)

# Regenerate rail spans at the same sculpted cross-section and baluster spacing.
for o in list(collection('03_Balustrades').objects):bpy.data.objects.remove(o,do_unlink=True)
outer=6.55+DELTA
leftpath=[(-outer,-5.25),(-8.0,-5.48),(-6.5,-5.72),(-5.0,-5.9),(-3.7,-6.01),(-2.4,-6.02)]
rightpath=[(-x,y) for x,y in reversed(leftpath)]
paths=[[( -outer,5.75),(outer,5.75)],[(-outer,-5.25),(-outer,5.75)],[(outer,-5.25),(outer,5.75)],leftpath,rightpath]
for path,name in zip(paths,['Rear','Left','Right','Front_Curve_Left','Front_Curve_Right']):railing(path,name)
for o in collection('03_Balustrades').objects:
    if '_GoldRibbon' in o.name:
        for i in range(0,len(o.data.vertices),4):
            vs=[o.data.vertices[i+j] for j in range(4)];cx=sum(v.co.x for v in vs)/4;cy=sum(v.co.y for v in vs)/4
            for v in vs:v.co.x=cx+(v.co.x-cx)*.12;v.co.y=cy+(v.co.y-cy)*.12

# Classify COMPLETE original floral assemblies by their original anchors before moving.
# Flowers, pollen, leaves, hanging vines, gold ornaments and planter bodies share ONE transform.
anchors=[
    ('Rear_Large_Left',(-5.65,5.55),(-5.65-DELTA,5.55)),
    ('Rear_Small_Left',(-2.65,5.55),(-2.65,5.55)),
    ('Rear_Small_Right',(2.65,5.55),(2.65,5.55)),
    ('Rear_Large_Right',(5.65,5.55),(5.65+DELTA,5.55)),
    ('Side_Back_Left',(-6.5,3.5),(-outer,3.5)),
    ('Side_Back_Right',(6.5,3.5),(outer,3.5)),
    ('Side_Front_Left',(-6.5,-3.6),(-outer,-3.6)),
    ('Side_Front_Right',(6.5,-3.6),(outer,-3.6)),
    ('Front_Garland_Left',(-5.4,-5.75),(-7.0625,-5.63)),
    ('Front_Garland_Right',(5.4,-5.75),(7.0625,-5.63)),
    ('Foreground_Planter_Left',(-6.45,-7.15),(-8.35,-5.42)),
    ('Foreground_Planter_Right',(6.45,-7.15),(8.35,-5.42)),
]
assembly_members={a[0]:[] for a in anchors}
for cname in ['05_Planters','06_Foliage','07_White_Flowers']:
    for o in collection(cname).objects:
        c=center(o);name,old,new=min(anchors,key=lambda a:(c.x-a[1][0])**2+(c.y-a[1][1])**2)
        distance=math.hypot(c.x-old[0],c.y-old[1]);assert distance<1.12,(o.name,distance,name)
        o.location.x+=new[0]-old[0];o.location.y+=new[1]-old[1]
        assembly_members[name].append(o.name)
        o['perimeter_assembly']=name

# Guarantee original non-flag decorations have not been dropped or duplicated.
after={n:len(collection(n).objects) for n in preserved_groups}
assert before==after,(before,after)
assert all(sorted(o.name for o in collection(n).objects)==original_prop_names[n] for n in preserved_groups)
assert signature(floor)==floor_hash
bpy.context.view_layer.update()

# A planter must meet the railing without intersecting a separate stone pillar.
planter_feet=[o for o in collection('05_Planters').objects if o.name.endswith('_Foot')]
pillar_feet=[o for o in collection('02_Pillars').objects if o.name.endswith('_Plinth')]
def xy_bounds(o):
    v=[o.matrix_world@Vector(p) for p in o.bound_box]
    return [min(p.x for p in v),max(p.x for p in v),min(p.y for p in v),max(p.y for p in v)]
for planter in planter_feet:
    a=xy_bounds(planter)
    for pillar in pillar_feet:
        b=xy_bounds(pillar)
        assert not (min(a[1],b[1])>max(a[0],b[0]) and min(a[3],b[3])>max(a[2],b[2])),(planter.name,pillar.name)

def segment_distance(point,a,b):
    point,a,b=map(Vector,[point,a,b]);v=b-a;t=max(0,min(1,(point-a).dot(v)/v.length_squared));return (point-a-t*v).length
placements=[]
for name,old,new in anchors:
    distance=min(segment_distance(new,path[i],path[i+1]) for path in paths for i in range(len(path)-1))
    assert distance<.3,(name,distance)
    placements.append({'assembly':name,'old_xy':old,'new_xy':new,'obj_y_up_anchor':[new[0],0,-new[1]],'distance_to_railing':distance,'parts_preserved':len(assembly_members[name])})
# No above-floor props may enter the central 10 x 10 board volume.
intruders=[]
for n in preserved_groups+['03_Balustrades']:
    for o in collection(n).objects:
        corners=[o.matrix_world@Vector(v) for v in o.bound_box]
        if max(p.x for p in corners)>-5 and min(p.x for p in corners)<5 and max(p.y for p in corners)>-5 and min(p.y for p in corners)<5:
            intruders.append(o.name)
assert not intruders,intruders
assert not any(o.name.startswith(('Banner','Flag','Main_Left','Main_Right')) for o in scene.objects)

# Preview-only distant photographic plate; never exported with the OBJ.
bg=collection('99_Background_Preview_Only')
scene.render.resolution_x=1672;scene.render.resolution_y=941;scene.render.resolution_percentage=100
frame=source_camera.data.view_frame(scene=scene);depth=60
local=[v*(-depth/v.z) for v in frame];verts=[source_camera.matrix_world@v for v in local]
xmin=min(v.x for v in local);xmax=max(v.x for v in local);ymin=min(v.y for v in local);ymax=max(v.y for v in local)
me=bpy.data.meshes.new('Preview_Background_Plate');me.from_pydata(verts,[],[(0,1,2,3)]);me.update()
o=bpy.data.objects.new('Preview_Background_Plate',me);bg.objects.link(o);uv=me.uv_layers.new(name='UVMap')
for li in me.polygons[0].loop_indices:
    v=local[me.loops[li].vertex_index];uv.data[li].uv=((v.x-xmin)/(xmax-xmin),(v.y-ymin)/(ymax-ymin))
mat=bpy.data.materials.new('Preview_Only_Background');mat.use_nodes=True;nt=mat.node_tree;nt.nodes.clear()
t=nt.nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(OUT/'textures/Palace_Background.png'))
e=nt.nodes.new('ShaderNodeEmission');e.inputs['Strength'].default_value=2**(-scene.view_settings.exposure)
end=nt.nodes.new('ShaderNodeOutputMaterial');nt.links.new(t.outputs['Color'],e.inputs['Color']);nt.links.new(e.outputs[0],end.inputs['Surface']);me.materials.append(mat)
o.visible_shadow=False;o.visible_diffuse=False;o.visible_glossy=False;o.visible_transmission=False

def project(p):
    v=world_to_camera_view(scene,source_camera,Vector(p));return [v.x*1672,(1-v.y)*941]
foot=[project((x,y,0)) for x,y in [(-WIDTH/2,7),(WIDTH/2,7),(WIDTH/2,-8),(-WIDTH/2,-8)]]
def inside(p,q):
    cross=[(q[(i+1)%4][0]-q[i][0])*(p[1]-q[i][1])-(q[(i+1)%4][1]-q[i][1])*(p[0]-q[i][0]) for i in range(4)]
    return min(cross)>=0 or max(cross)<=0
samples=[(x,y) for y in range(160,941,5) for x in list(range(0,200,5))+list(range(1472,1672,5))]
coverage=sum(inside(p,foot) for p in samples)/len(samples)
assert coverage>.995,coverage
info={'footprint':[WIDTH,DEPTH],'ratio':WIDTH/DEPTH,'previous_too_wide':[22,15],'original':[14,15],
      'central_square':[10,10],'board_obj_y':.004,'base_thickness':.19,'original_floor_geometry_and_uv_preserved':True,
      'original_floor_texture_sha256':hashlib.sha256((OUT/'textures/floor_basecolor.png').read_bytes()).hexdigest(),
      'source_original_floor_sha256':hashlib.sha256((ROOT/'textures/floor_basecolor.png').read_bytes()).hexdigest(),
      'original_prop_counts':before,'revised_prop_counts':after,'flora_assemblies':placements,'central_prop_intrusions':intruders,
      'flags_included':False,'source_camera_side_floor_coverage':coverage,'coverage_region':'x<200 or x>=1472; y>=160; step5; 1672x941 source camera only',
      'camera_footprint_pixels':foot,'projection_is_estimated':True,
      'separate_flag_placement_note':'Move side flag assemblies outward by 2.75 world units on X from the original map; do not scale. Additional pole geometry is not included.'}
assert info['original_floor_texture_sha256']==info['source_original_floor_sha256']
(OUT/'layout_validation.json').write_text(json.dumps(info,indent=2),encoding='utf-8')

def camera(name,location,target,scale):
    ob=bpy.data.objects.get(name)
    if ob is None:ob=bpy.data.objects.new(name,bpy.data.cameras.new(name));scene.collection.objects.link(ob)
    ob.location=location;ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler();ob.data.type='ORTHO';ob.data.ortho_scale=scale;ob.data.clip_end=200;ob.data.shift_y=0
    return ob
camera('Camera_Balanced_Top',(0,-.5,35),(0,-.5,0),21.2)
camera('Camera_Balanced_Front',(0,-32,1.15),(0,0,1.15),21.2)
camera('Camera_Balanced_Overview',(19,-25,26),(0,-.5,.3),26)
scene.camera=source_camera;scene.render.film_transparent=False;scene.cycles.samples=40
for im in bpy.data.images:
    if im.source=='FILE':im.filepath='//textures/'+Path(im.filepath).name
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Terrace_Balanced.blend'),compress=True,relative_remap=False)
print('BALANCED_LAYOUT_VALID',json.dumps({'ratio':WIDTH/DEPTH,'coverage':coverage,'preserved':after}),flush=True)
if '--draft' in sys.argv:
    scene.render.resolution_percentage=65;scene.render.filepath=str(OUT/'renders/draft_game.png');bpy.ops.render.render(write_still=True)
    bg.hide_render=True;scene.camera=bpy.data.objects['Camera_Balanced_Top'];scene.render.film_transparent=True
    scene.render.resolution_x=1600;scene.render.resolution_y=1250;scene.render.resolution_percentage=70
    scene.render.filepath=str(OUT/'renders/draft_top.png');bpy.ops.render.render(write_still=True)
print('BALANCED_BUILD_COMPLETE',flush=True)
