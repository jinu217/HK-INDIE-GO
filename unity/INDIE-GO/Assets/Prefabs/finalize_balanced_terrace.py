import bpy,json,sys,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Terrace_Balanced.blend'))
s=bpy.context.scene;bg=bpy.data.collections['99_Background_Preview_Only']
for im in bpy.data.images:
    if im.source=='FILE':
        p=Path(bpy.path.abspath(im.filepath));assert p.is_file(),str(p);im.reload()
def render(cam,filename,w,h,samples=72,background=False):
    s.camera=bpy.data.objects[cam];bg.hide_render=not background
    s.render.resolution_x=w;s.render.resolution_y=h;s.render.resolution_percentage=100
    s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGBA'
    s.render.film_transparent=not background;s.cycles.samples=samples
    s.render.filepath=str(OUT/'renders'/filename);bpy.ops.render.render(write_still=True)
    print('RENDER_READY',filename,flush=True)
if '--skip-render' not in sys.argv:
    render('Camera_Source_Match','01_game_camera.png',1672,941,96,True)
    render('Camera_Balanced_Top','02_top_view.png',1908,1500,80)
    render('Camera_Balanced_Front','03_front_view.png',1800,560,64)
    render('Camera_Balanced_Overview','04_full_overview.png',1700,1350,80)
s.camera=bpy.data.objects['Camera_Source_Match'];bg.hide_render=False;s.render.film_transparent=False
s.render.resolution_x=1672;s.render.resolution_y=941;s.cycles.samples=96
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Terrace_Balanced.blend'),compress=True,relative_remap=False)
materials=[]
for mat in bpy.data.materials:
    if not mat.use_nodes or mat.name.startswith(('QA_','Distant_','Preview_')):continue
    p=mat.node_tree.nodes.get('Principled BSDF')
    if p is None:continue
    images=[n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
    col=list(p.inputs['Base Color'].default_value)
    def srgb(x):return 12.92*x if x<.0031308 else 1.055*x**(1/2.4)-.055
    materials.append({'name':mat.name,'basecolor_srgb':[srgb(x) for x in col[:3]]+[col[3]],'metallic':p.inputs['Metallic'].default_value,'roughness':p.inputs['Roughness'].default_value,'basecolor_texture':Path(images[-1].filepath).name if images else None})
(OUT/'material_manifest.json').write_text(json.dumps(materials,indent=2))

# Join within semantic groups, keep the central placement plane separate.
floor=bpy.data.objects['Central_Floor_Flat'];floor.name='BoardSurface_10x10'
parts=[floor]
for col in list(bpy.data.collections):
    if not col.name.startswith(('01_','02_','03_','04_','05_','06_','07_')):continue
    objs=[o for o in col.objects if o.type in {'MESH','CURVE'} and o!=floor]
    if not objs:continue
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0];bpy.ops.object.convert(target='MESH');bpy.ops.object.join()
    obj=bpy.context.object;obj.name=col.name;parts.append(obj)
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.wm.obj_export(filepath=str(OUT/'Terrace_Balanced_NoFlags.obj'),export_selected_objects=True,apply_modifiers=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_uv=True,export_normals=True,export_materials=True,export_pbr_extensions=True,path_mode='RELATIVE',export_triangulated_mesh=True)
mtl=OUT/'Terrace_Balanced_NoFlags.mtl';lines=[]
for line in mtl.read_text().splitlines():
    if line.startswith('map_'):
        key=line.split()[0];name=Path(line.split()[-1].replace('\\','/')).name
        assert (OUT/'textures'/name).is_file(),name
        if key=='map_Kd':lines.append('Kd 1.000000 1.000000 1.000000')
        line=key+' textures/'+name
    lines.append(line)
mtl.write_text('\n'.join(lines)+'\n')
stats={'groups':len(parts),'vertices':sum(len(o.data.vertices) for o in parts),'triangles':sum(len(p.vertices)-2 for o in parts for p in o.data.polygons),'footprint':[19.5,15],'ratio':1.3,'central_square':[10,10],'board_height_obj_y':.004,'flags_included':False,'render_engine':'Blender Cycles, not Unity','preserved_first_floor_texture':True}
(OUT/'model_info.json').write_text(json.dumps(stats,indent=2));print('EXPORT_COMPLETE',stats,flush=True)
