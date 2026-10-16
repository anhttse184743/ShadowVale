# Map 1 character and rowing revision — 2026-10-02

Unity 6000.3.24f1; Blender 5.2.2 LTS. Tests ran in an independent copy of
ShadowVale because the main Editor remained open. Runtime code, imported animation
assets and required resource prefabs were copied/compared with the main project;
`published-sha256.json` records exact matches. No scene or Map 2 file was overwritten.

## Results

- Broader regression: 9/9 passed (`unity-regression-results.xml`), covering
  opening, boss cinematic, Hung visuals and extraction.
- After final rowing contact refinement: 4/4 extraction tests passed
  (`unity-test-results.xml`). Fresh per-test save folders prevent prior-run file
  replacement conflicts. User saves were not touched.
- Hung versus Nam: less than 2% standing body-height difference at both base
  positions and the forest terrain point. Evaluated skinned sole matches the
  sampled surface within 2 cm (see three metric files).
- Each tested mission enemy has one active rifle; held aim and magazine state
  survive radio handoff. Captures show rifles on enemies and seated commander/Nam.
- Boarding starts with all blockers alive, some alive, and none alive.
- Checkpoint reload retains the defeated guard and four unique blocker identities.
- Holding Escape completes boarding once; boss/radio skip returns to extraction.
- Completion and autosave occur after departure; the active scene stays Map 1.
- 26 exported takes, 30 fps, original 28-bone rigs retained.
- Hull and starboard paddle clearance: 151 static geometry samples, including endpoint.
- All 25 extraction prefab GUID references resolve in the published project.

## Visual review

Reviewed rendered standing pairs, descending steps, seating, close rowing and
12 samples across the full boarding/departure sequence. Hung's lower body stays
on the bench; the upper body turns for the paddle. Both hands guide the prop.
Nam and the commander carry rifles while seated. Water uses its own soft material,
not the yellow tracer material. Camera moves closer to the steps then widens.

`map1-extraction-review.mp4` is a silent 960x540, 10 fps camera review (the source
animation is 30 fps). It omits GUI overlays and gameplay audio. `complete.png`
and quest assertions verify the departed state; this is not a full manual playthrough
or an assertion that every possible camera angle/contact has been exhaustively tested.

The earlier visual regressions came partly from missing published resources:
Map01ActorAssets and regenerated animation/prefab references were absent or stale
in the main project. These are now included, with hash checks rather than relying
on the validation copy alone.

## Follow-up: paddle arms

Corrected crossed grips, common elbow pole, excessive chest twist, and wrist-based
prop attachment. Reviewed six successive close frames through a stroke and exported
an 18-second close camera video. The complete extraction suite passed 4/4; after
freezing the final grip for completion/pause, natural playback and skip/checkpoint
checks passed 2/2 (`rowing-final-tests.xml`). Runtime sources match the main project.
The correction is post-retargeting in Unity; the source FBX takes were not regenerated.


Latest follow-up: see OCT6-VERIFICATION.md and oct6-tests.xml (5/5 passed).
