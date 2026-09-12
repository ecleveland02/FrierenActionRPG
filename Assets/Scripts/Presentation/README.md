# Frieren.Presentation

Everything in here reacts to gameplay. Nothing in here is read by gameplay.

That is enforced by the compiler, not by discipline: this assembly references Core, Characters,
Magic and Enemies, and **no assembly references it back**. So no combat code can call into a flash,
a tracer or a shake even by accident, and the whole layer can be deleted or replaced wholesale when
real VFX arrive without a single change to a gameplay file.

Everything subscribes to events that already existed - `CharacterHealth.Damaged`,
`CharacterBarrier.Broke`, `EnemyMelee.SwingStarted`, `CharacterSpellcaster.CastResolved` and so on.
If a component in here is missing or switched off, the game plays exactly as it did before; it just
says less about what is happening.

These are placeholders: primitives, `LineRenderer`, `MaterialPropertyBlock` and IMGUI. No art
assets, no packages, no particle systems. They exist so the combat loop can be *judged* - a 0.55s
wind-up you cannot see is not a dodge window, it is a coin flip - not so it can look good.
