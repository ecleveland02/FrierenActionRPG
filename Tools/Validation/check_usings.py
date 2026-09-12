#!/usr/bin/env python3
"""Find types used without a using directive that can resolve them.

Neither assistant on this project can compile the C# it writes, so a missing `using` survives until
someone opens Unity. `check_assemblies.py` catches the assembly-level version of this (CS0234,
CS0012); this catches the file-level one (CS0246), which is the more common mistake by far and the
one that has actually shipped: a play-mode test base class that used [UnityTearDown] without
`using UnityEngine.TestTools;`.

It resolves project types by scanning every declaration in the tree, so it stays correct as things
move between namespaces. For Unity's own types it knows only the handful of namespaces this project
depends on outside the UnityEngine root, and it only flags those in positions where they can only be
a type: an attribute, or a static member access.
"""
import collections
import os
import re
import sys

SRC = "Assets/Scripts"

DECL = re.compile(r"^\s*(?:public|internal|private|protected|sealed|static|abstract|partial|\s)*"
                  r"\b(?:class|interface|enum|struct)\s+([A-Za-z_]\w*)", re.M)
NAMESPACE = re.compile(r"^\s*namespace\s+([\w.]+)", re.M)
USING = re.compile(r"^\s*using\s+(?:static\s+)?([\w.]+)\s*;", re.M)
ALIAS = re.compile(r"^\s*using\s+(\w+)\s*=\s*([\w.]+)\s*;", re.M)

# Unity and NUnit types this project uses from outside the roots that are always available.
# Only names checked in unambiguous positions belong here.
EXTERNAL = {
    "UnityTest": "UnityEngine.TestTools",
    "UnitySetUp": "UnityEngine.TestTools",
    "UnityTearDown": "UnityEngine.TestTools",
    "LogAssert": "UnityEngine.TestTools",
    "SceneManager": "UnityEngine.SceneManagement",
    "LoadSceneMode": "UnityEngine.SceneManagement",
    "Keyboard": "UnityEngine.InputSystem",
    "Mouse": "UnityEngine.InputSystem",
    "Gamepad": "UnityEngine.InputSystem",
    "InputSystem": "UnityEngine.InputSystem",
}


def strip(text):
    """Remove strings and comments, so a word in a doc comment is not mistaken for a type."""
    text = re.sub(r'@"(?:[^"]|"")*"', '""', text)
    text = re.sub(r'"(?:\\.|[^"\\])*"', '""', text)
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    text = re.sub(r"//[^\n]*", "", text)
    return text


def source_files():
    found = []
    for root, dirs, names in os.walk(SRC):
        dirs[:] = [d for d in dirs if not d.startswith(".")]
        found += [os.path.join(root, n) for n in sorted(names) if n.endswith(".cs")]
    return found


def main():
    files = source_files()
    declared = collections.defaultdict(set)

    for path in files:
        raw = open(path).read()
        match = NAMESPACE.search(raw)
        namespace = match.group(1) if match else ""
        for name in DECL.findall(strip(raw)):
            declared[name].add(namespace)

    problems = []

    for path in files:
        raw = open(path).read()
        body = strip(raw)
        match = NAMESPACE.search(raw)
        own = match.group(1) if match else ""

        visible = set(USING.findall(raw))
        aliases = {name for name, _ in ALIAS.findall(raw)}

        # A namespace can see every ancestor of itself without a using.
        parts = own.split(".") if own else []
        for i in range(len(parts), 0, -1):
            visible.add(".".join(parts[:i]))

        # Members declared in this file shadow nothing, but they do explain a matching word.
        local = set(re.findall(r"\b(?:const|readonly|static)?\s*[\w<>\[\],.?]+\s+(\w+)\s*(?:[;={]|=>)", body))

        for token in sorted(set(re.findall(r"\b([A-Z]\w*)\b", body))):
            if token in aliases:
                continue

            # A constant or field named after a type explains the word by itself. ProjectPaths
            # has `const string InputReader = "..."`, which is not a use of the InputReader type.
            if token in local:
                continue

            homes = declared.get(token)

            if homes:
                if any(home in visible for home in homes):
                    continue
                # Written out in full, or reached through another type.
                if re.search(r"[\w.]\.\s*" + re.escape(token) + r"\b", body):
                    continue
                problems.append(
                    f"{path}: '{token}' lives in {sorted(homes)} and no using here reaches it")
                continue

            if token in EXTERNAL and EXTERNAL[token] not in visible:
                # Only positions where the word cannot be anything but a type.
                attribute = re.search(r"\[\s*" + re.escape(token) + r"\s*[\]\(]", body)
                static_call = re.search(r"\b" + re.escape(token) + r"\s*\.\s*[A-Za-z_]", body)

                if attribute or static_call:
                    problems.append(f"{path}: '{token}' needs 'using {EXTERNAL[token]};'")

    if problems:
        for problem in sorted(set(problems)):
            print("PROBLEM:", problem)
        sys.exit(f"\n{len(set(problems))} problem(s) across {len(files)} files.")

    print(f"OK - {len(files)} C# files, every type reachable from its usings.")


if __name__ == "__main__":
    main()
