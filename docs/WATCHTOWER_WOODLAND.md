# Watchtower Woodland

Build stamp: `m7.9 woodland combat verified`.

Eric paused the Kael replacement and authorized Codex to extend Claude's milestone work with the
locally imported environment, audio and creature assets. This pass preserves the existing player,
spell definitions, four Watchtower objectives and persistent IDs. The new encounter has its own
stable ID, `watchtower.woodland.encounter`.

## Play

Open `Assets/Scenes/Boot.unity` (F7), then press Play. Boot loads Watchtower.
Start at the forest camp, follow the trail, fight the two woodland sentinels, then enter the
courtyard for the original two-enemy encounter and magic puzzles. The gate can be unlocked or
burned; the broken stair can be repaired; the aqueduct can be filled and frozen.

Movement, attacks, blocking and the spell wheel remain Claude's existing controls. The subsequent
Claude updates that promote Kael to Player.prefab and add camera/lock-on/spell-VFX work are preserved.

## What changed

- A compact 80-by-100-metre woodland footprint, camp, trail, trees, grass, rocks, obelisk and
  architectural detailing around the existing Watchtower geometry.
- Two persistent spawners, four enemies total. The new monster prefab reuses Sentinel gameplay,
  stats and damage reactions, with imported idle/run/attack/hit/death clips. A ground flash keeps
  the existing attack wind-up readable. No root motion or second movement integrator.
- Scene-owned exploration/combat music with a two-second equal-power crossfade. Chase, attack or
  stagger against a living target starts combat music. Four quiet game-time seconds precede the
  return to exploration, so a brief stagger or loss of sight does not chatter between tracks.
- Forest birds and wind, plus distance-attenuated campfire audio and a small ember effect.
- Baked navigation steering for the woodland enemy variant. Existing enemies without this
  optional component retain their old steering. Paths refresh at most twice per second and reuse
  a 32-corner buffer; perception reuses a collider buffer instead of allocating each scan.
- Streaming compressed imports for the five selected audio loops; shared, instancing-enabled
  material copies; static batching flags for scenery; simple vegetation colliders; no grass
  colliders; a bounded particle count and a shadow-free local camp light. These are scoped cost
  reductions, not a measured claim of an FPS improvement.

The project uses the **built-in renderer**; Claude subsequently removed the unused URP packages.
Generated materials match that pipeline. Vendor vegetation shaders and subdued wind are preserved
in material copies. Unity may update imported material serialization during import.

## Scenes and recovery

The two Kael experiment scenes are moved, with their GUIDs intact, into `Assets/Scenes/Archive/`.
Their builders now target those paths. Nothing was permanently deleted. `TestScene` is retained
because it is a useful regression playground and is referenced by the scene catalog. Boot,
TestScene and Watchtower are the only build scenes; imported vendor demos are not included.

The first world build preserves a local pre-dressing copy at
`ArtSource/WorldBackups/Watchtower-before-woodland.unity`. It is not an imported Unity scene.

## Rebuilding and asset ownership

`Frieren > World > Build Watchtower Woodland` runs `WatchtowerWorldBuilder.Build`.
It opens Watchtower and replaces only its named `WorldDressing` hierarchy, refreshes generated
materials/enemy prefab/navigation data, and reapplies the documented ground/spawn/material
changes. Back up any hand edits to generated content before rebuilding. Do not run Regenerate
Core Scenes for this feature: that rebuilds unrelated core assets.

Generated assets live under `Assets/Art/World/Watchtower`. The imported packs below must be
present with their original metadata. They were initially local user imports and were subsequently
included in Eric's Unity upload commits. Review their licenses before sharing the repository or
redistributing source assets; this pass does not establish redistribution rights.

- Polytope Studio / Lowpoly Environments (pine trees, rocks, grass).
- Stylized Labs / Stylized Fantasy / Props Sample (camp props and obelisk).
- Stylized3DMonster / Monster04 (mesh, material and in-place animation clips).
- Fantasy Skybox FREE / FS003 Day.
- 25 RPG Game Tracks: Light Ambient 3 (Loop), Action 2 (Loop).
- Nature – Essentials: Forest Birds, Forest Wind, Small Firecamp loops.

Source code and generated placement assets can be reviewed separately from those packs. A fresh
clone without the same imports cannot render this world; importing the packs is a prerequisite.

## Verification

Unity 6000.0.32f1 generated and saved the scene through editor APIs. All **260 EditMode tests** and
**83 PlayMode tests** passed. The latter includes real boot, original puzzle defaults, persistent
IDs, four enemy spawns, navigation movement, imported monster bone animation, and combat music
returning to exploration after all enemies die. All four static validators also passed; geometry
was checked explicitly against `Assets/Scenes/Watchtower.unity`.

The full suite exposed and fixed one actual combat defect: `SwingLanded?.Invoke(Strike())` skipped
damage altogether when no listener existed. Damage now happens before the optional notification.
Test fixtures also needed fresh bootstrap state per boot case and the same zero minimum movement
threshold as the production prefabs. The channel-mana assertion now respects whole-tick spending:
an unaffordable remaining fraction is retained, not drained for free.

Remaining creative work: richer ruin surface art, more varied encounters, and subjective
music-volume/combat-feel tuning in play mode. No FPS gain is claimed without profiling a build.
