# Watchtower Woodland

Build stamp: `m7.1 watchtower woodland`.

Eric paused the Kael replacement and authorized Codex to extend Claude's milestone work with the
locally imported environment, audio and creature assets. This pass preserves the existing player,
spell definitions, four Watchtower objectives and persistent IDs. The new encounter has its own
stable ID, `watchtower.woodland.encounter`.

## Play

Open `Assets/Scenes/Boot.unity` (F7), then press Play. Boot loads Watchtower.
Start at the forest camp, follow the trail, fight the two woodland sentinels, then enter the
courtyard for the original two-enemy encounter and magic puzzles. The gate can be unlocked or
burned; the broken stair can be repaired; the aqueduct can be filled and frozen.

Movement, attacks, blocking and the spell wheel remain Claude's existing controls. The player is
still the established capsule prototype, not the unfinished Kael model.

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

The installed URP package is **not** the active renderer in this checkout. Generated materials
therefore match the existing built-in pipeline. Vendor vegetation shaders and subdued wind are
preserved in material copies; imported originals are not overwritten.

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
present with their original metadata. They are local user imports, not files Codex is authorized
to redistribute publicly. Do not blindly add the untracked vendor packs to Git.

- Polytope Studio / Lowpoly Environments (pine trees, rocks, grass).
- Stylized Labs / Stylized Fantasy / Props Sample (camp props and obelisk).
- Stylized3DMonster / Monster04 (mesh, material and in-place animation clips).
- Fantasy Skybox FREE / FS003 Day.
- 25 RPG Game Tracks: Light Ambient 3 (Loop), Action 2 (Loop).
- Nature – Essentials: Forest Birds, Forest Wind, Small Firecamp loops.

Source code and generated placement assets can be reviewed separately from those packs. A fresh
clone without the same imports cannot render this world; importing the packs is a prerequisite.

## Verification

Unity 6000.0.32f1 generated and saved the scene through editor APIs. The first nine Watchtower
play-mode tests passed, including persistent IDs, original puzzle defaults, four enemy spawns,
baked ground paths and combat music returning to exploration after all enemies die.

Additional full-suite and animation-binding checks are recorded in `COLLABORATION.md` after they
finish. Static geometry validation must name `Assets/Scenes/Watchtower.unity` explicitly: after
Unity saves the scene its YAML no longer contains the class-name marker the checker's automatic
discovery expects.

Remaining creative work: replace the player capsule when the character is ready, richer ruin
surface art, more varied encounters, and subjective music-volume/combat-feel tuning in play mode.
