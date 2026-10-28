# Combat feedback and stamina

Enemy presentation is shared by `Map01EnemyController` and `EnemyAgent` through
`EnemyFeedback`. It binds to the player's existing FootstepBank, gunshot AudioSlice
and ShotTracer prefab at runtime; no scene regeneration is required.

- Footfalls follow actual horizontal travel, alternate feet and stop when stationary,
  dead or held by the mission. Teleports reset the stride. Footsteps use full 3D audio,
  linear attenuation from 2 to 20 metres; gunshots use 2 to 65 metres. The source is
  translated relative to the camera listener so hearing distance is measured from Nam.
- Aimed, hostage and suppressive fire share sound/tracer presentation. Traces stop at
  the nearest obstruction while ignoring the shooter's own colliders. Suppressive
  shots remain cosmetic. Each enemy reuses up to three tracers.
- Confirmed bullet damage emits short round blood droplets at the hit point. This
  includes Nam, guards and Hùng's separate rescue health. Invulnerability, dead targets
  and blocked shots do not emit blood. Twelve particle systems are reused per scene;
  material and mesh are packaged under `Resources/CombatFeedback`.
- Map 1 stamina is 300 (previously 100); drain remains 12/s and recovery 16/s. Old
  checkpoints retain their saved stamina value and recover towards the new maximum.
- Accuracy uses measured CharacterController displacement. A sprint toggle while
  stationary or pushing into a wall no longer selects running spread. Actual speed
  above 2.7 m/s with sprint enabled selects running. Crouch and airborne handling remain.
- Standing spread is 0.4 degrees without aiming / 0.1 degrees while aiming. Full bloom
  adds at most 0.9 degrees. Three opening rounds add no bloom; recoil is gentler, with
  about 0.55 degrees of climb over three rounds and 12 degrees over a full magazine.

Validation covers Gameplay EditMode tests, CombatFeedbackIntegrationTests (real
Shift input and collision in Play Mode, asset wiring, shot audio/tracers/blood and
effect expiry), ForestGameplayMergeTests (save/load and pause), ShotTracerLifecycleTests
and GuardDamageFollowsMap01Balance. Spatial audio settings are checked automatically;
final loudness preference should be judged while playing with the user's audio setup.
