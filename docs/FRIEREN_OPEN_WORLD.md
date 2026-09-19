# Frieren Open World Foundation

Latest pass: `open world v5 - layered meadows and tall grass`.
Reference inspected in Unity: Polytope `Lowpoly_Demos/Environment_Free/Environment_Free.unity`.
Its instanced short-grass, high-grass and poppy helper prefabs are now used in noise-shaped patches
across temperate terrain. Roads, village, water, steep slopes and high mountains are excluded.
Grass density uses two scales of variation, with sparse flowers between taller stands. The supplied
grass material retains its wind shader. Terrain blends two grass surfaces and adds subtle lowland
relief; source vendor assets are unchanged. A ground-level preview is available through
`WorldFoliagePass.PreviewGround`. GPU instancing and limited detail draw distance bound visible work;
frame rate has not been profiled in player movement.

Current pass: `open world v4 - forest river and village trails`.
Roads are now blended dirt terrain layers, with a higher-resolution village tile instead of raised
road ribbons. The east river has a carved channel, a descending water surface, animated ripples,
and a timber footbridge. This is visual water, not a swimming or fluid simulation system.
A roughly 1.4 by 1.3 km woodland west/northwest of Eldenbrook uses terrain-instanced trees normalized
to 13 m broadleaf / 16 m pine source heights, with size variation, a trail, a clearing and undergrowth.
Tree and grass placement excludes roads, the river and the village square. The old invisible spawn
platform is removed; the actual TerrainCollider supports the player.
The preview command also checks terrain colliders, total tree population, the local channel bed and
spawn alignment, and exports village, forest and bridge images to `ArtSource/WorldPreviews`.
These checks do not replace a play-mode traversal or performance profile.

Eldenbrook starter village: six exterior cottages (one designated as the inn), a stone well,
benches and barrels around an open square. The central site is levelled with a gradual hillside
transition. Kael spawns at the south side of the square. Village homes have blocking wall colliders
and closed doors; interiors, inhabitants and shops are not yet implemented. The nearest river now
bypasses the village to the east. Build stamp: `open world v3 - Eldenbrook starter village`.

Mountain pass: build stamp `open world v2 - rocky mountain ranges`. Existing northern and western
ranges now include ridged relief and pointed northern summits. Stone splats use slope as well as
altitude, with 90 m rock texture tiling readable from mountain viewpoints and restrained normal strength. Snow collects above a
variable snowline and recedes on steep faces. Foothill scree clusters use the installed rock prefab.
Terrain trees and newly placed scenery sample the saved heightfield for placement. The player camera
can see terrain up to 6.5 km away. This is a visibility setting, not a measured performance result.
Rebuilds save a dated copy of the previous scene in `ArtSource/WorldBackups`; terrain assets are
regenerated in place, so those scene copies alone do not restore earlier heightmaps.

Build with **Frieren > World > Build Frieren Open World (12 km)**. It creates
`Assets/Scenes/FrierenOpenWorld.unity`: nine adjacent 4 km terrain tiles, or 144 km² / 55.6 square
miles. Terrain is the world-scale foundation, not a claim that all of it is fully dressed yet.

The layout takes only broad geographic cues from the supplied map: snowy northern ranges, northern
and central forests, an eastern coast, western highlands and southern drylands. Low-poly trees use
terrain instances, which are substantially cheaper than thousands of individual prefab GameObjects.

The ocean sits at 21 m. The mainland intentionally falls below it at the outer terrain edges, giving
the first pass a continuous coast. Regional markers identify future region anchors. The scene is
standalone for now: it deliberately does not replace Boot or the Watchtower vertical slice. It does
include the project's standard `PlayerSpawner` and third-person camera at the Central Meadow marker,
so pressing Play while this scene is open spawns Kael into the terrain. Editor play mode loads Boot
additively for its services.

This is not yet a streaming system. Eldenbrook and its surroundings are the first dressed region;
quests, inhabitants and encounter placement are not implemented across this world.
