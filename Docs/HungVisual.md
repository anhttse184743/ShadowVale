# Hung visual

## Escort behaviour — 2026-10-19

Per the latest direction, the Map 1 escort keeps the original `Hung_DiKhapKhieng` clip and `LimpRate`, at 1.8 m/s. The Mixamo arm-injury previews remain outside the game.

Hùng now takes a complete NavMesh route to the shelter door independently. Nam can stay behind or flank the pursuers without pulling Hùng back. Combat and Hùng's spoken request for cover do not stop his walk. Each pursuit wave can request cover once, using the existing approved `m1_story_15` voice; requests wait for current speech and share the wounded-call cooldown. Acknowledged wave requests are saved, while older saves skip calls for waves already spawned.

The HUD asks Nam to protect Hùng on the journey. Either character reaching the existing shelter-door trigger still starts the homecoming. Hùng stops if killed; no later quest, scene terrain, rig or Map 2 asset is changed.

Relevant tests: `HungHeadsHomeIndependentlyThenStaysAtTheBase`, `HungKeepsWalkingHomeAndCallsForCoverWhenPursuersArrive`, `HungEscortKeepsTheOriginalLimpClip`, plus existing wave/save/retry/homecoming checks. Current-run results are reported separately; the historical validation below does not certify this change.

## Historical setup

Built against Khoa 65c6039 (matching origin/Khoa when fetched).

The supplied hung.fbx is imported as Humanoid. Resources/Characters/HungVisual.prefab uses the dedicated NPCs/Materials/hung - Material.001.mat and the existing skin/normal textures, at 1.78m height. No duplicate model or texture is generated.

Map01HungVisual attaches the visual to the existing Hung root at startup and hides its placeholder renderers. The root NavMeshAgent, rescue health, quest and save identity remain unchanged. AC_Hung is a dedicated one-layer NPC controller referencing the existing Idle_Tuned, Walk_Tuned, Run_Tuned, Talking and Dying clips. No weapon/aim layer or copied animation clips are included. Talking plays during mission reports and stops when Hung moves; death takes priority. Navigation velocity drives locomotion. Rescue death and retry drive death/reset animation.

Rebuild the visual asset using ShadowVale > Characters > Prepare Hung. The setup requires the dedicated textured material; it does not replace it with a flat-color fallback.

Validated in an isolated Unity 6000.3.24f1 project synchronized from the latest Khoa commit: HungVisualTests (skin/normal, humanoid, movement, death, retry) and the full rescue-to-final-boss route passed. Screenshots are stored outside Assets.
