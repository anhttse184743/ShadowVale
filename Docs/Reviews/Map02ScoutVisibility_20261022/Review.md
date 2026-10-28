# Scout visibility — 22/10/2026

Fixed in G:/game/Capstone/ShadowVale on Khoa. The latest interactive Editor.log identifies the screenshot session as the older G:/game/ShadowVale checkout; this patch does not modify that other checkout.

Reproduced on the merged version as well: the approved injured pose had a renderer minimum of y=0.565 while the actor socket was y=0.774, and tall instanced rice obscured the body. The legacy ScoutAid cube was still rendered beside the real actor.

The injured mesh is now grounded from its actual bounds against a real ground collider after the actor visual correction; final minimum is about 0.735 on ground at 0.720. Only the resting mesh offset changes; the actor root, skeleton and navigation stay intact. All standing renderers return when escort starts.

A 3.2 m radius clearing and 0.85 m half-width approach from the bridge remove only local rice instances. Meshes/materials are shared as before. The opening is stable across restore and remains through the play session. The prototype ScoutAid renderers/colliders are disabled after the chapter owns the interaction. The D2_03 pin follows Tâm; the HUD identifies the scout and explains E. The objective says near the bridge, matching its actual field location rather than claiming he is underneath the bridge.

Validation: reproduced the buried-pose assertion before fixing. Seven regression cases passed (actor presentation and Day 2 state); the final dedicated visibility/rescue/save-load/escort test passed again after removing the prototype cube. Tests use isolated save roots. The scout test checks clearance, visible renderers, bounds, objective distance, conversation, save/load and restoration of standing visuals for escort. Screenshot captured and visually reviewed in Unity.

No paid audio, model replacement, existing dialogue rewrite, user save replacement or other checkout changes.
