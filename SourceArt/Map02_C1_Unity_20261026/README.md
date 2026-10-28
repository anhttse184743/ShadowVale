# C1 Map 2 — v4 approved integration, 26 October 2026

The user approved bringing the latest Nam/Hùng landing demo into the game and committing/pushing `thinh`, then merging into `dev`. This is Map 2's landing scene, not the C1 outpost of Map 1.

`C1_Approved_v4.blend` is a compressed, portable copy of the approved demo. Unused cast/actions were removed. `source-receipt.json` records its hash and the original approved source hash. The original source is not overwritten. Nam has 62 bones and Hùng 28. Mesh/weights/bind/active curves are compared by `verify_archive.py`.

## Reproduce the import

1. Open a copy of `C1_Approved_v4.blend` in Blender 5.2 and run `export_approved.py` in background mode. The exporter writes only the four FBXs in `Assets/_Project/Art/Map02ArrivalC1` and C1 JSON data. It reads the committed standalone `Assets/Resources/Map02ArrivalC1/C1Pack.json` for pack ownership timing. It never saves the open source.
2. In Unity 6000.3.24f1 run `ShadowVale.Map01.Editor.Map02ArrivalC1Import.Run` with `-executeMethod`, or call it from an editor tool. It rebuilds only the two C1 prefabs, their skin material and `Approved.asset`.
3. Run `Map02ApprovedC1Tests`, `Map02ArrivalMisstepTests` and `Map02ArrivalTests`. The explicit `RecordMisstepContactReview` test records the Unity camera for visual review.

The model FBXs have Humanoid avatars for bone queries. Motion FBXs import as **Legacy transform tracks**, sampled directly on the inner FBX object with its Animator disabled. The outer prefab stores any calibration. This deliberately preserves the reviewed hand/contact transforms; Generic/Humanoid root extraction changed the hips during the first diagnostic import. Runtime planar travel is stored separately in `C1Travel.json`.

During the brief C1 action the approved meshes replace only Nam/Hùng's visible skinned renderers. The baked backpack follows the approved chest while worn, the hand while carried, and the landing while set down. Gun renderers are hidden independently of which gun is equipped. The supporting knee alone adapts to the game's solid upper landing, preserving the authored ankle and entire upper body. On walking on, completion or skip the temporary cast is removed and the exact original renderer states/equipment are restored. Player master FBX, normal gameplay animations and scenes are unchanged.

## Limits

This imports the approved v4; it does not change its timing. The Blender review still records five self-contact flags and one right-forearm angular-rate flag (frame 127, about 16.3 degrees/frame at 30 fps). The release can still feel fast. The source-pose import test is not proof that every body/equipment intersection is absent. A recorded 1080p/60 fps video is an encoded review artifact, not a measured gameplay frame-rate guarantee.

Integration results and Unity captures are documented in `Docs/Reviews/C1_Unity_20261026/KET_QUA.md`.

## Backpack import pitfall found during integration

The gameplay backpack renderer has local scale 100 under the imported root. `BakeMesh(mesh, true)` produced a detached prop only about 7 mm across; merely switching the flag kept the size but lost the pivot by about 130 m. The final snapshot evaluates each source vertex with its skin weights and `bone.localToWorldMatrix * bindpose`, then centres it in world units. This runs once per interaction and preserves the existing mesh/UV/materials. Worn placement uses a shoulder/head anatomical frame because the two rigs have different chest bone roll. Ground placement uses the actual mesh bottom instead of a fixed pivot offset. Tests check world size/pivot on the gameplay prefab, plus the actual worn/ground bounds in the cutscene.

Reference actually read: [Unity SkinnedMeshRenderer.BakeMesh](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/skinnedmeshrenderer/bakemesh), including renderer-relative vertices and scale compensation. The behavior is verified with Unity 6000.3.24f1 in this project.

Skinning matrix reference actually read: [Unity Mesh.bindposes](https://docs.unity3d.com/kr/current/ScriptReference/Mesh-bindposes.html). All available weights per vertex are retained via `GetAllBoneWeights`/`GetBonesPerVertex`.

The player's imported mesh is CPU-readable in Editor inspection but is not readable in Play Mode. The importer therefore stores a separate readable copy of only the 4,400-vertex backpack as `Nam_C1_Backpack.asset`, referenced by `Approved.asset`. It does not change the player FBX import settings. Both the source-data test and actual Play Mode run cover this dependency.
