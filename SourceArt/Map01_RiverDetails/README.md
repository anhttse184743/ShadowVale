# River details revision

- Source: `SourceArt/Map01_RiverDetails/RiverDetails.blend`; reproducible with Blender 5.2 via `build_details.py`.
- Apply to the open Map 1 scene: **ShadowVale > Map 1 > Apply river and bunker details**. The importer preserves existing work and takes a scene backup before updating.
- Boat is a separate object at the river surface sampled from the water mesh (Y = 0.05), with its keel immersed and gunwale above water. It has planks, ribs, seats, floorboards and a paddle.
- 586 existing rock instances use three shared, 2,180-triangle meshes with moss patches and trailing leafy vines. Existing instance bounds are preserved.
- Bunker interior footprint and spawn remain connected to the entrance. A continuous earth mound covers the roof; the approach has stone cheeks, moss and sandbags.
- Nearby uncollected Supplies/Loot points get a pulsing gold screen-space arrow within 32 metres; markers hide on collection and while paused, in inventory or on the map.
- Unity validation: successful import and complete NavMesh exit path. Play-mode checks passed grounded spawn, walking out of the bunker, bridge railing collision, sliding along the railing and wading slowdown.
- Backups and rendered previews are under `Tools/Map01OptimizedReports`.

September 22 follow-up: separate terrain-fitted mound with 938 grass clumps; perimeter submerged 0.14m into sampled terrain. Original north jetty/canoe source vertices are exported by inspect_chunk.py and used to remove only their geometry from visual/collision chunks. Northern jetty and rebuilt boat are independent objects at z=85/90, with dedicated collision and bank navigation checks.
