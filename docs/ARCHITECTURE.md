# Architecture

How the foundation fits together, and where Milestones 2-7 attach to it.

## Assemblies

Code is split into assembly definitions so a change in one system does not recompile the others,
and so the dependency direction is enforced by the compiler rather than by discipline.

```
Frieren.Data          -> (nothing)
Frieren.Save          -> (nothing)
Frieren.Core          -> Data, Save
Frieren.World         -> Core
Frieren.Characters    -> Core, Data, Save
Frieren.Magic         -> Core, Characters, Data
Frieren.Enemies       -> Core, Characters, Data
Frieren.Player        -> Core, Characters, Magic, Data
Frieren.Presentation  -> Core, Characters, Magic, Enemies, Data
Frieren.Core.Editor   -> everything above
Frieren.Tests.EditMode-> everything above
Frieren.Tests.PlayMode-> everything above
```

The direction is enforced by the compiler. `Frieren.Enemies` and `Frieren.Player` are siblings that
cannot see each other: both are only ways of driving the same `Frieren.Characters` components, which
is what stopped the enemy work in Milestone 5 from dragging player code in with it.

An assembly reference is not unused just because no type from it appears by name in the source - an
inheritance chain crossing an unreferenced assembly is a CS0012 waiting to happen. Run
`Tools/Validation/check_assemblies.py` before pushing; it catches that and reference cycles.

| Assembly | Folder | Holds |
|---|---|---|
| `Frieren.Data` | `Scripts/ScriptableObjects` | Base types for authored content. Depends on nothing, so everything can depend on it. |
| `Frieren.Save` | `Scripts/Save` | Save file format, storage backends, `SaveService`. Deliberately knows nothing about scenes, input or gameplay. |
| `Frieren.Characters` | `Scripts/Characters` | What any character needs, player or enemy: motor, action lock, animation abstraction, stats, health, mana, persistence. |
| `Frieren.Player` | `Scripts/Player` | Player-specific intent only: locomotion, dodge, interaction probe, camera rig, spawner, spell input. |
| `Frieren.Magic` | `Scripts/Magic` | Spell definitions, effects, and the component that casts them. |
| `Frieren.World` | `Scripts/World` | Objects magic acts on. Depends only on Core, never on Magic. |
| `Frieren.Enemies` | `Scripts/Enemies` | Perception, behaviour, melee, spawning. Cannot see `Frieren.Player`. |
| `Frieren.Presentation` | `Scripts/Presentation` | Reacts to gameplay and is read by none of it. Flashes, telegraphs, beams, numbers. |
| `Frieren.Core` | `Scripts/Core` | Bootstrap, service registry, scene loading, game state, input, debug tooling. |
| `Frieren.Core.Editor` | `Scripts/Core/Editor` | Editor-only: setup validation, asset creation, scene regeneration, menus. |
| `Frieren.Tests.EditMode` | `Scripts/Tests/EditMode` | Decisions that resolve in one call. Instant, no scene. |
| `Frieren.Tests.PlayMode` | `Scripts/Tests/PlayMode` | Anything needing a frame to pass: the enemy, channelling, regeneration, the real scene. |

Remaining gameplay assemblies (`Frieren.Combat`, `Frieren.Quests`, ...) get their
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

## Magic (Milestone 4)

```
SpellDefinition (asset)                world object (asset-free)
  cost, cast time, targeting                    |
  effects: [ ... ]  ------------.               |
                                 v              v
CharacterSpellcaster --> SpellEffect.Apply --> MagicPulse --> IMagicReceiver
  takes CharacterActionLock       |             (element +      FlammableObject
  spends CharacterMana            |              magnitude)     LevitatableObject
  resolves targeting -> SpellContext
```

**A spell is a list, not a class.** `SpellDefinition` holds cost, timing, targeting and an ordered
list of `SpellEffect` assets. "Fire that also lights torches" is authored by adding an effect, not by
writing a subclass. Effects are ScriptableObjects with no per-cast state, so one damage effect can be
shared by six spells and tuned once.

**World objects react to elements, never to spells.** This is the mechanism behind the project's
central promise. A crate burns because it received enough `Heat`, not because it was hit by "Fire" -
so a later explosion, a lava pool or a burning arrow lights that same crate with no change to the
crate. Ask "was this Fire?" anywhere in a receiver and the design is already broken.

`MagicElement`, `MagicPulse` and `IMagicReceiver` live in `Frieren.Core` for the same reason as
`IInteractable`: `Frieren.Magic` and `Frieren.World` both need the contract and neither may depend on
the other.

**Damage and world effects are separate.** `DealDamageEffect` acts on characters,
`MagicPulseEffect` acts on the world. Keeping them apart allows a spell that burns crates without
hurting anyone, or a bolt that hurts without setting anything alight. Fire simply carries both.

**Casting reuses the Milestone 2 lock and the Milestone 3 pool.** A cast holds
`CharacterActionLock` for its duration, so locomotion stands down and a dodge cannot interrupt it -
the same lock the dodge takes, which is why it was worth building with one claimant. Mana is spent
when the cast *begins*: spending on release would let a player cancel a frame early and never pay.

**Targeting resolves instantly.** A travelling bolt is presentation - a visual played along the
resolved line - so adding one later changes when effects fire, not how targeting works.

**A character is a magic receiver like anything else.** `CharacterLevitation` implements
`IMagicReceiver` and answers Force, exactly as a crate answers Heat. Levitation therefore does not
need to know whether it is aimed at a person or a block, and "lift yourself onto the ledge" and
"lift the block onto the ledge" are two solutions to one problem rather than two features. Today
only the self-cast version is bound to a spell; the object side works and waits for one.

**Channelled spells are a cast mode, not a spell.** `SpellCastMode.Channelled` holds the action lock,
drains mana per second and re-applies the effect list on a tick until released, out of mana, or past
a duration cap. Levitation uses it and needed no code of its own; a sustained beam or a held shield
would be the same mode with different effects. Targeting re-resolves every tick, which is what makes
holding a spell on an object feel like holding it rather than having thrown something at it.

**Input stays in the player layer.** `CharacterSpellcaster.TryCast` reads no input;
`PlayerSpellInput` calls it. An enemy in Milestone 5 casts the same spells through the same
component with a behaviour tree driving it.

## Combat and enemies (Milestone 5)

```
EnemyPerception --> EnemyBrain --> CharacterMotor      (movement, shared with the player)
  range, FOV, LOS     Idle/Chase/    EnemyMelee        (wind-up -> strike -> recovery)
  with hysteresis     Attack/         |
                      Stagger/Dead    v
                            ^    CharacterHealth.TakeDamage
                            |         |
                            |         v
                       Damaged   IDamageModifier (ordered)
                                      |
                                 CharacterBarrier (order 0)
```

**An enemy is a character with a decision-maker instead of a keyboard.** `Frieren.Enemies`
references Core, Characters and Data - not Player, not Magic. Everything about having a body comes
from Milestone 3: motor, health, stats, action lock. Only the deciding is new. That split is the
whole reason the character layer was built as components rather than as a `PlayerController`.

**`EnemyBrain` is not `GameStateMachine`.** One is menus and pausing, the other is one wolf's
opinion of the next two seconds. Collapsing them would put "loading" and "staggered" in one enum.

**Perception is three tests, cheapest first**: range, then field of view, then a raycast. Losing a
target is deliberately harder than gaining one - a larger radius and a delay - so stepping behind a
pillar does not reset the fight. A target set by being hit from behind survives long enough for the
enemy to turn around.

**No NavMesh, on purpose.** The brain hands a direction to `CharacterMotor` and the motor does the
rest. A bake needs level geometry that does not exist yet. An agent that writes to the same motor
replaces the steering later without touching a single decision.

**`IDamageModifier` is the seam everything defensive plugs into.** `CharacterHealth` runs an
incoming hit through its modifiers in `ModifierOrder`, each returning the remainder. `CharacterBarrier`
is the first, at order 0. Armour, resistances, damage-over-time reduction and a dodge's
invulnerability are later implementations of the same interface. Health never learns about any of
them; it applies whatever survives the pass. Putting the barrier inside health instead would have
made the second defensive mechanic a rewrite.

**Warding is an element like Heat.** `CharacterBarrier` implements `IMagicReceiver` and answers
`MagicElement.Warding`, exactly as a crate answers Heat. So a barrier spell, a warding rune, an
ally's shield and a defensive item are all the same thing arriving from different places. It
refreshes to full rather than accumulating - a barrier is held, not stockpiled, and stacking would
make hiding in a corner charging up the correct opening move.

**Zoltraak is an effect, not a class.** Piercing is a property of the damage, so it lives in
`PiercingDamageEffect` and any spell can have it by swapping which damage effect is in its list. The
beam is clipped by a thin ray against solid geometry before the thick cast runs, so a shot that
grazes the floor is not stopped by it.

**A pulse reaches every receiver on an object, not the first.** The player carries both levitation
and a barrier. Delivering to whichever component happened to be highest in the inspector would have
made behaviour depend on component order, which is not a thing anyone should have to know.

**`GameLayers` holds the layer numbers.** Masks are integers, so inserting a layer in Project
Settings silently repoints every mask built from a literal, with no error - enemies just stop seeing
you. The constants live in one file and an editor check confirms at load that they still name the
layers `ProjectSettings` says they do.

## Presentation (Milestone 5.5)

```
gameplay events                 Frieren.Presentation
  CharacterHealth.Damaged ----> CharacterCombatFeedback --> CharacterFlash
  CharacterBarrier.Broke  ---->            |                FloatingCombatText
  EnemyMelee.SwingStarted ----> EnemyCombatFeedback  ------>  (the same flash)
  Spellcaster.CastResolved ---> SpellTracer
  CharacterHealth.Died    ----> DeathSink
                                     |
                          ServiceLocator: IScreenShake, TimeScaleService
                                     |
                            CameraShake (on the camera rig)
```

**Nothing references this assembly.** It depends on Core, Characters, Magic and Enemies; no gameplay
assembly depends on it. So the compiler forbids a combat class from ever calling `PlayFlash()`, and
the entire layer is deletable when real VFX arrive. Same one-directional rule as
`ICharacterAnimation`, made structural instead of conventional.

**Presentation derives, it does not demand.** Where a fact is missing it is inferred rather than
added to gameplay's API: healing is read off `CharacterHealth.Changed` moving upwards, above a
threshold that suppresses regeneration dribble, because adding a `Healed` event would be a
presentation concern reshaping a gameplay contract. The one exception is
`CharacterSpellcaster.CastResolved`, added because the resolved geometry genuinely could not be
derived from outside - and even then it hands over information the caster already had.

**Nothing here is required.** Every output is fetched through `ServiceLocator.TryGet` or a null
check. No shake service, no time service, no flash shell - each is skipped. A presentation layer
that throws when the thing it wanted to decorate is absent has the dependency backwards.

**One writer for `Time.timeScale`, with three inputs.** `Scale = BaseScale * min(DipFactor,
HoldFactor)`. The base is game state - 0 paused, 1 playing - and multiplies, so a pause always wins
outright. The dip is hit-stop: an event with a duration that expires by itself. The hold is a
sustained slow released by whoever claimed it, which is what the spell wheel uses.

Each writing `Time.timeScale` directly would be a defect waiting to happen: a dip expiring after a
pause began would set the scale back to 1 and unpause the game, the same shape as the Milestone 1
pause bug. Dip and hold take the stronger of the two rather than multiplying, so a hit landing while
the wheel is open does not compound into a near-freeze.

The hold is a single-holder claim with a named owner, the same shape as `CharacterActionLock`. The
arithmetic is in `TimeScaleState`, plain C# with no Unity types, and tested - the alternative test
would have to write the editor's real clock and could leave it at zero.

**The flash cannot tint the character.** `PlaceholderCharacterAnimation` already owns that renderer
and rewrites it every frame from speed and grounding. So `CharacterFlash` builds a shell - a copy of
the mesh, scaled a few percent up, disabled until wanted - which nothing contends for and which
reads as an aura rather than as the character changing colour.

## Testing (Milestone 5.6)

Two assemblies, split by what a test needs rather than by what it covers.

**Edit mode is for decisions.** Anything that resolves inside a single call: `BurnState.AddHeat`,
`TimeScaleState.RequestDip`, `LockedObject.ReceiveMagic`, the resource pool, the cooldown tracker.
These run instantly, need no scene, and are the reason so much logic in this project is extracted
into plain C# in the first place - `JumpGate`, `MotorMath`, `OrbitCameraSolver`,
`InteractionSelector`, `SpellCooldownTracker`, `TimeScaleState`. Extracting the decision out of the
MonoBehaviour is what makes it cheap to check.

**Play mode is for everything that takes time.** A wind-up, a regeneration delay, a barrier lapse, a
character actually rising off the ground. No amount of plain-C# extraction covers "the enemy walked
over and hit me", so that lives here.

Three rules the play-mode fixtures follow, each of which exists because ignoring it produces a test
that lies:

- **Build from code, not from prefabs.** A test that instantiates `Player.prefab` breaks whenever
  the prefab changes and tests YAML rather than behaviour. `TestWorld` assembles what it needs.
- **Assemble inactive, activate last.** `CharacterStats.Awake` logs an error when it has no
  definition, and a logged error fails a Unity test; `FlammableObject` reads its thresholds once in
  `Awake`, so configuring it afterwards silently measures the defaults.
- **Wait on conditions, never on frame counts.** Frame timing varies with what else the editor is
  doing.

**One fixture breaks all three deliberately.** `BootSceneSmokePlayModeTests` loads the real `Boot`
scene, because it is the only automated check that the hand-authored YAML deserialises into the
fields it was meant for. `check_unity_yaml.py` proves a reference resolves; only Unity proves that
`castMode: 1` reached `castMode`.

## Persistence (Milestone 6)

```
world object                         Frieren.Core.Persistence
  FlammableObject   --.
  WaterBasin        --+--> IPersistentState --> PersistentObject --> ISaveable --> SaveService
  RepairableObject  --|      key, capture,        one entry per
  LockedObject      --|      restore              GameObject
  CharacterPersistence |            ^
  CharacterSpellcaster-'            |
                              SceneObjectId (the save key)
```

**Identity is the hard part, not serialisation.** Every convenient answer is wrong: a hierarchy path
breaks on a reparent, an instance id changes every run, a sibling index changes when someone inserts
a prop. Ids are therefore authored on `SceneObjectId`, and an editor check refuses blanks and
duplicates - the two ways an authored id fails, both silent. A blank id means the object never
registers and simply does not persist; a duplicate means two objects share an entry, so a door opens
because a crate burned.

**Gameplay does not know the save system exists.** World objects implement `IPersistentState`, a
Core interface with a key, a capture and a restore, exactly as they implement `IMagicReceiver`.
`PersistentObject` is the only class that speaks `ISaveable`. A burning crate should no more know
about save files than it knows what Fire is.

**One entry per GameObject, not per component.** A door that is locked, burnable and interactive is
one thing in the world; adding a fourth behaviour to it should not add a fourth key. An entry naming
a component that has since been deleted is skipped rather than treated as an error, so removing a
behaviour does not invalidate everyone's saves.

**Runtime-spawned objects get their ids from whatever spawned them.** `EnemySpawner` names each
spawn `<its own id>.<index>`, stable across sessions because it comes from the spawn point's
position in the list rather than from creation order. `SaveService` already restored late
registrations, which is what makes an object that appears after a load work at all.

**Cooldowns are stored as seconds remaining, never as an absolute expiry.** `Time.time` restarts
every session, so a saved expiry has either already passed or sits hours in the future. The same
trap waits for any future timer that gets persisted.

## Where the next milestones attach

| Milestone | Attaches via |
|---|---|
| 3 - Character architecture | Done. The rigged character still needs to replace `PlaceholderCharacterAnimation` with `MecanimCharacterAnimation`. |
| 4 - Magic framework | Done, with nine spells. New spells are assets; only a genuinely new *kind* of effect needs code. |
| 5 - Enemy | Done. Steering is direct; a navigation agent writing to the same motor replaces it when there is a level to bake. `PlayerDodge.IsInvulnerable` is still not read by anything - it wants to be an `IDamageModifier`. |
| 6 - Persistence | Done. Any new object persists by implementing `IPersistentState` and carrying a `SceneObjectId` plus a `PersistentObject`. Nothing else changes. |
| 7 - Vertical slice | Absorbs the briefed Milestone 6 breadth. `GameSceneDefinition` per area, added to `SceneCatalog` and Build Settings; `PlayerSpawner` handles arrival in each. Which scene the player was in is the one thing persistence does not yet record. |

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
