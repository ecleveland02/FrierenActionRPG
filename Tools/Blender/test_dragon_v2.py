"""Non-destructive source-rig pose tests; not a production animation pack."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]/'ArtSource/DragonInspectionV2'
out=root/'RigTests';out.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'DragonV2_Inspected.blend'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
scene=bpy.context.scene
scene.camera.location=(1,-2,1)
center=Vector((0,0,.32))
scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene.camera.data.ortho_scale=1.4
scene.render.fps=30;scene.frame_start=1;scene.frame_end=61
scene.display.shading.background_type='WORLD'
if scene.world is None:scene.world=bpy.data.worlds.new('Preview World')
scene.world.color=(.12,.12,.12)
rig.show_in_front=True
def evaluated():
    obj=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());data=obj.to_mesh()
    result=[v.co.copy() for v in data.vertices];obj.to_mesh_clear();return result
baseline=evaluated()
report={'vertices':len(mesh.data.vertices),'unweighted_vertices':sum(not any(g.weight>0 for g in v.groups) for v in mesh.data.vertices),'tests':{}}
tests={'FrontLeg_A':'bone_38','FrontLeg_B':'bone_66','RearLeg_A':'bone_6','RearLeg_B':'bone_15','Wing_A':'bone_28','Wing_B':'bone_51'}
scene.render.filepath=str(out/'Rest.png');bpy.ops.render.render(write_still=True)
for label,name in tests.items():
    rig.animation_data_clear()
    for p in rig.pose.bones:p.matrix_basis.identity()
    pb=rig.pose.bones[name];pb.rotation_mode='XYZ'
    for frame,angle in [(1,0),(16,20),(31,0),(46,-20),(61,0)]:
        pb.rotation_euler.x=math.radians(angle)
        pb.keyframe_insert(data_path='rotation_euler',frame=frame,group=name)
    rig.animation_data.action.name='TEST_'+label;rig.animation_data.action.use_fake_user=True
    scene.frame_set(16);bpy.context.view_layer.update()
    points=evaluated()
    report['tests'][label]={'bone':name,'moved_vertices':sum((a-b).length>1e-6 for a,b in zip(points,baseline)),'max_displacement':max((a-b).length for a,b in zip(points,baseline))}
    scene.render.filepath=str(out/(label+'.png'));bpy.ops.render.render(write_still=True)
    scene.frame_set(1)
rig.animation_data_clear()
for p in rig.pose.bones:p.matrix_basis.identity()
scene.frame_set(1)
bpy.context.view_layer.objects.active=rig
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_distance=1.8
        area.spaces.active.region_3d.view_location=center
bpy.ops.wm.save_as_mainfile(filepath=str(out/'DragonV2_PoseTests.blend'))
(out/'report.json').write_text(json.dumps(report,indent=2))
print('TEST_REPORT',json.dumps(report))
