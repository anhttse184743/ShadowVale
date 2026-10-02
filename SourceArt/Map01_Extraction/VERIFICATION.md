# Map 1 extraction verification

Validated in Unity 6000.3.24f1 and Blender 5.2.2 LTS on 2026-10-01.

- Unity regression run: **6 passed, 0 failed**. See `unity-test-results.xml`.
- Existing quest route and boss death animation remain functional.
- Natural boss death continues through the seven-second radio into extraction.
- Natural boarding completes only after departure; cinematic saving is blocked.
- Holding Escape for one second completes boarding without a direct Skip call.
- Reloading an extraction checkpoint restores the defeated blocker and four unique guard identities.
- Repeated skip calls and legacy completed-stage restoration are idempotent.
- All 16 Unity clips are Humanoid, 30 fps. Radio is 7 seconds; boarding is 6 seconds.
- Blender export validation: 28 unchanged bones per rig; 16 takes. Maximum baked
  foot-target residual is 0.00000174 m in Blender authoring space.
- Full hull clearance checked at 151 path samples, including the final endpoint.
- Unity captures reviewed at seated-ready, descending steps/shelf, seating,
  departure and final departed positions. Preview: `unity-departure-preview.png`.
- Original character FBXs and Map 2 scene have no changes in the working diff.
- `git diff --check` passes.

The screenshots are camera renders; the existing completion UI is selected by the
completed quest stage, which is asserted after departure in the integration test.
