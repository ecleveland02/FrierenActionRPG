"""Read-only-source smoke test of the cloth preview and exported FBX."""
import bpy
import json
import math
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = root / 'ArtSource/KaelMeshyCloth'
bpy.ops.wm.open_mainfile(filepath=str(source / 'KaelCloth.blend'))
scene = bpy.context.scene
coat = bpy.data.objects['Kael_CoatCloth']
initial = None
max_motion = 0
for frame in range(1, 61):
    scene.frame_set(frame)
    evaluated = coat.evaluated_get(bpy.context.evaluated_depsgraph_get())
    points = [coat.matrix_world @ v.co for v in evaluated.data.vertices]
    assert all(math.isfinite(component) for p in points for component in p)
    assert all(p.length < 3 for p in points), 'Cloth exploded outside character bounds'
    if initial is None:
        initial = points
    max_motion = max(max_motion, max((a-b).length for a, b in zip(initial, points)))
assert max_motion > .001, 'Preview cloth did not move'
scene.camera.location = (0, -4, .85)
from mathutils import Vector
scene.camera.rotation_euler = (Vector((0, 0, .82)) - scene.camera.location).to_track_quat('-Z', 'Y').to_euler()
scene.render.filepath = str(source / 'Wind_Frame60.png')
bpy.ops.render.render(write_still=True)
print('BLENDER_CLOTH_TEST ' + json.dumps({'frames': 60, 'maximum_vertex_motion_m': max_motion}))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root / 'Assets/Art/Characters/KaelMeshyCloth/KaelCloth.fbx'))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(meshes) == 2
coat = next(o for o in meshes if o.name == 'Kael_CoatCloth')
assert len(coat.data.color_attributes) > 0, 'Freedom colors lost during FBX export'
assert len(coat.data.uv_layers) > 0
assert any(m.type == 'ARMATURE' for m in coat.modifiers)
assert not bpy.data.actions, 'Unexpected animation exported'
assert sum(len(o.data.polygons) for o in meshes) == 10400
print('FBX_CLOTH_TEST PASS: two skinned meshes, UVs, freedom colors, 10400 triangles, no animation')
