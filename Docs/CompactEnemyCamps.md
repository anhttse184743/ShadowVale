# Compact enemy camps

Target: Assets/_Project/Scenes/Maps/Map 1.unity.
Replaces only Field camp models under Enemy outpost 1/2/3. Camp roots, mission
anchors, six guard identities and quest references are preserved.

Footprint approximately 12 x 10 metres: one smaller command tent and planning
table, radio, small supply tarp, cooking fire, two short entrance sandbag walls.
The old sleeping tents, long perimeter and wide plank courtyard are removed.
Shrubs reclaim some old clearing edges; approach corridors remain open.
Two guards per camp patrol a compact rectangle. Scene-specific visionRange is
14 metres and attackRange 12 metres. This does not globally modify enemy AI,
chase behaviour, difficulty multipliers or other guards.

Builder: CompactEnemyCamps.Build (refuses to overwrite existing generated assets).
Validation checks every guard's patrol and navigation from the base to B/C1/C2/C3.
Old save files may restore earlier guard positions until a new game is started.

Validation passed: six patrol paths and five story anchors connected; BinocularsLogACampOnlyWhenAimedAtIt passed in Unity Play Mode (1/1). Screenshot is an Editor camera render, so guard animation is not playing.
