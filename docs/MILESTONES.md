# Milestones

Each milestone must produce something playable or testable, and be verified before the next starts.

| # | Goal | Status |
|---|---|---|
| 1 | Project foundation | **Complete** (unverified in-editor - see note) |
| 2 | Placeholder third-person player | **Complete** (unverified in-editor - see note) |
| 3 | Modular character architecture | Not started |
| 4 | Data-driven magic framework + 8 prototype spells | Not started |
| 5 | First enemy and basic combat | Not started |
| 6 | Reusable environmental interaction systems | Not started |
| 7 | Gray-box vertical slice | Not started |

> **Note on status.** Milestones 1 and 2 were both authored in an environment without Unity
> installed, so nothing here has been compiled or run. Milestone 2 was written before Milestone 1
> had been opened in the editor, at the user's request, so two unverified milestones are stacked.
> The first editor session should expect to fix things, and should verify Milestone 1 first: if the
> foundation is wrong, Milestone 2's symptoms will be misleading.
>
> The original wording follows.
>
> The code was authored in an environment without Unity installed, so nothing here has been
> compiled or run. Static checks passed (brace balance, assembly reference
> consistency, every project type resolvable from its file's usings, every scene and asset GUID
> cross-reference resolving, all Unity YAML parsing). Treat the milestone as unverified until the
> project opens cleanly, the EditMode tests pass, and the save round-trip in the README works.

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

**Next: Milestone 3 - modular character architecture**

Scope: `CharacterStats`, `CharacterHealth`, `CharacterMana` and the rest as separate components in
`Frieren.Characters`, built so an enemy and the player share them.

The strong recommendation is to **open the project and verify Milestones 1 and 2 before starting**.
Two unverified milestones are already stacked; a third would mean debugging three layers at once.

One thing to settle at the start:

- **Where stats come from.** A `CharacterStatsDefinition` ScriptableObject per character archetype,
  or values authored per prefab. Recommendation: the ScriptableObject, because Milestone 5 wants
  several enemies sharing a template, and because progression in a later milestone needs a base to
  apply modifiers to. Per-prefab values give you nothing to reference when a spell says "+20% max
  mana".

Milestone 3 is complete when the player's health and mana are components rather than fields, a
placeholder enemy can be given the same components, and at least one of them persists through a
save and load.
