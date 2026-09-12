#!/usr/bin/env python3
"""Static checks for the Unity C# in this repository, runnable without Unity.

Both assistants working on this project write code they cannot compile. These are the
checks that catch the resulting mistakes cheaply, in order of how much time each has
actually saved:

  1. Inheritance chains that cross an unreferenced assembly (C# error CS0012). Using a
     type whose *base class* lives elsewhere requires referencing that assembly too, even
     though the base is never named. This is invisible on inspection and produced a
     confusing cascade the first time it happened.
  2. `using Frieren.X` where the owning assembly is not referenced (CS0234).
  3. UnityEngine.InputSystem used without the package reference.
  4. UnityEditor used from a non-editor assembly.
  5. Assembly reference cycles.
  6. Unbalanced braces.

Usage:  python3 Tools/Validation/check_assemblies.py
Exit code is non-zero when anything fails, so it works as a pre-push gate.
"""
import json
import os
import re
import sys

SCRIPTS = "Assets/Scripts"


def load_assemblies():
    found = {}
    for root, _, files in os.walk(SCRIPTS):
        for name in files:
            if name.endswith(".asmdef"):
                found[root.replace(os.sep, "/")] = json.load(open(os.path.join(root, name)))
    return found



# ---------------------------------------------------------------------------
# Engine modules
# ---------------------------------------------------------------------------
# A `using UnityEngine.X` whose module is not in Packages/manifest.json is a CS0234 that no amount
# of asmdef checking will find, because the reference is to an engine module rather than to a
# project assembly. It cost a build: GPT added EnemyNavigation using UnityEngine.AI without adding
# com.unity.modules.ai, and Frieren.Enemies stopped compiling, which took Player, Presentation and
# both test assemblies down with it.
#
# Vendor scripts count. Anything under Assets/ without an asmdef lands in Assembly-CSharp and breaks
# the build just as thoroughly as our own code does.
#
# Only namespaces whose module is unambiguous are listed. Everything else - Rendering,
# SceneManagement, Events, Assertions, Pool, Jobs - lives in CoreModule and is always present, and
# guessing about the rest would produce false alarms nobody reads.
NAMESPACE_MODULES = {
    "UnityEngine.AI": "com.unity.modules.ai",
    "UnityEngine.Animations": "com.unity.modules.animation",
    "UnityEngine.Audio": "com.unity.modules.audio",
    "UnityEngine.Playables": "com.unity.modules.director",
    "UnityEngine.Timeline": "com.unity.modules.director",
    "UnityEngine.Tilemaps": "com.unity.modules.tilemap",
    "UnityEngine.Video": "com.unity.modules.video",
    "UnityEngine.XR": "com.unity.modules.xr",
    "UnityEngine.TerrainUtils": "com.unity.modules.terrain",
    "UnityEngine.UI": "com.unity.ugui",
    "UnityEngine.EventSystems": "com.unity.ugui",
    "UnityEngine.InputSystem": "com.unity.inputsystem",
}

USING_NAMESPACE = re.compile(r"^\s*using\s+(UnityEngine\.[A-Za-z0-9_.]+)\s*;", re.M)


def check_engine_modules():
    found = []
    manifest_path = os.path.join("Packages", "manifest.json")

    if not os.path.exists(manifest_path):
        return found

    with open(manifest_path) as handle:
        installed = set(json.load(handle).get("dependencies", {}))

    for root, dirs, names in os.walk("Assets"):
        dirs[:] = [d for d in dirs if not d.startswith(".")]

        for name in sorted(names):
            if not name.endswith(".cs"):
                continue

            path = os.path.join(root, name)

            with open(path, errors="ignore") as handle:
                text = handle.read()

            for namespace in sorted(set(USING_NAMESPACE.findall(text))):
                # Longest match wins, so UnityEngine.AI.Navigation resolves to the AI module.
                module = None

                for known, candidate in NAMESPACE_MODULES.items():
                    if namespace == known or namespace.startswith(known + "."):
                        if module is None or len(known) > len(module[0]):
                            module = (known, candidate)

                if module and module[1] not in installed:
                    found.append(
                        f"{path}: uses '{namespace}' but '{module[1]}' is not in "
                        "Packages/manifest.json - this is a CS0234 that fails the whole assembly")

    return found


def main():
    if not os.path.isdir(SCRIPTS):
        sys.exit(f"Run this from the repository root; {SCRIPTS} not found.")

    assemblies = load_assemblies()

    def assembly_for(path):
        best, longest = None, -1
        for folder, data in assemblies.items():
            if path.startswith(folder + "/") and len(folder) > longest:
                best, longest = data, len(folder)
        return best

    files = [os.path.join(r, f).replace(os.sep, "/")
             for r, _, fs in os.walk(SCRIPTS) for f in sorted(fs) if f.endswith(".cs")]

    declared, namespaces = {}, {}
    for path in files:
        asm = assembly_for(path)
        if asm is None:
            sys.exit(f"{path} is not inside any assembly definition.")
        source = open(path).read()
        for ns in re.findall(r"^namespace ([\w.]+)", source, re.M):
            namespaces.setdefault(asm["name"], set()).add(ns)
        for match in re.finditer(
                r"^\s*(?:public|internal)\s+(?:static\s+|sealed\s+|abstract\s+|partial\s+)*"
                r"(?:class|interface|struct|enum)\s+(\w+)\s*(?::\s*([\w.]+))?", source, re.M):
            declared[match.group(1)] = (asm["name"], match.group(2))

    def chain(type_name, seen=None):
        seen = seen or set()
        if type_name in seen or type_name not in declared:
            return set()
        seen.add(type_name)
        asm_name, base = declared[type_name]
        result = {asm_name}
        if base:
            result |= chain(base.split(".")[-1], seen)
        return result

    problems = []
    for path in files:
        asm = assembly_for(path)
        source = open(path).read()
        body = re.sub(r'"(?:\\.|[^"\\])*"', '""', source)
        body = re.sub(r"^\s*///[^\n]*", "", body, flags=re.M)
        body = re.sub(r"//[^\n]*", "", body)
        body = re.sub(r"/\*.*?\*/", "", body, flags=re.S)

        if body.count("{") != body.count("}"):
            problems.append(f"{path}: unbalanced braces")

        referenced = set(asm.get("references", [])) | {asm["name"]}

        for ns in re.findall(r"^using (Frieren\.[\w.]+);", source, re.M):
            owners = {a for a, nss in namespaces.items() if ns in nss}
            if owners and not owners & referenced:
                problems.append(
                    f"{path}: CS0234 - `using {ns};` is provided by {sorted(owners)}, "
                    f"but {asm['name']} references {sorted(referenced)}")

        for type_name in declared:
            if not re.search(r"\b" + re.escape(type_name) + r"\b", body):
                continue
            missing = chain(type_name) - referenced
            if missing:
                problems.append(
                    f"{path}: CS0012 - uses {type_name}, whose base chain needs "
                    f"{sorted(missing)}; add it to {asm['name']}")

        if "UnityEngine.InputSystem" in source and "Unity.InputSystem" not in asm.get("references", []):
            problems.append(f"{path}: uses the Input System without referencing Unity.InputSystem")

        if re.search(r"\bUnityEditor\b", body) and "Editor" not in (asm.get("includePlatforms") or []):
            problems.append(f"{path}: uses UnityEditor from non-editor assembly {asm['name']}")

    edges = {d["name"]: [r for r in d.get("references", []) if r.startswith("Frieren.")]
             for d in assemblies.values()}

    def find_cycle(node, seen):
        if node in seen:
            return seen + [node]
        for ref in edges.get(node, []):
            found = find_cycle(ref, seen + [node])
            if found:
                return found
        return None

    for node in edges:
        cycle = find_cycle(node, [])
        if cycle:
            problems.append("assembly cycle: " + " -> ".join(cycle))

    problems.extend(check_engine_modules())

    unique = sorted(set(problems))
    if unique:
        print(f"FAILED - {len(unique)} problem(s):\n")
        for problem in unique:
            print("  " + problem)
        sys.exit(1)

    print(f"OK - {len(files)} C# files across {len(assemblies)} assemblies, no problems found.")


if __name__ == "__main__":
    main()
