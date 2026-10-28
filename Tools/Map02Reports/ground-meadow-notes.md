# Map 2 ground and meadow refinement

Only Map 2 and new Map02_Village assets are affected.

- Paddy interiors lowered by up to 0.62 m, with a smooth 3 m bank transition. Homestead clearings and canal edges are protected.
- Shared displacement applied to ground, road ribbons, field parcels and all 27,548 rice instances. Ground and ribbon MeshColliders match the new surfaces.
- 36,688 short/tall grass clumps use four shared meshes, GPU instancing and 20 m tiles. Grass has no colliders or shadow casting; lower-detail meshes are selected at distance.
- 44 additional banyans reuse the existing Blender-generated botanical meshes and LOD groups, with small trunk colliders. Existing trees remain.
- 159 dirt ribbons use a local seamless procedural albedo and normal texture.
- NavMesh rebuilt; all 17 route checks passed. Saved-scene reload and grass grounding passed.

Editor source: Map02GroundMeadow.cs and Map02MeadowPolish.cs under Assets/_Project/Map01/Editor (the existing Map 2 builder assembly).

The initial operation is guarded against reapplication. To rebuild the terrain pass, restore Tools/Map02Reports/Map2_before_ground_meadow.unity to the Map 2 scene first; do not reapply displacement on an already modified scene. PolishMeadow can be reapplied to the saved Map 2 scene.

Validation: ground-meadow-checks.txt. Preview images: map02-meadow-path.png, map02-meadow-village.png, map02-meadow-overview.png.
Performance techniques are in place; runtime FPS has not been profiled on target hardware.

## Joined road/bund correction (supersedes the layered surfaces above)

Map02JoinedSoil.cs replaces the road, bund, parcel and yard overlays with paint on a single continuous ground mesh. The 273 old overlay renderers and colliders are disabled, retained only as editable source footprints for mask regeneration. The ground renderer and collider share Joined_Continuous_Soil.asset, so material boundaries cannot expose floating mesh edges.

Ricefield_Gravel_Albedo.png is an original generated pale beige fine-gravel texture, blended through Joined_Soil_Mask.png by Map02JoinedSoil.shader. The mask uses the actual road footprints, including home approaches and junctions; a feathered irregular transition blends gravel into grass. Paddy depression remains in the shared mesh.

All 64,236 grass/rice instances were regrounded to the shared collider. Validation: joined-soil-checks.txt (23,942 local surface samples, 17 route checks, saved-scene reload). Preview: map02-joined-junction.png, map02-joined-path.png, map02-joined-village.png. Backup: Map2_before_joined_soil.unity. Use Join road and paddy banks for future regeneration; the older PolishMeadow workflow is superseded.
