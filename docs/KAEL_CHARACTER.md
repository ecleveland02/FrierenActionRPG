# Kael character prototype

First stylized low-poly blockout based on the owner's concept sheet, supplied September 11, 2026.
This establishes a model/rig/animation/import pipeline. It is not a finished match for the anime
illustration: facial likeness, hair, garment construction and motion still need an art pass.

## Delivered files

- `ArtSource/Kael/Kael.blend`: editable mesh, 25-bone rig, eight actions and studio lighting.
- `ArtSource/Kael/Reference/Kael_Concept.png`: owner's supplied visual reference.
- `ArtSource/Kael/Previews/`: Blender renders of idle/front, back, walk, run, cast and dodge poses.
- `ArtSource/Kael/build-report.json`: geometry counts and clip lengths.
- `Tools/Blender/build_kael.py`: reproducible model/rig/motion generator for Blender 5.2.
- `Assets/Art/Characters/Kael/Models/`: base FBX and eight animation FBXs.
- `Assets/Art/Characters/Kael/Kael.controller`: Unity-generated locomotion and action controller.
- `Assets/Prefabs/Characters/KaelPlayer.prefab`: copy of the working player with Kael visuals.
- `Assets/Scenes/KaelTestScene.unity`: copy of TestScene using the Kael prefab.
- `Assets/Scripts/Core/Editor/KaelPrototypeBuilder.cs`: scoped import rules, builder and validation.

Existing gameplay C# and the original Player prefab/Boot/TestScene were not edited. The new prefab
uses the existing `MecanimCharacterAnimation` instead of `PlaceholderCharacterAnimation`.
All prefab, controller and importer metadata were produced by Unity, not hand-authored YAML.

## Try it in Unity

1. Exit Play mode, allow Unity to import/recompile, then clear any unrelated Console errors.
2. Run **Frieren > Kael > Validate Character Assets**. It checks avatar validity, curve-to-bone
   bindings, collider count and skinning at five timestamps per clip. A successful run logs PASS.
3. Choose **Frieren > Kael > Open Character Test Scene**, then press Play. This scene uses the
   existing editor bootstrap guard; Boot must remain available in the project's build settings.
4. Test WASD, mouse look, Shift sprint, Space jump, Ctrl dodge and E interaction. Movement and
   collision still use the original character motor and CharacterController.
5. Inspect the animated model for facing direction, floor contact, limb seams, coat penetration
   and staff positioning. Foot sliding is expected until stride timing is tuned to gameplay speed.
6. To preview casting, select `Kael_Cast.fbx` in the Project window and play its Animation preview.
   Alternatively trigger `CastStart` in the running Kael Animator. This is visual-only; no new
   spell-casting gameplay, mana, damage or input bindings were added.

The committed generated assets are already wired; no manual Inspector setup is needed for the new
test scene. To adopt Kael in another scene, set that scene's PlayerSpawner **Player Prefab** to
KaelPlayer. Keep changes to existing scenes coordinated with Claude.

## Animation and rig contract

| Clip | Duration | Use |
|---|---|---|
| Idle | 2.0 s | Loop, default locomotion |
| Walk | 1.0 s | Loop, MoveBlend 0.5625 |
| Run | 0.733 s | Loop, MoveBlend 1 |
| Air | 1.0 s | Loop while IsGrounded is false |
| Land | 0.4 s | Ground contact, then locomotion |
| Dodge | 0.4 s | Dodge trigger |
| Cast | 1.2 s | CastStart trigger / animation preview |
| Hit | 0.5 s | Hit trigger / animation preview |

Generic rig, about 1.78 m, 6,280 source triangles, 3,394 vertices before Unity import splitting.
Blender is Z-up/-Y-forward; the FBX export converts axes for Unity. The animation root stays in
place and Animator root motion is disabled. The 2 m gameplay capsule is intentionally retained
from the working player. No runtime IK or cloth simulation is required: the leg solves, coat
motion and staff orientation are baked into the clips.

`Kael_Body` and `Kael_Staff` are separate skinned meshes. StaffSocket is a bone under the right
hand, suitable for a later equipment pass. This is not yet an equipment system. The hand shape
is simplified; there are no individually animated fingers, facial expressions or lip sync.

The controller consumes Speed, MoveBlend, VerticalVelocity and IsGrounded from the existing
adapter. Jump/land presentation is primarily driven by IsGrounded. CastRelease has no separate
release clip in this prototype; Attack and Death are not implemented. Action states return to
locomotion; airborne transitions can need further polish when actions overlap landing/jumping.

## Regenerating or editing

Prefer editing the Blender source manually once art work begins. Running the generator again
overwrites the generated `.blend`, FBXs and previews, so preserve hand edits before doing so.

From the repository root in PowerShell:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --python Tools/Blender/build_kael.py
```

Unity's scoped importer sets Generic animation, baked root transforms and loop flags. Art-only
FBX updates reimport automatically; no controller rebuild is needed. **Build Character Prototype**
creates a new controller/prefab/test scene only when those outputs do not already exist. It refuses
to overwrite them. Never use Regenerate Core Scenes to rebuild Kael: that tool owns the original
placeholder setup. Keep `.meta` files and use Git LFS for binary assets.

## Verification and limitations

- Blender 5.2.1 successfully generated all nine FBXs and the editable source; renders were inspected.
- Unity 6000.0.32f1 compiled the initial builder and imported all eight clips in a separate validation
  copy, then generated the controller, player prefab and test scene successfully (exit code 0).
- The final motion revisions and subsequently added validation/capture methods have **not** been
  recompiled or executed in Unity: permission for that follow-up process was declined. Run the
  validation menu and Play-mode checklist above before treating this character as verified.
- No 40-pose PASS result or final Unity render is claimed. The included previews are Blender renders.
- The validation copy remains at `C:\Users\ericc\Code\KaelValidation_20260911`; its first build log
  is `kael-build.log`. It is a disposable development copy, not the working project.
- The initial batch import reported render-pipeline fallback shader warnings with graphics disabled;
  final material appearance must be checked in the normal editor. The project currently uses Built-in.
- Flat-color materials and segmented forms are intentional blockout assets. Coat/leg intersections,
  rigid-looking elbows, limited casting hand posing and foot sliding still need refinement. No UV
  texture painting, production deformation topology, Humanoid retargeting or expression rig is included.

Next milestone: approve/refine the silhouette and face against the concept, then tune gait/dodge
timing in the playable scene. Upgrade garment weighting and hand posing before expanding the clip set.
