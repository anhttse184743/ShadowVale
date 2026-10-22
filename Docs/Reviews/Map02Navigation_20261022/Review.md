# Map 2 — navigation, playable boundary and Day 1 / Night 1 review

## Important checkout mismatch
The user's Unity Editor log identified G:\game\ShadowVale as the project just played. This is a separate older checkout (HEAD 1ad9f613) with nearest-objective selection and the retired Contact/VillageHelp mission HUD. The active implementation is G:\game\Capstone\ShadowVale (HEAD 697e90f2 plus local work). Neither checkout was reset or overwritten.

## Changes
- The minimap displays the complete square north-up survey. Player, companion, objective and route all use the same Cartesian projection as the full map. Previously radial clamping moved corner pins away from the rendered terrain.
- Contact/report/medicine pins follow the real NPC. Guidance identifies the person and action; the tracked task stays selected when walking near another task.
- Four physical colliders bound the actual 220 x 200 m authored ground after boat arrival. The surrounding hills/fields are scenery meshes without colliders, not additional playable terrain.
- Mission-owned fall/outside recovery replaces the protection lost when the exploration-only controller is disabled. It uses recent collider/NavMesh-confirmed ground; invalid old saves fall back to a validated nearby ground point or landing. Recovery preserves health, inventory, ammunition and chapter progress.
- Removed unused circular-mask texture generation from the HUD.

## Day 1 / Night 1 route to play
1. Landing bank conversation, then E at anh Lực's gate for the password. The bank conversation does not complete the contact task.
2. E to accompany Hùng, walk to chị Lành's porch and finish bandaging.
3. Three independent jobs, in any order (J selects the tracked job): medicine to chị Lành for ông Sáu; three crate trips then warehouse placement puzzle; two stakes and three planks at the south dock.
4. E report to anh Thành only after all three jobs. This changes to Night 1.
5. E at the porch meal to play “Lời hứa”.
6. Accompany Hùng to bamboo post, check/correct the lamp, then report to the guard.
7. Accompany Hùng to village gate, inspect/correct two faults, report and give the separate patrol medicine packet.
8. E at the sleeping mat to change watch; silent rest scene then Day 2.

## Scope and limits
This review does not declare final character art, hand contact or every new voice recording complete. The 48-cue Day 1 / Night 1 voice plan still includes pending recordings; no paid audio was generated. Later-day systems are retained and checked for chapter progression compatibility.

## Verification
- tests.xml: 29/29 passed, including real CharacterController perimeter blocking, invalid underworld save recovery, stable focus, real escort traversal to the south dock, puzzle integration, all six chore orders and start-save reload.
- final.xml: final rerun and chapter-wide reachable-site/save/phase checks (see result XML).
- Screenshot day1-hud.png captures the real Full HD in-game HUD.
- The start-save export goes through isolated slot 1 and the actual Map02Mission.BeginGame load route. The player's real slot is installed only after tests, with its previous contents backed up.

## Installed player save
- Menu: Tải game → Ô LƯU 01 → MAP 2 — NGÀY 1 · CÓ CHỖ NGHỈ CHÂN.
- Path: C:\Users\dangk\AppData\LocalLow\DefaultCompany\ShadowVale\shadowvale-slot-1.json
- Starts with zero completed tasks and zero elapsed time, immediately after the boat scene; the bank introduction is still unseen.
- Previous slot 1: Logs/Map02Navigation_20261022/slot-backup/shadowvale-slot-1-before.json.
- SHA-256: 515A2E7126AD278A836A1791DCABE2C6C8395FED44AE23FEF1DEFA51F3E07697
- All other player slots were preserved.

Final results: tests.xml 29/29, final.xml 9/9, perimeter-final.xml 2/2 passed. ready.xml repeats boundary and focus regressions after moving dynamic pin refresh out of the HUD getter into the per-frame mission update.

ready.xml: 9/9 passed. All 23 chapter sites have complete NavMesh routes from the command post. The final source has no whitespace errors (git diff --check).
