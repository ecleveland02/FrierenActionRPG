# Kael RPG (working title)

A non-commercial, third-person single-player action RPG inspired by the tone and worldbuilding of
*Frieren: Beyond Journey's End*. Kael is an original human apprentice mage from Eldenbrook; the
adventure does not depend on canon characters or storylines. Original story, placeholder art.

The repository name, `Frieren` namespaces and editor menus remain unchanged. See
[docs/KAEL_ROADMAP.md](docs/KAEL_ROADMAP.md) for the current creative direction and
[docs/COLLABORATION.md](docs/COLLABORATION.md) for the Codex/Claude handoff and validation gate.

The design principle everything else serves: **magic is a tool, not merely a weapon.** Spells are
expected to interact with the world - burning, freezing, lifting, flooding, repairing, unlocking -
and most environmental problems should have more than one solution.

> Status: **Milestones 1-5 complete.** 1 through 4 are confirmed in the editor: a controllable
> placeholder capsule with camera, jump, dodge, interaction, health, mana, and a data-driven magic
> system. Milestone 5 adds the first enemy - detection, chasing, a telegraphed swing, stagger and
> death - and the remaining six spells, including Zoltraak and a channelled Barrier. It has not been
> run in the editor yet. Milestone 6 makes the world remember what you did to it - a burnt crate
> stays burnt, an opened door stays open, a dead enemy stays dead. A follow-on pass adds placeholder combat feedback - a wind-up telegraph,
> spell beams, damage numbers, screen shake and hit-stop - so the loop can actually be judged.
> See [docs/MILESTONES.md](docs/MILESTONES.md).

---

## Requirements

| | |
|---|---|
| Unity | **6000.0 LTS** (6000.0.32f1 pinned in `ProjectSettings/ProjectVersion.txt`) |
| Git LFS | Required before cloning - art binaries are tracked through LFS |

```bash
git lfs install
git clone https://github.com/ecleveland02/FrierenActionRPG.git
```

Opening in a different Unity 6 patch release is fine; the Hub will offer to upgrade. Opening in
Unity 2022 or earlier is not supported.

> **Never used Unity before, or setting up a new machine?**
> [docs/SETUP.md](docs/SETUP.md) walks through installing Unity, cloning the branch, opening the
> project and wiring up a code editor.

## First open

1. Open the project folder in Unity Hub. The first import takes a few minutes while packages resolve.
2. Check the Console. `ProjectSetupValidator` runs once per session and reports anything misconfigured.
   It also links `InputReader.asset` to `FrierenControls.inputactions` automatically the first time,
   which is the one reference that cannot be stored in git (see
   [docs/DECISIONS.md](docs/DECISIONS.md), decision 9).
3. `Frieren > Open Boot Scene` (or `F7`), then press Play.

If the Console reports that Active Input Handling is wrong, set
**Project Settings > Player > Active Input Handling** to *Input System Package (New)* and restart
the editor. It should already be set - `ProjectSettings.asset` is committed with it.

## Running and testing

Press Play from `Boot.unity`. Boot loads `TestScene` additively and hands control to the
`Playing` state, then `PlayerSpawner` drops a capsule in and points the camera at it.

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | `WASD` | Left stick |
| Look | Mouse | Right stick |
| Sprint | `Shift` | L3 |
| Jump | `Space` | A / cross |
| Dodge | `Ctrl` | B / circle |
| Interact | `E` | X / square |
| Cast | Left mouse (hold for channelled spells) | Right trigger |
| Block | **Right mouse (hold)** | Left trigger |
| Spell wheel | **`Q` (hold)** — point with the mouse or press `1`-`8` | Left bumper, point with the left stick |
| Select spell | `1`-`8`: bolt, Zoltraak, fire, ice, levitate, water, mending, unbinding | - |
| Pause | `Esc` | Start |

Debug keys: `F1` overlay, `F2` damage 15, `F3` heal or revive, `F4` spend 15 mana, `F5` quick save,
`F6` change the `SaveProbe` counter, `F9` quick load.

**Movement checks, in the order worth doing them:** the capsule spawns and the camera follows;
movement is camera-relative; walking off the platform and jumping a frame late still jumps (coyote
time); pressing jump just before landing jumps on landing (input buffering); dodging mid-run ignores
steering until it ends; the capsule tints blue with speed and amber in the air; backing the camera
into the wall pulls it in rather than clipping; standing near a grey cube and pressing interact turns
it green.

**The save round-trip test:** press `F6` three times (counter reads 3), `F5` to save, `F6` twice more
(counter reads 5), then `F9`. The counter should snap back to 3. `Frieren > Saves > Open Save Folder`
shows the JSON that was written.

**Editing any scene directly:** press Play from any scene, not just Boot. `SceneBootstrapGuard`
pulls the Boot scene in behind it so services exist, and suppresses the first-scene load so the
scene under test is not immediately replaced.

**Combat and the spells:** walk north-east until the Stone Sentinel notices you. Press `2` and cast
Zoltraak twice to kill it, or press `3` and hold to raise a barrier while it swings. West of spawn,
two ledges with a trough between them: fill the trough with Water (`7`), freeze it with Ice (`5`),
and walk across - or hold Levitation (`6`) and float over instead. Further west, a locked door that
Unbinding (`9`) opens and Fire (`4`) burns down. South-east, a broken stump that Mending (`8`) puts
back together if you hold it long enough.

**Automated tests:** `Window > General > Test Runner`. **EditMode** runs 209 tests instantly and needs no
scene: they cover the service
locator, the game state machine, the save system, jump timing, motor maths, camera orbit maths,
interaction scoring, the character resource pool, spell cooldowns, burn state, the damage barrier,
the water basin, the lock and the time scale.

**PlayMode** runs 67 more and takes about a minute: the enemy end to end (detection, chasing, the
wind-up landing no damage, escaping it, stagger, death), channelling and mana drain, regeneration
delays, the barrier lapsing, levitation lifting the body, the trough draining, and a smoke test that
boots the real project and checks all nine spells are wired up, plus save-and-load round trips for
every world object. Read failures top down: a smoke-test
failure means the scene or asset YAML is wrong, and everything below it builds its own objects.

## Layout

```
Assets/
  Art/            Characters, Environments, Materials, VFX, UI  (placeholders only for now)
  Audio/  Animations/  Prefabs/
  Scenes/         Boot.unity, TestScene.unity
  ScriptableObjects/   Authored data assets (scene definitions, settings)
  Settings/Input/ FrierenControls.inputactions
  Scripts/
    Core/         Bootstrap, Services, Scenes, StateMachine, Input, Interaction, Debugging, Editor
    Characters/   Motor, action lock, animation, stats, health, mana - shared by player and enemies
    Player/       Locomotion, dodge, interaction probe, camera rig, spawner
    Save/         Save service, storage backends, file format
    ScriptableObjects/  Shared base types for authored content
    Magic/        Spell definitions, effects, and the component that casts them
    World/        Objects magic acts on: flammable, levitatable, water basin, repairable, locked
    Enemies/      Perception, behaviour state machine, melee, spawning
    Presentation/ Reacts to gameplay, read by none of it: flashes, telegraphs, beams, numbers
    Combat/ Inventory/ Equipment/ Quests/ Dialogue/   (empty - later milestones)
    Tests/EditMode/   Decisions that resolve in one call
    Tests/PlayMode/   Anything that needs a frame to pass
docs/
```

`Assets/Scripts/ScriptableObjects/` holds *base classes* for authored content.
`Assets/ScriptableObjects/` holds the authored *asset instances*. System-specific ScriptableObject
classes live with the system that owns them, not in a shared bucket.

## Editor menu

| Menu | Purpose |
|---|---|
| `Frieren > Open Boot Scene` (`F7`) | Jump to Boot |
| `Frieren > Open Test Scene` (`F8`) | Jump to the test scene |
| `Frieren > Setup > Create Missing Core Assets` | Recreate any deleted settings asset, non-destructively |
| `Frieren > Setup > Configure Build Settings` | Reset the build scene list with Boot first |
| `Frieren > Setup > Regenerate Core Scenes` | Rebuild the player prefab, Boot and TestScene from code (destructive - confirms first) |
| `Frieren > Saves > Open Save Folder` | Reveal `persistentDataPath/Saves` |
| `Frieren > Saves > Delete All Saves` | Wipe save files |

## Troubleshooting

**A scene or the player prefab fails to open, or its objects are unwired.** Run
`Frieren > Setup > Regenerate Core Scenes`. The prefab and both core scenes can be rebuilt from
code; that tool is the authoritative description of what they should contain.

**The capsule falls through the floor, or will not move.** Check that `Player.prefab`'s only
collider is the `CharacterController` on the root - a stray `CapsuleCollider` on the Visual child
fights it for the same space.

**The camera sits inside the capsule.** `OrbitCameraRig` discards hits on the target's own
colliders, so this means `SetTarget` was never called: check the `PlayerSpawner`'s camera rig
reference.

**Packages fail to resolve.** The versions in `Packages/manifest.json` are pinned for Unity 6000.0.
If your editor version disagrees, open Package Manager and let it resolve, then commit the updated
`Packages/packages-lock.json`.

**Materials look magenta.** The project currently renders with the Built-in pipeline; URP is
installed but not yet configured. See [docs/DECISIONS.md](docs/DECISIONS.md), decision 6.

## Documentation

- [docs/SETUP.md](docs/SETUP.md) - installing Unity, cloning, and setting up an editor from scratch
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) - how the systems fit together and where the next ones plug in
- [docs/DECISIONS.md](docs/DECISIONS.md) - decisions taken without asking, and why
- [docs/MILESTONES.md](docs/MILESTONES.md) - the plan and current status

## Legal

Non-commercial fan work. *Frieren: Beyond Journey's End* is the property of its rights holders;
this project is not affiliated with or endorsed by them. No assets from the anime or manga are used
or redistributed. All code and assets here are original or placeholder.
