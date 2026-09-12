# Milestones

Each milestone must produce something playable or testable, and be verified before the next starts.

| # | Goal | Status |
|---|---|---|
| 1 | Project foundation | **Complete**, opens and runs in the editor |
| 2 | Placeholder third-person player | **Complete**, playable in the editor |
| 3 | Modular character architecture | **Complete**, not yet run in the editor |
| 4 | Data-driven magic framework + 8 prototype spells | Not started |
| 5 | First enemy and basic combat | Not started |
| 6 | Reusable environmental interaction systems | Not started |
| 7 | Gray-box vertical slice | Not started |

> **Note on status.** Both milestones are now confirmed in the editor on Unity 6000.0 (Windows).
> The project compiles with zero errors, boots, loads the test scene, spawns the player, and the
> character moves under player control with a following camera. Pause and resume work, and the save
> probe round-trips.
>
> Three real defects were found by running it, all of which static analysis had missed:
> `SceneBootstrapGuard` suppressing the first-scene load from an unsaved scene, pause being
> unreleasable because the state change ran inside an Input System callback, and the mouse look
> delta being summed when it should be assigned.
>
> Jump, dodge and interaction have not been separately reported on.

---

## Milestone 1 - Foundation (complete)

**Delivered**

- Folder structure and Git hygiene: `.gitignore`, `.gitattributes` with LFS rules and Unity
  SmartMerge, `ProjectSettings` committed with the Input System active and Linear colour space.
- Five assembly definitions with an enforced dependency direction.
- `ServiceLocator` - explicit registration from a single composition root.
- `Bootstrapper` - the composition root, plus `SceneBootstrapGuard` so any scene can be entered
  directly in the editor.
- `GameStateMachine` over Booting / MainMenu / Loading / Playing / Paused.
- `SceneLoader` - additive loading, transitions, progress events - driven by `GameSceneDefinition`
  assets and a `SceneCatalog`.
- Save system: `ISaveable`, `SaveEntry`, `SaveGameData`, `SaveMigration`, `SaveService`, and
  `ISaveStorage` with file and in-memory backends. Atomic writes, versioned files, order-independent
  restore.
- `InputReader` ScriptableObject over `FrierenControls.inputactions`; Gameplay and UI maps bound for
  keyboard/mouse and gamepad.
- `GameLog` with channel filtering and release-build stripping; `DebugOverlay`; `SaveProbe`.
- Editor tooling: setup validation, non-destructive asset creation, code-driven scene regeneration,
  build-settings configuration, save-folder menus.
- `Boot.unity` and `TestScene.unity`, both committed and regenerable.
- 36 EditMode tests.

**Known limitations**

1. Not compiled or run - see the note above.
2. No player, camera, combat, magic or enemies. Pressing Play gives a static gray-box scene.
3. Rendering uses the Built-in pipeline. URP is installed but unconfigured (decision 6).
4. `SceneLoader` reports `AsyncOperation.progress` directly. It does not hold activation, so a
   loading screen cannot currently wait at 90% for a keypress. Add when a loading screen exists.
5. `SaveGameData.PlayTimeSeconds` and `SceneId` are written but nothing populates them yet; that
   belongs with the systems that own the data.
6. `SceneBootstrapGuard.BootSceneName` is a string constant. Renaming `Boot.unity` requires editing it.
7. No PlayMode tests. The systems worth testing at this stage are plain C# and covered by EditMode
   tests; PlayMode tests become worthwhile once there is a player to drive.
8. Package versions in `manifest.json` are pinned against Unity 6000.0 and may need resolving on a
   different editor version.

---

## Milestone 2 - Placeholder third-person player (complete)

**Delivered**

- `Frieren.Characters` (references nothing) and `Frieren.Player` assemblies.
- `CharacterMotor`: gravity with terminal velocity, sphere-cast grounding, jump by target height,
  variable jump height, safe teleport. The only thing that moves a character.
- `CharacterActionLock`: single-holder claim on the body, first used by the dodge.
- `ICharacterAnimation` with `PlaceholderCharacterAnimation` (squash and tint a primitive) and
  `MecanimCharacterAnimation` (drives an `Animator`, tolerates missing parameters).
- `PlayerLocomotion`: camera-relative movement, walk/sprint, acceleration and deceleration rates,
  reduced air control, turn-toward-movement, coyote time and jump buffering.
- `PlayerDodge`: directional dash holding the action lock, with cooldown and an exposed
  invulnerability window.
- `PlayerInteractor`: throttled non-allocating overlap probe, best-candidate selection, an event for
  the prompt UI that does not exist yet.
- `OrbitCameraRig` and `OrbitCameraSolver`: orbit, follow smoothing, pitch clamping, and collision
  that ignores the target's own colliders.
- `IInteractable` in `Frieren.Core`, plus `DebugInteractable` to test against.
- `PlayerSpawner`: instantiates the prefab and wires the camera both ways.
- `Player.prefab` (capsule, facing marker, no visual colliders) and an expanded `TestScene` with a
  platform, a step, a wall for camera collision, and two interactables.
- Editor regeneration extended to rebuild the prefab and the new scene contents.
- 61 new EditMode tests (97 total).

**Known limitations**

1. Not compiled or run. See the note above.
2. No animations - only the architecture for them. The placeholder squashes and tints a capsule.
3. Layer masks all default to everything. `PlayerInteractor` filters by component and the camera
   filters by object, so this is correct but does more physics work than needed. Narrow the masks
   once the project can be profiled.
4. `PlayerDodge.IsInvulnerable` is exposed but nothing reads it; there is no damage until
   Milestone 5.
5. No slope-slide behaviour. Walking onto a slope steeper than the controller's limit stops the
   character rather than sliding them off.
6. `MecanimCharacterAnimation` is untested against a real controller, because there is no rig yet.
   Expect to adjust parameter names in Milestone 3.
7. The camera has no options for sensitivity or invert beyond inspector fields, and no shoulder
   offset or aim mode. Those belong with the UI and combat milestones.
8. Jump is not variable-height in practice: `CharacterMotor.CancelAscent` exists but nothing calls
   it, because the Jump action is bound as a plain button with no release event wired.

**How to test it in the editor**

Open `Boot.unity` and press Play. `WASD` or left stick to move, mouse or right stick to look,
`Shift` or L3 to sprint, `Space` or A to jump, `Ctrl` or B to dodge, `E` or X to interact.

Specific things worth checking, in order:

1. The capsule spawns and the camera follows it. If not, the problem is the spawner wiring.
2. Movement is camera-relative: pushing forward moves away from the camera whichever way it faces.
3. Walking off the platform and pressing jump a frame late still jumps (coyote time).
4. Pressing jump just before landing jumps on landing (input buffering).
5. Dodging mid-run dashes, and steering is ignored until it ends.
6. The capsule tints toward blue with speed, amber in the air, and flashes on dodge.
7. Backing the camera into the wall pulls it in rather than clipping through.
8. Standing near a grey cube and pressing interact turns it green and logs.

---

---

## Milestone 3 - Modular character architecture (complete)

**Delivered**

- `CharacterStatsDefinition`, a ScriptableObject per archetype holding maximum health and mana and
  their regeneration rates and delays. `Stats_Player.asset` is the first one.
- `CharacterStats`, the component everything reads stats through. A passthrough today; the seam
  equipment, buffs and progression attach to later.
- `ResourcePool`, a plain class holding the bounded-value arithmetic: clamping, proportional
  rescaling when a maximum moves, all-or-nothing withdrawal.
- `CharacterResource`, the shared base for regenerating pools, with `CharacterHealth` and
  `CharacterMana` on top. Health adds damage, death and revival; mana adds all-or-nothing spending.
- `DamageInfo` and `DamageType`, so the damage signature does not have to change when resistances
  and critical hits arrive.
- `CharacterPersistence`, one `ISaveable` per character covering all its vitals.
- `CharacterVitalsReadout`, an on-screen bar and test keys, since nothing can hurt you yet.
- 33 new EditMode tests (130 total).

**Known limitations**

1. Not yet run in the editor.
2. Regeneration is untested automatically. It needs a running clock, and edit-mode tests have none;
   it belongs in a play-mode test once something exists worth driving one for.
3. `CharacterPersistence.SaveId` is authored per instance. Fine for the player and hand-placed
   characters, wrong for the enemies spawned at runtime in Milestone 5, which will need ids derived
   from their spawner.
4. `DamageType` is carried but never read. There are no resistances until there is something to
   resist.
5. `CharacterStats` applies no modifiers. There is nothing to modify with yet.
6. Death raises an event that nothing listens to. The player does not ragdoll, respawn or stop
   taking input.
7. `Frieren.Characters` references the Input System package solely for the debug readout. Both go
   when a real HUD exists.

**How to test it in the editor**

Open `Boot`, press Play. A second panel appears under the debug overlay showing health and mana.

1. `F2` damages 15. Health falls; mana regenerates on its own, health does not.
2. `F4` spends 15 mana, and mana regeneration pauses for 1.5 seconds before resuming.
3. `F2` repeatedly to zero: the readout reads DEAD, further `F2` does nothing, `F3` revives.
4. The save round trip: `F2` twice, `F5` to save, `F2` twice more, `F9` to load. Health should snap
   back to the saved value.

That last one is the milestone's real test: it proves stats, vitals and persistence work together.

---

**Next: Milestone 4 - the magic framework**

Scope: a data-driven spell system, and prototypes for the eight starting spells.

**A scope note worth raising before starting.** The plan has Milestone 4 build eight spells and
Milestone 6 build the environmental interaction system those spells act on. For at least Fire, Ice,
Levitation, Repair and Unlock that is backwards: those spells are *defined* by what they do to world
objects, so building them first means five spells with nothing to affect, then rewriting them.

The suggestion is to bring the Milestone 6 interfaces forward into Milestone 4 - the contract for
"this object responds to a spell effect of type X" - and build one example object per effect
alongside its spell. Milestone 6 then becomes breadth (more object kinds, more combinations) rather
than the first attempt.

One thing to settle at the start:

- **How a spell composes its behaviour.** A `SpellDefinition` holding a list of effect
  ScriptableObjects, or one class per spell. Recommendation: the effect list, because it is the only
  version where "fire that also lights torches" is authored rather than coded, and because the brief
  explicitly asks for multiple solutions to environmental problems.
