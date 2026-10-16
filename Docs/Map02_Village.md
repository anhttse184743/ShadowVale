# Map 2 — Ap Bac reference layout

Scene: `Assets/_Project/Scenes/Maps/Map 2.unity`.

Updated from the supplied reference image, including the follow-up requirement that houses sit **among the rice paddies**. The footprint remains 220 × 200 metres. This is an environment layout, not a historical reconstruction or playable mission.

- Fourteen existing bamboo/thatch and stilt houses are distributed on dry homestead islands inside twenty rice plots. Eight homes occupy the western fields and six the eastern fields. Each has a footpath to a field lane.
- A 10.5-metre-wide meandering canal replaces the straight western river. Wooden crossings occupy the central and southern lanes. Canal-bank paths connect the two evacuation landings.
- A northern clearing represents the helicopter landing area. Sandbag positions sit in the northeast and southeast. These are environment props and design markers; no actors or helicopters are added.
- Palms, bank planting, peripheral orchards, raised bunds and earth roads frame the fields. Existing houses, interiors and workyard props are retained. Story markers remain design-only.

## Generation and validation

`Assets/_Project/Map01/Editor/Map02ReferenceLayout.cs` extends the original builder. Use **ShadowVale > Map 2 > Apply Ap Bac reference layout** on the original village scene. The operation backs up the scene before modifying it and refuses to apply twice, protecting subsequent manual edits. Restore the backup to regenerate from the old layout.

The revised terrain, rice rows, water mesh, materials and baked navigation data are separate `Reference_*` / `Map02_Reference_NavMesh` assets in `Assets/_Project/Art/Environment/Map02_Village`.

Review renders and navigation results are in `Tools/Map02Reports/map02-reference-overview.png`, `map02-homes-in-fields.png` and `reference-checks.txt`. Navigation checks cover the central crossing, southern crossing and northern western route. They do not validate mission logic, which remains unimplemented. Campaign routing and build settings are unchanged.

## Natural village revision

The latest scene follows the additional rural-painting references: winding earth lanes, unequal curved field boundaries, softly irregular homestead yards, differently angled houses, shade-tree groves, banana clumps, haystacks, partial bamboo fences and patchy waterside planting. All fourteen homes remain surrounded by rice fields. The continuous deformation preserves the canal/terrain alignment and the original map boundary. New assets use the `Natural_` prefix; shared Map 1 materials and meshes are not modified.

The editor command **ShadowVale > Map 2 > Naturalize village and field paths** in `Map02NaturalLayout.cs` upgrades the reference scene once and saves a `Map2_before_natural_*` backup. Restore that scene before regenerating; it refuses to apply the deformation twice.

Unity compilation and generation completed successfully. `natural-checks.txt` records seventeen complete navigation routes: three cross-map destinations plus approaches to all fourteen homes. Review images: `map02-natural-overview.png`, `map02-natural-village.png`, `map02-natural-canal.png`. These supersede the earlier reference screenshots.

## Blender botanical revision

Editable Blender source: `SourceArt/Map02_Village/Map02_Botanical_Models.blend`; deterministic authoring script: `build_botanical.py`. Run with Blender 5.2 in background mode. The script also exports indexed vertex-colour meshes to `Botanical.meshdata.json.gz` and records the mesh budgets in `botanical-budget.json`.

- Separate ripe golden rice with drooping grain heads and upright green young rice; two mesh detail levels per plant type.
- Twenty paddies receive a seeded shuffle of ten mature and ten young crops. 27,548 clumps replace the prior 8,452 (3.26× the clump density). Home yards and access paths remain clear.
- Thirty-eight old shade trees are replaced with banyans featuring fused grey trunks, spreading roots, branching crowns and hanging aerial roots.
- Exactly three royal poincianas use scarlet/orange flower clusters, fine leaflets and broad crowns. Their positions are randomly selected from eligible riverbank/village locations, with clearance checks against trunks and large banyan crowns.

`Map02BotanicalLayout.cs` provides **ShadowVale > Map 2 > Apply Blender rice banyan and flamboyant**. It backs up the natural scene, refuses duplicate application and imports only scene-specific `Botanical_*` assets. Restore a pre-botanical scene to regenerate.

`VillageRiceInstances.cs` renders shared rice meshes in batches of up to 1,023 GPU instances in both edit mode and play mode under the project's URP pipeline. It uses the detailed mesh near the camera and a reduced mesh farther away. Plant matrices remain serialized in the scene; no multi-million-vertex combined crop mesh is stored. Rice casts no individual shadows. Tree LODs and trunk colliders are retained.

Validation: Unity compilation and generation pass; all seventeen navigation routes pass, including the two bridge destinations and approaches to all fourteen homes. A save/reopen check verifies twenty instanced fields and all 27,548 plants. Reports and screenshots use `botanical-checks.txt`, `map02-botanical-overview.png`, `map02-botanical-village.png`, `map02-banyan.png`, `map02-flamboyant.png` and `map02-rice-close.png` in `Tools/Map02Reports`.

## Clustered village and connected parcels (2026-10-01)

The current revision supersedes the earlier dispersed homestead arrangement. Fourteen existing houses form a western village around a common and two connected lanes. Twenty-five unequal polygonal parcels occupy the eastern bank and the land north and south of the village. The shared deformation curves their boundaries alongside the existing canal. Mature golden crops dominate, with young green parcels interspersed.

`Map02ClusterLayout.cs` provides **ShadowVale > Map 2 > Cluster village and reshape rice parcels**. It backs up the input scene to `Tools/Map02Reports/Map2_before_cluster.unity` and rejects repeat application. Restore that backup before regenerating. Existing house prefabs and nearby household props are relocated together; the ground, crop planting and navigation are rebuilt for the new arrangement. Render and collision geometry share `Cluster_Continuous_Terrain.asset`.

Review images: `map02-cluster-overview.png`, `map02-cluster-village.png`, and `map02-cluster-plan.png`. Generation checks are recorded in `cluster-checks.txt`, including house approaches, both cross-river routes, and serialized crop counts after reopening the saved scene.

## Blender roadside planting (2026-10-01)

The clustered layout now includes 28 flared bamboo clumps at roadside field edges, 33 additional small roadside trees, and 161 low shrubs forming interrupted hedgerows along parcel boundaries. Dry-ground grass increases from 10,556 to 85,134 clumps. House approaches, the common and the two bridge routes remain accessible.

Editable source: `SourceArt/Map02_Village/Map02_Roadside_Details.blend`. Run the self-contained `build_roadside.py` with Blender to regenerate the two mesh detail levels for bamboo, shrubs, roadside trees and meadow grass. `Roadside.meshdata.json.gz` carries indexed, vertex-coloured geometry into Unity; `roadside-budget.json` records mesh costs. Bamboo has segmented leaning culms, raised nodes, upper branches and narrow hanging leaves. Grass uses instanced batches; trees and bamboo use LOD groups and small base colliders.

`Map02RoadsideLayout.cs` exposes **ShadowVale > Map 2 > Add Blender roadside bamboo and lush banks**. It requires the clustered scene, saves `Map2_before_roadside.unity`, and refuses duplicate application. Curved lane splines and blended junctions share one cached road-distance field for soil paint, planting clearance and terrain shaping. A dedicated mask and material add fine earth texture without floating road meshes. Existing house geometry is preserved.

Validation: all 14 house routes and both cross-river routes pass. Saved scene reload preserves all 85,134 meadow instances. See `Tools/Map02Reports/roadside-checks.txt` and the four `map02-roadside-*.png` renders. Runtime frame rate has not been benchmarked; the denser vegetation uses more rendering work despite instancing and LODs.

## Physical river (2026-10-02)

The water implementation uses a bounded wave-height surface, matching CPU buoyancy and shader wave equations. `RiverWater` supplies channel sampling, current velocity and a bounded ripple buffer. `RiverBuoyantBody` applies displaced-volume lift at four hull points, drag and an optional soft mooring. `RiverWaterTrigger` detects eligible rigidbodies and character controllers at water level; bridge traffic is rejected by height.

`Map02PhysicalRiver.cs` installs the water mesh and material, enables depth/opaque textures on Map 2 cameras, adds interaction volumes and converts the two existing canal boats to buoyant moored rigidbodies. The operation retains a pre-water scene backup. The bounded-water, current-driven drift, wave/mooring stability and ripple-buffer checks passed; results are in `Tools/Map02Reports/physical-river-checks.txt`. This is surface-based game water, not a volumetric fluid solver, and does not add swimming controls.

An isolated Unity 6000.3.24f1 Play Mode run also passed for 12 seconds: a dropped Rigidbody acquired buoyancy through a real trigger callback, floated and drifted 5.19 metres downstream; two hull proxies using Map 2 boat mass, collider dimensions and float points stayed near their moorings; interaction and wake ripples populated the bounded buffer. See `Tools/Map02Reports/river-runtime-checks.txt` and the reusable project in `Tools/WaterRuntimeValidation`. This validates runtime physics independently of the main project's startup menu, not a full Map 2 gameplay run. The earlier `river-play-checks.txt` failure records startup routing to the menu instead of Map 2. The new **ShadowVale > Map 2 > Play water preview (skip main menu)** route still needs verification in the main editor.

The saved scene render is `Tools/Map02Reports/map02-physical-river.png`. Vegetation and the village layout remain in the scene; the water shader adds waves, depth tint, reflected light and interaction ripples.

### Closed shoreline (2026-10-02)

The original water strip stopped short of the sloped banks. `RiverShorelineMesh` now extends the rendered surface three metres beyond each physics bank, underneath the opaque terrain. The saved `PhysicalRiver_Surface.asset` is updated in place with the same GUID; regeneration uses the same helper. The channel center, water height, physics sampling bounds and boat settings are preserved.

The isolated Unity geometry check sampled 3,200 points along the expanded edges against the current `Roadside_Continuous_Terrain` mesh. Every sample is covered by terrain, with at least 0.694 m clearance above the highest possible wave crest. See `Tools/Map02Reports/shoreline-checks.txt`. The original mesh is backed up as `PhysicalRiver_Surface_before_shoreline.asset`. The earlier screenshot predates this fix; this validation checks geometry rather than the main editor's rendered view.

## Surrounding landscape and sky (2026-10-02)

Map 2 now has a separate `03 Surroundings • Map 2 landscape` root extending roughly 700 metres beyond its playable footprint. North (+Z) has wooded hills with a river valley suggesting the route toward Map 1. East and west have continuing yellow/green rice parcels and tree belts. South (-Z) has a widening canal mouth opening into a broad river, with a distant opposite bank. This is a visual geographic connection to Map 1; no scene-transfer trigger is added.

The scenery contains 1,160 simple merged distant trees and 22,028 rice clumps in spatial mesh groups. The tropical cloud sky and distance fog replace the empty background; the scene camera's far plane is 1,800 metres. Scenery has no colliders and is outside the playable environment's NavMesh hierarchy. Existing house, planting, path, water-physics and navigation records remain unchanged.

`Map02Surroundings.cs` exposes **ShadowVale > Map 2 > Build surrounding landscape and sky**. It reads the actual half-metre terrain perimeter, preserves all 1,680 edge heights exactly, and saves generated assets under `Map02_Village/Boundary`. Rebuilding replaces its own root and updates assets in place. Save manual scene edits first. The original scene is backed up at `Tools/Map02Reports/Map2_before_surroundings.unity`.

Compilation, generation, repeat generation and three URP review renders passed in an isolated copy of the project before installation. `Tools/prepare_map02_boundary.py` prepares the dependency snapshot; `Tools/install_map02_boundary.py` checks that the main scene did not change during authoring before installing the result. Reports: `surroundings-checks.txt` and `surroundings-install-checks.txt`. Review images: `map02-boundary-overview.png`, `map02-boundary-north.png`, `map02-boundary-south.png`. Runtime performance of the expanded scenery has not been benchmarked.

## Player exploration from the boat landing (2026-10-02)

The saved scene now includes `04 Exploration • player at boat landing`: the existing Player prefab and animation controller, a spawn marker on the bank end of the northern timber landing at approximately (-6.91, 1.31, 67.17), and a third-person camera. The overview camera and its audio listener are disabled. Combat/hotbar behaviours are disabled on this scene instance; shared player assets are unchanged.

Open **ShadowVale > Map 2 > Play from boat landing** to reload the saved Map 2 and enter Play Mode directly. Normal Play also starts Map 2 when its exploration scene is open. Other scenes keep the original Boot/menu routing. `ForestMenu` does not overlay the exploration rig. This mode does not create a Map 1 mission or alter its saves.

Controls: WASD or arrow keys move, Shift toggles sprint, Ctrl/C toggles sneak, Space jumps, mouse looks, wheel zooms, Escape releases the pointer and clicking captures it again. R returns to the landing. Falling below the water or leaving the playable footprint also resets to the landing; swimming is not implemented.

`Map02PlayerSetup.cs` builds the rig once. `Map02PlayerPlayValidation.Run` is a batch-only checker that enters actual Play Mode, injects a virtual keyboard, checks grounded spawn, jumping above 0.5 m, walking beyond 2 m, driven animation, R/reset recovery and a single active camera/listener, then exits Unity. The virtual keyboard must be explicitly enabled in a background editor. The checker excludes Unity Editor Search indexing exceptions from gameplay error detection. All checks passed before `Tools/install_map02_player.py` installed the tested scene. Reports: `player-setup-checks.txt`, `player-play-checks.txt`, `player-install-checks.txt`; screenshot: `map02-player-boat-spawn.png`. Backup: `Map2_before_player.unity`.
