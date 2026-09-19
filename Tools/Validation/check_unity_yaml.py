#!/usr/bin/env python3
"""Structural checks on the hand-authored .unity, .prefab and .asset files.

These files are written by generator scripts rather than by the Unity editor, so the usual safety
net - the editor refusing to save something malformed - does not exist. Everything here is a
mistake that has actually been made in this project at least once.

Run before every push, alongside check_assemblies.py.
"""
import os
import re
import sys

ASSETS = "Assets"

# Only the files this project hand-authors. Everything else under Assets/ was written by Unity or
# by a package (the URP settings, the Kael art spike's prefabs), and uses prefab-variant and
# stripped-object forms this checker deliberately does not model.
OWNED = (
    "Assets/Scenes/",
    "Assets/ScriptableObjects/",
    "Assets/Prefabs/Characters/Player.prefab",
    "Assets/Prefabs/Characters/Enemy_Sentinel.prefab",
)
ANCHOR = re.compile(r"^--- !u!(\d+) &(\d+)")
LOCAL_REF = re.compile(r"\{fileID: (\d+)\}")
GUID_REF = re.compile(r"\{fileID: -?\d+, guid: ([0-9a-f]{32}), type: \d+\}")
META_GUID = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)

# fileID 0 is Unity's null reference. Built-in meshes, materials and shaders live in the two
# built-in GUIDs and have no .meta file of their own.
BUILTIN_GUIDS = {
    "0000000000000000f000000000000000",
    "0000000000000000e000000000000000",
    "0000000000000000d000000000000000",
}

problems = []


def known_guids():
    guids = {}
    for root, _, files in os.walk(ASSETS):
        for name in files:
            if not name.endswith(".meta"):
                continue
            path = os.path.join(root, name)
            match = META_GUID.search(open(path).read())
            if match:
                guids[match.group(1)] = path[: -len(".meta")]
    return guids


def package_guids():
    """GUIDs owned by packages, which live outside Assets and have no .meta here."""
    found = set()
    for base in ("Library/PackageCache", "Packages"):
        if not os.path.isdir(base):
            continue
        for root, _, files in os.walk(base):
            for name in files:
                if not name.endswith(".meta"):
                    continue
                match = META_GUID.search(open(os.path.join(root, name), errors="ignore").read())
                if match:
                    found.add(match.group(1))
    return found


def check_document(path, text, guids):
    anchors = {}
    for line in text.splitlines():
        match = ANCHOR.match(line)
        if not match:
            continue
        class_id, anchor = match.group(1), int(match.group(2))
        if anchor in anchors:
            problems.append(f"{path}: fileID {anchor} is declared twice")
        anchors[anchor] = class_id

    # Every local {fileID: n} must name something in the same file.
    for anchor in {int(m) for m in LOCAL_REF.findall(text)}:
        if anchor != 0 and anchor not in anchors:
            problems.append(f"{path}: references fileID {anchor}, which is not in this file")

    # Every external GUID must resolve to an asset that exists.
    for guid in set(GUID_REF.findall(text)) if guids is not None else ():
        if guid not in guids and guid not in BUILTIN_GUIDS:
            problems.append(f"{path}: references guid {guid}, which no asset owns")

    check_hierarchy(path, text, anchors)
    check_components(path, text, anchors)


def blocks(text):
    """Yields (class_id, anchor, body) for each YAML document in the file."""
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


def check_hierarchy(path, text, anchors):
    """A Transform's children must each name it back as their father.

    Stripped transforms are the exception, and modelling them is the whole reason this function is
    longer than it looks like it should be. A nested prefab - an imported FBX dropped into a hand
    authored prefab - appears as a stub carrying only m_CorrespondingSourceObject and
    m_PrefabInstance. It has no m_Father at all: its parent is named by m_TransformParent on the
    PrefabInstance that owns it. Treating a missing m_Father as "father 0" reported every correctly
    nested prefab as broken, so the same invariant is checked against the instance instead.
    """
    fathers = {}
    children = {}
    stripped_owner = {}

    for class_id, anchor, body in blocks(text):
        if class_id != "4":
            continue

        instance = re.search(r"^  m_PrefabInstance: \{fileID: (\d+)\}", body, re.M)

        if instance and int(instance.group(1)) != 0:
            stripped_owner[anchor] = int(instance.group(1))
            continue

        father = re.search(r"^  m_Father: \{fileID: (\d+)\}", body, re.M)
        fathers[anchor] = int(father.group(1)) if father else 0
        listed = re.search(r"^  m_Children:\n((?:  - \{fileID: \d+\}\n?)*)", body + "\n", re.M)
        children[anchor] = [int(m) for m in LOCAL_REF.findall(listed.group(1))] if listed else []

    # Where each PrefabInstance says it hangs.
    instance_parent = {}
    for class_id, anchor, body in blocks(text):
        if class_id != "1001":
            continue
        parent = re.search(r"^    m_TransformParent: \{fileID: (\d+)\}", body, re.M)
        instance_parent[anchor] = int(parent.group(1)) if parent else 0

    for parent, kids in children.items():
        for kid in kids:
            if kid in stripped_owner:
                owner = stripped_owner[kid]

                if owner not in instance_parent:
                    problems.append(
                        f"{path}: transform {parent} lists stripped child {kid}, whose "
                        f"m_PrefabInstance {owner} is not a PrefabInstance in this file")
                elif instance_parent[owner] != parent:
                    problems.append(
                        f"{path}: transform {parent} lists stripped child {kid}, but its "
                        f"PrefabInstance {owner} is parented to {instance_parent[owner]}")
            elif kid not in fathers:
                problems.append(f"{path}: transform {parent} lists child {kid}, which is not a Transform")
            elif fathers[kid] != parent:
                problems.append(
                    f"{path}: transform {parent} lists child {kid}, but {kid}'s father is {fathers[kid]}")

    for child, father in fathers.items():
        if father == 0:
            continue

        # A stripped transform is a stub for an object inside a nested prefab. It carries no
        # m_Children, so a real object parented to one is correct and unlistable. This is the
        # mirror of the case above and shows up whenever something is attached to a bone of an
        # imported model - a cloth collider, a weapon socket, a hit spark.
        if father in stripped_owner:
            continue

        if child not in children.get(father, []):
            problems.append(f"{path}: transform {child} claims father {father}, which does not list it")

    # A nested prefab nobody lists is invisible in the hierarchy, which is the mirror of the check
    # above and just as silent a failure.
    for instance, parent in instance_parent.items():
        if parent == 0:
            continue
        if not any(kid in stripped_owner and stripped_owner[kid] == instance
                   for kid in children.get(parent, [])):
            problems.append(
                f"{path}: PrefabInstance {instance} is parented to transform {parent}, "
                f"which does not list any of its stripped transforms as a child")


def check_components(path, text, anchors):
    """A GameObject's component list and its components' m_GameObject must agree.

    Stripped GameObjects are exempt in both directions. A stub for an object inside a nested
    prefab has no m_Component list of its own - the real list lives in the source prefab - so a
    component added to one is recorded on the PrefabInstance as an addition rather than in a list
    here. Requiring the list reported every such addition as broken.
    """
    stripped = {int(a) for a in re.findall(r"^--- !u!\d+ &(-?\d+) stripped", text, re.M)}

    owners = {}
    listed = {}
    for class_id, anchor, body in blocks(text):
        if class_id == "1":
            found = re.search(r"^  m_Component:\n((?:  - component: \{fileID: \d+\}\n?)*)",
                              body + "\n", re.M)
            listed[anchor] = [int(m) for m in LOCAL_REF.findall(found.group(1))] if found else []
            if not listed[anchor] and anchor not in stripped:
                problems.append(f"{path}: GameObject {anchor} has no components, not even a Transform")
            continue
        owner = re.search(r"^  m_GameObject: \{fileID: (\d+)\}", body, re.M)
        if owner:
            owners[anchor] = int(owner.group(1))

    for go, comps in listed.items():
        for comp in comps:
            if comp not in owners:
                problems.append(f"{path}: GameObject {go} lists component {comp}, which has no m_GameObject")
            elif owners[comp] != go:
                problems.append(
                    f"{path}: GameObject {go} lists component {comp}, which belongs to {owners[comp]}")

    for comp, go in owners.items():
        if go == 0 or go in stripped:
            continue

        if comp not in listed.get(go, []):
            problems.append(f"{path}: component {comp} belongs to GameObject {go}, which does not list it")


def check_missing_metas():
    """Only for paths this project authors.

    Vendor packages arrive with their own .meta files, and some ship folder metas for folders whose
    entire contents are gitignored - Unity warns about those and then tidies them up itself. Failing
    the gate on somebody else's asset store package trains people to ignore the gate.
    """
    for root, dirs, files in os.walk(ASSETS):
        dirs[:] = [d for d in dirs if not d.startswith(".")]

        if not (root.replace(os.sep, "/") + "/").startswith(OWNED):
            continue
        for name in files:
            if name.endswith(".meta") or name.startswith("."):
                continue
            if not os.path.exists(os.path.join(root, name + ".meta")):
                problems.append(f"{os.path.join(root, name)}: has no .meta file")
        for name in files:
            if not name.endswith(".meta"):
                continue
            if not os.path.exists(os.path.join(root, name[: -len(".meta")])):
                problems.append(f"{os.path.join(root, name)}: orphan .meta with no asset")


# ---------------------------------------------------------------------------
# Cross-file references
# ---------------------------------------------------------------------------
# A reference into another asset carries both a guid and a fileID, and the fileID has to name an
# object that actually exists over there. Getting the guid right and the fileID wrong is worse than
# getting both wrong: the editor resolves the file, fails to find the object, and hands the field
# whatever it did find. That surfaces at runtime as "Specified cast is not valid" from inside a
# coroutine, several layers away from the asset that is actually broken.
#
# It cost a round trip. Nine spell VFX assets pointed at fileID 100100000, which is the prefab
# asset object rather than its root GameObject, so every one of them threw on first cast.
#
# Only prefabs are resolved. Scene and asset files are checked the same way elsewhere, and meshes,
# sprites and clips inside imported binaries have fileIDs this cannot see.
CROSS_REF = re.compile(r"\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: \d+\}")


def prefab_anchors_by_guid():
    """Every .prefab in the project, mapped guid -> set of anchors it defines."""
    found = {}

    for root, dirs, files in os.walk(ASSETS):
        dirs[:] = [d for d in dirs if not d.startswith(".")]

        for name in sorted(files):
            if not name.endswith(".prefab"):
                continue

            path = os.path.join(root, name)
            meta = path + ".meta"

            if not os.path.exists(meta):
                continue

            with open(meta, errors="ignore") as handle:
                guid = re.search(r"^guid: (\w+)", handle.read(), re.M)

            if not guid:
                continue

            with open(path, errors="ignore") as handle:
                text = handle.read()

            found[guid.group(1)] = (path, set(re.findall(r"^--- !u!\d+ &(-?\d+)", text, re.M)))

    return found


def check_cross_references(path, text, prefabs):
    for match in CROSS_REF.finditer(text):
        file_id, guid = match.group(1), match.group(2)

        if guid not in prefabs:
            continue

        target, anchors = prefabs[guid]

        if file_id in anchors:
            continue

        problems.append(
            f"{path}: references {{fileID: {file_id}}} in {target}, which defines no such object. "
            "A prefab asset is referenced by its root GameObject's fileID, not by 100100000.")


def main():
    packages = package_guids()
    # Without a package cache on disk there is no way to tell a package reference from a broken
    # one, so that check stands down rather than crying wolf.
    guids = (known_guids() | {g: "<package>" for g in packages}) if packages else None
    prefabs = prefab_anchors_by_guid()
    scanned = 0
    for root, dirs, files in os.walk(ASSETS):
        dirs[:] = [d for d in dirs if not d.startswith(".")]
        for name in sorted(files):
            if not name.endswith((".unity", ".prefab", ".asset")):
                continue
            path = os.path.join(root, name).replace(os.sep, "/")
            if not path.startswith(OWNED):
                continue
            body = open(path).read()
            check_document(path, body, guids)
            check_cross_references(path, body, prefabs)
            scanned += 1

    check_missing_metas()

    if guids is None:
        print("note: no package cache on disk, so package GUID references were not checked.")

    if problems:
        for problem in sorted(set(problems)):
            print("PROBLEM:", problem)
        sys.exit(f"\n{len(set(problems))} problem(s) across {scanned} files.")

    print(f"OK - {scanned} scenes, prefabs and assets, no problems found.")


if __name__ == "__main__":
    main()
