# Command bunker upgrade verification

Applied the user-approved Blender workbench, mission board and inward-facing
chair set to the shared BriefingTable/MissionBoard prefabs used by Map 1.

Verification: validation.txt (Unity editor), chair_asset_audit.txt (28 source
chairs), chair_repairs.txt (19 corrections in 11 source models and five corrected
scene chairs), chair_validation.txt (six new bunker chairs).

Four complete editor NavMesh paths reach the existing 4 m crafting interaction
range. Crafting point and cutscene root names/positions are unchanged. This is
editor geometry, import and navigation validation; a full gameplay playthrough
has not been run for this asset change.

01_bunker_workbench.png and 02_bunker_board.png are actual Unity scene renders,
not the Blender proposal. The remaining numbered images inspect camp seating.

Original water and riverside vegetation work in Map 1/Map 2 is preserved.

Board wall fit: moved Map 1 MissionBoard 0.3945 m toward the wall; nearest rear geometry is 3 mm from the visible wall. Nine wall samples agree. See board_wall_mount.txt and 06_board_wall_side.png.

Commander/cutscene sync: Map01Mission now references an authored alliedCommanderPost on the left side of the mission board. Local dialogue turns the commander toward Nam and restores his authored facing on close. The rescue return layout ends at a reporting point 1.8 m in front of the commander, via a left-side aisle marker. Details: commander_placement.txt; runtime probe: commander_runtime.txt; renders: 07_commander_beside_board.png and 08_commander_dialogue.png.


Commander-only camera: diagonal framing now includes the mission board behind the officer. Play Mode verified board centre inside the viewport; Nam shot still matches the original camera position rule and 48-degree FOV. See camera_validation.txt, 08_commander_dialogue.png and 09_nam_dialogue_unchanged.png.

Commander camera moved 0.65 m left, with additional shoulder/backpack avoidance when Nam stands in the foreground. Play Mode rechecked a closer, left-offset Nam approach: officer and board centre remain clear; Nam's own camera still matches the original rule.

Local commander conversations now stage Nam at the authored post plus 1.8 m along its forward direction before camera framing and foot support are sampled. CharacterController is temporarily disabled for placement, and the fixed position is held throughout dialogue. Closing leaves Nam at that spot. Play Mode verified three different approach positions converge on the same mark; see fixed_briefing_position.txt.
