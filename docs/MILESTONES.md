# Milestones

Each milestone must produce something playable or testable, and be verified before the next starts.

| # | Goal | Status |
|---|---|---|
| 1 | Project foundation | **Complete** (unverified in-editor - see note) |
| 2 | Placeholder third-person player | Not started |
| 3 | Modular character architecture | Not started |
| 4 | Data-driven magic framework + 8 prototype spells | Not started |
| 5 | First enemy and basic combat | Not started |
| 6 | Reusable environmental interaction systems | Not started |
| 7 | Gray-box vertical slice | Not started |

> **Note on Milestone 1's status.** The code was authored in an environment without Unity installed,
> so nothing here has been compiled or run. Static checks passed (brace balance, assembly reference
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

**Next: Milestone 2 - placeholder third-person player**

Scope: `CharacterController`-based movement, a camera rig, sprint, jump, gravity, dodge, an
interaction probe, and the animation *architecture* (not animations). A capsule stands in for the
character.

Two things to settle at the start of Milestone 2:

- **Camera.** Cinemachine 3 or a hand-written rig. Cinemachine is the pragmatic answer - it is free,
  first-party, and hand-rolling collision-aware third-person framing is a genuine time sink - but it
  is a dependency, and the brief asks for no unnecessary third-party dependencies. Recommendation:
  take Cinemachine, because the alternative is re-implementing it worse.
- **Motor.** `CharacterController` or `Rigidbody`. Recommendation: `CharacterController`, because
  action-RPG movement wants authored, predictable motion, and physics-driven characters fight the
  designer. Revisit only if magic needs to apply real forces to the player - levitation on the
  *player* would be the case that forces the question.

Milestone 2 is complete when a capsule can be driven around `TestScene` with a working camera, and
the interaction probe reports what it is pointing at.
