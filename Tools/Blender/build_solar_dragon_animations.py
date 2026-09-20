import math
import os
import bpy

SOURCE = r"C:\Users\ericc\Code\Frieren\FrierenActionRPG\ArtSource\Dragon\SolarDragon\hi3d-solar-dragon-rig.blend"
OUTPUT_BLEND = r"C:\Users\ericc\Code\Frieren\FrierenActionRPG\ArtSource\Dragon\SolarDragon\SolarDragon_Gameplay.blend"
OUTPUT_FBX = r"C:\Users\ericc\Code\Frieren\FrierenActionRPG\Assets\Art\Bosses\SolarDragon\SolarDragon_Gameplay.fbx"

bpy.ops.wm.open_mainfile(filepath=SOURCE)
rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
if rig.animation_data is None:
    rig.animation_data_create()

original_actions = list(bpy.data.actions)

def clear_pose():
    for bone in rig.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = (0.0, 0.0, 0.0)
        bone.location = (0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)

def pose(frame, rotations=None, locations=None):
    clear_pose()
    for name, angles in (rotations or {}).items():
        bone = rig.pose.bones.get(name)
        if bone is not None:
            bone.rotation_euler = tuple(math.radians(value) for value in angles)
    for name, value in (locations or {}).items():
        bone = rig.pose.bones.get(name)
        if bone is not None:
            bone.location = value
    for bone in rig.pose.bones:
        bone.keyframe_insert("rotation_euler", frame=frame, group=bone.name)
        bone.keyframe_insert("location", frame=frame, group=bone.name)

def action(name, end, keys, loop=False):
    created = bpy.data.actions.new(name=name)
    rig.animation_data.action = created
    for frame, rotations, locations in keys:
        pose(frame, rotations, locations)
    if loop:
        for curve in created.fcurves if hasattr(created, "fcurves") else []:
            curve.modifiers.new("CYCLES")
    created.frame_start = 1
    created.frame_end = end
    created.use_fake_user = True
    return created

tail_left = {f"Tail_{index:02d}": (0, 0, index * 1.5) for index in range(1, 11)}
tail_right = {name: (0, 0, -angles[2]) for name, angles in tail_left.items()}

action("Dragon_Idle", 60, [
    (1, {**tail_left, "Chest": (1, 0, 0), "Wing_Upper.L": (0, 0, -3), "Wing_Upper.R": (0, 0, 3)}, {}),
    (30, {**tail_right, "Chest": (-2, 0, 0), "Neck_01": (1, 0, 0), "Wing_Upper.L": (0, 0, 1), "Wing_Upper.R": (0, 0, -1)}, {}),
    (60, {**tail_left, "Chest": (1, 0, 0), "Wing_Upper.L": (0, 0, -3), "Wing_Upper.R": (0, 0, 3)}, {}),
], True)

action("Dragon_Walk", 32, [
    (1, {"UpperArm.L": (18, 0, 0), "UpperArm.R": (-12, 0, 0), "Thigh.L": (-15, 0, 0), "Thigh.R": (18, 0, 0), **tail_left}, {}),
    (9, {"UpperArm.L": (0, 0, 0), "UpperArm.R": (0, 0, 0), "Thigh.L": (0, 0, 0), "Thigh.R": (0, 0, 0)}, {}),
    (17, {"UpperArm.L": (-12, 0, 0), "UpperArm.R": (18, 0, 0), "Thigh.L": (18, 0, 0), "Thigh.R": (-15, 0, 0), **tail_right}, {}),
    (25, {"UpperArm.L": (0, 0, 0), "UpperArm.R": (0, 0, 0), "Thigh.L": (0, 0, 0), "Thigh.R": (0, 0, 0)}, {}),
    (32, {"UpperArm.L": (18, 0, 0), "UpperArm.R": (-12, 0, 0), "Thigh.L": (-15, 0, 0), "Thigh.R": (18, 0, 0), **tail_left}, {}),
], True)

action("Dragon_Bite", 30, [
    (1, {}, {}),
    (8, {"Chest": (8, 0, 0), "Neck_01": (18, 0, 0), "Neck_02": (12, 0, 0), "Jaw": (-28, 0, 0)}, {}),
    (14, {"Chest": (-5, 0, 0), "Neck_01": (-24, 0, 0), "Neck_02": (-18, 0, 0), "Head": (-8, 0, 0), "Jaw": (4, 0, 0)}, {}),
    (21, {"Neck_01": (-8, 0, 0), "Jaw": (-10, 0, 0)}, {}),
    (30, {}, {}),
])

action("Dragon_WingBuffet", 42, [
    (1, {}, {}),
    (10, {"Chest": (-8, 0, 0), "Wing_Upper.L": (0, -20, -48), "Wing_Upper.R": (0, 20, 48), "Wing_Forearm.L": (0, -15, -35), "Wing_Forearm.R": (0, 15, 35)}, {}),
    (20, {"Chest": (10, 0, 0), "Wing_Upper.L": (0, 10, 28), "Wing_Upper.R": (0, -10, -28), "Wing_Forearm.L": (0, 8, 22), "Wing_Forearm.R": (0, -8, -22)}, {}),
    (30, {"Wing_Upper.L": (0, -8, -22), "Wing_Upper.R": (0, 8, 22)}, {}),
    (42, {}, {}),
])

tail_windup = {f"Tail_{index:02d}": (0, 0, index * 4.0) for index in range(1, 11)}
tail_strike = {f"Tail_{index:02d}": (0, 0, -index * 5.0) for index in range(1, 11)}
action("Dragon_TailSwipe", 38, [
    (1, {}, {}),
    (12, {**tail_windup, "Pelvis": (0, 0, 12), "Chest": (0, 0, -6)}, {}),
    (19, {**tail_strike, "Pelvis": (0, 0, -16), "Chest": (0, 0, 8)}, {}),
    (28, {**tail_right, "Pelvis": (0, 0, -5)}, {}),
    (38, {}, {}),
])

action("Dragon_Death", 75, [
    (1, {}, {}),
    (18, {"Chest": (-12, 0, 0), "Neck_01": (18, 0, 0), "Jaw": (-18, 0, 0), "Wing_Upper.L": (0, -18, -20), "Wing_Upper.R": (0, 18, 20)}, {}),
    (45, {"Pelvis": (0, 62, 0), "Chest": (-22, 28, 0), "Neck_01": (28, 18, 0), "Head": (18, 0, 0), "Wing_Upper.L": (0, -35, -38), "Wing_Upper.R": (0, 28, 34)}, {}),
    (75, {"Pelvis": (0, 88, 0), "Chest": (-30, 35, 0), "Neck_01": (38, 24, 0), "Head": (24, 0, 0), "Jaw": (-10, 0, 0), "Wing_Upper.L": (0, -48, -50), "Wing_Upper.R": (0, 38, 42)}, {}),
])

for old in original_actions:
    bpy.data.actions.remove(old)

scene = bpy.context.scene
scene.render.fps = 30
scene.frame_start = 1
scene.frame_end = 75
rig.animation_data.action = bpy.data.actions.get("Dragon_Idle")
bpy.ops.wm.save_as_mainfile(filepath=OUTPUT_BLEND)
os.makedirs(os.path.dirname(OUTPUT_FBX), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=OUTPUT_FBX,
    use_selection=False,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0.0,
    path_mode="COPY",
    embed_textures=False,
)
print("SOLAR_DRAGON_EXPORT_OK", OUTPUT_FBX)
