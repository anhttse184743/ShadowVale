# Nam's opening salute

`Nam_Salute_Briefing.blend` contains Nam's existing 28-bone animation rig and skinned visual, the untouched Mixamo Salute source, and a baked `Nam_Salute_Briefing` action at 30 fps. The character models and their skeletons in the game are not replaced.

The action begins and ends with a straight arm beside the thigh. Both transitions use eased local joint rotations: shoulder elevation and gradual elbow flexion when raising, shoulder lowering and gradual elbow extension when lowering. Elbow flexion slightly leads shoulder elevation to keep the hand close to the body. The palm stays aligned with the forearm; fingers form the salute early and remain grouped until the arm is almost lowered. Raise: frames 0–21; hold: 21–27; lower: 27–57. The held frame is extended in Unity to match the actual reply audio.

## Manual editing

1. Save a copy of the `.blend`. Select Nam, choose the action in Action Editor, then enter Pose Mode. Inspect frames 0, 6, 12, 18, 21, 27, 33, 39, 45, 51 and 57 from front and right side.
2. Adjust `RightArm`, `RightForeArm` and `RightHand` rotations and key them with I. Keep the palm aligned with the forearm and fingers aimed at the helmet brim. Keep frame 21 and frame 27 identical; frame 0 and frame 57 must have the same straight arm. Do not change rest pose, bone names, root scale or skeleton.
3. The two `Salute_*` empties mark the held contact and straight-arm endpoint; moving them alone does not change baked keys. To regenerate, edit `tip` (held contact), `down` (relaxed arm direction) or `lower_end` (lowering duration) in `author_salute.py`, then run Blender with `--background --python SourceArt/Map01_Opening/author_salute.py`. Regeneration replaces this action and its FBX, so preserve manual key edits separately.
4. Export only the Nam armature to `Assets/_Project/Art/Characters/Animations/Opening/Nam_Salute_Briefing.fbx`. Bake at 30 fps, Sampling Rate 1, Simplify 0; disable All Actions, NLA Strips and Add Leaf Bones. Keep -Z Forward/Y Up and the existing scale.
5. In Unity, use **ShadowVale → Cutscene → Prepare Nam salute**. It imports the FBX and updates the existing `.anim` reference in the opening prefab. It also adds the right-hand finger curves for the gameplay avatar: fingers extended and grouped while saluting, relaxed during the transition. The reusable animation rig stays at 28 bones.

The same menu calibrates elbow extension against the actual gameplay avatar. Its reference pose retains a small bend when a straight-arm clip is retargeted, so the generated `.anim` adds an extension correction at rest and smoothly fades it out during the lift. The held salute is preserved. `elbow-calibration.txt` records the measured result; neither the character model nor its avatar is edited.

## Unity timing and layering

The prefab `Assets/_Project/Map01/Resources/Cutscenes/Map01Opening.prefab` exposes `Salute Raise Seconds`, `Salute Lower Seconds`, `Salute Hold Time` and `Salute Lower Start`. The clip holds at 0.7 seconds and lowers from 0.9 seconds. Update these clip-time values if the baked frame ranges change.

Nam's `soldierIdle` is a separate copy of the existing idle with upright spine/neck/head muscle curves. Its right-arm curves are copied from salute frame zero to keep the same straight arm while listening and during transitions. His salute layer owns only the right arm and fingers. The commander still uses the original idle. No LateUpdate hand snapping is used. The camera settles on Nam before he raises his arm and pans between subjects by interpolating its focus, avoiding a turn away from the action.

Nam and the commander are unarmed throughout the opening, including the title, salute and camera handoff. Weapon objects are disabled while the cutscene owns input; Nam's previously active weapon objects are restored only when gameplay resumes. The briefing commander's weapon stays disabled. This does not change the armed extraction actors.

Run `OpeningCutsceneTests` and `DialogueVoiceTests.OpeningSkipDoesNotQueueAnotherBriefing`. Check actual rendered front/right views at intermediate frames, held salute, release and handoff. Voice files, quest state, terrain and other animations remain unchanged.
