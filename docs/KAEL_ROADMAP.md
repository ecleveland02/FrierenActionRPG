# Kael RPG: current direction

Condensed from the owner's roadmap supplied on 2026-09-11. This updates the earlier prototype
scope without renaming serialized types, assemblies, assets or the GitHub repository.

## Vision

A non-commercial, third-person, single-player 3D fantasy action RPG with original protagonists
and story. Inspiration is the tone and worldbuilding of Frieren, not dependence on its canon.
**Magic is a tool, not just a weapon.** Exploration, investigation, environmental interaction
and creative quest solutions matter alongside real-time combat. Offer multiple solutions.

## Protagonist and opening

Kael is a 20-year-old human apprentice mage from Eldenbrook. Curious, kind, observant, slightly
sarcastic and initially unsure in combat, he travels at Master Orin's urging with a spellbook,
*A Collection of Obscure Magic*. He has no prophecy, famous bloodline or legendary equipment.
His idea of greatness grows from power and recognition toward understanding, patience and service.

Eldenbrook can eventually include a square, inn, shops, shrine, workshop, farms and nearby forest.
The proposed opening introduces Orin, travel, a fallen tree that can be burned or bypassed, a weak
monster and a ruin with mysteries that reward returning with new spells. This is future content.

## Immediate scope and acceptance

1. Open and compile the existing Unity foundation. Run EditMode tests and validate Boot, movement,
   camera, interaction, pause and the existing save probe in Play mode.
2. Add only necessary character resources and the modular magic prototype after that gate passes.
3. Prove three spells: Basic Magic Projectile, Light and Fire. A projectile can hit a damageable
   target; Light provides persistent illumination and a utility interaction; Fire damages a target
   and ignites an environmental object. Include mana, targeting, casting/cooldown rules and hooks
   for presentation. This is an acceptance target, not a claim of implemented behavior.
4. Then add one enemy, basic combat and a small gray-box ruin with a completable objective.

Use ScriptableObject spell definitions with stable IDs, editable casting/targeting data and a list
of reusable effects. Separate delivery (such as projectile travel), effects and target capabilities.
Avoid an isolated hardcoded class for every named spell. Keep combat, environmental and information
effects equally supported. Runtime state belongs to the caster/target, not shared spell assets.
Expose VFX/audio/animation hooks without building their full systems now. Use existing input,
action-lock and animation seams where appropriate.

## Later scope

Barrier, Water Creation, Ice, Levitation, Object Repair, Unlock and Detection are deferred. Mastery
should improve flexibility, precision, cost, duration or new applications rather than only damage.
Inventory, progression, dialogue, quests, additional regions and coordinated multistage spell audio
follow proven foundations. Starting supplies remain placeholder design data: staff, book, backpack,
three herbs, two days of food, flask, 20 gold, map and Orin's letter.

## Asset direction

Kael is about 1.78 m tall, lean and lightly athletic, with youthful features, gray-blue eyes and
messy dark-brown hair. Practical worn clothing: navy knee-length coat, muted gold/cream trim,
light undershirt, charcoal trousers/gloves, brown boots and shoulder satchel. His slightly crooked
wooden staff has a small blue crystal and is a separate swappable mesh, a gift from Orin.

Use Blender and an FBX export pipeline. Keep a placeholder player through foundation validation.
A later humanoid rig supports body, fingers/toes, staff attachment and limited coat/hair bones;
cloth simulation and final animation are not prerequisites. Introduce small props before investing
in the complete character. Match the current Built-in rendering pipeline until a deliberate change.

## Delivery expectations

Preserve working systems; keep components modular and data editable. At each implementation
milestone report changed files, key components, Inspector/scene setup, test results/instructions,
limitations, discovered risks and the next milestone. Build a small system, prove it, then expand.
