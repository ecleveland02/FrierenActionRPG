# Meshy Kael cloth prototype

This is an isolated derivative, not a replacement for the working player.
The source Meshy FBXs and textures have not been overwritten.

## Try it in Unity

1. Open this project in Unity 6000.0 and let package resolution/import/compilation finish.
   The manifest now enables the built-in `com.unity.modules.cloth` module.
2. Run **Frieren > Kael > Build Meshy Cloth Test**.
3. Run **Frieren > Kael > Open Meshy Cloth Test**, then press Play.
4. Check wind at rest, movement/turning, jumping, and dodging. This copy has **no
   animation controller yet**, so the character stays in the bind pose while moving.
   Actual animated-leg collision still needs testing after Humanoid clips are assigned.

The builder creates `KaelClothPlayer.prefab`, `KaelClothTestScene.unity`, and a
material through Unity APIs. It refuses to overwrite existing outputs. The
original Player prefab, original test scene, and procedural Kael assets stay unchanged.
If validation stops the build, inspect the Console error before retrying; a failure
after asset creation can leave partial outputs which must be moved aside in Unity.

## What was prepared

- `ArtSource/KaelMeshyCloth/KaelCloth.blend`: editable, packed-albedo source with
  cloth, pins, preview wind, and bone-parented preview collision proxies. Play the
  timeline sequentially from frame 1 to simulate. Preview objects are not exported.
- `Assets/Art/Characters/KaelMeshyCloth/KaelCloth.fbx`: separate body and lower-coat
  skinned meshes; original UVs and total 10,400 triangles retained.
- Coat: 894 vertices, 1,604 triangles, 173 pinned vertices in Blender. Unity may
  split UV vertices or weld cloth particles, so its counts can differ.
- Waist/upper attachment seams remain pinned and retain their original skinning.
  Free fabric is blended toward the pelvis instead of stretching between the legs.
- `ClothFreedom` vertex-color red channel stores normalized freedom, not display
  color. The builder maps this to 0–0.35 metres of allowed cloth displacement using
  particle positions rather than assuming mesh and cloth indices match.
- Explicit Humanoid spine mapping: this export's `Spine02` is Unity Spine,
  `Spine01` is Chest, and `Spine` is UpperChest. Auto-mapping by name is misleading.
- Unity capsule proxies cover thighs, shins, and pelvis. They are triggers, so they
  do not create additional solid player movement colliders.
- `CharacterClothWind` supplies mild world-space acceleration and periodic gusts.
  It clears transform motion on large teleports and never moves the character.

## Limits and tuning

The source is a fused, generated mesh, not a separately tailored garment over a
complete body. Extreme lifts can expose incomplete hidden surfaces or rough cut
boundaries. This is a first cloth pass requiring gameplay/art review, not finished
production tailoring. The upper cape/hood, satchels, and central white tabard are
not separately simulated. Self-collision is disabled for this first pass.

In Play mode, find `KaelClothVisual/…/Kael_CoatCloth`. Its Cloth component controls
stiffness, damping, maximum distances and colliders; Character Cloth Wind controls
wind and gusts. Save lasting changes to the prefab outside Play mode.

For a moving world object to push the coat, explicitly add that object's capsule
collider to the Cloth component's **Capsule Colliders** array, or assign sphere
colliders in its sphere-pair array. For this spawned player, set scene-object
references on the spawned instance (or wire them through a future spawning system),
not on the prefab asset. Preserve the existing leg/pelvis entries. Mesh and box
colliders are not supported by Unity Cloth, and Wind Zones are not discovered
automatically. A weather system can call `CharacterClothWind.SetWind`.

The original roughness and metallic maps are retained, but this first Unity material
uses albedo, the normal map, and scalar smoothness 0.2; no channel packing is done.
The current project uses the Built-in render pipeline despite having URP installed.

## Verification

- Blender render checked before/after separation: appearance retained in bind pose.
- Export reimport test: two skinned meshes, UVs, freedom colors, 10,400 triangles,
  no unwanted animation.
- Blender wind simulation: 60 frames, finite bounded vertices and measurable motion.
- New runtime code compiled with the project's existing runtime sources against
  installed Unity 6000.0.32f1 assemblies, adding the newly enabled Cloth module.
- New editor builder compiled against the installed Unity 6000.0.32f1 assemblies.
- **Unity import, avatar validity, constraint mapping, prefab generation and Play-mode
  physics have not yet been verified.** The editor closed before this final check.
  The builder explicitly rejects invalid avatar or particle-coordinate mappings.

Reproducible Blender commands (run from the repository root):

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/Blender/prepare_kael_cloth.py
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/Blender/validate_kael_cloth.py
```

The preparation script regenerates only this derivative and its previews. Copy the
Blender file elsewhere before manually editing it if you plan to rerun the script.

Reference: [Unity 6000.0 Cloth manual](https://docs.unity3d.com/6000.0/Documentation/Manual/class-Cloth.html).
