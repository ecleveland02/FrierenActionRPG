# Getting the project open, and writing code in it

Written for a machine that has never had Unity on it. If you already have Unity 6 and an IDE, skip
to [section 3](#3-get-the-files-onto-your-machine).

No command line is required. Unity Hub can clone the repository itself, and GitHub Desktop covers
pulling and pushing afterwards.

---

## 1. Install Unity Hub

Download Unity Hub from <https://unity.com/download>. Hub is the launcher; it is not the editor.

Sign in with a Unity account and activate a **Unity Personal** licence, which is free and fine for a
non-commercial fan project. Hub will prompt for this on first run; nothing works until a licence is
activated.

## 2. Install the editor

In Hub: **Installs → Install Editor → 6000.0 LTS** (the "Unity 6" long-term support stream).

The project pins `6000.0.32f1` in `ProjectSettings/ProjectVersion.txt`. If Hub offers a different
`6000.0.x` patch, take it - patch differences within 6000.0 are safe, and Unity will silently update
that file when it opens the project. Do **not** use Unity 2022 or earlier; the code targets Unity 6
APIs and will not compile.

Modules to tick during install:

- **Build support for your own platform** (Windows Build Support / Mac Build Support / Linux Build
  Support). Needed to make a runnable build later; not needed just to press Play.
- On Windows, **Microsoft Visual Studio Community** if you do not already have a C# editor. This is
  the simplest path - it installs the editor and wires it up for you.

Skip Android, iOS, WebGL and the documentation module. They are large and this project does not need
them.

## 3. Get the files onto your machine

The repository has a single branch, `claude/frieren-rpg-foundation-svvrqc`, and it is the default
branch. There is no `main` yet. Anything that clones the repository therefore lands on the right
branch with no extra step.

### The quick way: Unity Hub

Hub can clone it for you. **Projects → Add → Add project from repository**, give it
`https://github.com/ecleveland02/FrierenActionRPG.git` and a local folder. Hub clones the default
branch and registers the project in one go, so you can skip straight to
[section 4](#4-open-it).

Two things Hub does not do, both of which matter later rather than now:

- It is not a Git client. It clones once; it will not pull Claude's next commit or push your work
  back. For that you still want GitHub Desktop, your IDE's Git panel, or the command line - all of
  which work on the folder Hub just created, so installing one later costs nothing.
- Git LFS handling is not guaranteed. Nothing in the repository uses LFS yet, so this is harmless
  today, but run `git lfs install` before the first art asset arrives.

### GitHub Desktop, if you want pull and push without a terminal

1. Install from <https://desktop.github.com> and sign in with the account that owns the repository.
   It bundles Git and Git LFS.
2. **File → Clone repository**, pick `ecleveland02/FrierenActionRPG` from the **GitHub.com** tab,
   choose a local path, **Clone**.

Afterwards, **Fetch origin** pulls Claude's changes and **Commit** then **Push origin** sends yours.
Rider and VS Code can clone from their welcome screens and offer the same operations.

### Git on the command line

```bash
git lfs install
git clone https://github.com/ecleveland02/FrierenActionRPG.git
cd FrierenActionRPG
```

Git from <https://git-scm.com/downloads>, LFS from <https://git-lfs.com>. `git lfs install` matters:
`.gitattributes` routes FBX, textures, audio and fonts through LFS, and cloning without it turns
those files into small text pointers. Invisible until the first piece of art lands.

### Not "Download ZIP"

GitHub's **Code → Download ZIP** gives a folder Unity will open, and it is still the wrong choice. It
is not a git repository, so there is no way to pull the next commit or push your work back - you
would be re-downloading and hand-merging forever. It also substitutes pointer files for anything
tracked by LFS.

### Where to put it

Somewhere local. A path inside OneDrive, Dropbox or iCloud Drive will cause file-locking fights with
Unity's `Library` folder and produce import errors that look like corruption.

## 4. Open it

If Hub cloned the repository for you it is already in the Projects list; click it.

Otherwise: **Projects → Add → Add project from disk**, and pick the `FrierenActionRPG` folder - the
one containing `Assets`, `Packages` and `ProjectSettings`.

**The first open takes several minutes.** Unity is resolving packages, importing every asset and
building the `Library` folder, which is machine-local and gitignored. It is not frozen.

### What you should see when it finishes

Open the Console (**Window → General → Console**). Expect:

- `[Setup] Linked Assets/ScriptableObjects/Input/InputReader.asset to ...FrierenControls.inputactions`
  This is normal and happens exactly once. That one reference could not be committed, because
  Unity's importer generates its id locally, so a startup script wires it on first load.
- Possibly a warning about Active Input Handling. If you get an *error* saying the Input System is
  not the active handler, set **Edit → Project Settings → Player → Active Input Handling** to
  *Input System Package (New)* and restart Unity.

Then **Frieren → Open Boot Scene** (or `F7`) and press Play.

Neither milestone has ever been run, so there may well be compile errors. If there are, copy the
Console output and paste it back into the Claude session - the errors name the file and line, which
is all that is needed to fix them.

## 5. Set up code editing

Unity does not edit code itself. It hands `.cs` files to an external editor and generates a C#
project so that editor can offer autocomplete and go-to-definition.

Pick one:

| Editor | Cost | Notes |
|---|---|---|
| **Visual Studio Community** | Free | Windows and Mac. Easiest if installed via Hub in step 2. |
| **VS Code** | Free | Any OS. Needs two extensions: **C# Dev Kit** and **Unity** (both Microsoft). |
| **JetBrains Rider** | Free for non-commercial | Best Unity support of the three. This project qualifies for the free licence. |

Then, in Unity: **Edit → Preferences → External Tools** (on Mac, **Unity → Settings → External
Tools**), set **External Script Editor** to your choice, and click **Regenerate project files**.

That last click is the step people miss. It writes the `.sln` and `.csproj` files your editor reads.
Those files are gitignored on purpose - they are generated from the assembly definitions and would
conflict constantly if committed. Regenerating them is always safe.

After that, double-clicking any script in Unity's Project window opens it in your editor with full
IntelliSense.

## 6. Writing code

Scripts live under `Assets/Scripts`. You can create and edit them either in Unity's Project window
(**right-click → Create → Scripting → MonoBehaviour Script**) or directly in your editor - Unity
watches the folder and picks up changes either way.

Two things worth knowing early:

**Unity recompiles when it regains focus.** Save in your editor, alt-tab to Unity, and it compiles.
There is a small spinner in the bottom-right while it does. Compile errors appear in the Console and
**block Play mode entirely** until fixed.

**The project is split into assembly definitions.** Each `.asmdef` file under `Assets/Scripts` is a
separate compiled assembly, which is why editing one system does not recompile the others. The cost
is that a new folder cannot use another assembly's code until you add a reference: select the
`.asmdef`, add the assembly to its **Assembly Definition References** list, and click Apply. The
current dependency graph is in [ARCHITECTURE.md](ARCHITECTURE.md).

Running the tests: **Window → General → Test Runner → EditMode → Run All**.

## 7. Working alongside Claude

Both of us push to `claude/frieren-rpg-foundation-svvrqc`.

Before you start editing, get the latest:

```bash
git pull origin claude/frieren-rpg-foundation-svvrqc
```

To send your own changes back:

```bash
git add -A
git commit -m "what you changed"
git push origin claude/frieren-rpg-foundation-svvrqc
```

In GitHub Desktop these are **Fetch origin**, then a summary and **Commit**, then **Push origin**.
Unity Hub cannot do either; it only clones.

**Close Unity, or at least save your scenes, before pulling.** Unity holds scenes and prefabs in
memory and will overwrite files underneath a pull when it next saves.

After your first successful open, commit `Packages/packages-lock.json`. Unity generates it while
resolving packages, and committing it pins the exact package versions so a second machine resolves
identically.

When you want this branch to become the mainline, open a pull request on GitHub and merge it into
`main`. There is no rush; a branch is a perfectly good place for pre-alpha work.

## Troubleshooting

**"Package resolution error" or a package fails to load.** The versions in `Packages/manifest.json`
are pinned for Unity 6000.0 and were written without an editor available to verify them. Open
**Window → Package Manager**, let it resolve, update anything it flags, and commit the resulting
`packages-lock.json`.

**A scene opens empty, or objects have missing script references.** Run
**Frieren → Setup → Regenerate Core Scenes**. The player prefab and both scenes can be rebuilt from
code; that tool is the authoritative description of what they should contain.

**Everything is magenta.** The project renders with the Built-in pipeline on purpose right now (URP
is installed but not configured - see [DECISIONS.md](DECISIONS.md), decision 6). Magenta means
something is looking for a URP material. Reset the affected renderer to the default material.

**Unity hangs on "Importing" for a very long time on first open.** Give it ten minutes. If it is
still stuck, quit Unity, delete the `Library` folder, and reopen - `Library` is a rebuildable cache
and deleting it is safe.

**Compile errors immediately after opening.** Expected, and useful. Paste the Console output into
the Claude session.
