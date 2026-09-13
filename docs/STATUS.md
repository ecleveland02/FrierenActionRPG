# Project status and forward plan

Last updated 2026-09-13, at commit `c0a075e`.

This is the single page to read before picking up work. It says what exists, what is actually
verified, what is known to be broken or missing, and what should happen next. For the reasoning
behind individual decisions see [`DECISIONS.md`](DECISIONS.md); for the dependency graph see
[`ARCHITECTURE.md`](ARCHITECTURE.md); for the rules every agent must follow see
[`../AGENTS.md`](../AGENTS.md).

---

## 1. What this is

A non-commercial 3D single-player action RPG fan project in **Unity 6000.0.32f1**, C#, original
characters and story, inspired by the world and themes of *Frieren: Beyond Journey's End*.
Placeholder art only until the loop is proven fun.

The design pillar everything serves: **magic is a tool, not merely a weapon.** Spells burn, freeze,
lift, flood, repair and unlock, and environmental problems should have more than one solution. Every
system is data-driven, so spells, enemies, items and quests are authored content, never hardcoded
branches.

**Render pipeline: Built-in.** URP was installed but never configured and has been removed. This was
a deliberate, argued decision, not drift: all seven Polytope environment shaders are
`#pragma surface` inside `CGPROGRAM`, which is built-in-only syntax that does not compile under URP.
Do not reintroduce URP without revisiting that. See section 6.

---

## 2. Where it actually stands

| Milestone | State |
|---|---|
| 1 Foundation: bootstrap, scenes, save, input, debug | Verified in the editor |
| 2 Placeholder third-person player | Verified in the editor |
| 3 Modular character architecture | Verified in the editor |
| 4 Magic framework, 3 spells | Verified in the editor |
| 5 First enemy, remaining 6 spells | Verified in play by the owner |
| 5.5 Combat feel | Verified in play by the owner |
| 5.6 Play-mode tests | Written; **the Test Runner has never been opened** |
| 6 Persistence | Written; not exercised deliberately |
| 6.1 Block on RMB, spell wheel on Q | Verified in play by the owner |
| 6.2 Time slows while the wheel is open | Verified in play by the owner |
| 7 The Watchtower vertical slice | Verified in play by the owner |
| 7.1 Camera pass: cursor, lock-on, framing | Verified in play by the owner |
| 7.2 Kael as the player character | Model swapped and pushed; **retargeting unverified** |
| 7.3 Spell icons and spell VFX | Wired and pushed; **unverified on screen** |
| 8 HUD: vitals, spell slot, lock-on reticle | Written; **unverified on screen** |

"Verified in play by the owner" means a human pressed Play and it behaved. It does not mean tested.

**351 tests exist (268 EditMode, 83 PlayMode) and not one has ever been run.** This is the single
largest unverified surface in the project and the cheapest thing anyone could fix.

---

## 3. Architecture in one page

13 assemblies, enforced acyclic by `.asmdef`:

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
Frieren.UI            -> Core, Characters, Magic, Player, Data
Frieren.Core.Editor   -> all of the above
Frieren.Tests.EditMode / Frieren.Tests.PlayMode -> all of the above
```

`Frieren.Core` must never depend on a gameplay assembly. Invert with an interface instead.

`Frieren.Presentation` is world-space feedback and does not reference `Frieren.Player`.
`Frieren.UI` is screen-space and does. Keep that split: it is the reason the HUD could be added
without giving the flash-and-floating-text layer a dependency on player input.

### Contracts worth knowing before writing anything

| Contract | What it buys |
|---|---|
| `ICharacterAnimation` | Gameplay reports state, presentation reacts. One-directional. Swapping the placeholder capsule for a rigged character required zero gameplay changes. |
| `IMagicReceiver` / `MagicPulse` / `MagicElement` | World objects react to **elements, not spell identities**, so a new spell works on existing objects for free. |
| `SpellDefinition` + ordered `SpellEffect` list | A spell is not a class. "Fire that also lights torches" is authored by adding an effect to a list. |
| `CharacterActionLock` | Single-holder claim. One ability owns the body at a time; locomotion stands down and never claims it itself. |
| `TimeScaleService.TryHold` | Owner-scoped claim on time scale. `Scale = BaseScale * min(DipFactor, HoldFactor)`. |
| `CursorService.RequestPointer` | Owner-scoped claim on the pointer. Same shape. Gameplay is the state with **no** claims. |
| `ISaveable` / `IPersistentState` / `SceneObjectId` | Order-independent restore, one save entry per GameObject, late registration supported. |
| `ServiceLocator` | Composition root registry. **No singletons.** Everything is constructed in `Bootstrapper`. |

### The rule that keeps being re-learned

Logic worth testing goes in a plain C# class, not a MonoBehaviour. `JumpGate`, `OrbitCameraSolver`,
`MotorMath`, `ResourcePool`, `BurnState`, `TimeScaleState`, `LockOnPicker` and
`SpellWheelInput.SlotFor` and `BarSmoothing` all exist because their logic was extracted out of
components.

---

## 4. The validation toolchain — read this before writing YAML

**No agent on this project has Unity.** The entire asset layer, every `.unity`, `.prefab`, `.asset`
and `.meta`, is hand-authored YAML with md5-derived GUIDs (`md5("frieren.asset." + path)`). Four
Python checkers stand in for the compiler and the editor. **Run all four before every push.**

```
python3 Tools/Validation/check_assemblies.py      # CS0012, CS0234, cycles, engine modules, braces
python3 Tools/Validation/check_usings.py          # CS0246, missing usings including BCL generics
python3 Tools/Validation/check_unity_yaml.py      # anchors, refs, hierarchy, components, metas
python3 Tools/Validation/check_level_geometry.py  # slopes vs the 45 degree limit, spawns in solids
```

Every one of these exists because something shipped broken. Recent additions:

- **Engine modules.** Cross-references every `using UnityEngine.X` against `Packages/manifest.json`.
  Added after `UnityEngine.AI` was used without `com.unity.modules.ai`, a CS0234 that took down four
  assemblies at once.
- **Stripped transforms.** A nested prefab's stub carries no `m_Father`; its parent is named by
  `m_TransformParent` on the owning `PrefabInstance`. The checker verifies that in both directions.
- **Cross-file references.** Any `{fileID, guid}` pointing into a project prefab must name an object
  that prefab actually defines. Added after nine spell VFX assets referenced `fileID: 100100000`,
  which is the prefab asset object rather than its root GameObject; every one threw
  "Specified cast is not valid" on first cast. A wrong guid shows as a pink missing reference; a
  right guid with a wrong fileID looks completely normal in the inspector and only fails at runtime.

**When you add a check, prove it fires.** Break the thing deliberately, watch it get caught, restore.
A check that has never failed is not a check. One added earlier looked for a scene object named
`SliceObjectives` while the scene names it `Objectives`; it matched nothing and silently passed on
every run for weeks.

### Referencing a prefab from YAML

A prefab asset is referenced by its **root GameObject's fileID** — the object whose Transform has
`m_Father: {fileID: 0}` — not by `100100000`. That id differs per prefab and must be read out of the
target file. `100100000` is only correct for `m_SourcePrefab`.

### Build stamp discipline

`Assets/Scripts/Core/Debugging/BuildStamp.cs` holds a version string shown in the in-game debug
overlay. **Bump it in every commit that changes behaviour.** It is the only way the owner can tell
whether the editor is running your change or a cached older one, and it has already resolved one
multi-round confusion.

---

## 5. What changed most recently (2026-09-12 to 09-13)

Nine commits, `785e981` through `b45f767`.

**Four reported bugs fixed.** Boot loaded the test scene instead of the Watchtower. The camera could
not look around, because `InputReader.Initialize()` called `Dispose()`, which nulls every public
event delegate; entering play from a non-Boot scene means components subscribe first and
`SceneBootstrapGuard` pulls Boot in additively afterwards, so the camera's handler was dropped on the
floor. Split into `Unwire()` (detaches actions, keeps listeners) and `Dispose()` (both). Enemies were
untextured for two separate reasons: the material the prefab actually used had every texture slot
empty, and all 73 vendor materials pointed at a shader GUID that was never committed. Enemy
animations stopped after one cycle because the controller uses standalone `.anim` assets, not the FBX
sub-clips, and all eleven had `m_LoopTime: 0`.

**Camera pass.** `CursorService` (claim-based, so closing the wheel cannot free a pointer the pause
menu still holds). The wheel now shows the real cursor, warps it to the wheel centre and reads its
absolute position, and a left click commits the slot under it. Lock-on via middle mouse, Tab or right
stick, with flick-to-switch counted in pixels for a mouse and seconds of deflection for a stick,
because one threshold cannot mean both. Dark-Souls framing: the pivot lifts 0.55m in camera space,
putting the character about 9.5% of screen height below centre, applied to the pivot rather than the
camera so the collision sweep still runs along the line the camera travels.

**URP removed.** 35 materials converted to built-in Standard, package dropped, `packages-lock.json`
recomputed by reachability from the manifest roots rather than hand-edited (eleven entries fell out).

**Kael is the player.** Now `KaelCloth.fbx`: textured, humanoid-rigged, 32-bone skeleton. The nested
prefab block was lifted from Unity's own `KaelClothPlayer.prefab` rather than hand-derived. The eight
animation FBXs moved from Generic to Humanoid, which was necessary rather than cosmetic: Generic
clips bind by transform path and the source rig names bones `Shoulder.L` where KaelCloth names them
`LeftShoulder`, so as Generic they would not have played at all.

**Spell icons and VFX.** `SpellDefinition` gained an icon sprite and wheel tint; the wheel draws
pictures with the tint behind the art rather than multiplying it. New `SpawnVfxEffect` spawns
particles as an ordinary effect in the spell's list. Channelled spells were the trap — they apply
their whole effect list every tick, so a held ward would have spawned a dozen shields;
`oneInstancePerAnchor` fixes it by asking the scene rather than remembering anything, which keeps the
asset stateless and stays correct with two casters channelling at once.

**Tooling.** `Update.bat` never checked whether `git pull` succeeded, so a pull refused because Unity
had rewritten an open asset left the editor on the old build with the failure scrolled off screen.
That cost two rounds of debugging a bug that did not exist. It now lists local modifications first,
checks the exit code, and fails loudly. `PlayerSpawner` reports the spawned body once — renderer
count, bounds, animator state — and says outright when it is the placeholder rather than the rigged
character.

---

## 6. Known gaps, in priority order

1. **343 tests have never been run.** Open the Test Runner. This is the highest value hour available.
2. **Kael's retargeting is unverified.** Humanoid retargeting quality depends on the two rest poses
   agreeing. If a clip looks wrong, it is the avatar T-pose, fixed in the Rig tab under Configure,
   not in any file. The source rig's `Coat` chain and `StaffSocket` are not humanoid bones and are
   dropped in retargeting; that matters when the staff gets attached.
3. **Kael has no Death or Attack clip.** Death is handled by `DeathSink` tipping the body.
   `MecanimCharacterAnimation` ignores an action with no matching trigger, so neither logs an error.
4. **Spell icon choices are guesses.** Mapped from the Blink pack's class folder names without ever
   seeing the images.
5. **The Water spell's VFX is a snow hit.** Closest thing the pack has to a splash. Replace first.
6. **~1.8 GB of audio** across five overlapping music libraries, against GitHub's free LFS allowance
   of 1 GB storage and 1 GB monthly bandwidth. Explicitly deferred by the owner, but it is a cliff.
7. **The HUD is unverified.** Built at runtime in code rather than as a prefab, so it cannot be
   inspected without pressing Play. Font is the engine builtin; a real one is a single change in
   `HudBuilder.Font`.
8. **`MonsterCelMaterial.mat`** references a texture GUID not in the repo, so it renders white.
   Nothing uses it; inventing a texture would be a guess.

---

## 7. What should happen next

**Immediately, before new features:**

- Run the Test Runner. Fix what fails. 343 tests written blind will not all pass.
- Confirm Kael animates correctly, and fix the avatar T-pose if not.
- Look at the nine spell icons and the nine VFX on screen; swap what reads wrong.

**Then, the natural next milestone — Milestone 8, the HUD.** Health, mana, the selected spell,
cooldowns and a lock-on reticle, replacing the `OnGUI` placeholders. It is the gate on judging combat
feel honestly, and lock-on has already run into it.

**After that, in rough order:**

- **Attach the staff.** The magic staff pack is imported and unused; the rig has a `StaffSocket`
  bone, which is currently dropped by humanoid retargeting and will need an explicit transform.
- **A second enemy type** that forces a different answer than the first, to prove the enemy data
  model is actually general.
- **Audio.** The libraries are imported and `CombatMusicState` / `EncounterMusic` exist but are
  minimal.
- **A second area**, to prove the scene and persistence systems hold across a transition. Persistence
  is written and has never been exercised deliberately.
- **Trim the audio** before the LFS cliff arrives.

**Not yet:** final art, dialogue, quests, inventory. The brief's constraint still holds — the
vertical slice has to be fun first.

---

## 8. Working agreements

- **Two agents share this repo** (Claude and Codex/GPT) with the owner as integrator. Strict file
  ownership, documented in [`COLLABORATION.md`](COLLABORATION.md). Never edit a file the other agent
  owns in the current handoff without saying so.
- **Hand-editing YAML is permitted but gated.** `AGENTS.md` rule 1 originally banned it; that was
  written when the alternative was the editor. In practice the entire asset layer is hand-authored,
  and the four validators are what make it safe. The rule is now: hand-edit if you must, run all four
  checkers, and never guess a fileID you can read out of a file.
- **Report honestly.** Say what is verified, what is written but unrun, and what you could not check.
  Several rounds have been lost to a confident claim that something worked.
- **Bump `BuildStamp` on every behavioural commit.**
- Owner preference: **no em dashes** in prose. Be a critical partner, not an agreeable one. Give real
  numbers, not encouragement.
