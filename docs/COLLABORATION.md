# Codex / Claude handoff

## Current handoff: 2026-09-11

Owner subsequently confirmed Milestone 2 works well and requested character/animation work. Codex
built a separate Kael blockout, rig and eight-clip prototype from the supplied concept sheet. See
[KAEL_CHARACTER.md](KAEL_CHARACTER.md) for files, setup, validation evidence and limitations. Original
gameplay scripts and core scenes were preserved. Initial Unity generation passed; final motion
revisions and added validation methods still need the editor checks documented there.

Owner supplied the Kael roadmap; see [KAEL_ROADMAP.md](KAEL_ROADMAP.md). Foundation and placeholder
player code exist; editor validation is still pending. No magic or final character asset is ready.
Codex aligned documentation and prepared this checklist. Direct messaging to Claude has not been
established; this file is a shared handoff, not evidence that Claude has received an assignment.

Local checkout: `C:\Users\ericc\Code\Frieren\FrierenActionRPG`.
Observed branch: `claude/frieren-rpg-foundation-svvrqc`; inspected baseline: `8215c3a`.
Pinned editor: `6000.0.32f1`. Its executable and Blender 5.2 were found on this machine.
An executable being present does not establish licensing, successful launch or project compatibility.

Validation attempt: Unity 6000.0.32f1 batch EditMode run could not start tests. The first sandboxed
attempt timed out connecting to licensing; a retry outside the sandbox connected, then reported
`No valid Unity Editor license found. Please activate your license.` No test-result XML was
produced. Finish sign-in and license activation in Unity Hub, then rerun the gate. Local diagnostic
log: `Logs/foundation-editmode.log` (gitignored). Compilation and gameplay remain unverified.

## Suggested division of work

- Codex: foundation validation and fixes first; afterward Blender prop pipeline and isolated assets.
- Claude: review foundation findings; afterward collaborate on the three-spell framework using
  existing input, character action lock and animation interfaces.
- Owner: creative decisions and Play-mode feel checks, especially movement, camera and magic utility.

These are proposed areas, not concurrent file claims. Before editing, check current Git status,
read this handoff and agree the files for the active chunk. Separate checkouts/worktrees are useful
for simultaneous code edits; each Unity instance must use its own project folder. Preserve `.meta`
files and avoid concurrent edits to scenes, prefabs and settings. Review before integrating commits.
Record actual results and remaining work here after each chunk. Do not mark unrun checks as passed.

## Foundation validation gate

Open this existing folder through Unity Hub's Add project from disk; do not create another project.
The editor opens local files; Git/GitHub synchronizes code and assets between collaborators.

- [ ] Unity finishes package import and compilation with no errors. Record actual editor version.
- [ ] Review generated package lock and any import changes before integrating them.
- [ ] Run Window > General > Test Runner > EditMode > Run All; record pass/fail counts and failures.
- [ ] Frieren > Open Boot Scene (F7), then Play: one player spawns and camera follows.
- [ ] Check camera-relative movement, sprint, jump, buffered jump, coyote time and dodge.
- [ ] Check camera collision at the test wall and interact with both cubes using E.
- [ ] Pause and resume with Esc; confirm input and movement recover.
- [ ] F6 three times, F5, F6 twice, F9: the SaveProbe returns from 5 to 3.
- [ ] Start directly from TestScene and verify the bootstrap guard and player wiring.
- [ ] Stop and start Play again; verify no duplicate persistent systems or stale input.

No new Inspector fields or scene changes are required by this documentation update. Use the
committed scenes/prefab first. Scene regeneration overwrites authored scene/prefab contents, so
inspect any failures and preserve user edits before using it.

## Next implementation handoff

After the gate passes, build the minimal resources and three-spell prototype described in the
roadmap. Keep environmental Fire and persistent utility Light in its acceptance criteria. Defer
the remaining spells, inventory, full character rig and large region. Include explicit setup and
tests with every implementation handoff.
