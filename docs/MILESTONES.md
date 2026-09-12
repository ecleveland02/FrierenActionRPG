# Milestones

Each milestone must produce something playable or testable, and be verified before the next starts.

| # | Goal | Status |
|---|---|---|
| 1 | Project foundation | **Complete**, opens and runs in the editor |
| 2 | Placeholder third-person player | **Complete**, playable in the editor |
| 3 | Modular character architecture | **Complete**, not yet run in the editor |
| 4 | Data-driven magic framework + 3 spells | **Complete**, confirmed in the editor |
| 5 | First enemy and basic combat, plus the remaining spells | **Complete**, not yet run in the editor |
| 6 | Reusable environmental interaction systems | Contract landed in M4; three more receivers landed in M5; breadth remaining |
| 7 | Gray-box vertical slice | Not started |

Outside the milestone sequence, a **Kael character spike** exists in its own prefab and scene, built
by Codex. It proves the Blender-to-Unity path for a skinned, clothed character early, which is real
de-risking, and it touches nothing the milestones own. It is unverified in the editor and is not a
replacement for the placeholder capsule - see `AGENTS.md` for what it would need first.

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

---

## Milestone 4 - Magic framework (complete, 3 spells)

**Delivered**

- `MagicElement`, `MagicPulse`, `IMagicReceiver` in Core: the contract between spells and the world,
  pulled forward from Milestone 6 so the environmental spells have something to act on.
- `SpellDefinition`: cost, cast time, cooldown, targeting, range, and an ordered list of effects.
- `SpellEffect` with two implementations: `DealDamageEffect` (characters) and `MagicPulseEffect`
  (the world). A spell carries whichever it needs.
- `CharacterSpellcaster`: takes the action lock, spends mana at cast start, resolves targeting,
  applies effects, reports refusals with a reason.
- `SpellCooldownTracker`, keyed by spell id so cooldowns can be saved later.
- `PlayerSpellInput`: cast input and number-key selection, with an on-screen list.
- `FlammableObject` (Heat lights it, Cold and Water put it out, it burns out and disables) and
  `LevitatableObject` (Force lifts it, it holds, then sinks).
- Three spells: Arcane Bolt, Fire, Levitation. Levitation lifts the caster; the machinery for
  lifting objects exists and is unused, waiting for a telekinesis spell.
- Scene props: two crates, a liftable block, and a target dummy built from the Milestone 3 components.
- 23 new EditMode tests (153 total).

**Known limitations**

1. Confirmed in the editor: casting, mana cost, channelling, and self-levitation. Fire and Arcane
   Bolt have not been separately reported on.
2. Five of the eight briefed spells are missing: Ice, Barrier, Water, Repair, Unlock. Ice and Water
   need only assets, since `FlammableObject` already answers Cold and Water. Barrier, Repair and
   Unlock need new effect types and new receivers.
3. No VFX at all. A cast is a log line, a colour change and a moving block.
4. Targeting is instant. No travelling projectile, no arc, no area preview.
9. Channelling is not covered by edit-mode tests. Like regeneration, it needs a running clock, so it
   belongs in a play-mode test once one is worth setting up.
10. A channelled cast holds the action lock, so the character cannot move while holding a spell.
   That reads as concentration and suits the setting, but if it turns out to feel bad the fix is a
   flag on the spell rather than a change to the framework.
5. Casting cannot be interrupted by damage, because nothing yet interrupts anything.
6. Cooldowns are not saved. `SpellCooldownTracker` is keyed by id so they can be, but
   `CharacterPersistence` does not write them.
7. Known spells are a list on the prefab. Spell discovery is a later milestone.
8. `FlammableObject` and `LevitatableObject` do not persist. A burnt crate is unburnt after a load.
11. `LevitatableObject`, the pale blue block in the test scene, and `Effect_Pulse_Force` are
   currently unreachable. They work, and any future spell emitting Force at a point will drive
   them, but no spell does today. They are kept rather than deleted because that spell - a
   telekinesis or a shockwave - is clearly coming.

**How to test it in the editor**

Open `Boot`, press Play. A spell list appears under the vitals panel.

1. `1`, `2`, `3` select Arcane Bolt, Fire, Levitation. Cast with left mouse.
2. Aim at the tall dummy and cast Bolt. Its health drops; mana drops; the spell greys out briefly.
3. Aim at a crate and cast Fire. It warms toward orange, catches, burns, then blackens and vanishes.
   One Fire cast reaches both crates if you stand so they are within 2.5m of the impact.
4. Select Levitation and **hold** the cast button. You rise for as long as you hold, up to eight
   metres, and settle back down when you let go. You keep control of your movement while aloft.
5. Cast until mana runs out: the console explains the refusal rather than nothing happening.

Item 3 is the one that matters. The crate has no idea Fire exists - it reacts to Heat - so any later
spell carrying Heat will light it with no change to the crate.

---

## Milestone 5 - First enemy, and the rest of the spells (complete, unverified in the editor)

**Build stamp: `m5 enemies and the full spell set`.** The debug overlay prints this. If it says
anything else, the build being looked at is not this one and nothing observed in it is evidence.

### Architecture, before the detail

**The enemy gets its own assembly and its own state machine.** `Frieren.Enemies` sits beside
`Frieren.Player`, referencing Core, Characters and Data but not Player and not Magic. An enemy is a
character driven by a decision-maker instead of a keyboard, which is precisely the split that
Milestone 3 was built for - so the enemy reuses `CharacterMotor`, `CharacterHealth`,
`CharacterStats` and `CharacterActionLock` unchanged, and adds only the deciding.

`EnemyBrain` is a five-state machine (Idle, Chase, Attack, Stagger, Dead), deliberately not the
application-level `GameStateMachine`. Sharing them would put "the game is loading" and "this
sentinel is reeling" in one enum.

**No NavMesh.** Steering is direct: the brain points a direction at `CharacterMotor` and the motor
does the rest. A NavMesh needs baking, baking needs real level geometry, and a gray-box arena has
none. When there is a level, an agent that writes to the same motor drops in with no change to the
brain's decisions.

**Barrier needed a damage pipeline, which combat wanted anyway.** `IDamageModifier` is a seam on
`CharacterHealth`: implementers get a chance to reduce an incoming hit before it lands, in a defined
order. `CharacterBarrier` is the first, at order 0. Armour, resistances and damage-over-time
reduction are later implementations of the same interface, and health never learns about any of
them - it applies whatever reaches it.

**Zoltraak is an effect, not a class.** Its defining property is that it does not stop at the first
body, so it is a `PiercingDamageEffect` in a spell's effect list. Any spell can be made piercing by
swapping which damage effect it carries.

**Delivered**

- `Frieren.Enemies`: `EnemyPerception` (range, then field of view, then line of sight - cheapest
  first, with hysteresis so a pillar does not reset the fight), `EnemyMelee` (wind-up, strike,
  recovery, cooldown - the wind-up is the dodge window), `EnemyBrain`, `EnemySpawner`.
- `IDamageModifier` and the ordered modifier pass in `CharacterHealth`, with a `DamageReduced` event.
- `CharacterBarrier`: raised by a Warding pulse, refreshed rather than stacked, lapses shortly after
  the pulses stop.
- `MagicElement.Warding`.
- Three new world receivers: `WaterBasin` (Water fills it, Cold freezes it into a standable surface,
  Heat thaws then boils it), `RepairableObject` (Restoration mends it, progress decays), and
  `LockedObject` (Unbinding picks it, enough Force breaks it, a key opens it - all through one path).
- `PiercingDamageEffect`, which clips its beam at solid geometry with a thin ray so a grazing shot
  does not stop on the floor.
- Six new spells: **Zoltraak** (piercing, 45 damage, 40m), **Barrier** (channelled self-ward), Ice,
  Water Creation, Mending (channelled) and Unbinding. With the existing three that is nine, in the
  order 1-9 on the number row.
- `GameLayers`: the project's layer indices and masks in one place, with an editor check that they
  still name the layers `ProjectSettings` says they do.
- `Tools/Validation/check_unity_yaml.py`: anchor uniqueness, unresolved references, GameObject and
  component agreement, transform parent/child bidirectionality, missing and orphan `.meta` files.
- `Enemy_Sentinel.prefab` and its `Stats_Sentinel` archetype (80 health - two Zoltraaks).
- Test scene: an enemy spawn, a trough between two ledges, a broken pillar, and a locked door.
- 26 new EditMode tests (179 total).

**A fix worth naming.** `MagicPulseEffect` delivered a pulse to the *first* `IMagicReceiver` it
found on an object. The player now carries two - levitation and the barrier - which would have made
behaviour depend on inspector component order. It now offers the pulse to every receiver it finds.

**Known limitations**

1. **Nothing here has been run in the editor.** It compiles by static analysis and the YAML
   validates, which is not the same thing. Milestone 4 shipped three defects that only running it
   found.
2. Combat tuning is guesswork: 80 enemy health, 12 melee damage, a 0.55s wind-up, 45 for Zoltraak.
   These are first numbers, not measured ones.
3. The player can die and nothing happens - no death screen, no respawn. F3 revives.
4. Enemy attacks cannot be interrupted by the player except through the stagger, and the stagger has
   a 0.9s cooldown so it cannot be chained into a stun-lock. Whether that is the right number is
   unknown until it is played.
5. Steering is direct, so an enemy will walk into a wall if the player stands behind one.
6. No VFX, no audio, no hit reaction beyond the placeholder tint. A Zoltraak looks like nothing.
7. `EnemyBrain`, `EnemyMelee` and `EnemyPerception` have no automated tests: all three need a
   running clock and a physics scene, which is a play-mode test setup that does not exist yet. The
   barrier, the basin and the lock are covered, because their decisions are frame-free.
8. World receivers still do not persist. A mended pillar is broken again after a load.
9. Barrier does not stop the melee swing's *knock*, because there is no knockback yet.
10. `LevitatableObject` and `Effect_Pulse_Force` remain unreachable, still waiting for a telekinesis
   spell.

**How to test it in the editor**

Open `Boot`, press Play. Check the overlay reads `m5 enemies and the full spell set` before trusting
anything below.

1. **The enemy.** Walk north (`+Z`). At about 14m the Stone Sentinel notices you, closes, and swings.
   The console narrates each state change. Take a hit or two and watch the health bar; press `F3` to
   heal.
2. **Zoltraak.** Press `2`, aim at the sentinel, cast. 45 damage, so two kill it. Line up the
   sentinel and the target dummy and one cast hits both, the second for 80% - that is the piercing.
3. **Barrier.** Press `3` and **hold**. The vitals panel shows the barrier and its remaining
   strength. Let the sentinel hit you while holding: the barrier absorbs it and the panel drops
   rather than your health. Release and it lapses in a third of a second.
4. **Water and Ice, the pillar of the whole design.** Go west to the two ledges with a trough
   between them. Press `7` and cast Water twice into the trough - it fills. Press `5` and cast Ice -
   it freezes, and you can walk across. Now do it the other way: select Levitation (`6`), hold, and
   float over. Two answers, neither scripted, neither knowing about the other.
5. **The locked door.** Further west. Walk up to it and press Interact: it says Locked. Press `9`
   and cast Unbinding: it opens. Or reload, press `4`, and burn it down with Fire instead - the door
   panel is flammable. Two answers again, and the second one is free: `FlammableObject` was written
   in Milestone 4 and knows nothing about doors.
6. **Mending.** The broken stump south-east of spawn. Press `8` and **hold** - it takes about two
   seconds of sustained casting. Let go early and the progress decays away.

Items 4 and 5 are the ones that matter. Nothing in the trough knows what Ice is; nothing in the door
knows what Fire is. Each reacts to an element, which is why new spells keep working on old objects.

---

**Next: Milestone 6 - environmental interaction, in breadth**

The contract landed in Milestone 4 and now has five receivers. Milestone 6 is no longer about
building the system; it is about having enough object kinds and enough combinations that a room can
be designed around them, plus the thing all five currently lack: persistence. A burnt crate, a
mended pillar and an opened door should still be burnt, mended and open after a load.
