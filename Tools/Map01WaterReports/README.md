# Map 1 river shoreline repair

The old surface was a fixed 12 m strip. The current `StageTerrain` banks meet the
water level farther out, leaving strips of exposed riverbed beside that mesh.

The repair samples the actual terrain collider every 0.5 m along the playable
river, finds both intersections with the existing -0.25 m water level, and extends
the rendered surface 2 m beneath each bank. UV2 holds signed water depth. A thin
terrain-following wet bank overlay softens the shore. Terrain, navigation,
colliders, bridges and the water level are unchanged.

The outside river is also fitted to its ground mesh. Both surfaces now use the
same `Map02_Village/PhysicalRiver.mat` and `RiverWater` simulation as Map 2,
including 0.055 m wave amplitude and 0.65 flow speed. Only this scene uses the
fitted meshes and wet bank assets under `Map01_Realism/Shoreline`.

To rebuild after a terrain edit, open Map 1 in Edit Mode and select
**ShadowVale > Map 1 > Repair river shoreline**. This saves the current scene and
regenerates the dedicated shoreline assets. The first scene backup is kept
locally in `Library/Map01WaterRepair/Map1-before-water.unity`.

Validation: `validation.txt` records 96,494 terrain samples covering the channel,
minimum bank clearance and shader checks. `before-*.png` and `after-*.png` are
Unity renders from the same three viewpoints (z = -65, 0, 65). They are Edit Mode
renders; gameplay and frame rate were not benchmarked.

The additional menu **ShadowVale > Map 1 > Synchronize water with Map 2** opens
Map 1 and rebuilds its fitted river with the shared material and simulation.
The material is shared intentionally: editing it affects both maps. Depth and
opaque color textures are enabled on the gameplay camera for refraction. The
overlap beneath the banks exceeds the maximum wave displacement, so animation
cannot expose the mesh boundary.

Map 1 also has 50 fitted interaction sensors using Map 2's `RiverWaterTrigger`.
They emit ripples when a CharacterController enters or moves through the water.
The water repair/synchronization command recreates these sensors automatically;
**ShadowVale > Map 1 > Enable footsteps and river interaction** installs them
without rebuilding the water mesh. `interaction-coverage.txt` records coverage
and `character-ripples.txt` records the isolated Play Mode character test.
