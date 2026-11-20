import bpy,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent;OUT=ROOT/'Unity_Terrace_Background'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Terrace_Detailed/Terrace_Detailed.blend'))
scene=bpy.context.scene;cam=bpy.data.objects['Camera_Source_Match'];scene.camera=cam
scene.render.resolution_x=1672;scene.render.resolution_y=941;scene.render.resolution_percentage=100
scene.render.film_transparent=False
bpy.data.collections['90_Distant_Palace_Optional'].hide_render=True
# Camera-facing image plate, behind all terrace geometry. The source frame
# includes the calibrated lens shift; the plate is fitted to that exact frame.
frame=cam.data.view_frame(scene=scene);depth=60
local=[v*(-depth/v.z) for v in frame]
xmin=min(v.x for v in local);xmax=max(v.x for v in local);ymin=min(v.y for v in local);ymax=max(v.y for v in local)
verts=[cam.matrix_world@v for v in local]
me=bpy.data.meshes.new('Background_Plate');me.from_pydata(verts,[],[(0,1,2,3)]);me.update()
o=bpy.data.objects.new('Background_Plate',me);scene.collection.objects.link(o)
uv=me.uv_layers.new(name='UVMap')
for li in me.polygons[0].loop_indices:
    vi=me.loops[li].vertex_index;v=local[vi];uv.data[li].uv=((v.x-xmin)/(xmax-xmin),(v.y-ymin)/(ymax-ymin))
mat=bpy.data.materials.new('Unlit_Background_Preview');mat.use_nodes=True;nt=mat.node_tree;nt.nodes.clear()
tex=nt.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(OUT/'Palace_Background.png'))
em=nt.nodes.new('ShaderNodeEmission');em.inputs['Strength'].default_value=2**(-scene.view_settings.exposure)
end=nt.nodes.new('ShaderNodeOutputMaterial');nt.links.new(tex.outputs['Color'],em.inputs['Color']);nt.links.new(em.outputs[0],end.inputs['Surface']);me.materials.append(mat)
# The background artwork should not relight or reflect into the 3D game board.
o.visible_shadow=False;o.visible_diffuse=False;o.visible_glossy=False;o.visible_transmission=False
scene.cycles.samples=48;scene.render.filepath=str(OUT/'Preview_With_3D_Model.png')
bpy.ops.render.render(write_still=True)
(OUT/'placement_reference.json').write_text(json.dumps({'image_resolution':[1672,941],'preview_renderer':'Blender Cycles; not a Unity runtime test','model_camera':'Camera_Source_Match','camera_facing_plane_distance':depth,'plane_width':xmax-xmin,'plane_height':ymax-ymin,'plane_local_center':[(xmin+xmax)/2,(ymin+ymax)/2,-depth],'lighting':'unlit camera-only plate','opaque':True},indent=2))
print('BACKGROUND_COMPOSITE_VERIFIED',flush=True)
