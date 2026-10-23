# Bunker furniture — approved and installed in Map 1

The user approved Revision2 and requested a final 0.30 m clearance from the crafting tabletop side edge to the cot-side entrance timber. Installed by `CommandBunkerUpgrade.FurnitureLayout.cs` using `export_approved.py`.

Final crafting root: Unity (-68.44, 0.12, -68.23), rotation Y=180 degrees. Door timber outside edge X=-66.74, table side X=-67.04; rear table edge Z=-68.77, visible wall face Z=-68.80. Two inward-facing chairs. ForestPoint `workbench` follows the moved table. Original BriefingTable root stays at (-65, 0.12, -63) for briefing references, with a horizontal 3.30m table and two equal-length slab benches. Five shared ammunition shelves use EquipmentShelfHeadSizedFinal with resized helmets. Blender collider proposal geometry is excluded from mesh export.

Reports and Unity captures: `Tools/CommandBunkerReports/furniture_layout_validation.txt`, `crafting_door_clearance.txt`, `furniture_runtime_validation.txt`, and `layout_final_*.png`. Isolated CharacterController collision probes pass for the table, two benches and both crafting chairs. Runtime checks verify workbench registration, proximity and opening/closing the actual crafting panel handler.

The following describes the original review artifacts, which remain available:

Original Blender proposal before application.

- Existing detailed crafting workbench reused, two chairs facing its front.
- Rear tabletop edge to preview wall face: 0.030 m, verified numerically.
- New meeting table: 3.30 m long, approximately 1.17 m wide, six chairs, worn timber joinery, two oil lamps and olive helmets inspired by the supplied reference.
- The doorway and walls are a conceptual review context, not an extraction of the Unity bunker. Final world placement must be matched to the user's marked entrance after approval.

Outputs: `01_layout.png`, `02_workbench.png`, `03_meeting.png`, and editable `BunkerLayout_Proposal.blend`.

`build_preview.py` builds the proposal. `fix_clearance.py` is a one-time correction for the first render only; do not run it on a freshly rebuilt proposal, since its offsets would accumulate.
