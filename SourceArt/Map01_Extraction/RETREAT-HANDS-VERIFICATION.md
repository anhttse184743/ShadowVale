# Map 1 retreat hands revision — 2026-10-06

Changes are in Unity runtime contact solving; original skeletons, FBXs and editable Blender animation source are preserved. No Map 2 or scene terrain edits.

- Added two-hand contacts for waiting/lowering, not only active seated fire. AK geometry uses local +X toward its magazine; corrected rifle roll instead of treating local +Y as up.
- Kept Hung's rifle in front during lowering, replacing the broken behind-back stow pose. Rifle is hidden at the existing paddle pickup transition.
- Positioned the rifle within both arms' reach. Waiting palm contact checks for Hung and commander require less than 2.5 cm error. Measured errors are recorded in retreat-grip-metrics.txt (both below 0.1 mm at the sampled pose).
- Sample the current playable pose before procedural solving, so repeated render/animation updates do not accumulate torso rotations or suppress the final hand correction.
- Paddle grip derives from hand mesh weights where readable, with a rig-endpoint fallback. Close frames were checked after animation LateUpdate, including pull and recovery. Manual coroutine camera captures before animation completion were unreliable; the capture harness now records after LateUpdate.
- Only one in three outgoing shots produces a tracer. Width reduced from 0.055 to 0.012 m; lifetime from 0.12 to 0.045 s. Short shot spacing 0.48 s, inter-burst pause 2.2 s. Muzzle light and gunshot volume reduced. Harmless enemy return fire is limited to the first five seconds of departure, at 3.5-second intervals. Gameplay tracer settings are unchanged.

Validation:
- stable-retreat.xml: full extraction suite 5/5 passed before the final paddle mesh-contact adjustment.
- palm-surface.xml and capture-final.xml: natural full boarding/departure/completion passed after that adjustment and after correcting capture timing.
- grip-contact.xml: contact and existing-guard combat tests 2/2 passed. Existing enemies die without consuming gameplay ammunition or creating extra loot.
- Reviewed waiting and lowering close images, rowing pull/recovery frames and seated cover views. Latest metrics are in retreat-cover-results.txt and retreat-live-cover-results.txt.
- Main-project runtime/test file hashes match the validation project.

Previews: map1-extraction-retreat-final.mp4, hung-short-stroke-retreat-final.mp4, seated-cover-fire-retreat-final.mp4. These are silent camera-frame previews resampled to the sequence duration, not a live audio capture or native 30-fps recording. Blender was used for video encoding in this revision; animations were not rebaked.
