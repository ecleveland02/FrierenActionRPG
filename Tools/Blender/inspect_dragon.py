import bpy, math
from mathutils import Vector
from pathlib import Path
root=Path('C:/Users/ericc/Code/Frieren/FrierenActionRPG/ArtSource/DragonInspection')
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(next(root.glob('*.fbx'))))
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.context.view_layer.objects.active=mesh; mesh.select_set(True)
mesh.rotation_euler.z+=math.radians(45)
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
print('BOUNDS',[(min(v.co[i] for v in mesh.data.vertices),max(v.co[i] for v in mesh.data.vertices)) for i in range(3)])
for mat in mesh.data.materials:
    if mat.use_nodes:
        for n in mat.node_tree.nodes:
            if n.type=='BSDF_PRINCIPLED': n.inputs['Metallic'].default_value=0; n.inputs['Roughness'].default_value=.8
for img in bpy.data.images:
    if img.source=='FILE':
        img.filepath=str(next(root.rglob('*.JPEG'))); img.reload(); img.pack()
bpy.ops.object.camera_add(); cam=bpy.context.object; bpy.context.scene.camera=cam
cam.data.type='ORTHO'; cam.data.ortho_scale=1.3
scene=bpy.context.scene; scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO'; scene.display.shading.color_type='TEXTURE'
scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
for name,pos in [('front',(0,-2,.34)),('side',(2,0,.34)),('top',(0,0,2))]:
    cam.location=pos; cam.rotation_euler=(Vector((0,0,.34))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(root/(name+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'DragonInspected.blend'))
