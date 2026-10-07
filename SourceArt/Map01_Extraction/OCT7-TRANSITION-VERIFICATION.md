# Map 1 boarding/turn transition — 2026-10-07

- Extraction prefab directly references the same Nam_Rifle_Run.fbx clip used by AC_Player's Locomotion_Rifle blend tree. Removed generic name-contains-Run selection. The natural-playback test checks the exact clip name.
- Approach 0.55 s; boarding 2.9 s; seating 2.1 s. Nam runs down the existing first five anchors, makes a short jump from the low shelf toward the hull, then sits. Existing Humanoid Jump was reused; hand contacts keep the rifle supported during the hop. This is runtime animation composition; no source skeleton or animation FBX was rebaked.
- Hung lowers his rifle, turns with hands near his thighs, then blends hand direction/contact toward the paddle. Wrist local rotation is retained during turning instead of locking wrist orientation in world space. Paddle hand orientation now blends with contact weight rather than snapping immediately at the start of reach.
- Inspector tuning: Gameplay Rifle Run, Hung Turn Seconds, Paddle Reach Seconds, Apply Hand Contacts. Manual-edit guide explains authored poses versus runtime contacts, and includes a one-action Blender FBX exporter.
- oct7-final.xml: natural playback and skip/checkpoint tests 2/2 passed.
- oct7-visual.xml: natural playback passed again after adding rifle support to the reused jumping motion.
- Inspected rotation/reach/run/hop/sit frames from after LateUpdate captures. Main-project runtime, prefab and jump asset hashes match the validation project.
- Main Map 1 and Map 2 scene geometry was not edited. Completion still follows departure; no Map 2 load.

Video: map1-extraction-oct7-transition.mp4. Silent camera preview, resampled from captured frames to 5.55 s boarding + 18 s departure; not a native 30-fps audio recording.

Manual instructions: MANUAL-ANIMATION-EDIT-VI.md. Single-action export helper: export_selected_action.py.
