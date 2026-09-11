# Decisions

Choices made without asking, per the instruction to pick something reasonable and document it.
Each is reversible; the cost of reversing is noted where it matters.

---

### 1. Unity 6000.0 LTS, pinned

Unity 6 LTS is the current long-term release and the one URP toon shading, the Input System and
`AI Navigation 2.x` are all current against. `ProjectVersion.txt` pins `6000.0.32f1`.

*Reversing:* trivial upward within Unity 6. Downgrading to 2022 LTS would mean re-pinning package
versions in `manifest.json`.

---

### 2. Input System package only, and no generated C# wrapper

`Active Input Handling` is set to *Input System Package (New)*, not *Both*. "Both" leaves the legacy
`UnityEngine.Input` API live, which means legacy calls can slip in unnoticed and then break the day
rebinding or gamepad support is added.

The Input System can generate a C# wrapper class from the `.inputactions` asset. This project does
not use it. That file is generated, checked in, and conflicts on every merge where two people touched
bindings; it also couples every consumer to exact action names. `InputReader` resolves actions by
name once at initialisation instead, and everything else depends only on `InputReader`.

*Cost:* a renamed action fails at runtime with a warning rather than at compile time. Mitigated by
the warning naming the missing action, and by there being one file to fix.

---

### 3. A service locator instead of one singleton per system

By Milestone 5 there are save, scene, input, state, combat and AI services that need each other. A
static `Instance` on each makes every one impossible to fake in a test and hides the dependency
graph entirely. `ServiceLocator` is populated in exactly one place - `Bootstrapper` - so there is a
single file to read to know what exists at runtime.

A service locator is still a global. The honest reason to use one here rather than constructor
injection is that Unity instantiates MonoBehaviours itself, so constructor injection needs a DI
container, and that is a bigger dependency than this project should take on at Milestone 1.

*Reversing:* if it becomes a problem, the fix is to inject the few services a system actually needs
through serialized fields or an installer. Nothing in the design blocks that.

---

### 4. Additive scene loading throughout

Including for the "main" gameplay scene. A single-mode load destroys the persistent systems object.
The town → forest → ruins → dungeon flow in Milestone 7 also wants two scenes resident during a
transition. The cost is that `SceneLoader` must track and unload the previous gameplay scene
explicitly rather than getting it for free.

---

### 5. Save entries are per-system JSON blobs

The alternative - one big serializable class listing every system's state - means every new system
edits a shared file, and every save file version needs a migration. Per-system blobs keyed by a
stable `SaveId` mean additions need no migration at all, and a missing key is simply a system at
its defaults.

`JsonUtility` (not `Newtonsoft.Json`) because it ships with Unity and needs no dependency. Its
limits are real and worth knowing: no dictionaries, no polymorphism, no `null` distinction for
value types, and reading a blob as the wrong type yields defaults silently rather than throwing.
`SaveEntryTests` pins that last behaviour down so it is a known quantity rather than a surprise.

*Reversing:* swapping the serializer means changing `SaveEntry` and `SaveService`, not every system.

---

### 6. URP installed but not configured; Built-in pipeline active

The brief says not to build final art before the gameplay loop is proven. A pipeline asset,
renderer features and a toon shader are art work. URP is in `manifest.json` so the switch is a
configuration step rather than a package install, but nothing is wired up yet, and gray-box
primitives render correctly under Built-in with the default material.

*When to revisit:* before any real character or environment art, and before the Milestone 3 rigged
character. Switching later means re-authoring any materials created in the meantime - which is why
placeholder materials should stay at the default one until then.

---

### 7. Content is identified by a stable string `Id`

`IdentifiableScriptableObject` gives every authored asset an `Id` that defaults to its asset name.
Save files, quest conditions and spell unlocks reference that string, never an asset reference or a
build index, so content can be renamed and moved without invalidating saves.

The tradeoff: an `Id` is a content contract. Changing one after saves exist orphans data. The field
is tooltipped accordingly.

---

### 8. Scenes are committed *and* regenerable from code

`Boot.unity` and `TestScene.unity` are checked in so the project works on clone. They can also be
rebuilt by `Frieren > Setup > Regenerate Core Scenes`.

Both, because a scene file is the one asset that cannot be meaningfully reviewed in a diff, and a
corrupted one is otherwise unrecoverable without redoing the wiring by hand. The generator also
documents, in code, exactly what the Boot scene is supposed to contain.

---

### 9. `InputReader.actions` is wired by an editor script, not by the committed asset

A `.inputactions` file is imported by a `ScriptedImporter`, which assigns the `InputActionAsset` a
generated `fileID`. That value exists only in the local `Library` folder, so a committed reference
to it cannot be authored by hand.

`ProjectSetupValidator` assigns the reference once, on first editor load, if it is empty. The
alternative was a manual step in every clone's setup instructions, which is the kind of thing people
forget and then debug for twenty minutes.

---

### 10. Git LFS for binary art; Unity SmartMerge configured for YAML

`.gitattributes` routes FBX, textures, audio and fonts through LFS, and marks Unity YAML as
mergeable with `UnityYAMLMerge`. SmartMerge needs a one-time local config per machine - the command
is in the comment at the top of `.gitattributes`. Without it, two people editing one scene is a
guaranteed conflict.

---

### 11. Layers and tags reserved up front

`TagManager.asset` defines layers for Player, Enemy, NPC, Ground, Interactable, MagicTarget,
Projectile and Trigger. Layer *indices* end up baked into physics masks and prefabs, so reshuffling
them later silently breaks collision filtering. Cheaper to reserve them now than to renumber later.

---

### 12. A pause toggle in Milestone 1

Strictly this is Milestone 2 territory. It is here because it is the only thing that exercises
input → state machine → game reaction end to end, which makes the milestone actually verifiable
rather than merely compiled. It is about fifteen lines in `Bootstrapper` and moves out when a real
pause menu exists.
