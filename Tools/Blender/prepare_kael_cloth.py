"""Prepare an isolated cloth-ready Meshy derivative; originals are never written."""
import bpy
import bmesh
import json
import numpy as np
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Assets/Art/Characters/KaelMeshy'
OUTPUT = ROOT / 'ArtSource/KaelMeshyCloth'
OUTPUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(SOURCE / 'KaelNoSkin.fbx'))
body = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
rig.animation_data_clear()
rig.data.pose_position = 'REST'

# Connected shells reveal whether the generated garment can be isolated safely.
adj = {v.index: set() for v in body.data.vertices}
for edge in body.data.edges:
    a, b = edge.vertices
    adj[a].add(b)
    adj[b].add(a)
remaining = set(adj)
shells = []
while remaining:
    todo = [min(remaining)]
    shell = set()
    while todo:
        i = todo.pop()
        if i in shell:
            continue
        shell.add(i)
        todo.extend(adj[i] - shell)
    remaining -= shell
    shells.append(shell)
shells.sort(key=len, reverse=True)
report = []
for shell in shells:
    pts = [body.matrix_world @ body.data.vertices[i].co for i in shell]
    report.append({'vertices': len(shell),
                   'min': [min(p[i] for p in pts) for i in range(3)],
                   'max': [max(p[i] for p in pts) for i in range(3)]})
print('SHELLS ' + json.dumps(report))

material = bpy.data.materials.new('KaelClothPreview')
material.use_nodes = True
nodes = material.node_tree.nodes
shader = nodes.get('Principled BSDF')
texture = nodes.new('ShaderNodeTexImage')
texture.image = bpy.data.images.load(str(SOURCE / 'Meshy_AI_Create_a_game_ready_3_biped_texture_0.png'))
material.node_tree.links.new(texture.outputs['Color'], shader.inputs['Base Color'])
shader.inputs['Roughness'].default_value = .8
body.data.materials.clear()
body.data.materials.append(material)

# Inspect the candidate fabric selection before any topology is changed.
pixels = np.empty(len(texture.image.pixels), dtype=np.float32)
texture.image.pixels.foreach_get(pixels)
width, height = texture.image.size
pixels = pixels.reshape(height, width, 4)
uvs = body.data.uv_layers.active.data
fabric_faces = set()
highlight = bpy.data.materials.new('FabricSelection')
highlight.use_nodes = True
highlight.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = (0.03, 1, .15, 1)
body.data.materials.append(highlight)
for face in body.data.polygons:
    pos = body.matrix_world @ face.center
    uv = sum((uvs[i].uv for i in face.loop_indices), Vector((0, 0))) / len(face.loop_indices)
    r, g, b = pixels[min(height-1, int(uv.y*height)), min(width-1, int(uv.x*width)), :3]
    cloth_color = (b > r * 1.10 and b > g * 1.015) or min(r, g, b) > .36
    if .375 < pos.z < 1.00 and (abs(pos.x) > .10 or pos.y > .04) and cloth_color:
        fabric_faces.add(face.index)

edge_faces = {}
for face in body.data.polygons:
    for edge in face.edge_keys:
        edge_faces.setdefault(edge, []).append(face.index)
neighbors = {f.index: set() for f in body.data.polygons}
for faces in edge_faces.values():
    for i in faces:
        neighbors[i].update(set(faces) - {i})

def components(indices):
    left = set(indices)
    result = []
    while left:
        group = set()
        todo = [min(left)]
        while todo:
            i = todo.pop()
            if i in group:
                continue
            group.add(i)
            todo.extend(neighbors[i] & left - group)
        left -= group
        result.append(group)
    return sorted(result, key=len, reverse=True)

# Remove isolated trouser/texture speckles, and close small gold-trim holes.
fabric_faces = set.union(*[c for c in components(fabric_faces) if len(c) > 80])
for group in components(set(neighbors) - fabric_faces):
    if len(group) < 50:
        fabric_faces.update(group)
for i in fabric_faces:
    body.data.polygons[i].material_index = 1
print('FABRIC_FACES', len(fabric_faces))

# Separate without remeshing: retain the original UVs, silhouette and skin data.
garment = body.copy()
garment.data = body.data.copy()
bpy.context.collection.objects.link(garment)
garment.name = 'Kael_CoatCloth'
body.name = 'Kael_Body'
for obj, keep in [(garment, fabric_faces), (body, set(neighbors) - fabric_faces)]:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in keep], context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for face in obj.data.polygons:
        face.material_index = 0

# Red is normalized freedom (0 = pinned, 1 = 35 cm); exported for inspection.
edge_counts = {}
for face in garment.data.polygons:
    for edge in face.edge_keys:
        edge_counts[edge] = edge_counts.get(edge, 0) + 1
boundary = {i for edge, count in edge_counts.items() if count == 1 for i in edge}
colors = garment.data.color_attributes.new(name='ClothFreedom', type='FLOAT_COLOR', domain='POINT')
pins = garment.vertex_groups.new(name='ClothPins')
hip_index = garment.vertex_groups['Hips'].index
pin_count = 0
for vertex in garment.data.vertices:
    z = (garment.matrix_world @ vertex.co).z
    freedom = max(0, min(1, (.89-z)/.49))
    if vertex.index in boundary and z > .72:
        freedom = 0
    colors.data[vertex.index].color = (freedom, freedom, freedom, 1)
    pins.add([vertex.index], 1-freedom, 'REPLACE')
    if freedom == 0:
        pin_count += 1
    # Stop the free hem stretching between opposing thighs. Keep exact original
    # skinning at the attachment seam so the two meshes remain aligned there.
    hip_blend = min(1, freedom * 5)
    old_weights = [(g.group, g.weight) for g in vertex.groups if g.group != pins.index]
    for group_index, weight in old_weights:
        garment.vertex_groups[group_index].add([vertex.index], weight*(1-hip_blend), 'REPLACE')
    previous_hip = sum(w for i, w in old_weights if i == hip_index)
    garment.vertex_groups[hip_index].add([vertex.index], previous_hip*(1-hip_blend)+hip_blend, 'REPLACE')
assert pin_count > 10 and len(garment.data.vertices) > 300

export = ROOT / 'Assets/Art/Characters/KaelMeshyCloth'
export.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='DESELECT')
for obj in [rig, body, garment]:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(export / 'KaelCloth.fbx'), use_selection=True,
    object_types={'ARMATURE', 'MESH'}, axis_forward='-Z', axis_up='Y',
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
    add_leaf_bones=False, bake_anim=False, path_mode='AUTO')

# Blender preview physics is source-only. Unity gets its own runtime Cloth setup.
cloth = garment.modifiers.new('Preview Cloth (Unity uses its own solver)', 'CLOTH')
cloth.settings.vertex_group_mass = pins.name
cloth.settings.quality = 8
cloth.settings.mass = .25
cloth.settings.tension_stiffness = 25
cloth.settings.compression_stiffness = 25
cloth.settings.shear_stiffness = 15
cloth.settings.bending_stiffness = .5
cloth.collision_settings.use_self_collision = False
cloth.collision_settings.distance_min = .008
cloth.point_cache.frame_start = 1
cloth.point_cache.frame_end = 120
rig.data.pose_position = 'POSE'
for name in ['LeftUpLeg', 'LeftLeg', 'RightUpLeg', 'RightLeg']:
    bone = rig.data.bones[name]
    start = rig.matrix_world @ bone.head_local
    end = rig.matrix_world @ bone.tail_local
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, location=(start+end)/2)
    proxy = bpy.context.object
    proxy.name = 'PreviewCollision_' + name
    proxy.rotation_euler = (end-start).to_track_quat('Z', 'Y').to_euler()
    proxy.scale = (.065, .065, (end-start).length*.48)
    world = proxy.matrix_world.copy()
    proxy.parent = rig
    proxy.parent_type = 'BONE'
    proxy.parent_bone = name
    proxy.matrix_world = world
    proxy.modifiers.new('Collision', 'COLLISION')
    proxy.hide_render = True
    proxy.display_type = 'WIRE'
bpy.ops.object.effector_add(type='WIND', location=(-2, -2, .7))
wind = bpy.context.object
wind.name = 'PreviewWind_NOT_EXPORTED'
wind.rotation_euler = Vector((1, .5, 0)).to_track_quat('Z', 'Y').to_euler()
wind.field.strength = 15
wind.field.noise = 1
summary = {'source': 'KaelNoSkin.fbx', 'cloth_vertices': len(garment.data.vertices),
           'cloth_triangles': len(garment.data.polygons), 'pinned_vertices': pin_count,
           'body_triangles': len(body.data.polygons), 'animation_exported': False}
(OUTPUT / 'build-report.json').write_text(json.dumps(summary, indent=2) + '\n')
print('KAEL_CLOTH_PREPARED ' + json.dumps(summary))

scene = bpy.context.scene
scene.world = bpy.data.worlds.new('Studio')
scene.world.color = (.2, .2, .2)
for name, position, power in [('Key', (-3, -4, 4), 400), ('Fill', (3, -2, 3), 250), ('Back', (0, 4, 4), 400)]:
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = power
    data.size = 4
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = position
    obj.rotation_euler = (Vector((0, 0, .9)) - obj.location).to_track_quat('-Z', 'Y').to_euler()
bpy.ops.object.camera_add(location=(0, -4, 1))
cam = bpy.context.object
cam.data.type = 'ORTHO'
cam.data.ortho_scale = 1.9
scene.camera = cam
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16
scene.render.resolution_x = 800
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.frame_end = 120
texture.image.pack()
for name, position in [('Front', (0, -4, .85)), ('Back', (0, 4, .85))]:
    cam.location = position
    cam.rotation_euler = (Vector((0, 0, .82)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = str(OUTPUT / (name + '.png'))
    bpy.ops.render.render(write_still=True)
cam.location = (0, -4, .85)
cam.rotation_euler = (Vector((0, 0, .82)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT / 'KaelCloth.blend'))
