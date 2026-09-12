"""Read Meshy FBXs without modifying them; print rig/material/geometry facts."""
import bpy
import json
from pathlib import Path
from mathutils import Vector

root = Path(__file__).resolve().parents[2]

def action_info(action):
    curves = [curve for layer in action.layers for strip in layer.strips
              for bag in strip.channelbags for curve in bag.fcurves]
    changing = []
    for curve in curves:
        values = [point.co.y for point in curve.keyframe_points]
        if values and max(values) - min(values) > 0.0001:
            changing.append(curve.data_path)
    return {'name': action.name, 'frames': list(action.frame_range),
            'curves': len(curves), 'changing_channels': len(changing)}

for path in sorted((root / 'Assets/Art/Characters/KaelMeshy').glob('*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_image_search=True)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    rigs = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
    points = [o.matrix_world @ Vector(v) for o in meshes for v in o.bound_box]
    print('MESHY_INSPECT ' + json.dumps({
        'file': path.name,
        'meshes': [{'name': o.name, 'vertices': len(o.data.vertices),
                    'triangles': sum(len(p.vertices)-2 for p in o.data.polygons),
                    'groups': len(o.vertex_groups),
                    'skin': [m.object.name if m.object else None for m in o.modifiers if m.type == 'ARMATURE'],
                    'materials': [m.name if m else None for m in o.data.materials]} for o in meshes],
        'rigs': [{'name': o.name, 'bones': [
            {'name': b.name, 'parent': b.parent.name if b.parent else None}
            for b in o.data.bones]} for o in rigs],
        'bounds_min': [min(p[i] for p in points) for i in range(3)] if points else None,
        'bounds_max': [max(p[i] for p in points) for i in range(3)] if points else None,
        'actions': [action_info(a) for a in bpy.data.actions],
        'images': [{'name': i.name, 'path': i.filepath, 'size': list(i.size)} for i in bpy.data.images],
    }))
