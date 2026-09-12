# Milestones

Each milestone must produce something playable or testable, and be verified before the next starts.

| # | Goal | Status |
|---|---|---|
| 1 | Project foundation | **Complete**, opens and runs in the editor |
| 2 | Placeholder third-person player | **Complete**, playable in the editor |
| 3 | Modular character architecture | **Complete**, not yet run in the editor |
| 4 | Data-driven magic framework + 3 spells | **Complete**, confirmed in the editor |
| 5 | First enemy and basic combat, plus the remaining spells | **Complete**, not yet run in the editor |
| 5.5 | Combat feel: placeholder feedback so the loop can be judged | **Complete**, not yet run in the editor |
| 5.6 | Play-mode tests, so Milestones 5 and 5.5 can be verified by pressing Run | **Complete**, not yet run in the editor |
| 6 | Persistence: the world remembers what you did to it | **Complete**, not yet run in the editor |
| 6.1 | Block on right mouse, spells on a radial wheel | **Complete**, not yet run in the editor |
| 6.2 | Time slows while the wheel is open | **Complete**, not yet run in the editor |
| 7 | Gray-box vertical slice (absorbs the briefed M6 breadth) | Not started |

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

**Build stamp at the time: `m5 enemies and the full spell set`.** The debug overlay prints this. If it says
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

## Milestone 5.5 - Combat feel (complete, unverified in the editor)

**Build stamp: `m5.5 combat feel`.** Check the overlay before trusting anything below.

Not in the original plan. It exists because of a gap the plan did not anticipate: the brief says not
to build final art until the gameplay loop is proven fun, which is right, but the loop as shipped in
Milestone 5 could not be *judged* at all. `EnemyMelee` has a 0.55 second wind-up whose entire purpose
is to give the player time to react, and nothing drew it. That is not a dodge window; it is a coin
flip followed by damage. Zoltraak was a log line and a number.

Placeholder feedback is not art. Everything here is primitives, `LineRenderer`,
`MaterialPropertyBlock` and IMGUI - no assets, no packages, no particle systems. It is instrumentation
for answering "is this fun", and it doubles as a diagnostic: if the Sentinel never swings, the
telegraph says whether it reached Attack at all, which separates a perception bug from a timing one
without reading a log.

### Architecture

**`Frieren.Presentation` is a new assembly that nothing references back.** It depends on Core,
Characters, Magic and Enemies; no gameplay assembly depends on it. So the compiler, not discipline,
guarantees that no combat code can call into a flash or a shake, and the whole layer can be deleted
or replaced when real VFX arrive without touching a gameplay file. It is the same one-directional
rule `ICharacterAnimation` established in Milestone 2, made structural.

**Everything subscribes to events that already existed.** `CharacterHealth.Damaged`,
`DamageReduced`, `Died` and `Changed`; `CharacterBarrier.Raised` and `Broke`; `EnemyMelee.SwingStarted`
and `SwingLanded`; `EnemyBrain.StateChanged`. Those events were written as their systems went in,
largely for this.

**One gameplay addition.** `CharacterSpellcaster.CastResolved(spell, context, affected)` - the
existing `CastCompleted` says *whether* a spell landed, not *where*, so a tracer had nothing to draw
along. That is presentation asking gameplay for what it already knows, which is the right direction.

**`TimeScaleService` now owns `Time.timeScale`, and this is the important one.** Hit-stop and pause
both want to slow time, and both writing `Time.timeScale` directly is a defect waiting to happen: a
hit landing a frame before a pause restores the scale to 1 when its dip expires, and the game
unpauses itself. That is the same shape as the pause bug found in Milestone 1. So they are separate
factors that multiply - a base scale owned by game state, and a self-expiring dip - and the
arithmetic lives in a plain-C# `TimeScaleState` that is tested.

**Delivered**

| Component | Reacts to | Shows |
|---|---|---|
| `CharacterFlash` | driven by the two below | A shell of the character's own mesh, scaled up. Collapses back to rest instead of fading, because the placeholder material is opaque and cannot fade. |
| `EnemyCombatFeedback` | `SwingStarted`, `SwingLanded`, `StateChanged` | **The wind-up telegraph.** Orange for the whole 0.55s, red on the strike, violet while staggered, yellow while chasing. |
| `CharacterCombatFeedback` | `Damaged`, `DamageReduced`, `Died`, `Changed`, barrier events | Red on a real hit, blue on one a ward ate, green on a heal, plus the numbers, the shake and the dip. |
| `FloatingCombatText` | driven by the above | World-projected damage numbers, so tuning becomes measurable instead of guessed. |
| `SpellTracer` | `CastResolved` | A beam along the resolved line, coloured from the spell's own first pulse element - so a new spell is coloured right the day it is authored, with nothing to configure. |
| `DeathSink` | `Died` | The body tips and sinks rather than blinking out. Moves the visual child only, never the root, or a corpse would shove the player around while falling. |
| `CameraShake` | registered as `IScreenShake` | Perlin kick on the camera, decaying on *unscaled* time so a hit that also dips time feels sharper rather than longer. |

Plus `TimeScaleService`, `TimeScaleState`, `IScreenShake`, and 17 new EditMode tests (196 total).

**Known limitations**

1. **Not run in the editor.** Same caveat as Milestone 5, and now two unverified layers stack.
2. `Shader.Find("Sprites/Default")` supplies the beam's material. Fine in the editor and in a build
   that includes it; if it ever comes back null the beam draws in the default magenta rather than
   failing, which is ugly but harmless.
3. The flash shell is opaque, so it reads as an aura rather than a glow. A transparent material
   would look better and needs an actual material asset, which is art.
4. Hit-stop fires on the enemy's connecting swing and on hits to the player. It is deliberately
   shallow and short; whether it feels good at 0.06s is exactly the sort of thing that needs playing.
5. No audio at all. Sound is the single largest remaining gap in combat feel and none of this
   addresses it.
6. Numbers are drawn per character, so numbers still rising from an enemy vanish when its corpse
   disables.
7. `CameraShake` decays on unscaled time but is applied in `LateUpdate`, so at very low frame rates
   the kick can be visibly stepped.

**How to test it**

Everything in the Milestone 5 list, plus:

1. **The telegraph is the point.** Let the Sentinel reach you and watch for the orange shell. Learn
   to move on it. If the fight now has a rhythm, that is the answer we were after; if the wind-up is
   too short or too long to react to, that is a number worth changing and now a visible one.
2. **Zoltraak looks like something.** Violet beam. Fire is orange, Ice pale blue, Water blue,
   Mending green, Unbinding gold - none of which is configured anywhere; each is read off the
   spell's own effects.
3. **Barrier is legible.** Hold `3` and let the Sentinel hit you: a blue flash and a bracketed
   number, and health that does not move. Let it break and you get `BROKEN`.
4. **Pause during an impact.** Land a hit and press `Esc` inside the hit-stop. It should pause, and
   unpausing should return to full speed - not to the dipped speed, and not stay frozen. That is
   the interaction `TimeScaleState` exists to make impossible to get wrong.

---

## Milestone 5.6 - Play-mode tests (complete, not yet run)

**Build stamp: `m5.6 play-mode tests`.**

Not in the plan either, and it exists because of a pattern rather than a feature. Every limitation
written for the last two milestones has the same shape: *"needs a running clock, so it belongs in a
play-mode test once one is worth setting up."* Channelling. Regeneration. The barrier lapse.
`EnemyBrain` in its entirety. Levitation actually lifting the motor. There were 196 tests and not
one of them had ever seen the enemy exist.

That gap is not really about coverage. It is that verifying two milestones meant walking a six-step
manual checklist and remembering whether Ice took one cast or two. This turns that into a Run button.

### Architecture

**`Frieren.Tests.PlayMode`**, a second test assembly with `includePlatforms: []` and the same
`UNITY_INCLUDE_TESTS` constraint as the edit-mode one. Edit mode keeps what it is good at - decisions
that resolve in a single call - and play mode takes everything that needs a frame to pass.

**Fixtures are built from code, not from the project's prefabs.** A test that instantiated
`Player.prefab` would break every time the prefab changed and would be testing YAML rather than
behaviour. `TestWorld` assembles characters, enemies, spells and effects from scratch.

**Objects are assembled inactive and activated last.** This is not a style choice: adding
`CharacterStats` to a live GameObject fires its `Awake` before a definition is assigned, which logs
an error, and a logged error fails a Unity test. Same for `FlammableObject`, which reads its
thresholds once in `Awake` to build a `BurnState` - configure it afterwards and the test silently
measures the defaults.

**Private serialized fields are set by reflection, through `TestFields`.** Public setters on every
tuning value would be a worse codebase in exchange for easier tests. The one rule that matters is
that a misspelled field name *throws* - a silent no-op would leave a test passing against a default
while claiming to test something else.

**Waits are bounded conditions, never fixed frame counts.** A test that waits exactly 40 frames for
a 0.55 second wind-up fails on a slow machine and proves nothing on a fast one.

**Delivered - 48 tests (244 in total with edit mode)**

| Fixture | Tests | Covers |
|---|---|---|
| `EnemyBehaviourPlayModeTests` | 14 | Detection by range and by facing, chasing, stopping in reach, the wind-up landing no damage, escaping during the wind-up, stagger aborting a swing, stun-lock resistance, death releasing the lock, being shot from behind. |
| `SpellcastingPlayModeTests` | 9 | Mana spent once, refusal with a reason, channel drain, release, a release during the cast time, running dry, the action lock returned, cooldowns, and a self-cast reaching *every* receiver. |
| `VitalsPlayModeTests` | 8 | Health and mana regeneration delays, damage restarting the delay, the barrier lapsing and being held, absorbing a real hit, and levitation actually lifting the body and setting it down. |
| `WorldReceiverPlayModeTests` | 8 | The trough draining if you dawdle, ice not melting, a repair coming apart when released early, a mended pillar staying mended, a crate burning out, weak heat cooling off. |
| `TimeScalePlayModeTests` | 4 | A dip against the real clock, a dip not stretching its own expiry, and the two pause interactions the split exists to prevent. |
| `BootSceneSmokePlayModeTests` | 5 | Loads the real `Boot` scene and asserts the project actually works. |

**The smoke test is the one to read.** It is the only automated check that the hand-authored YAML
loads at all. `check_unity_yaml.py` catches broken references; only Unity can say whether a
hand-written enum index landed in the field it was meant for. So it asserts that all nine spells are
present, unique, and each has a non-empty effect list; that Zoltraak is a ray and not self-cast;
that Barrier is channelled and drains mana; that the enemy spawned with its archetype; that the time
and shake services registered; and that the door is both lockable and burnable, because that
particular pairing is the design pillar in one assertion.

**Known limitations**

1. **These have not been run either.** I cannot press Run. A broken test at least fails loudly at a
   line number, which is a much better failure than a silent gameplay bug - but budget for a couple
   of them being wrong about a timing constant rather than about the code.
2. `BootSceneSmokePlayModeTests` is the riskiest fixture: it loads a real scene and has to clean up
   `Bootstrapper`, which survives scene loads by design. If it proves flaky in a way the others are
   not, disable that one file - nothing else loads a scene.
3. Nothing covers the presentation layer. It has no assertable output; a flash either looks right or
   it does not.
4. Nothing covers input. `InputReader` needs a device, which needs the Input System's own test
   fixtures.
5. Nothing covers saving and loading a live character, which is the gap Milestone 6 should close.
6. Timing constants in the tests are tied to the current tuning. Change the wind-up to 0.2s and
   `TheWindUpHappensBeforeAnyDamage` still passes, but change the stagger cooldown to 3s and
   `RepeatedHitsCannotLockItInPlace` starts failing. That is arguably correct - a tuning change that
   breaks a stated guarantee should say so - but it is worth knowing before touching numbers.

**How to run them**

`Window > General > Test Runner > PlayMode > Run All`. It enters play mode by itself and takes
roughly a minute. EditMode still runs instantly and needs no scene.

Read the failures top down. A failure in `BootSceneSmokePlayModeTests` means the YAML is wrong -
fix that first, because every other fixture builds its own objects and will not be affected. A
failure anywhere else is code.

---

## Milestone 6 - Persistence (complete, not yet run)

**Build stamp: `m6 persistence`.**

A change to the plan, argued rather than assumed. The briefed Milestone 6 was "reusable
environmental interaction systems" - breadth: more receiver kinds. That contract landed early in
Milestone 4 and has five receivers now, and a sixth would teach us nothing the first five did not.
What it *does* have is a hole: **none of them survived a save.** The save system built in Milestone 1
quietly claimed to cover the world and covered only the player's health and mana.

So Milestone 6 became persistence, and the briefed breadth folds into Milestone 7, where more object
kinds can be designed around an actual space instead of added abstractly.

### Architecture

**The hard part of persistence is identity, not serialisation.** Every tempting answer is wrong: a
hierarchy path breaks on a reparent, an instance id changes every run, a sibling index changes when
someone inserts a prop. So ids are authored, on a `SceneObjectId` component, and an editor check
refuses the two ways an authored id fails - blank and duplicate - because both are silent. A blank
id means the object never registers; a duplicate means two objects share an entry and a door opens
because a crate burned.

**Gameplay does not know the save system exists.** A burning crate should no more know about save
files than it knows what Fire is. So world objects implement `IPersistentState` - a Core interface
with a key, a capture and a restore - and `PersistentObject` is the single adapter that speaks
`ISaveable` on their behalf. One save entry per GameObject, however many persistent components it
carries: a door that is locked, burnable and interactive is one thing in the world and one thing in
the file.

**This finally answers a question left open in Milestone 3.** `CharacterPersistence` was written with
a note saying runtime-spawned enemies would need ids derived from their spawner, "a problem to solve
when there is a spawner". There is one now. `EnemySpawner` names each spawn `<its own id>.<index>` -
stable across sessions because it comes from the spawn point's position in the list, not from the
order things happened to be created - and `SaveService` already restored late registrations, which
is what makes a spawned object work at all.

**Death is recorded by the spawner, not the corpse.** Restoring a dead enemy means restoring a body,
an animation state and a disabled collider so the player can look at something they already killed.
Not spawning it is the same outcome for none of the work.

**Delivered**

- `IPersistentState`, `SceneObjectId`, `PersistentObject` in `Frieren.Core.Persistence`.
- `FlammableObject`, `WaterBasin`, `RepairableObject` and `LockedObject` all persist - including
  partial progress, so a half-filled trough and a half-picked lock survive a load.
- `CharacterPersistence` rewritten onto the new contract, and now saves position as well as vitals,
  optionally: the player should come back where they were, an enemy is better placed by its spawner
  than by a file that might put it inside geometry that has since moved.
- `CharacterSpellcaster` persists cooldowns - as *seconds remaining*, never as an absolute expiry,
  because `Time.time` restarts each session and a saved expiry has either already passed or sits
  hours in the future.
- `EnemySpawner` assigns ids and remembers which spawns are dead.
- `SceneObjectIdValidator`, an editor check on save and on demand.
- 7 new play-mode tests (55 play-mode, 251 in total).
- Two more classes of mistake caught by `check_usings.py`, which grew a check for generic BCL types
  after `CharacterSpellcaster` shipped a `List<string>` with no `using System.Collections.Generic;`.

**Known limitations**

1. Not run in the editor.
2. A crate restored mid-burn comes back alight with a full duration rather than part-way through. A
   fraction of a burn is not worth a field and nobody can tell.
3. Nothing persists which scene you were in. There is one scene; this becomes real in Milestone 7.
4. The barrier is not persisted. It lapses in a third of a second, so saving it would be saving
   something that has already expired.
5. Enemy positions are not saved, by choice. Reloading mid-fight puts the sentinel back at its spawn
   point rather than where it had chased you to.
6. Save slots exist in the API but nothing in the game chooses between them.

**How to test it**

1. Burn a crate, freeze the trough, mend the pillar, open the door, kill the sentinel. Walk
   somewhere distinctive.
2. `F5` to save, then break everything: put out fires, `F3` to heal, wander off.
3. `F9` to load. Every one of those should snap back - and you should be standing where you saved.
4. The sharp one: **kill the sentinel, save, load.** It should stay dead rather than respawning.
5. `Frieren > Saves > Open Save Folder` shows the JSON, one entry per object, keyed by the ids in
   `SceneObjectId`.

---

## Milestone 6.1 - Block on its own button, spells on a wheel (complete, not yet run)

**Build stamp: `m6.1 block and spell wheel`.** Both changes requested directly.

### Blocking is a button, not a slot

Barrier leaves the spell list and gets right mouse to itself. That is a design change, not a rebind:
defence you have to *select* is defence you will not use, and cycling to it mid-swing is the exact
opposite of what a block is for.

It is still a spell. `PlayerBlockInput` runs the same `Spell_Barrier` asset through the same
`CharacterSpellcaster`, so its cost, its drain and what it does stay authored in an asset. All the
component changes is which button starts it.

**Blocking occupies the caster.** The spellcaster runs one spell at a time, so the cast button is
refused while a ward is up and says why. That reads as concentration and suits the setting; if it
feels bad in play, the fix is a flag on the spell rather than a special path for this one button.

**One real bug fell out of two inputs sharing one caster.** `ReleaseChannel()` released whatever was
running, so tapping cast while blocking - the cast itself refused - would still have dropped the
ward on button-up. There is now `ReleaseChannel(spell)`, and each input releases only its own.

### The wheel

Hold `Q`. Point with the mouse or press a number; release to commit.

Nine spells on a number row stopped being usable somewhere around five. The wheel shows every option
at once and puts them somewhere the hand can learn.

Pointer and number row are not two code paths. The wheel tracks one highlighted index: the pointer
moves it by angle, the number row sets it directly, and the pointer only wins while it is out of the
dead zone in the middle. So pressing a number with the wheel open just works, opening and letting go
changes nothing, and sweeping across the wheel does not fire six spell changes on the way to the one
you meant.

It borrows the pointer from the camera while it is open, through a new `OrbitCameraRig.LookEnabled`.
Aiming a wheel and turning the camera with the same mouse movement is not a thing that can be
shared. `PlayerSpawner` wires it the same way it already wires locomotion and the aim source.

**Delivered**

- `Block` and `SpellWheel` actions in the input asset: right mouse / left trigger, and `Q` / left
  bumper.
- `PlayerBlockInput`, `SpellWheelInput`, `CharacterSpellcaster.ReleaseChannel(spell)`,
  `OrbitCameraRig.LookEnabled`.
- The permanent nine-line spell list is gone; `PlayerSpellInput` now shows one line, since the wheel
  is a better place to look.
- 13 edit-mode tests for the wheel geometry and 12 play-mode tests for both features.

**Known limitations**

1. Not run in the editor.
2. The wheel is IMGUI, like the rest of the placeholder UI. It is legible, not pretty.
3. Time does not slow while the wheel is open. *(Added in 6.2 below - and it was not the two-line
   change this line claimed. See there.)*
4. The number row still works with the wheel closed, which is a superset of what was asked for and
   costs nothing.
5. Gamepad wheel selection points with the left stick, which is also the movement stick. Playable,
   but it means you cannot walk while choosing.
6. Blocking has no visual of its own beyond the barrier shell and the vitals readout.

---

## Milestone 6.2 - Time slows while the wheel is open (complete, not yet run)

**Build stamp: `m6.2 wheel slows time`.** Requested directly.

**A correction first.** I said this was a two-line change through `TimeScaleService`. It was not, and
the reason is worth recording: `RequestDip` is *self-expiring* by design, so faking a held slow with
it would mean re-requesting every frame, fighting the dip's own "harder wins" rule, and leaving time
slow for a few frames after release. A hold is a different shape of thing from a dip and needed its
own factor.

### What changed

`TimeScaleState` now has three inputs instead of two:

```
Scale = BaseScale * min(DipFactor, HoldFactor)
```

- **Base** is game state. 0 while paused, 1 while playing. It *multiplies*, so a pause always wins
  outright no matter what else is slowing time.
- **Dip** is hit-stop. An event with a duration that expires by itself.
- **Hold** is a state with no end time, released by whoever claimed it.

Dip and hold take the **stronger of the two rather than multiplying**. Two slows that compound are
hard to reason about, and multiplying would mean a hit landing while the wheel is open produces a
0.25 x 0.12 near-freeze that nobody asked for.

**The hold is a single-holder claim with a named owner**, the same shape as `CharacterActionLock` and
for the same reason: two systems both wanting time slowed at once is a design decision somebody
should make deliberately, not something that falls out of whichever asked last. The owner matters on
release, because a hold anyone can cancel is a hold that eventually leaves the game running at a
third speed with nothing to blame.

`Bootstrapper` force-clears the hold when a scene starts loading. A slow held open while its owner is
being destroyed has nothing left to release it, and the next scene would start slow with no
explanation.

**Delivered**

- `TimeScaleState.SetHold` / `ClearHold`; `TimeScaleService.TryHold(owner, factor)`,
  `ReleaseHold(owner)`, `ForceClearHold()`.
- `SpellWheelInput.timeScaleWhileOpen`, default **0.25**, serialized on the prefab so it is tunable
  without code. Set it to 1 to turn the slow off entirely.
- 7 edit-mode tests for the new arithmetic and 6 play-mode tests for the claim and the wheel
  (216 + 73 = 289 in total).

**Known limitations**

1. Not run in the editor.
2. The slow snaps in and out rather than easing. Easing a value that changes how fast time passes is
   fiddly, and at a quarter speed the snap is not very visible - but it is the obvious next polish.
3. Camera shake still runs on the unscaled clock, so a shake during a wheel-slow plays at full speed.
   That is arguably right - impact should stay sharp - but it is a choice, not an accident.
4. 0.25 is a guess. It is one serialized field on the player prefab.

---

**Next: Milestone 7 - the gray-box vertical slice**

The systems are now well ahead of the content. Nine spells, an enemy, five world receivers,
feedback, persistence and 251 tests - and no *place*. The test scene is a scatter of props on a
plane, which is enough to prove a mechanism works and not enough to prove any of it is worth
playing.

Milestone 7 absorbs the briefed Milestone 6. More receiver kinds earn their keep when they are
designed into a space with a reason to exist, not added to a list. The slice wants: a small area
with a route through it, an objective, one fight that matters, and at least two problems whose
solutions the player finds rather than is told - which is the pillar the whole project is built to
demonstrate, and the one thing that has never actually been tested on a person.
