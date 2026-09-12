#!/usr/bin/env python3
"""Sanity-check hand-authored gray-box geometry.

The scenes in this project are written by generator scripts, so nobody looks at the level until
Unity opens it. These are the mistakes that are invisible in the YAML and obvious the moment you
walk into them - a ramp too steep for the character controller, a spawn point inside a wall, an
objective volume that is solid and therefore a wall itself.

Usage: check_level_geometry.py [scene ...]   (defaults to every scene with an objective in it)
"""
import math
import os
import re
import sys

# From the CharacterController on Player.prefab.
SLOPE_LIMIT_DEGREES = 45.0
STEP_OFFSET = 0.3
CAPSULE_RADIUS = 0.5

ANCHOR = re.compile(r"^--- !u!(\d+) &(\d+)")
NAME = re.compile(r"^  m_Name: (.*)$", re.M)
POS = re.compile(r"^  m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}", re.M)
SCALE = re.compile(r"^  m_LocalScale: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}", re.M)
ROT = re.compile(r"^  m_LocalRotation: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+), w: ([-\d.e]+)\}", re.M)
FATHER = re.compile(r"^  m_Father: \{fileID: (\d+)\}", re.M)
GAMEOBJECT = re.compile(r"^  m_GameObject: \{fileID: (\d+)\}", re.M)
IS_TRIGGER = re.compile(r"^  m_IsTrigger: (\d)", re.M)
ACTIVE = re.compile(r"^  m_IsActive: (\d)", re.M)

problems = []


def documents(text):
    current = None
    body = []
    for line in text.splitlines():
        match = ANCHOR.match(line)
        if match:
            if current:
                yield current[0], current[1], "\n".join(body)
            current = (match.group(1), int(match.group(2)))
            body = []
        elif current:
            body.append(line)
    if current:
        yield current[0], current[1], "\n".join(body)


def quat_to_matrix(x, y, z, w):
    return (
        (1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)),
        (2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)),
        (2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)),
    )


def matmul(a, b):
    return tuple(tuple(sum(a[r][k] * b[k][c] for k in range(3)) for c in range(3)) for r in range(3))


def parse(text):
    objects, transforms, colliders = {}, {}, {}

    for class_id, anchor, body in documents(text):
        if class_id == "1":
            name = NAME.search(body)
            active = ACTIVE.search(body)
            objects[anchor] = {
                "name": name.group(1).strip() if name else "?",
                "active": active.group(1) == "1" if active else True,
            }
        elif class_id == "4":
            owner = GAMEOBJECT.search(body)
            pos, scale, rot, father = POS.search(body), SCALE.search(body), ROT.search(body), FATHER.search(body)
            transforms[anchor] = {
                "go": int(owner.group(1)) if owner else 0,
                "pos": tuple(float(v) for v in pos.groups()) if pos else (0, 0, 0),
                "scale": tuple(float(v) for v in scale.groups()) if scale else (1, 1, 1),
                "rot": tuple(float(v) for v in rot.groups()) if rot else (0, 0, 0, 1),
                "father": int(father.group(1)) if father else 0,
            }
        elif class_id == "65":
            owner = GAMEOBJECT.search(body)
            trigger = IS_TRIGGER.search(body)
            if owner:
                colliders[int(owner.group(1))] = trigger.group(1) == "1" if trigger else False

    return objects, transforms, colliders


def world(transform_id, transforms):
    """World position, cumulative scale and the object's own rotation matrix."""
    chain = []
    current = transform_id
    guard = 0

    while current and current in transforms and guard < 64:
        chain.append(transforms[current])
        current = transforms[current]["father"]
        guard += 1

    position = (0.0, 0.0, 0.0)
    scale = (1.0, 1.0, 1.0)
    rotation = ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))

    for node in reversed(chain):
        # A local position is placed by its PARENT's rotation, never by its own. Getting that
        # backwards puts every rotated object somewhere it is not, and the numbers still look
        # plausible enough to believe.
        local = tuple(node["pos"][i] * scale[i] for i in range(3))
        rotated = tuple(sum(rotation[r][c] * local[c] for c in range(3)) for r in range(3))
        position = tuple(position[i] + rotated[i] for i in range(3))
        rotation = matmul(rotation, quat_to_matrix(*node["rot"]))
        scale = tuple(scale[i] * node["scale"][i] for i in range(3))

    return position, scale, rotation


def tilt_degrees(rotation):
    """Angle between the box's local up and world up."""
    up_y = max(-1.0, min(1.0, rotation[1][1]))
    return math.degrees(math.acos(abs(up_y)))


def check(path):
    text = open(path).read()
    objects, transforms, colliders = parse(text)
    boxes = []

    for transform_id, node in transforms.items():
        go = node["go"]

        if go not in objects:
            continue

        position, scale, rotation = world(transform_id, transforms)
        boxes.append({
            "name": objects[go]["name"],
            "active": objects[go]["active"],
            "pos": position,
            "scale": scale,
            "tilt": tilt_degrees(rotation),
            "solid": go in colliders and not colliders[go],
            "trigger": colliders.get(go, None) is True,
        })

    for box in boxes:
        if box["solid"] and box["tilt"] > 1.0 and box["tilt"] > SLOPE_LIMIT_DEGREES:
            problems.append(
                f"{path}: '{box['name']}' is tilted {box['tilt']:.1f} degrees, steeper than the "
                f"{SLOPE_LIMIT_DEGREES:.0f} degree slope limit - the player cannot walk up it")

    # A spawn point buried in a wall is a character that starts inside geometry.
    for box in boxes:
        if "Spawn" not in box["name"]:
            continue

        for solid in boxes:
            if not solid["solid"] or not solid["active"]:
                continue

            inside = all(
                abs(box["pos"][i] - solid["pos"][i]) < solid["scale"][i] * 0.5 + (CAPSULE_RADIUS if i != 1 else 0.0)
                for i in range(3))

            if inside:
                problems.append(
                    f"{path}: spawn '{box['name']}' at {fmt(box['pos'])} is inside solid "
                    f"'{solid['name']}'")

    for box in boxes:
        if box["name"].startswith("Goal_") and box["solid"]:
            problems.append(
                f"{path}: objective volume '{box['name']}' has a solid collider, so it is a wall")

    return boxes


def fmt(v):
    return f"({v[0]:.1f}, {v[1]:.1f}, {v[2]:.1f})"


def report(path, boxes):
    print(f"\n{path}")
    print(f"  {len(boxes)} placed objects")

    interesting = [b for b in boxes if b["tilt"] > 1.0 and (b["solid"] or b["trigger"])]

    for box in sorted(interesting, key=lambda b: b["name"]):
        print(f"  slope  {box['name']:<24} {box['tilt']:.1f} deg at {fmt(box['pos'])}")

    # Only flat surfaces: an axis-aligned box's top is its centre plus half its height, and for
    # a tilted one that number means nothing.
    surfaces = [b for b in boxes if b["solid"] and b["active"] and b["tilt"] <= 1.0
                and b["scale"][1] < 1.5 and b["scale"][0] > 2 and b["scale"][2] > 2]

    for box in sorted(surfaces, key=lambda b: b["pos"][2]):
        top = box["pos"][1] + box["scale"][1] * 0.5
        print(f"  floor  {box['name']:<24} top y={top:.2f}  z {box['pos'][2] - box['scale'][2] / 2:.1f}"
              f"..{box['pos'][2] + box['scale'][2] / 2:.1f}")


def main():
    scenes = sys.argv[1:]

    if not scenes:
        scenes = [os.path.join("Assets/Scenes", n) for n in sorted(os.listdir("Assets/Scenes"))
                  if n.endswith(".unity") and "SliceObjectives" in open(
                      os.path.join("Assets/Scenes", n)).read() or False]
        scenes = [s for s in scenes if os.path.exists(s)]

    if not scenes:
        print("No slice scenes found; nothing to check.")
        return

    for path in scenes:
        report(path, check(path))

    print()

    if problems:
        for problem in sorted(set(problems)):
            print("PROBLEM:", problem)
        sys.exit(f"\n{len(set(problems))} problem(s).")

    print("OK - geometry looks walkable.")


if __name__ == "__main__":
    main()
