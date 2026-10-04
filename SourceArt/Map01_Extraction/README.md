# Map 1 extraction authoring

`Map01_Extraction.blend` contains Nam, Hung, the original Mixamo reference actions,
the jetty mesh reference exported from Map 1, and thirteen baked actions for each rig.
Textures are packed. Original character files, rest skeletons and source clips are
not overwritten. Each exported rig retains 28 bones; no leaf bones are added.

## Rebuild

1. In Unity, run **ShadowVale → Cutscene → Prepare extraction** to export current
   jetty references and check the navigable route. This tool opens Map 1; save any
   scene edits before running it.
2. Run Blender in background with `--python SourceArt/Map01_Extraction/author_extraction.py`.
   Use `-- --no-render` to omit the preview render. Clips are baked at 30 fps.
3. Run **Prepare extraction** again to import the FBXs and rebuild the `.anim`
   references and `Resources/Cutscenes/Map01Extraction.prefab`.
4. Run `ShadowVale.Map01.Tests.ExtractionTests` and `EndingCutsceneTests` in the
   EditMode Test Runner. Tests enter Play Mode and use isolated save files.

## Animation details

Mixamo's armature axis is converted into each target rig's rest space before
rotation retargeting. The connected Hips bone is lowered through the existing root
bone. Foot contacts use IK and an analytic forward-knee solve without stretching;
the script retains the authoring constraints in the editable rig. Hand constraints
support the shelf, radio gesture, magazine motion and lowered travel grip.

Use the Action Editor to select `Nam_*` or `Hung_*` takes. Constraints are muted on
the baked rig so exported keys are not evaluated twice. Rerun the authoring script
to change contact targets. Unity imports the baked 28-bone takes as Humanoid clips
so Nam and Hung's existing Humanoid Animators evaluate their body pose consistently.
Root height is baked into the pose; the extraction controller owns movement through
the terrain anchors and the boat root.

Paddle splash and covering rifle audio are synthesized locally. The legacy engine asset is retained for serialized compatibility but is not played.

## Runtime

`Map01Extraction` controls radio, extraction gameplay, automatic boarding and
departure. Quest IDs 0–9 are unchanged. Extraction is 10 and boarding is 11;
completion is still 9. Cinematics cannot be saved. Four deterministic extraction
guard identities are spawned before checkpoint enemy snapshots are restored.
Legacy completed saves remain complete.

Hung's existing actor and the opening scene's allied commander are reused. The
hull, benches, oars and passengers share a moving root; the jetty remains static.
The final completion path saves and displays the existing Map 1 completion UI.
It does not load Map 2.

`animation-validation.json` records bone counts, fps and foot-target residuals.
`unity-layout.json` records boarding, blocker and departure positions.
`departure-clearance.txt` records the sampled hull and paddle clearance check.
Unity test results and captures are under `Logs/Extraction`.

## Takes (each exported for Nam and Hung)

| Take | Seconds | Playback |
| --- | ---: | --- |
| Radio_Receive | 7 | Once |
| Seated_Rifle_Ready | 3 | Loop |
| Seated_Rifle_Fire | 1.2 | Once |
| Seated_Rifle_Reload | 3.6 | Once |
| Seated_Lower_Weapon | 2 | Once |
| Board_Boat_StepDown | 6 | Once |
| Boat_Turn_And_Sit | 3.2 | Once |
| Boat_Seated_Travel | 4 | Loop |

The exporter resets scene FPS after every FBX import, because source files can
change Blender's scene rate. Unity tests check all sixteen imported sample rates.
During extraction, the Playables owner temporarily detaches the ordinary Animator
controller; it restores it when radio playback returns to gameplay. This prevents
Hung's normal idle controller from overwriting the authored seated pose.

The existing weapon prefab's grip is constrained to the right hand after pose
evaluation, with barrels pointing outboard. Hung's standing visual offset is
compensated at his seat; his original prefab and skeleton remain unchanged.

## Playing the integrated sequence

Open the existing Map 1 scene and continue its normal quest line. Defeating the
commander now starts the radio briefing and the northern jetty objective. Clear
the four extraction guards and nearby threats, then approach the jetty marker.
Boarding starts automatically. The completion screen is unlocked only after the
boat departs. Hold Esc for one second to skip a cinematic; boss/radio skips return
to extraction gameplay, while a boarding skip commits the departed state.

Runtime configuration: `Assets/_Project/Map01/Resources/Cutscenes/Map01Extraction.prefab`.
The prefab references the generated `.anim` assets; source FBXs remain beside them
under `Assets/_Project/Art/Characters/Animations/Extraction`.

Final verification: **6/6 Unity regression tests passed**. See `VERIFICATION.md`
and `unity-test-results.xml`. `unity-departure-preview.png` is captured from the
integrated Unity sequence.

## Revised departure (2026-10-02)

Arrival alone triggers boarding. Hung stows his rifle, takes the paddle and rows
from the stern. Nam and the allied commander remain armed; their firing sectors
exclude the other passengers. Boarding takes about 10 seconds and departure 18.
The original skeletons and Map 2 are unchanged.

`Resources/Characters/Map01ActorAssets.asset` is required: it stores neutral
body-height calibration and the existing rifle prefab/grip reference. It must be
published together with the extraction prefab and animation assets. Hung's standing
visual corrects the evaluated sole plane after Humanoid retargeting; seated poses
use separate bench anchors. Paddle contact correction accounts for Humanoid arm
lengths after the Blender keys are applied. These corrections do not alter bones.

Additional takes: Rifle_Stow (1.8s), Oar_Pickup (2s), Row_Start (1.6s),
Row_Loop (2.4s, loop), Row_Stop (2s), exported for both rigs at 30 fps.

The review video is a silent 10 fps camera capture for animation inspection,
not a recording of gameplay UI/audio. The animation clips themselves are 30 fps.

## Hung paddle grip correction

The Unity post-retarget contact pass now assigns the left hand to the upper grip
and the right hand to the lower shaft. Each elbow has its own outward/downward
pole; chest rotation is limited to 7�17 degrees. Wrist/finger axes come from the
existing Hand_end transforms, and the paddle follows palm centers rather than
wrist joints. The cycle separates submerged pulling from lifted recovery.
This change is in Map01Extraction.cs; it does not replace the underlying FBX takes
or add finger bones to the original 28-bone rig. Close review video:
`hung-rowing-close-review.mp4` (silent, 10 fps).
