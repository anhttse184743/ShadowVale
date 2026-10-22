# Dev → Khoa integration — 2026-10-22

Merged origin/dev 7e33c936 into Khoa after 1331bab3.

Preserved the Day 1 / Night 1 minigames, staging, safe rest marker, map safety, input-release guards and all 49 approved free voice recordings. Integrated dev's shared character HUD, inventory/skills, Day 2 / Night 2 logic, named scout model, weapon balance and Nam motion assets.

Resolved seven conflicting files by combining behavior, rather than choosing an entire branch. Day 1 / Night 1 use their authored family cast and staging; the injured scout uses dev's prefab. Generic face-to-face conversation staging applies from Day 2 onward so it cannot reposition the bandage/meal scenes.

Integration fixes:
- Direct Map 2 entry places Hùng and Thành at the arrival site even when there is no carried boat party.
- Weapon skeleton checks allow missing optional forearm twist tracks while retaining strict checks for the required skeleton. The full new Nam motion layer still requires its complete rig.
- NPC prefab root motion is disabled; navigation owns actor movement. Existing Day 1 performance sampling is retained.
- Shared inventory shows the reserved quest medicines and blocks weapon switching while escorting or working.
- The synthetic patrol test re-enters its elevated exposure fixture after restore, because map safety correctly recovers out-of-world saves.
- The jump test uses deterministic 60 Hz simulation during its timed flight assertions, restoring the previous simulation setting afterward.

Validation:
- Unity 6000.3.24f1 compiled the combined runtime/editor code.
- Initial regression: 39/45 passed; six failures were investigated.
- Targeted rerun after corrections: 6/6 passed. Thus all 45 selected cases have passing results, across the two runs; this is not a single 45/45 final run.
- The rerun completes all ten Day 1 / Night 1 objectives through normal WASD/E and puzzle input, then enters Day 2 and restores player control. No teleport, Complete(), EndScene() or restored-prefix shortcut is used in this full-flow driver.
- Voice coverage test confirms all 49 spoken cues remain mapped to their approved recordings.
- XML reports and full-flow steps are attached here. Screenshots and detailed Unity logs remain under Logs/DevSync_20261022 and Logs/Map02PlayableAudit_20261022 locally.

No new paid audio was generated. Local save slots, Blender backups and generated test scenes are not part of this merge commit. This validation covers the selected regression scenarios, not every possible player action or a new-player usability session.
