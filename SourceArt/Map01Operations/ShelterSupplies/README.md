# Shelter supply animation source

User-approved local Nam backpack actions and Hùng resting pose. Skeletons are unchanged (Nam 62 bones, Hùng 28). The repaired wrist geometry is isolated in the cinematic Nam model; Player.fbx and gameplay rigs remain untouched.

Editable Blender originals are preserved at `C:/Users/dangk/Documents/Codex/2026-09-20/du/output/Shelter_Supplies_20261019`:
- `Nam_Backpack_Reach_v9_20261019/Nam_Backpack_Collect_Reach_v9_20261019.blend`
- `Nam_Backpack_Reach_v9_20261019/Nam_Backpack_Handoff_Reach_v9_20261019.blend`
- `Hung_Stretcher_Complete_PREVIEW_20261019.blend`

Blender export command uses explicit source/output destinations:
`blender -b --python export_performances.py -- --source-dir SOURCE_FOLDER --output-dir EXPORT_FOLDER --include-hung`

After placing the exported FBX and cloth JSON in `Assets/_Project/Art/Characters/Animations/ShelterSupplies`, run `ShadowVale.Map01.Editor.ShelterSuppliesSetup.Import` in Unity. It preserves references and builds the presentation resource. No scene-save operation is part of this pipeline.

Cloth topology is supplied directly in JSON because FBX import may split vertices; all 481 frames are exported even while the prop is hidden. Game timing drives the character, backpack, flap, cargo and cloth from one 16-second clock.
