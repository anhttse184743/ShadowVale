# Khoa / dev integration — 2026-10-14

Merged `origin/dev` (`ce7a18d`) into Khoa (`ab28b38`).

The rescue hint keeps bush-pull priority and the reused sorting buffer from dev.
Hostage firing keeps the cinematic Animator-controller guard and dev's rifle cache.
The intro, either-person shelter trigger, and completion when Nam enters the shelter
remain as implemented on Khoa; dev's save cache and non-alloc ground probes remain.

Dev adds four forearm twist bones to Nam. Regenerated 14 derived rescue assets with
`RescueStorySetup.Prepare`: partial Avatars/mask, recovery/approach clips and refined
hand bindings. The original model, source FBXs, compressed editable Blender source
and dev's village/river scene are preserved.

Updated the arrival test to sample bounded `RiverWater` components rather than the
removed legacy canal mesh. On the new village terrain, shore IK now levels the
shoe against the ground at its final solved contact, avoiding a stale slope normal
at the landing edge. The existing 6-degree walking-foot check remains unchanged.

Unity 6000.3.24f1 compiled the merged project. **19 distinct targeted checks passed**,
including rescue intro/execution, bush success/escape and hint readiness, finite stone
loot, three escort waves/save-load, both shelter arrival orders, Avatar/hand binding
compatibility, aim/reload, skin contacts, tracer lifecycle, and natural/skipped Map 2
arrival. `checks.json` contains the latest passing result for each selected check.
This was a targeted regression run, not the entire project test suite.

Directly reviewed fresh bush contact close-ups and Map 2 boat/landing/foot captures.
Full captures and Unity logs are retained in the isolated validation workspace;
generated videos, caches and logs are not added to the game repository.
