# Architecture

How the foundation fits together, and where Milestones 2-7 attach to it.

## Assemblies

Code is split into assembly definitions so a change in one system does not recompile the others,
and so the dependency direction is enforced by the compiler rather than by discipline.

```
Frieren.Data          (no dependencies)
    ^
    |
Frieren.Core  ------> Frieren.Save   (no dependencies)
    ^                      ^
    |                      |
Frieren.Core.Editor   Frieren.Tests.EditMode
```

| Assembly | Folder | Holds |
|---|---|---|
| `Frieren.Data` | `Scripts/ScriptableObjects` | Base types for authored content. Depends on nothing, so everything can depend on it. |
| `Frieren.Save` | `Scripts/Save` | Save file format, storage backends, `SaveService`. Deliberately knows nothing about scenes, input or gameplay. |
| `Frieren.Core` | `Scripts/Core` | Bootstrap, service registry, scene loading, game state, input, debug tooling. |
| `Frieren.Core.Editor` | `Scripts/Core/Editor` | Editor-only: setup validation, asset creation, scene regeneration, menus. |
| `Frieren.Tests.EditMode` | `Scripts/Tests/EditMode` | Edit-mode tests. |

Gameplay assemblies (`Frieren.Player`, `Frieren.Combat`, `Frieren.Magic`, ...) get their own asmdefs
as those folders gain code. They should depend on `Frieren.Core` and `Frieren.Data`, and on each
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

## Where the next milestones attach

| Milestone | Attaches via |
|---|---|
| 2 - Player controller | New `Frieren.Player` asmdef. Reference the `InputReader` asset; read `GameStateMachine.Current` to ignore input while paused. |
| 3 - Character architecture | `CharacterStats`, `CharacterHealth`, `CharacterMana` etc. as separate components. Any that persists implements `ISaveable` and registers with `SaveService`. |
| 4 - Magic framework | `SpellDefinition : IdentifiableScriptableObject` with a list of effect objects. Its `Id` is what a save file records as "known spells". Environmental interaction is an effect type, not a special case. |
| 5 - Enemy | Its own behaviour state machine - not `GameStateMachine`, which is for application modes. |
| 6 - Environmental interaction | Components responding to spell effect *types*, so a new spell reusing an existing effect works on existing objects with no changes. |
| 7 - Vertical slice | `GameSceneDefinition` per area, added to `SceneCatalog` and Build Settings; `SceneLoader` handles the transitions. |

## Conventions

- One responsibility per class. `PlayerController` must not become the place everything lands.
- ScriptableObjects for authored data; MonoBehaviours for runtime behaviour; plain C# for logic that
  needs neither - plain C# is the testable kind, so prefer it.
- Content is referenced by stable string `Id`, never by asset path or build index.
- Comments explain *why*. The code already says what.
