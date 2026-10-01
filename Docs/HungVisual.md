# Hung visual

Built against Khoa 65c6039 (matching origin/Khoa when fetched).

The supplied hung.fbx is imported as Humanoid. Resources/Characters/HungVisual.prefab uses the dedicated NPCs/Materials/hung - Material.001.mat and the existing skin/normal textures, at 1.78m height. No duplicate model or texture is generated.

Map01HungVisual attaches the visual to the existing Hung root at startup and hides its placeholder renderers. The root NavMeshAgent, rescue health, quest and save identity remain unchanged. AC_Hung is a dedicated one-layer NPC controller referencing the existing Idle_Tuned, Walk_Tuned, Run_Tuned, Talking and Dying clips. No weapon/aim layer or copied animation clips are included. Talking plays during mission reports and stops when Hung moves; death takes priority. Navigation velocity drives locomotion. Rescue death and retry drive death/reset animation.

Rebuild the visual asset using ShadowVale > Characters > Prepare Hung. The setup requires the dedicated textured material; it does not replace it with a flat-color fallback.

Validated in an isolated Unity 6000.3.24f1 project synchronized from the latest Khoa commit: HungVisualTests (skin/normal, humanoid, movement, death, retry) and the full rescue-to-final-boss route passed. Screenshots are stored outside Assets.
