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
