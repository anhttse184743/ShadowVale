# Map 1 — damage presentation, power sabotage, and alarm reaction

## Mission sequence

Point 2: clear the station guards → solve the radio circuit → the faceplate is torn and exposed wire ends appear → follow the objective marker to the adjacent field power converter → disable its three fuse carriers with the timing mini game → the converter has an open burnt panel and sustained grey smoke → report to the commander → point 3. Stage IDs remain unchanged. Radio completion alone stays in stage 5; both `radioDisabled` and `powerDisabled` are required for stage 6.

## Interaction

At the power converter press E. Nam walks to its authored approach marker and touches the panel. Space or the on-screen button operates the current fuse when the moving needle is in the target band. Three successful steps; ten seconds per step, an untimed ready screen between steps. A wrong input or timeout ends that attempt and permits retry. It does not create an additional response squad. No new paid dialogue was generated.

## Art and effects

The existing radio stays as the grounded base. A separate damaged overlay adds bent sheet metal, a hanging cover, cracked terminals, thin insulated leads of differing lengths and exposed copper ends. The field converter has a steel skid, carrying handles, side cooling slits, a voltmeter, three fuse carriers, damaged sockets and a buckled service cover. A cable connects the devices.

The smoke is a cosmetic particle system (48-particle cap, 7 particles/s, 2.5–4 s lifetime), with soft fading, growth and low-frequency billowing. It is not registered as tactical smoke and cannot grant extra concealment. It uses scaled time and is restored from the saved damage state.

Native meshes and materials are original project-generated assets authored by `Map01SabotageSetup.Build`; no downloaded or paid-provider models. Rebuild with Unity Editor closed and inspect the result before shipping:
`Unity.exe -batchmode -projectPath G:\game\ShadowVale -executeMethod ShadowVale.Map01.EditorTools.Map01SabotageSetup.Build -logFile Logs/PowerSabotage/build.log`
The authoring entry point saves Map 1 and exits its Editor. Editable references are in `Map01OperationsLayout.sabotage` and `Map01SabotageProps`.

## Save compatibility

Story snapshot revision 5 adds `powerDisabled`, `powerSeed`, `powerStep`. Old saves already at stage 6 or later count power sabotage as completed, retaining their existing progress. New runs cannot report point 2 until both devices have been disabled. Restoring a completed source resumes the smoke; restoring an earlier checkpoint clears it.

## Rescue alarm

Updated camera treatment: see Map01_RescueFailure_Cinematic.md. The later user-approved version cuts briefly to bound Hung and then returns to Nam. Detection starts a short, navigation-checked backstep, a blend into an existing crouched alert pose, an upper-body look toward the threat, and a brief physical startle at the shot. Foot-cycle sampling follows distance travelled. Weapons remain visible during this reaction. The existing guard shout and real overseer aim/shot remain; failure follows a shorter hold and fade. No new enemy subtitles or paid voice production.

## Verification

Verified 2026-10-17 in Unity 6000.3.24f1 on the real Map 1 scene. The two integration cases passed (puzzle-revision-results.txt: 2 passed, 0 failed). After adjusting smoke contrast, the radio/power case passed again (radio-results.txt: 1 passed, 0 failed). It verifies radio-only gating, source fingertip reach within 11 cm, failed source attempt and retry, three accurate pulls, real smoke particle emission, save/load damage and squad state, and commander report. The rescue case verifies Nam retreats over 20 cm, camera remains with him, the overseer aims at captive Hùng, and bound collapse precedes failure. Reviewed first/mid/end gameplay frames and captive contact views; the full body is partly obscured by existing bushes in this scenario, so this is not an exhaustive animation review from every approach. Nam uses the existing backward rifle walk and crouch clips, with no rig changes.

Standalone rules checked 200 random seeds: 600 reachable timing steps plus wrong-pull and timeout failure for every seed. Device captures: Logs/Map01Operations/radio-damaged.png, power-smoke.png, power-ui.png. Device diagnostic views temporarily hide the player body to expose the prop; an equipped weapon remains visible in those diagnostic pictures, not floating in normal gameplay. Alarm frames: Logs/RescueStory/Execution/GameView. Video: Logs/Map01Operations/video/point-2.mp4 (automated staged review, no audio track). This pass does not claim a full Map 1–Map 2 playthrough. No new paid voice/audio was produced.
