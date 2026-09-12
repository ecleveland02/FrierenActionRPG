# Decisions

Choices made without asking, per the instruction to pick something reasonable and document it.
Each is reversible; the cost of reversing is noted where it matters.

---

### 1. Unity 6000.0 LTS, pinned

Unity 6 LTS is the current long-term release and the one URP toon shading, the Input System and
`AI Navigation 2.x` are all current against. `ProjectVersion.txt` pins `6000.0.32f1`.

*Reversing:* trivial upward within Unity 6. Downgrading to 2022 LTS would mean re-pinning package
versions in `manifest.json`.

---

### 2. Input System package only, and no generated C# wrapper

`Active Input Handling` is set to *Input System Package (New)*, not *Both*. "Both" leaves the legacy
`UnityEngine.Input` API live, which means legacy calls can slip in unnoticed and then break the day
rebinding or gamepad support is added.

The Input System can generate a C# wrapper class from the `.inputactions` asset. This project does
not use it. That file is generated, checked in, and conflicts on every merge where two people touched
bindings; it also couples every consumer to exact action names. `InputReader` resolves actions by
name once at initialisation instead, and everything else depends only on `InputReader`.

*Cost:* a renamed action fails at runtime with a warning rather than at compile time. Mitigated by
the warning naming the missing action, and by there being one file to fix.

---

### 3. A service locator instead of one singleton per system

By Milestone 5 there are save, scene, input, state, combat and AI services that need each other. A
static `Instance` on each makes every one impossible to fake in a test and hides the dependency
graph entirely. `ServiceLocator` is populated in exactly one place - `Bootstrapper` - so there is a
single file to read to know what exists at runtime.

A service locator is still a global. The honest reason to use one here rather than constructor
injection is that Unity instantiates MonoBehaviours itself, so constructor injection needs a DI
container, and that is a bigger dependency than this project should take on at Milestone 1.

*Reversing:* if it becomes a problem, the fix is to inject the few services a system actually needs
through serialized fields or an installer. Nothing in the design blocks that.

---

### 4. Additive scene loading throughout

Including for the "main" gameplay scene. A single-mode load destroys the persistent systems object.
The town → forest → ruins → dungeon flow in Milestone 7 also wants two scenes resident during a
transition. The cost is that `SceneLoader` must track and unload the previous gameplay scene
explicitly rather than getting it for free.

---

### 5. Save entries are per-system JSON blobs

The alternative - one big serializable class listing every system's state - means every new system
edits a shared file, and every save file version needs a migration. Per-system blobs keyed by a
stable `SaveId` mean additions need no migration at all, and a missing key is simply a system at
its defaults.

`JsonUtility` (not `Newtonsoft.Json`) because it ships with Unity and needs no dependency. Its
limits are real and worth knowing: no dictionaries, no polymorphism, no `null` distinction for
value types, and reading a blob as the wrong type yields defaults silently rather than throwing.
`SaveEntryTests` pins that last behaviour down so it is a known quantity rather than a surprise.

*Reversing:* swapping the serializer means changing `SaveEntry` and `SaveService`, not every system.

---

### 6. URP installed but not configured; Built-in pipeline active

The brief says not to build final art before the gameplay loop is proven. A pipeline asset,
renderer features and a toon shader are art work. URP is in `manifest.json` so the switch is a
configuration step rather than a package install, but nothing is wired up yet, and gray-box
primitives render correctly under Built-in with the default material.

*When to revisit:* before any real character or environment art, and before the Milestone 3 rigged
character. Switching later means re-authoring any materials created in the meantime - which is why
placeholder materials should stay at the default one until then.

---

### 7. Content is identified by a stable string `Id`

`IdentifiableScriptableObject` gives every authored asset an `Id` that defaults to its asset name.
Save files, quest conditions and spell unlocks reference that string, never an asset reference or a
build index, so content can be renamed and moved without invalidating saves.

The tradeoff: an `Id` is a content contract. Changing one after saves exist orphans data. The field
is tooltipped accordingly.

---

### 8. Scenes are committed *and* regenerable from code

`Boot.unity` and `TestScene.unity` are checked in so the project works on clone. They can also be
rebuilt by `Frieren > Setup > Regenerate Core Scenes`.

Both, because a scene file is the one asset that cannot be meaningfully reviewed in a diff, and a
corrupted one is otherwise unrecoverable without redoing the wiring by hand. The generator also
documents, in code, exactly what the Boot scene is supposed to contain.

---

### 9. `InputReader.actions` is wired by an editor script, not by the committed asset

A `.inputactions` file is imported by a `ScriptedImporter`, which assigns the `InputActionAsset` a
generated `fileID`. That value exists only in the local `Library` folder, so a committed reference
to it cannot be authored by hand.

`ProjectSetupValidator` assigns the reference once, on first editor load, if it is empty. The
alternative was a manual step in every clone's setup instructions, which is the kind of thing people
forget and then debug for twenty minutes.

---

### 10. Git LFS for binary art; Unity SmartMerge configured for YAML

`.gitattributes` routes FBX, textures, audio and fonts through LFS, and marks Unity YAML as
mergeable with `UnityYAMLMerge`. SmartMerge needs a one-time local config per machine - the command
is in the comment at the top of `.gitattributes`. Without it, two people editing one scene is a
guaranteed conflict.

---

### 11. Layers and tags reserved up front

`TagManager.asset` defines layers for Player, Enemy, NPC, Ground, Interactable, MagicTarget,
Projectile and Trigger. Layer *indices* end up baked into physics masks and prefabs, so reshuffling
them later silently breaks collision filtering. Cheaper to reserve them now than to renumber later.

---

### 12. A pause toggle in Milestone 1

Strictly this is Milestone 2 territory. It is here because it is the only thing that exercises
input → state machine → game reaction end to end, which makes the milestone actually verifiable
rather than merely compiled. It is about fifteen lines in `Bootstrapper` and moves out when a real
pause menu exists.

---

## Milestone 2

### 13. A hand-written camera rig, not Cinemachine

This reverses the recommendation made at the end of Milestone 1. The tradeoff has not changed -
Cinemachine is still the better answer for complex framing - but a blocker turned up: Cinemachine's
component script GUIDs live inside the package, so a committed scene or prefab cannot reference them
from outside the editor. Shipping Cinemachine would have meant shipping a scene that has to be wired
by hand before it works.

`OrbitCameraRig` is about two hundred lines doing one job: orbit, follow, and pull in on collision.
It is not a Cinemachine substitute. Switching later means deleting the component and pointing
`PlayerSpawner` at a Cinemachine target, which is a contained change.

---

### 14. `CharacterController`, not `Rigidbody`

Action-RPG movement wants authored, predictable motion. A physics-driven character fights the
designer on every slope, every step and every knockback, and the usual remedy is to suppress so much
of the physics that the Rigidbody stops earning its place.

The cost is real: forces have to be faked, and there is no free interaction with physics objects.
The decision to revisit is levitation cast on *the player*. Levitating world objects does not force
it; levitating the player does.

---

### 15. Character-level code lives in its own assembly, above the folder sketch

The original structure sketch has `Player/`, `Enemies/`, `Combat/` and so on, with no home for
things a player and an enemy both need. `Frieren.Characters` is that home, and it references
nothing.

Milestone 3 is explicitly "modular character architecture" and Milestone 5 needs a moving enemy.
Writing the motor as player-only and then rewriting it for enemies is the waste this avoids. The
code is identical either way; only the folder differs.

---

### 16. One integrator, enforced by execution order

`CharacterMotor` moves the `CharacterController` in its own `Update` at execution order 100.
Abilities set a velocity; nothing else moves the character.

The alternative - each ability calling `Move` itself - integrates gravity twice the moment two
abilities are active in one frame, and produces a bug that looks like "the character falls faster
while dodging" and takes an afternoon to find.

---

### 17. No serialized `AnimationCurve` or `LayerMask` in committed prefabs

Value fields are deliberately omitted from the hand-authored prefab YAML, on the understanding that
Unity constructs a MonoBehaviour with its field initializers and then overwrites only the keys
present in the file, so an omitted field takes its C# default rather than a zero.

> **This assumption is load-bearing and was not verified when the prefab was written.** It is the
> documented behaviour for `JsonUtility`, but the prefab path uses Unity's native serializer, which
> is a different mechanism. If it is wrong, every tuning value on `Player.prefab` is zero and the
> character cannot move at all. Check it by selecting `Assets/Prefabs/Characters/Player.prefab` and
> reading `PlayerLocomotion`'s Walk Speed in the Inspector: 4.5 confirms the assumption, 0 refutes
> it, in which case every value field has to be written into the YAML explicitly.

That property is only useful if the defaults are safe, which rules out two types. An
`AnimationCurve` serialises as keyframe data, and an empty curve evaluates to zero - a dodge that
silently does nothing. A `LayerMask`'s YAML shape is version-sensitive enough that getting it wrong
means a mask of zero, which is a probe that silently finds nothing. Both failure modes are invisible
rather than loud. So the dodge speed profile is two floats, and every mask defaults to everything
and is narrowed by checking the object rather than the layer.

Narrowing the masks for performance is a tuning pass once the project can be opened and profiled.

---

### 18. Namespaces may shadow banned Unity types, never live ones

`Frieren.Core.Input` and `Frieren.Characters.Animation` shadow `UnityEngine.Input` and
`UnityEngine.Animation`. Both are legacy APIs this project has decided not to use, so inside those
namespaces the shadowing is harmless and mildly protective.

`Frieren.Player.Cameras` is plural for the opposite reason. `Camera` is live API used constantly,
and a namespace named `Camera` makes every `Camera` reference in it a CS0118 error that names the
symbol rather than the cause.

---

### 19. Mouse and stick look input are handled differently

A mouse reports a delta already accumulated over the frame; a stick reports a position that must be
multiplied by delta time to become a rate. `InputReader.LookIsPointerDelta` reports which device the
last look event came from so consumers can branch.

There is a second, less obvious reason the two paths differ. An Input System `Value` action fires
only when its value *changes*, so a stick held at constant deflection stops raising events entirely.
Driving the camera from the callback alone makes it stall mid-turn. The stick is therefore polled
once per frame, while the mouse is read from the callback.

The mouse value is **assigned, not accumulated.** A delta control sums its events within a frame and
resets at the start of the next, so when several mouse events land in one frame each callback
reports the running total rather than its own increment. The last callback of the frame already
holds the whole delta.

*Corrected after review.* This entry originally claimed the opposite - that callbacks had to be
summed or part of a flick would be lost - and the code summed them. That triple-counts a
three-event frame and made the camera turn roughly twice as far as the mouse moved, with the error
scaling with event rate rather than being a constant the sensitivity setting could absorb.


---

### 20. Input callbacks record intent; they never reconfigure input

An Input System action callback runs inside the input pipeline, and changing which action maps are
enabled from in there is not something the Input System guarantees. Pause was doing exactly that:
the callback ran the state transition, and the state transition enabled and disabled maps.

The visible symptom was a game that could be paused and never unpaused, with the time scale stuck at
zero. The first fix - re-enabling the pause action when the UI map came up - was correct in
substance and useless in practice, because it was made from inside the callback.

So an input callback now sets a flag and nothing else. `Bootstrapper.Update` performs the
transition, outside the input pipeline. Any future input that reconfigures maps, opens a menu or
changes control scheme follows the same shape.

`Update` still runs at a zero time scale, so pausing does not prevent the code that unpauses from
running. That is worth knowing before anything else is moved into a coroutine or `FixedUpdate`,
where it would not.


---

### 21. Assembly references are validated by a script, not by eye

Both assistants on this project write C# they cannot compile, so mistakes that a compiler catches in
milliseconds otherwise survive until a human opens the editor. `Tools/Validation/check_assemblies.py`
runs the checks that are worth having without Unity.

The one that justified writing it is CS0012. Using a type whose base class lives in another assembly
requires referencing that assembly, even though the base class is never named in the file.
`SpellDefinition` derives from `IdentifiableScriptableObject` in `Frieren.Data`, so `Frieren.Player`
needs `Frieren.Data` despite never mentioning it. Nothing about reading `PlayerSpellInput.cs`
suggests that.

It also corrects an earlier mistake of mine. In Milestone 2 I removed `Frieren.Data` from
`Frieren.Player` as an unused reference, which was true at the time. Milestone 4 made it necessary
again, and the resulting error surfaced as `'Magic' does not exist in the namespace 'Frieren'` in a
different file - which points at the wrong problem entirely. An assembly reference is not unused
just because no type from it appears by name.


---

## Milestone 5

### 22. Defence is a damage pipeline, not a check inside health

The barrier could have been three lines inside `CharacterHealth.TakeDamage`. It is instead an
`IDamageModifier` that health knows nothing about, because the barrier is the first of at least
five: armour, elemental resistance, the dodge's invulnerability window, damage-over-time reduction,
and whatever equipment turns out to do.

Written the cheap way, the second one is a rewrite of health and the fifth is a method with five
branches in it that every future mechanic has to be threaded through. Written this way, each is a
component that implements one interface and declares where in the order it runs, and health keeps
its single job: apply what reaches it.

The cost is one array and a sort at `Awake`. That is not a real cost.

### 23. No NavMesh for the first enemy

Movement is a direction handed to `CharacterMotor`, the same component the player drives. The
obvious alternative, `NavMeshAgent`, was rejected for one reason: it needs a baked NavMesh, a bake
needs real level geometry, and the only level that exists is a gray-box arena that will be deleted.
Baking against geometry that is about to be thrown away, in an editor I cannot open, is work that
verifies nothing.

Direct steering has a real cost - an enemy will walk into a wall if the player stands behind one -
and that cost is visible and acceptable in a test arena. When there is a level, an agent that writes
to the same motor replaces the steering without touching a single decision in `EnemyBrain`, because
the brain never touches the transform.

### 24. Layer numbers live in `GameLayers`, not in literals

A `LayerMask` is an integer. Insert a layer in Project Settings and every mask built from a literal
silently points somewhere else: no compile error, no exception, no log line - enemies simply stop
seeing the player. That failure is nearly undiagnosable from the symptom.

So the indices and the common masks are constants in one file, and `ProjectSetupValidator` asserts
at editor load that each index still names the layer it claims. This also resolves the awkwardness
in decision 17: serialized `LayerMask` fields are still never hand-written into YAML, but they can
now take a meaningful default from a C# initializer instead of `~0`.

### 25. A magic pulse reaches every receiver on an object

`MagicPulseEffect` originally delivered to the first `IMagicReceiver` found on the target. That was
fine while no object had two. The player now carries `CharacterLevitation` and `CharacterBarrier`,
and the first-wins rule would have made which one worked depend on the order components sit in the
inspector - a rule nobody would guess and no error would report.

Every receiver on the object is now offered the pulse, and each decides for itself. The buffer is
shared and therefore not re-entrant: a receiver must not cast a spell from inside `ReceiveMagic`.
Receivers are meant to be passive, so that is a rule worth keeping rather than an allocation worth
paying for.

### 26. Hand-authored Unity YAML is validated by a script too

`Tools/Validation/check_unity_yaml.py` joins `check_assemblies.py` as a pre-push gate. The scenes,
prefabs and assets in this project are written by generator scripts rather than by the editor, so
the usual safety net - the editor refusing to save something malformed - does not exist.

Every check in it is a mistake already made at least once here: duplicate anchors, a `fileID` naming
nothing, a GUID no asset owns, a GameObject and its component disagreeing about who owns whom, a
transform listing a child that does not list it back, an orphan `.meta`, a missing `.meta`. It also
found a real one on its first run: a `TextArea` string containing a colon, which is not a legal
plain YAML scalar and would have imported as a broken asset.

It only inspects the files this project hand-authors. The URP settings and the Kael art spike's
prefabs were written by Unity and use prefab-variant and stripped-object forms it deliberately does
not model.


---

## Milestone 5.5

### 27. Presentation is an assembly nothing depends on

Feedback could have gone into the components that already exist - a flash in `CharacterHealth`, a
telegraph in `EnemyMelee`. It is a separate assembly instead, and the important property is not that
the code is tidier but that `Frieren.Presentation` is referenced by nothing. The compiler now
forbids a gameplay class from calling into it, in either direction of intent: not deliberately, not
by autocomplete, not in a hurry at 1am.

That buys two things. The whole layer is deletable when real VFX arrive, without touching a gameplay
file. And the placeholder nature is enforced rather than promised - there is no way for a temporary
flash to quietly become load-bearing, because nothing can read it.

The layer also derives rather than demands. Healing is inferred from `CharacterHealth.Changed`
rising past a threshold, not from a `Healed` event added for its benefit. The single exception,
`CastResolved`, exists because the resolved line genuinely cannot be reconstructed from outside the
caster.

### 28. One writer for `Time.timeScale`

Hit-stop is four frames of slowed time on impact. Pause is an indefinite stop. Written the obvious
way they both assign `Time.timeScale`, and then a hit landing just before a pause restores the scale
to 1 when its dip expires - and the game unpauses itself with the menu still up.

This project has already shipped one unrecoverable pause, by a different route (decision 20), so the
second one is worth designing out rather than fixing later. `TimeScaleService` multiplies two
independent inputs: a base scale that only game state writes, and a dip that expires on its own and
restores only its own factor. A dip during a pause changes nothing, because the base is zero.

The arithmetic lives in `TimeScaleState`, plain C# with no Unity types, for a reason specific to
this class: the natural test would write the real `Time.timeScale`, which in edit mode is the
editor's own clock, and a test that fails partway through would leave the editor frozen.

### 29. Feedback is a milestone, not a garnish

The brief says not to build final art before the loop is proven fun. Correct, and followed - there
is still no art here. But Milestone 5 shipped a fight that could not be *judged*: a 0.55 second
wind-up that nothing drew, so the dodge window was a coin flip, and a signature spell that was a log
line and a number.

Placeholder feedback is not art. It is the instrumentation that makes the fun question answerable at
all, and it is cheap - primitives, `LineRenderer`, `MaterialPropertyBlock` and IMGUI, no assets and
no packages. It also turned out to be a diagnostic: the telegraph reports whether the brain reached
its Attack state, which separates a perception bug from a melee-timing one without reading a log.


---

## Milestone 5.6

### 30. Two test assemblies, split by what a test needs

Edit mode takes decisions that resolve in one call; play mode takes anything that needs a frame to
pass. The split is not about coverage taste - it is that edit-mode tests cannot advance time, and
for two milestones running every limitation written down had the same wording: "needs a running
clock, so it belongs in a play-mode test once one is worth setting up." It became worth setting up
when 196 tests existed and none of them had ever seen the enemy exist.

The real argument is not coverage. It is that verifying a milestone meant a six-step manual
checklist ending in "and try to remember whether Ice took one cast or two". A Run button is a better
instrument, and it belongs to whoever is at the keyboard rather than to me.

### 31. Test fixtures are built from code, and assembled inactive

Instantiating `Player.prefab` in a test would break whenever the prefab changed and would be testing
the YAML rather than the behaviour. The YAML has its own checker and its own smoke test; everything
else builds what it needs from scratch.

The inactive part is not style. `CharacterStats.Awake` logs an error when it has no definition yet,
and a logged error fails a Unity test - so adding components to a live GameObject collapses the
whole fixture for a reason unrelated to what is being tested. `FlammableObject` is worse: it reads
its thresholds once in `Awake` to build a `BurnState`, so configuring it afterwards leaves a test
quietly measuring the defaults while claiming to measure something else.

Private serialized fields are reached by reflection through `TestFields`, which throws on a
misspelled name for exactly that reason. A silent no-op is the worst outcome available: a green test
that checks nothing.

### 32. One fixture loads the real scene on purpose

`BootSceneSmokePlayModeTests` breaks every rule above - it boots the actual project - because it is
the only automated check that the hand-authored YAML works. `check_unity_yaml.py` proves a GUID
resolves and a component belongs to the GameObject that lists it. Neither it nor the C# compiler can
say whether `castMode: 1`, typed by hand into an asset, landed in the field someone meant. Only
Unity can, and the cost of it not having is a spell that casts and silently does nothing.

So it asserts content, not just existence: nine spells, unique ids, every one with a non-empty effect
list, Zoltraak a ray, Barrier channelled and draining. And that the door is both lockable and
burnable, which is the multiple-solutions pillar reduced to a single assertion.


---

## Milestone 6

### 33. Milestone 6 became persistence, and the briefed breadth moved to Milestone 7

The brief's Milestone 6 was "reusable environmental interaction systems" - more receiver kinds. That
contract landed early, in Milestone 4, and has five implementations. A sixth would demonstrate
nothing the first five did not.

Meanwhile the save system built in Milestone 1 claimed to persist the game and persisted the
player's health and mana. Burn a crate, freeze a trough, open a door, kill an enemy, save, load, and
every one of them is undone. That is a foundation crack, and it gets worse with every receiver
added - retrofitting persistence across designed content is far more expensive than adding it for
five props.

So the breadth moves into Milestone 7, where more object kinds can be designed around a real space
with a reason to exist rather than added to a list abstractly.

### 34. Save identity is authored, and validated in the editor

Every convenient source of identity is wrong. A hierarchy path breaks the moment something is
reparented or renamed. An instance id is different every run. A sibling index changes when anyone
inserts a prop above it. A name is not unique.

So ids are authored on `SceneObjectId`, and because an authored id has exactly two failure modes -
blank and duplicate - and both are completely silent, an editor check runs on scene save and refuses
them. A blank id means the object never registers and quietly does not persist. A duplicate means
two objects share one save entry, and the symptom is a door that opens because a crate burned, which
is close to undiagnosable from the outside.

### 35. World objects implement a Core interface, not `ISaveable`

`ISaveable` belongs to `Frieren.Save`. Having a burning crate implement it would mean the crate
knows a save system exists, which is the same coupling the whole magic design exists to avoid - a
crate does not know what Fire is either.

So gameplay implements `IPersistentState` in Core, and `PersistentObject` is the single adapter that
speaks `ISaveable` on everyone's behalf. It also gives one entry per GameObject rather than per
component: a door that is locked, burnable and interactive is one thing in the world and should be
one thing in the file. An entry naming a component that has since been deleted is skipped rather
than treated as an error, so dropping a behaviour does not invalidate saves that mention it.

### 36. Timers persist as remaining, never as expiry

`SpellCooldownTracker` stores absolute `readyAt` times against `Time.time`, which is correct at
runtime and wrong on disk: `Time.time` restarts every session, so a saved expiry has either already
passed or sits hours in the future. Cooldowns are written as seconds remaining and rebuilt against
the new clock on load.

Worth writing down because it is not specific to cooldowns. Every timer this project persists later
- a buff, a respawn, a door that closes itself - has the same trap waiting.
