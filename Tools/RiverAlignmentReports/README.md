# Shared water and Map 2 vegetation grounding

Map 1 now uses Map 2's existing `PhysicalRiver.mat` and `RiverWater` component on
its main river and outside extensions. Its terrain-fitted mesh and -0.25 m river
level are retained. Refraction uses camera depth and opaque color textures.

The vegetation audit found two separate placement issues in Map 2:

- Four forest trees stood over low/wet banks, with gaps up to 1.846 m. These were
  moved onto dry ground and their trunk colliders were included in a new NavMesh.
- 143 `Patchy canal grasses` objects used a shared mesh at fixed height 1 m.
  Parts of these large patches projected over slopes and water, also leaving
  gaps up to about 1.8 m. Merely lowering the entire patch would bury its uphill
  side, so the mesh is split logically into its 300 connected blade components.
  Each component is fitted to the actual ground; underwater roots are removed.
  The original shared mesh is preserved. Scene-specific fitted copies are under
  `Map02_Village/GroundedVegetation`.

The pass also grounds grass instances in both near and far LODs, and audits
trees, bamboo, shrubs, coconut and banana models. The sampler uses the visible
soil surfaces (including clearing and paddy overlays), plus the actual outside
terrain at the map boundary. It does not assume a flat ground height or sample
building roofs. Rice arrays are preserved.

Use **ShadowVale > Map 2 > Ground all vegetation and canal grass** after terrain
changes. Use **ShadowVale > Map 1 > Synchronize water with Map 2** to regenerate
Map 1 water. Both commands save the scenes. Original scene snapshots are local
under `Library/RiverAlignment` and `Library/Map01WaterRepair`.

Evidence:

- `water-sync.txt`: shared material, wave amplitude and flow speed.
- `vegetation-repair.txt`: audit counts, retained/removed blades, and navigation.
- `vegetation-validation.txt`: actual root samples after reopening saved Map 2,
  including both grass LODs and woody vegetation.
- `map2-before-*.png`: after the initial four tree relocations, before the grass
  and broader vegetation pass.
- `map2-after-*.png`: same north/south shoreline viewpoints after all repairs.

Screenshots are Unity Edit Mode renders. Frame rate was not benchmarked.
