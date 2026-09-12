# Architecture

How the foundation fits together, and where Milestones 2-7 attach to it.

## Assemblies

Code is split into assembly definitions so a change in one system does not recompile the others,
and so the dependency direction is enforced by the compiler rather than by discipline.

```
Frieren.Data (no deps)        Frieren.Save (no deps)        Frieren.Characters (no deps)
        ^                            ^                              ^
        |                            |                              |
        +--------- Frieren.Core -----+                              |
                        ^                                           |
                        +-------------- Frieren.Player -------------+
                                              ^
                        Frieren.Core.Editor --+-- Frieren.Tests.EditMode
```

The direction is enforced by the compiler, and the leaves are deliberately dependency-free:
`Frieren.Characters` references nothing, so the enemy work in Milestone 5 cannot accidentally drag
player code in with it.

| Assembly | Folder | Holds |
|---|---|---|
| `Frieren.Data` | `Scripts/ScriptableObjects` | Base types for authored content. Depends on nothing, so everything can depend on it. |
| `Frieren.Save` | `Scripts/Save` | Save file format, storage backends, `SaveService`. Deliberately knows nothing about scenes, input or gameplay. |
| `Frieren.Characters` | `Scripts/Characters` | What any character needs, player or enemy: motor, action lock, animation abstraction, stats, health, mana, persistence. |
| `Frieren.Player` | `Scripts/Player` | Player-specific intent only: locomotion, dodge, interaction probe, camera rig, spawner. |
| `Frieren.Core` | `Scripts/Core` | Bootstrap, service registry, scene loading, game state, input, debug tooling. |
| `Frieren.Core.Editor` | `Scripts/Core/Editor` | Editor-only: setup validation, asset creation, scene regeneration, menus. |
| `Frieren.Tests.EditMode` | `Scripts/Tests/EditMode` | Edit-mode tests. |

Remaining gameplay assemblies (`Frieren.Combat`, `Frieren.Magic`, `Frieren.Enemies`, ...) get their
own asmdefs as those folders gain code. They should depend on `Frieren.Core` and `Frieren.Data`, and on each
other as little as possible. `Frieren.Core` must never gain a dependency on a gameplay assembly:
if core needs to talk to gameplay, that is a signal to invert it with an interface or an event.

## Boot sequence

`Boot.unity` contains a single `PersistentSystems` object carrying `Bootstrapper`, `SceneLoader` and
`DebugOverlay`. `Bootstrapper` runs at execution order -1000 and is the game's composition root:

1. Duplicate-instance guard, then `DontDestroyOnLoad`.
2. `ServiceLocator.Clear()` - defends against static state surviving a domain-reload-free play session.
3. Apply `LogSettings`, attach the debug overlay.
4. Construct `SaveService` over `FileSaveStorage` (or `InMemorySaveStorage` when the inspector flag is set).
5. Ensure a `SceneLoader`, register the `SceneCatalog`.
6. Build the `GameStateMachine` and register Booting / MainMenu / Loading / Playing / Paused.
7. Initialise the `InputReader` and subscribe pause.
8. Register everything in `ServiceLocator`.
9. Enter `Loading`, load the first scene, enter `Playing`.

Consumers resolve services in `Awake` or `Start` and cache the result. Resolving every frame works
but defeats the point of caching a reference.

### Entering play mode from an arbitrary scene

`SceneBootstrapGuard` runs at `AfterSceneLoad`, notices Boot is not present, and loads it additively.
It sets `BootedFromAnotherScene`, which tells `Bootstrapper` to skip its first-scene load so the
scene being tested survives. The whole mechanism is `#if UNITY_EDITOR` - a build always starts at Boot.

## Scene management

Everything loads additively, including the "main" gameplay scene. A single-mode load would destroy
the persistent systems object along with the old scene.

`SceneLoader.TransitionToGameplayScene` loads the new scene, sets it active so its lighting and
skybox apply, then unloads the previous gameplay scene - new-in-before-old-out, so nothing is ever
left standing in a void.

Scenes are referenced through `GameSceneDefinition` assets rather than names or build indices.
Build indices reorder silently; a save file that stored one breaks. A definition's stable `Id` is
what a save file or a quest records, and `SceneCatalog` resolves an id back to a definition.

## Save system

```
ISaveable  --Capture()-->  SaveEntry { Key, TypeName, Json }  -->  SaveGameData  -->  ISaveStorage
           <--Restore()--                                                              (file / memory)
```

The save file never learns the shape of any system's state. Each `ISaveable` serialises its own
plain state class into a `SaveEntry` under its `SaveId`. Consequences worth knowing:

- A new system can start persisting state without touching the file format.
- An old save missing a key simply leaves that system at its defaults - no migration needed for additions.
- `SaveMigration` exists for changes to the *envelope*, and refuses files from a newer build.
- `TypeName` is recorded for diagnostics only. It is never used to resolve a type, so a state class
  can be renamed or moved freely.

**Load order does not matter.** `SaveService.Load` restores everything registered at that moment, and
`Register` restores any saveable that arrives later and has an entry waiting. A player spawned by a
scene that is still streaming in gets its state either way.

`ISaveStorage` is the seam that keeps this testable - edit-mode tests run entirely on
`InMemorySaveStorage` - and the hook for cloud or platform saves later.

## Input

`InputReader` is a ScriptableObject wrapping `FrierenControls.inputactions`. It resolves actions by
name once, then exposes plain C# events and cached values. Consumers reference the asset in the
inspector; no singleton, no runtime lookup, and the Input System's generated C# wrapper is not used
(see decision 2).

Action maps: `Gameplay` (Move, Look, Jump, Sprint, Dodge, Interact, Cast, Pause) and `UI` (Navigate,
Submit, Cancel, Point, Click). Control schemes: Keyboard&Mouse and Gamepad. The gameplay actions
Milestone 2 needs already exist and are bound - only the consumer is missing.

## Debugging

`GameLog` tags every line with a `LogChannel` so a noisy system can be silenced alone. `Info` and
`Warn` carry `[Conditional]` on `UNITY_EDITOR` / `DEVELOPMENT_BUILD`, so in a release player the
calls and their string concatenation are removed by the compiler rather than skipped at runtime.
`Error` is unconditional and bypasses channel filtering - a silenced channel should not hide a fault.

`DebugOverlay` is IMGUI on purpose: no canvas, no prefab, no scene setup, works in any scene, and
will not collide with the real UI built later.

## Character layer (Milestone 2)

```
input  ->  PlayerLocomotion  --.
                               |--> CharacterMotor --> CharacterController
       ->  PlayerDodge       --'        (integrates, once per frame)
              |
              +-- holds --> CharacterActionLock  <-- locomotion stands down while held
```

**One integrator.** `CharacterMotor` moves the `CharacterController` in its own `Update`, at
execution order 100 so every ability has already set a velocity for the frame. Abilities call
`SetHorizontalVelocity` and `Jump`; nothing else calls `CharacterController.Move`. There is exactly
one place the character's position changes, so double integration is impossible by construction.

**One claimant at a time.** `CharacterActionLock` is a single-holder claim on the body. The dodge
takes it for its duration; locomotion sees it held and stops steering without ever taking it itself,
because movement is a character's default state rather than an action competing for it. Casting
(Milestone 4) and attacks (Milestone 5) take the same lock.

**Animation is told, never asked.** `ICharacterAnimation` is one-directional: gameplay reports state,
presentation reacts. `PlaceholderCharacterAnimation` squashes and tints a primitive;
`MecanimCharacterAnimation` drives an `Animator`. Swapping the placeholder capsule for the rigged
Blender character means changing which component is on the prefab, and nothing else. Animation must
never become the source of truth for whether the character is moving, or a missing clip turns into a
gameplay bug.

**Logic is extracted so it can be tested.** MonoBehaviours cannot be unit-tested meaningfully, so
the parts that are easy to get subtly wrong live in plain classes: `JumpGate` (coyote time and input
buffering), `OrbitCameraSolver` (orbit math, pitch clamping, yaw wrapping), `InteractionSelector`
(candidate scoring), `MotorMath` (gravity, jump height, camera-relative direction). The
MonoBehaviours are then thin wiring.

**The player is spawned, not placed.** `PlayerSpawner` instantiates the prefab and wires the camera
in both directions. One prefab stays the single definition of what a player is, and respawning,
loading a save into a named spawn point, and arriving through a specific door all reuse the same
step.

## Character vitals (Milestone 3)

```
CharacterStatsDefinition (asset, per archetype)
          |
   CharacterStats  ---- Changed ---->  CharacterHealth  ) both are
          |                            CharacterMana    ) CharacterResource
          |                                   ^
          |                                   |
          +--------------------------  CharacterPersistence (ISaveable)
```

**Stats come from an asset, not a prefab.** One `CharacterStatsDefinition` per archetype, so
Milestone 5's enemies share a template and a later "+20% maximum mana" has a base to multiply.
Nothing reads the asset directly: everything goes through `CharacterStats`, which is a passthrough
today and the seam equipment, buffs and progression plug into later.

**Health and mana are the same problem twice.** `CharacterResource` owns the shared part - a bounded
value, a maximum sourced from stats, regeneration that pauses after use, a change event. The
differences live in the subclasses: death in `CharacterHealth`, all-or-nothing spending in
`CharacterMana`. Stamina will be the third subclass, not a third implementation.

The arithmetic sits in `ResourcePool`, a plain class, for the same reason as `JumpGate` and
`MotorMath`: clamping, proportional rescaling and all-or-nothing withdrawal are easy to get subtly
wrong and impossible to confirm by looking at a health bar.

**Death is an event, not a behaviour.** `CharacterHealth` reports that health hit zero and does
nothing else - no disabling input, no animation, no despawn. The player and an enemy want opposite
things to happen there, and either one hardcoded would fight the other. A dead character ignores
further damage, so a corpse hit three more times does not die three more times.

**Vitals know nothing about saving.** `CharacterPersistence` implements `ISaveable` and reads them.
That gives a character one save key instead of one per component, keeps health and mana as pure
gameplay, and makes adding stamina a field rather than another registration.

**Initialisation is lazy, not `Awake`.** The pool is built on first access. Unity does not call
`Awake` in edit mode, so an eagerly built pool would be unreachable from an edit-mode test, and a
character assembled by a spawner may have its stats set after its components exist.

## Where the next milestones attach

| Milestone | Attaches via |
|---|---|
| 3 - Character architecture | Done. The rigged character still needs to replace `PlaceholderCharacterAnimation` with `MecanimCharacterAnimation`. |
| 4 - Magic framework | `SpellDefinition : IdentifiableScriptableObject` with a list of effect objects. Casting takes `CharacterActionLock` and pays through `CharacterMana.TrySpend`. Damage effects build a `DamageInfo`. Environmental interaction is an effect type, not a special case. |
| 5 - Enemy | Reuses `CharacterMotor` driven by a navigation agent instead of input. Its own behaviour state machine, not `GameStateMachine`, which is for application modes. `PlayerDodge.IsInvulnerable` is already exposed for damage to read. |
| 6 - Environmental interaction | Components responding to spell effect *types*. Separate from `IInteractable`: pressing a lever and burning a crate are different verbs with different rules. |
| 7 - Vertical slice | `GameSceneDefinition` per area, added to `SceneCatalog` and Build Settings. `PlayerSpawner` handles arrival in each. |

## Conventions

- One responsibility per class. `PlayerController` must not become the place everything lands.
- ScriptableObjects for authored data; MonoBehaviours for runtime behaviour; plain C# for logic that
  needs neither - plain C# is the testable kind, so prefer it.
- Content is referenced by stable string `Id`, never by asset path or build index.
- Comments explain *why*. The code already says what.
- A namespace segment may shadow a Unity type only if that type is one the project has banned.
  `Frieren.Core.Input` and `Frieren.Characters.Animation` shadow the legacy `Input` and `Animation`
  classes, which is harmless and mildly protective. `Frieren.Player.Cameras` is plural precisely
  because `Camera` is live API, and shadowing it would break every file in the namespace with a
  CS0118 that names the wrong cause.
