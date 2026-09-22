# Map 1 — current collision, trail, house and grass revision

The revision is saved in `Assets/_Project/Scenes/Maps/Map 1.unity`.

- The intelligence map table now has a solid box matching its tabletop/under-table volume. The shelter back wall uses one continuous low-friction box instead of the old triangle shell. The controller can slide along it and move away.
- Trails are vertex colors on the terrain, with blended edges. Raised path/clearing triangles have been removed; the walking terrain has no added trail height.
- Eight houses use the Blender-authored shared palm-thatch prefab, with a layered roof, irregular hanging leaf fringe, bamboo walls, an open veranda, seats, visible entry steps and an unobstructed central doorway. Near/far LODs are provided. Original village house visuals and redundant floor/wall collisions are replaced locally.
- The existing 4,621 groundcover placements use shared mixed-height meshes: three overlapping tall/short/medium groups nearby, two at middle distance and one far away. Foliage overlapping expanded verandas is excluded. Grass casts no shadows and has no collider.

The work leaves the existing Space-jump and shallow-water movement implementation intact.

Rebuild the house asset with Blender background mode and `SourceArt/Map01_Optimized/build_house.py`. Editable source: `SourceArt/Map01_Optimized/Map01_PalmHouse.blend`. Apply the incremental scene revision through **ShadowVale > Map 1 > Apply Latest Paths Houses and Collision**. This operates on the current Map 1 scene; the older full-map generators do not automatically preserve this last incremental pass.

Checks are recorded in `Tools/Map01OptimizedReports/latest-checks.txt`: a CharacterController with player dimensions is driven into the table, then into/along/away from the wall, and the original navigation routes are verified. `playcheck-result.txt` records the separate Play-mode mission flow test. `latest-summary.txt` records the most recent apply; removal counts are per run, so a repeat reports zero already-removed triangles.

Unity previews: `realism-house.png` and `realism-player.png` in the reports directory. These are captures of the scene, not Blender concept renders. No standalone-build FPS benchmark was performed.
