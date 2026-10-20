# Map 1: randomized puzzles and rescue alarm

## Player-facing rules

- Supply chest: three digits 0–5, six guesses per notebook page. A new run receives a new seed; loading or retrying keeps that run's code and notebook. Repeats and leading zeroes are valid. A previous untouched fixed-seed save is migrated; a save with guesses keeps its code so feedback remains true.
- Radio: randomly two or three rounds, ten seconds **per round** after the player presses Start. A connected 5×4 cable with no decoys is generated for each round; four, five, then six cells initially need repair, but their identities and count are not revealed. All twenty cells look alike and can rotate, including correct segments. There are no error-cell borders or locked-cell dots. All player rotations survive restore. Correct confirmation advances one round, with an untimed ready screen before the next ten-second clock. Only the final confirmation disables the radio.
- Wrong confirmation or timeout retains the existing alarm consequence; only one three-soldier response squad across all attempts. A failed attempt generates a fresh sequence next time. Closing an interaction preserves seed/round/rotation; old 16/20-cell circuit saves restart the new rules without resetting mission progress or reinforcements.
- Rescue alarm: existing detection and overseer-distance rules remain authoritative. The player's camera stays at Nam's position, with at most two degrees of FOV tightening, restrained angular movement, a directional warning, a short double heartbeat, the existing enemy shout and a minimum 1.6-second aim beat. The real overseer still aims at the bound captive and fires. Then the scene holds/fades before failure. No extra enemy subtitles; no paid voice work.

## Save fields

Story snapshot revision 4. `puzzleSeed` persists the supply code. `circuitRevision=1`, `circuitSeed`, `circuitRound`, and `circuitRotations` restore the new board together. Random new-run seeds use GUID entropy rather than the scene's legacy fixed seed. Mid-interaction saves remain blocked by the existing cinematic/session handoff.

## References and design application

- [How to Catch a Ninja: NPC Awareness in a 2D Stealth Platformer, Game AI Pro](https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter32_How_to_Catch_a_Ninja_NPC_Awareness_in_a_2D_Stealth_Platformer.pdf): player-readable awareness and reactions. Used as a principle for a directional alarm and an audible consequence in Nam's view, not a replacement for this game's hostage rules.
- [Klei: Mark of the Ninja Remastered official video](https://www.youtube.com/watch?v=sZ7ogfkR3hQ): visual/audio presentation reference. Search metadata and official description reviewed; no claim of frame-by-frame video playback.
- [GDC: Level Building for Stealth Gameplay](https://www.gdcvault.com/play/1013211/Level-Building-for-Stealth): reference for legible stealth spaces. Existing terrain and approach routes are preserved in this revision.

## Verification

Standalone C# rules checked 300 circuit seeds / 749 rounds: 20 distinct cable paths, both round counts, every generated round solvable. Unity regression: 20 distinct checks passed across the batch run and the focused visual retake. The first batch had 19 passes and one fixture failure because its rescue scenario had not accepted the commander briefing. After adding the real briefing prerequisite to that fixture, the alarm test and radio test both passed in the visual retake. See Logs/PuzzleRevision/final-summary.json and Logs/Map01Operations/video/puzzle-revision-test-cases.txt. Full HD radio UI and alarm GameView frames were inspected directly. Scripted flows exercise pause, wrong confirmation, one response squad, subsequent rounds, save/load, the final objective gate, hostage aim, bound collapse, and player-camera position. The radio MP4 is a silent automated visual review, not an audio listening approval.

Review files:
- Logs/Map01Operations/video/point-2-0090.png: Full HD circuit board.
- Logs/Map01Operations/video/point-2.mp4: automated radio failure/retry/success review, no audio track.
- Logs/RescueStory/Execution/GameView: alarm sequence in Nam's camera.

Reproduce the visual retake with the Editor closed: Unity.exe -projectPath G:\game\ShadowVale -executeMethod ShadowVale.Map01.Tests.Map01OperationsRecording.RecordPuzzleRevision. This test method closes its Editor instance after recording.

Follow-up difficulty adjustment: removed highlighted repair cells, locked dots and explicit repair-count hints. Powered-wire feedback remains a circuit state rather than an error marker. All 20 cells are rotatable.
