# Frieren Action RPG (working title)

A non-commercial fan project: a 3D single-player action RPG inspired by the world and themes of
*Frieren: Beyond Journey's End*. Original characters, original story, placeholder art.

The design principle everything else serves: **magic is a tool, not merely a weapon.** Spells are
expected to interact with the world - burning, freezing, lifting, flooding, repairing, unlocking -
and most environmental problems should have more than one solution.

> Status: **Milestone 1 complete** - project foundation only. There is no player character, no
> combat and no magic yet. See [docs/MILESTONES.md](docs/MILESTONES.md).

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

## Running and testing Milestone 1

Press Play from `Boot.unity`. Boot loads `TestScene` additively and hands control to the
`Playing` state. You should see a gray plane, a white cube, and a debug overlay.

| Key | Effect |
|---|---|
| `F1` | Toggle the debug overlay |
| `F5` | Quick save to `slot_0` |
| `F6` | Change the `SaveProbe` counter |
| `F9` | Quick load `slot_0` |
| `Esc` | Toggle Playing / Paused |

**The save round-trip test:** press `F6` three times (counter reads 3), `F5` to save, `F6` twice more
(counter reads 5), then `F9`. The counter should snap back to 3. `Frieren > Saves > Open Save Folder`
shows the JSON that was written.

**Editing any scene directly:** press Play from any scene, not just Boot. `SceneBootstrapGuard`
pulls the Boot scene in behind it so services exist, and suppresses the first-scene load so the
scene under test is not immediately replaced.

**Automated tests:** `Window > General > Test Runner > EditMode > Run All`. 36 tests cover the
service locator, the game state machine and the save system. They do not touch the disk.

## Layout

```
Assets/
  Art/            Characters, Environments, Materials, VFX, UI  (placeholders only for now)
  Audio/  Animations/  Prefabs/
  Scenes/         Boot.unity, TestScene.unity
  ScriptableObjects/   Authored data assets (scene definitions, settings)
  Settings/Input/ FrierenControls.inputactions
  Scripts/
    Core/         Bootstrap, Services, Scenes, StateMachine, Input, Debugging, Editor
    Save/         Save service, storage backends, file format
    ScriptableObjects/  Shared base types for authored content
    Player/ Combat/ Magic/ Enemies/ Inventory/ Equipment/ Quests/ Dialogue/   (empty - later milestones)
    Tests/EditMode/
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
| `Frieren > Setup > Regenerate Core Scenes` | Rebuild Boot and TestScene from code (destructive - confirms first) |
| `Frieren > Saves > Open Save Folder` | Reveal `persistentDataPath/Saves` |
| `Frieren > Saves > Delete All Saves` | Wipe save files |

## Troubleshooting

**A scene fails to open or its objects are unwired.** Run `Frieren > Setup > Regenerate Core Scenes`.
Both core scenes can be rebuilt from code; that tool is the authoritative description of what they
should contain.

**Packages fail to resolve.** The versions in `Packages/manifest.json` are pinned for Unity 6000.0.
If your editor version disagrees, open Package Manager and let it resolve, then commit the updated
`Packages/packages-lock.json`.

**Materials look magenta.** The project currently renders with the Built-in pipeline; URP is
installed but not yet configured. See [docs/DECISIONS.md](docs/DECISIONS.md), decision 6.

## Documentation

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) - how the systems fit together and where the next ones plug in
- [docs/DECISIONS.md](docs/DECISIONS.md) - decisions taken without asking, and why
- [docs/MILESTONES.md](docs/MILESTONES.md) - the plan and current status

## Legal

Non-commercial fan work. *Frieren: Beyond Journey's End* is the property of its rights holders;
this project is not affiliated with or endorsed by them. No assets from the anime or manga are used
or redistributed. All code and assets here are original or placeholder.
