# Map 1 tropical expansion

The saved scene is `Assets/_Project/Scenes/Maps/Map 1.unity`.

- Perspective shoulder camera: 62 degree FOV, mouse look, center reticle, obstacle avoidance, camera-relative WASD. Escape, inventory and map release the cursor.
- Three additional timber houses / enemy outposts with six additional guards, crates and barricades.
- Three gently sloped mesh hills with collision.
- 374 tall-grass cells across eligible land, with alternating coconut palms and banana plants. Grass provides crouched concealment through the existing mission system. Mission paths and spawn clearance are reserved.
- Vegetation uses shared leaf meshes, instanced materials and distance culling; grass is batched by cell without physics colliders.

The main optimized map build now applies this expansion automatically. To regenerate just the expansion, use **ShadowVale > Map 1 > Apply Tropical Expansion** with the scene saved and Play mode stopped. Generation uses a fixed seed and replaces its own named root. The expansion NavMesh is stored separately from the base map bake.

Validation completed in Unity 6000.3.24f1: original mission routes and new patrol loops are connected; all guards initialized; mission progression through supplies, bridge, evidence and delivery with Hung passed. The mission flow check teleports between objectives; it is not a manual end-to-end traversal or performance benchmark.

Reports: `Tools/Map01OptimizedReports/expansion-summary.txt`, `routes.txt`, `playcheck-result.txt`. Shoulder-camera render: `third-person.png`.
