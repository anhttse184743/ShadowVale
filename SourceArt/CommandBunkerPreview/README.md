# Approved command bunker models

The user approved the Blender sample and it is now installed in Unity.

Source: `CommandBunker_Proposal.blend`. `build_preview.py` regenerates the proposal;
`export_unity.py` reads the approved blend, evaluates bevels, converts text and
curves, bakes procedural wood into UV albedo atlases, and exports mesh data.
The review room and lights are excluded from the game assets.

Unity assets: `Assets/_Project/Art/Environment/CommandBunker/`.
The existing `LayoutTest/Prefabs/BriefingTable.prefab` and `MissionBoard.prefab`
retain their GUIDs and root names. Original visual renderers are disabled;
new Blender geometry is nested beneath those roots. Six separate chair instances
face the table, three on each side. Chair/table navigation obstacles carve around
the furniture. The crafting ForestPoint and cutscene anchor remain in place.
Changes propagate to the six scenes referencing these shared prefabs, including
Map 1, Map00_Tutorial and the art review/stage/benchmark scenes.

The furniture audit covered 28 chairs in 11 imported camp models. Nineteen needed
orientation corrections, installed through `CommandChairFacingImporter` and its
explicit `CommandBunker/ChairFacing.json` manifest. Reimport always starts from
the original FBX, so the correction does not accumulate. Wide desk rows retain
parallel seating toward the table edge. Five corresponding Map 1 chairs were
corrected, with exact original triangles removed from derived LOD0 batches.
LOD1 excludes these furniture details. The radio operator's set-aside chair keeps
its position and turns toward the radio desk.

`CommandBunkerUpgrade` is an editor-only import/verification utility. It polls
`Tools/CommandBunker.request` for `inspect`, `audit`, `capture` or `verify`.
`apply` regenerates the bunker prefabs from the approved mesh export; `chairs`
is a one-time migration and refuses to repeat once the manifest exists.
Reports and actual Unity renders are in `Tools/CommandBunkerReports/`.
Recovery copies of the original prefabs and the scene before the chair migration
are in `Library/CommandBunkerBackup/` (local, not versioned).
