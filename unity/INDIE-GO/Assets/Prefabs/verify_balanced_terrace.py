import bpy,json,math,hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT
layout=json.loads((OUT/'layout_validation.json').read_text())
assert layout['original_prop_counts']==layout['revised_prop_counts']
assert layout['original_floor_texture_sha256']==layout['source_original_floor_sha256']
assert not layout['central_prop_intrusions'] and not layout['flags_included']
assert all(a['distance_to_railing']<.3 for a in layout['flora_assemblies'])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=str(OUT/'Terrace_Balanced_NoFlags.obj'),forward_axis='NEGATIVE_Z',up_axis='Y')
objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(objects)==8,len(objects)
pts=[];board=[];triangles=0;invalid_uv=0
for o in objects:
    assert o.data.uv_layers
    pts.extend(o.matrix_world@v.co for v in o.data.vertices)
    for u in o.data.uv_layers.active.data:
        assert all(math.isfinite(x) for x in u.uv)
    for p in o.data.polygons:
        assert len(p.vertices)==3;triangles+=1
        m=o.data.materials[p.material_index];assert not m.name.startswith(('Preview_','Banner','Flag'))
        if m.name.startswith('Reference_Floor'):board.extend(o.matrix_world@o.data.vertices[v].co for v in p.vertices)
assert board
span=[max(v[i] for v in board)-min(v[i] for v in board) for i in range(3)]
assert abs(span[0]-10)<1e-5 and abs(span[1]-10)<1e-5 and span[2]<1e-6
assert abs(board[0].z-.004)<1e-5
bounds=[[min(p[i] for p in pts),max(p[i] for p in pts)] for i in range(3)]
floor_object=next(o for o in objects if o.name.startswith('01_Floor'))
fp=[floor_object.matrix_world@v.co for v in floor_object.data.vertices]
floor_bounds=[[min(p[i] for p in fp),max(p[i] for p in fp)] for i in range(3)]
assert abs(floor_bounds[0][0]+9.75)<1e-5 and abs(floor_bounds[0][1]-9.75)<1e-5,floor_bounds
assert abs(floor_bounds[1][0]+8)<1e-5 and abs(floor_bounds[1][1]-7)<1e-5,floor_bounds
# Natural leaf tips may overhang the platform by a few centimeters.
assert max(abs(bounds[0][0]),abs(bounds[0][1]))<9.81
assert all(math.isfinite(x) for p in pts for x in p)
images=[]
for m in bpy.data.materials:
    if not m.use_nodes:continue
    for n in m.node_tree.nodes:
        if n.type=='TEX_IMAGE' and n.image:
            p=Path(bpy.path.abspath(n.image.filepath));assert p.is_file()
            assert n.image.size[0]>0 and n.image.has_data,p.name
            images.append(p.name)
assert 'floor_basecolor.png' in images and 'walkway_marble.png' in images
result={'native_obj_reimport':'pass','tested_with':'Blender 5.3; Unity Editor runtime not available','groups':len(objects),'triangles':triangles,'board_size':span,'board_height_blender_z':board[0].z,'blender_xyz_bounds':bounds,'floor_bounds':floor_bounds,'materials':sorted(m.name for m in bpy.data.materials if m.users),'loaded_textures':sorted(set(images)),'all_original_nonflag_decorations_preserved':True,'all_flower_assembly_anchors_touch_railing_zone':True,'board_plane_clear':True,'flags_in_obj':False}
(OUT/'validation.json').write_text(json.dumps(result,indent=2));print('VERIFICATION_PASS',json.dumps(result),flush=True)
