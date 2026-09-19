# Kael movement v2

Build stamp: `kael movement v2 - stamina and humanoid locomotion`.

The existing Player visual uses `Assets/Art/Characters/Kael/KaelMovement.controller`.
The setup command is **Frieren > Kael > Set Up Responsive Movement**. The older Copy Controller
and Imported Model menu entries redirect here. This command retains the currently assigned model,
requires a valid Humanoid Avatar and verifies imported clips against its bones before assigning.

- WASD / left stick: camera-relative movement, running at full input (4.5 m/s).
- Hold Left Alt: walking (2 m/s). Partial stick input also reduces speed.
- Hold Left Shift / left stick press: sprint (7 m/s).
- Sprint drains 18 stamina per second from CharacterStamina, whose maximum and recovery are authored
  in CharacterStatsDefinition (defaults: 100, 25/second, 0.8 second recovery delay).
- Exhaustion requires releasing sprint and recovering at least 20% before sprinting again.
- Lock-on preserves directional strafing; sprint faces the movement direction.
- Turning follows input at up to 1080 degrees/second. Stationary turns blend the turn clips using
  actual body rotation; moving turns remain responsive without waiting for a one-shot animation.
- CharacterMotor remains the only movement integrator. Root motion is disabled.

The controller covers ground locomotion. Jump, dodge, combat and landing choreography are separate
follow-up animation work; their existing gameplay scripts remain in place.

Import repairs: each locomotion FBX creates its own Humanoid Avatar, avoiding the empty copied-avatar
reference on Breathing Idle. Filenames are matched exactly so Running cannot select Running Backward.
The walking-as-idle fallback and broad model search are no longer used by the setup commands.

Verification: Unity 6000.0.32f1 batch build compiles and samples the clips on the current player.
`Logs/KaelMovementBuild.log` and `Logs/KaelMovementTests.xml` contain local results.
Check actual movement feel in Play mode; automated pose checks do not establish foot-contact quality.
