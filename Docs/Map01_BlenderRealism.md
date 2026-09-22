# Map 1: Blender tropical environment refinement

Open `Assets/_Project/Scenes/Maps/Map 1.unity`.

## Delivered scene changes

- Daylight skybox with drifting procedural clouds, light haze and warmer direct sun.
- Blender-authored broadleaf forest trees with flared roots, coconut palms with curved trunks and feathered fronds, torn banana leaves, curved grass blades and timber houses.
- All 15 old baked palms (150 trunk/frond objects) removed from static environment chunks. The additional inland expansion palms are replaced with 25 coconut palms placed along both riverbanks, reserving the bridge and exit.
- Older village/base house side walls now have horizontal timber courses and windows. New outpost houses use a shared two-LOD detailed mesh while retaining their gameplay collision.
- Animated river shading combines irregular surface ripples, Fresnel sky tint, bank coloration, sun highlights and existing scene shadows. It uses an opaque surface with no real-time planar reflection/refraction pass.
- Grass cells that overlap the river are removed together with their concealment points. The remaining cells preserve crouched concealment.
- Trees/loose props immediately around new houses are cleared to keep their entrances usable. Original mission routes and guard loops are checked after the navigation bake.

## Reproducible Blender source

`SourceArt/Map01_Realism/TropicalAssets.blend` is the editable shared asset library.
`SourceArt/Map01_Realism/Map01_RefinedEnvironment.blend` contains the refined static environment.

Run Blender in background mode with `build_assets.py`, then `refine_static.py` from that directory. Both scripts resolve the project root relative to their own location; neither requires an external download. They export compressed mesh data alongside the .blend files. Blender's thumbnail-cache write warning in this sandbox does not affect the saved .blend files or mesh exports.

In Unity, use **ShadowVale > Map 1 > Apply Blender Tropical Realism** after saving the scene. The main optimized-map build also applies the expansion and this refinement automatically when the source exports are present.

## Rendering budget

Five forest variants share three LODs: 3,520 / 1,096 / 284 triangles per tree. The distant tree mesh is 29% smaller than the previous 400-triangle LOD. Near trees intentionally have more modeled detail.

Coconut LODs: 3,710 / 1,806 / 746 triangles. Banana: 936 / 196 / 108. A grass cell combines 16 tufts and uses one of three shared patch variants, with 8,192 / 2,048 / 896 triangles per cell. Houses: 3,048 / 1,176. Distance culling is enabled; distant foliage and all grass omit shadow casting. Repeated assets use instanced materials and shared meshes rather than per-object material copies. Static world chunks remain spatially divided.

The timing report measures CPU submission of `Camera.Render` inside the Editor, not GPU completion or actual gameplay FPS. A target-device standalone build still needs profiling before making a frame-rate guarantee.

## Verification and previews

- `Tools/Map01OptimizedReports/realism-summary.txt`: mesh counts, riverside positions and navigation validation.
- `Tools/Map01OptimizedReports/routes.txt`: original mission routes.
- `Tools/Map01OptimizedReports/playcheck-result.txt`: runtime mission initialization and progression test (teleports between objectives).
- `Tools/Map01OptimizedReports/realism-render-timing.txt`: bounded Editor render-submission measurement.
- `realism-player.png`, `realism-river.png`, `realism-house.png`, `realism-canopy.png` in the same report directory: Unity render captures.

This is a procedural game-ready interpretation of the supplied photographic references, not a photogrammetry asset set.
