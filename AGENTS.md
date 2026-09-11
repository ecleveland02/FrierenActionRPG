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
| 1 - Foundation: bootstrap, scenes, save, input, debug | Written, **not yet verified in-editor** |
| 2 - Placeholder third-person player | Written, **not yet verified in-editor** |
| 3 - Modular character architecture | Not started |
| 4-7 | Not started, see `docs/MILESTONES.md` |

**Milestones 1 and 2 were authored in an environment with no Unity installed. Nothing has been
compiled or run.** Static checks passed (brace balance, assembly reference direction and acyclicity,
type resolution, cross-component member access, scene and prefab GUID cross-references, transform
hierarchy consistency, YAML parsing) but those catch structural errors, not API errors.

Treat any Unity API call in this repository as unverified. Wrong overloads, renamed Unity 6 APIs and
wrong parameter orders are the most likely defects. Finding them is high-value work.

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
10. **Logic worth testing goes in a plain class.** MonoBehaviours cannot be unit-tested meaningfully.
    `JumpGate`, `OrbitCameraSolver`, `InteractionSelector` and `MotorMath` exist because their logic
    was extracted out of components.

## Layout

```
Assets/Scripts/
  ScriptableObjects/  Frieren.Data        base types for authored content   (no deps)
  Save/               Frieren.Save        save format, storage, service     (no deps)
  Core/               Frieren.Core        bootstrap, services, scenes, state, input, debug
  Core/Editor/        Frieren.Core.Editor setup validation, asset + scene generation, menus
  Characters/         Frieren.Characters  motor, action lock, animation      (no deps)
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

## Verifying a change

- `Window > General > Test Runner > EditMode > Run All`. 97 tests, no scene or disk needed.
- `F7` opens the Boot scene; press Play.
- Compile errors block Play mode entirely.

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
