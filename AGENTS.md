# Working on this project as an AI assistant

Context for any AI assistant asked to help with this repository: Claude, ChatGPT, Copilot, or
whatever comes next. Read this before proposing or writing code.

## What this is

A non-commercial 3D single-player action RPG fan project in **Unity 6000.0 LTS**, C#, using the
Input System package. Original characters and story, inspired by the world and themes of
*Frieren: Beyond Journey's End*. Placeholder art only.

The design principle everything serves: **magic is a tool, not merely a weapon.** Spells interact
with the world - burning, freezing, lifting, flooding, repairing, unlocking - and most environmental
problems should have more than one solution. Systems are built data-driven so spells, enemies, items
and quests are authored content, never hardcoded branches.

## Current state

| Milestone | Status |
|---|---|
| 1 - Foundation: bootstrap, scenes, save, input, debug | Verified in the editor |
| 2 - Placeholder third-person player | Verified in the editor, character is playable |
| 3 - Modular character architecture | Verified in the editor |
| 4 - Magic framework, 3 spells | Verified in the editor |
| 5 - First enemy, plus the remaining 6 spells | Written, not yet run in the editor |
| 5.5 - Combat feel: placeholder feedback | Written, not yet run in the editor |
| 5.6 - Play-mode tests | Written, not yet run in the editor |
| 6 - Persistence for the world | Written, not yet run in the editor |
| 6.1 - Block on right mouse, spell wheel on Q | Written, not yet run in the editor |
| 6.2 - Time slows while the wheel is open | Written, not yet run in the editor |

Running alongside the milestones is a **Kael character spike** built by Codex: a Meshy-derived
skinned character with Unity `Cloth`, its own prefab and scene, plus a separate animated prototype.
It is deliberately isolated - it does not touch `Player.prefab` or `TestScene.unity` - and is
unverified in the editor. See [`docs/KAEL_CLOTH.md`](docs/KAEL_CLOTH.md),
[`docs/KAEL_CHARACTER.md`](docs/KAEL_CHARACTER.md) and
[`docs/COLLABORATION.md`](docs/COLLABORATION.md).

**Before Kael can replace the placeholder capsule**, it needs the components Milestone 3 added to
the player after the spike was branched: `CharacterStats` (with a stats definition assigned),
`CharacterHealth`, `CharacterMana`, `CharacterPersistence`, and an `ICharacterAnimation`
implementation - `MecanimCharacterAnimation` once there is an animator controller, since
`PlaceholderCharacterAnimation` squashes a primitive and is wrong for a real mesh. Do not promote it
until the vertical slice works; that is the brief's constraint and it still holds.
| 4-7 | Not started, see `docs/MILESTONES.md` |

Milestones 1 and 2 were authored with no Unity installed, and have since been run. The project
compiles with zero errors, boots, spawns the player, and the character is controllable with a
following camera. Pause works and the save probe round-trips.

Running it found three defects that static analysis had missed, which is worth knowing before
trusting any similar reasoning: a scene guard that suppressed the first-scene load, a pause that
could not be released because the state change ran inside an Input System callback, and a mouse
look delta that was summed when it should have been assigned. **Anything not actually exercised in
the editor should still be treated as unverified**, including jump, dodge, interaction and scene
transitions.

## Hard rules

These are decisions with reasons behind them, recorded in `docs/DECISIONS.md`. Do not quietly
reverse one; argue for the change instead.

1. **Never hand-edit `.unity`, `.prefab`, `.asset` or `.meta` files.** They are hand-authored YAML
   with cross-referencing GUIDs, they do not merge, and a mistake shows up as missing script
   references rather than an error. Change them through the Unity editor, or through
   `Frieren > Setup > Regenerate Core Scenes`, which rebuilds them from code.
2. **One integrator.** `CharacterMotor.Update` is the only thing that calls
   `CharacterController.Move`. Abilities call `SetHorizontalVelocity` and `Jump`. Two callers means
   gravity integrates twice.
3. **No singletons.** Services are constructed in `Bootstrapper` and registered in `ServiceLocator`.
   Do not add a static `Instance` to anything.
4. **One claimant on a character at a time.** Abilities that take over the body acquire
   `CharacterActionLock`. Locomotion stands down while it is held and never acquires it itself.
5. **Animation is told, never asked.** Gameplay reports state through `ICharacterAnimation`.
   Animation is never the source of truth for whether something happened.
6. **No `AnimationCurve` or `LayerMask` serialized into committed prefabs.** Their bad defaults fail
   silently: an empty curve evaluates to zero, a zero mask matches nothing. Use plain floats, and
   default masks to everything, narrowing by checking the object rather than the layer.
7. **Content is referenced by stable string `Id`**, never by asset path or build index. Save files
   depend on those ids; changing one after saves exist orphans data.
8. **A namespace may shadow a Unity type only if that type is one the project has banned.**
   `Frieren.Core.Input` and `Frieren.Characters.Animation` shadow the legacy `Input` and `Animation`
   classes deliberately. `Frieren.Player.Cameras` is plural because `Camera` is live API and
   shadowing it breaks the namespace with a CS0118 that names the wrong cause.
9. **The Input System package only.** No `UnityEngine.Input`. Input reaches gameplay through the
   `InputReader` ScriptableObject, not the generated C# wrapper.
10. **Vitals are told their maximum by `CharacterStats`, never by a serialized field.** Two places
    to set a maximum is one too many, and the one that loses is the one someone forgot to update.
11. **Death is an event.** `CharacterHealth` reports that health hit zero; it does not decide what
    happens next. The player and an enemy want opposite things there.
12. **Logic worth testing goes in a plain class.** MonoBehaviours cannot be unit-tested meaningfully.
    `JumpGate`, `OrbitCameraSolver`, `InteractionSelector`, `MotorMath` and `ResourcePool` exist
    because their logic was extracted out of components.

## Layout

```
Assets/Scripts/
  ScriptableObjects/  Frieren.Data        base types for authored content   (no deps)
  Save/               Frieren.Save        save format, storage, service     (no deps)
  Core/               Frieren.Core        bootstrap, services, scenes, state, input, debug
  Core/Editor/        Frieren.Core.Editor setup validation, asset + scene generation, menus
  Characters/         Frieren.Characters  motor, action lock, animation, stats, vitals
  Player/             Frieren.Player      locomotion, dodge, interactor, camera, spawner
  Tests/EditMode/     Frieren.Tests.EditMode
  Combat/ Magic/ Enemies/ Inventory/ Equipment/ Quests/ Dialogue/   empty, later milestones
```

Dependency direction is enforced by assembly definitions and must stay acyclic. `Frieren.Core` must
never depend on a gameplay assembly; invert with an interface instead. Adding code to an empty folder
means adding an `.asmdef` for it.

Full picture: `docs/ARCHITECTURE.md`. Reasoning: `docs/DECISIONS.md`. Plan: `docs/MILESTONES.md`.

## Coding standards

Readable C#, small focused classes, clear naming, minimal coupling. ScriptableObjects for authored
data, MonoBehaviours for runtime behaviour, plain C# for logic that needs neither - prefer plain C#,
because it is the testable kind. Comments explain *why*; the code already says what. No unnecessary
third-party dependencies. No premature optimization.

Match the surrounding style. It is consistent on purpose.

## Say which build you expect, every time

`Frieren.Core.Debugging.BuildStamp.Current` is printed at the top of the debug overlay. Bump it on
any commit that changes behaviour, and tell the user what it should read. If the overlay does not
show the expected value, nothing observed in that session means anything.

This exists because several hours went into debugging behaviour from revisions that were never on
the machine. Git reporting a commit does not mean the editor is running it. Ask for the stamp before
trusting any report.

## Before pushing C# neither of us can compile

```
python3 Tools/Validation/check_assemblies.py
python3 Tools/Validation/check_usings.py
python3 Tools/Validation/check_unity_yaml.py
```

Neither substitutes for the Test Runner. EditMode covers decisions; PlayMode covers anything needing
a frame, and its smoke test is the only thing that proves hand-written YAML deserialised into the
fields it was meant for. Ask for both when reporting a change.

It catches, cheaply, the mistakes that are invisible on inspection: an inheritance chain crossing an
unreferenced assembly (CS0012), a `using` whose assembly is not referenced (CS0234), the Input System
or UnityEditor used without the right reference, reference cycles, unbalanced braces.

The first of those is the subtle one and it has already cost a round trip. Using a type whose *base
class* lives in another assembly requires referencing that assembly too, even though the base is
never named in the file. `SpellDefinition` derives from `IdentifiableScriptableObject` in
`Frieren.Data`, so `Frieren.Player` must reference `Frieren.Data` despite never mentioning it.

`check_usings.py` catches the file-level version of the same problem, CS0246: a type used without a
`using` that reaches it. That is the more common mistake by far, and it has already shipped once - a
play-mode test base using `[UnityTearDown]` with no `using UnityEngine.TestTools;`, which failed the
whole test assembly and with it the editor's compile.

The third script does the same job for the hand-authored `.unity`, `.prefab` and `.asset` files:
duplicate anchors, a `fileID` naming nothing, a GUID no asset owns, a GameObject and its component
disagreeing about who owns whom, a transform listing a child that does not list it back, and missing
or orphan `.meta` files. It caught a `TextArea` string containing a colon on its first run, which is
not legal plain YAML and would have imported as a broken asset.

The corollary: **an assembly reference is not unused just because no type from it appears by name.**
Check with the script before removing one.

## Verifying a change

- `Window > General > Test Runner > EditMode > Run All`. 130 tests, no scene or disk needed.
- `F7` opens the Boot scene; press Play.
- Compile errors block Play mode entirely.

## Who owns what, right now

| Area | Owner |
|---|---|
| The milestone line: core, characters, magic, combat, world systems | Claude |
| The Kael character spike: cloth, rig, animation clips, Blender pipeline | Codex |

Concretely, Codex owns `KaelClothBuilder.cs`, `KaelPrototypeBuilder.cs`, `Tools/Blender/`,
`KaelClothPlayer.prefab`, `KaelClothTestScene.unity`, `Assets/Art/Characters/`, `ArtSource/` and the
`KAEL_*.md` documents. Claude owns everything under the milestone plan, including `Player.prefab`
and `TestScene.unity`.

`CharacterClothWind.cs` sits in Claude's assembly but is Codex's component; leave it alone unless
the cloth work needs it changed. Diagnostics for someone else's subsystem go in a new file rather
than into theirs - see `KaelClothDiagnostics.cs`.

Neither owner edits the other's files without saying so first. This division is why the first
merge of the two lines of work had zero conflicts.

## More than one assistant at once

Unity YAML does not merge, and two agents editing the same file produce silent loss rather than a
conflict. So:

- **Only one agent commits to a given file.** Agree ownership before starting, by file or folder.
- **Only one agent touches `.unity`, `.prefab`, `.asset` and `.meta` at all.** This is not
  negotiable; see rule 1.
- **Pull before starting and push when done.** Do not sit on uncommitted work in a shared file.
- An agent that cannot run Unity should say so in its findings rather than implying it verified
  something. "This should compile" and "this compiles" are different claims.
- Review findings are more useful than parallel edits. A second model reading unverified code for
  API errors is high-value; a second model writing into the same scene is a merge accident waiting
  to happen.

Current working branch: `claude/frieren-rpg-foundation-svvrqc`. It is the repository's default
branch; there is no `main` yet.
