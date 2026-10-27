# Paddle grip / Map 1 → Map 2 regression — 2026-10-26

The grip constructor appended PaddleGripLeft/Right unconditionally to a mesh clone. If the incoming readable mesh already had those channels, Unity rejects adding an earlier channel again (frames can only be appended to a new or final channel). Reference: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/mesh/addblendshapeframe .

The copy now removes/rebuilds only those two owned channels, preserving every other channel/frame and its weight. Disposal restores the original mesh and weights and does not overwrite a renderer that another owner has replaced. No FBX, Avatar, skeleton or source mesh is edited. Preparation is published only after the entire boat setup succeeds; a partially created boat cannot be treated as ready for Map 2.

Validation:
- Seeded actual commander mesh with both existing grip names plus a foreign shape with two frames. Two constructor/set/dispose cycles passed; channels are not duplicated; foreign data, original mesh and all original weights are restored.
- Full departure/rowing/arrival on Direct3D 12 passed: commander limb lengths, both palm contacts, Hùng's supported right arm and straight wrists, Nam's cover fire, no commander gunfire, arrival to gameplay with player controls restored.
- Complete-checkpoint route with both existing grip channels passed on Direct3D12 with GPU Resident Drawer=InstancedDrawing; source Prepared=True and arrival Gameplay, player controls restored. See transition.txt.

The first full-capture attempt exhausted its 200-second real-time deadline while already disembarking: synchronous 5 Hz PNG readback slows frame advancement, while arrival intentionally caps animation dt. Test capture was reduced to 1 Hz and deadline widened; game animation timings are unchanged. A first checkpoint fixture failed because it queried an extraction component before creating it; it now uses the normal Map01Extraction.Get path.

GPU Resident Drawer was left enabled. Existing moving-boat renderer handling is unchanged; no error-log suppression or global rendering feature disable was introduced. The supplied BatchDrawCommand burst and duplicate-shape exception have zero occurrences in both final DX12 transition logs; counts are recorded in validation.json. This is observed validation, not proof of the engine warning's root cause or every possible graphics-driver case.

Telemetry/backend and solver messages are informational and were not hidden or changed; queued data was not deleted. No dialogue, voices, objectives or paid assets changed.

Final validation selects the latest successful result for each of three distinct cases across the included original runner reports. Earlier failed attempts remain visible in attempt-DX11.xml (capture deadline) and attempt-DX12.xml (fixture initialization). Another intermediate checkpoint attempt reached Gameplay but its explicit NoUnexpectedReceived check rejected the expected informational arrival-complete log. The final fixture allows normal info logs; automatic error/exception failure remains enabled. No production logs are suppressed.
