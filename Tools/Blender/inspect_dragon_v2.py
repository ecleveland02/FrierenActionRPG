import bpy, json, math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]/'ArtSource/DragonInspectionV2'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(next(root.glob('*.fbx'))))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
print('INSPECTION',json.dumps({'meshes':[{'name':o.name,'vertices':len(o.data.vertices),'polygons':len(o.data.polygons),'groups':len(o.vertex_groups),'modifiers':[(m.name,m.type) for m in o.modifiers]} for o in meshes],'rigs':[{'name':o.name,'bones':[b.name for b in o.data.bones]} for o in rigs],'actions':[a.name for a in bpy.data.actions]}))
for img in bpy.data.images:
    if img.source=='FILE':
        img.filepath=str(next(root.rglob('*.JPEG')));img.reload();img.pack()
for o in meshes:
    for mat in o.data.materials:
        if mat and mat.use_nodes:
            for n in mat.node_tree.nodes:
                if n.type=='BSDF_PRINCIPLED':n.inputs['Metallic'].default_value=0;n.inputs['Roughness'].default_value=.8
points=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
lo=Vector([min(p[i] for p in points) for i in range(3)])
hi=Vector([max(p[i] for p in points) for i in range(3)])
center=(lo+hi)/2;size=max(hi-lo)
bpy.ops.object.camera_add();cam=bpy.context.object;scene=bpy.context.scene;scene.camera=cam
cam.data.type='ORTHO';cam.data.ortho_scale=size*1.4
scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.light='STUDIO';scene.display.shading.color_type='TEXTURE'
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
for name,direction in [('Overview',(1,-2,1)),('Side',(1,1,.1)),('Front',(-1,-1,.1))]:
    cam.location=center+Vector(direction).normalized()*size*3
    cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(root/(name+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'DragonV2_Inspected.blend'))
