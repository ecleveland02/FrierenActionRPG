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
    """A Transform's children must each name it back as their father."""
    fathers = {}
    children = {}
    for class_id, anchor, body in blocks(text):
        if class_id != "4":
            continue
        father = re.search(r"^  m_Father: \{fileID: (\d+)\}", body, re.M)
        fathers[anchor] = int(father.group(1)) if father else 0
        listed = re.search(r"^  m_Children:\n((?:  - \{fileID: \d+\}\n?)*)", body + "\n", re.M)
        children[anchor] = [int(m) for m in LOCAL_REF.findall(listed.group(1))] if listed else []

    for parent, kids in children.items():
        for kid in kids:
            if kid not in fathers:
                problems.append(f"{path}: transform {parent} lists child {kid}, which is not a Transform")
            elif fathers[kid] != parent:
                problems.append(
                    f"{path}: transform {parent} lists child {kid}, but {kid}'s father is {fathers[kid]}")

    for child, father in fathers.items():
        if father != 0 and child not in children.get(father, []):
            problems.append(f"{path}: transform {child} claims father {father}, which does not list it")


def check_components(path, text, anchors):
    """A GameObject's component list and its components' m_GameObject must agree."""
    owners = {}
    listed = {}
    for class_id, anchor, body in blocks(text):
        if class_id == "1":
            found = re.search(r"^  m_Component:\n((?:  - component: \{fileID: \d+\}\n?)*)",
                              body + "\n", re.M)
            listed[anchor] = [int(m) for m in LOCAL_REF.findall(found.group(1))] if found else []
            if not listed[anchor]:
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
        if go != 0 and comp not in listed.get(go, []):
            problems.append(f"{path}: component {comp} belongs to GameObject {go}, which does not list it")


def check_missing_metas():
    for root, dirs, files in os.walk(ASSETS):
        dirs[:] = [d for d in dirs if not d.startswith(".")]
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


def main():
    packages = package_guids()
    # Without a package cache on disk there is no way to tell a package reference from a broken
    # one, so that check stands down rather than crying wolf.
    guids = (known_guids() | {g: "<package>" for g in packages}) if packages else None
    scanned = 0
    for root, dirs, files in os.walk(ASSETS):
        dirs[:] = [d for d in dirs if not d.startswith(".")]
        for name in sorted(files):
            if not name.endswith((".unity", ".prefab", ".asset")):
                continue
            path = os.path.join(root, name).replace(os.sep, "/")
            if not path.startswith(OWNED):
                continue
            check_document(path, open(path).read(), guids)
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
