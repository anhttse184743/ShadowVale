# Map 2 shot tracer lifecycle fix

Nam survives the single-scene Map 1 to Map 2 transfer. Previously his detached
ShotTracers scene root did not, leaving cached managed references to destroyed
native ShotTracer and LineRenderer objects. Firing then repeatedly threw
MissingReferenceException and flooded the Unity Console.

The pooled root now belongs to Nam. Renderers use world-space points so moving
the actor does not drag a fired tracer. Missing roots or individual slots are
recovered when needed. ShotTracer checks native object lifetime before accessing
the renderer, and the combat owner releases its pool on destruction. Healthy
shots reuse existing objects and shared materials.

Unity 6000.3.24f1 verification: 5/5 tests passed. Natural and skipped Map 2
arrival each fire three actual gameplay shots (ammo decremented normally), then
reuse the same pool for 500 tracer requests without unexpected logs or pool
growth. Additional checks cover destroyed managed wrappers, pool and slot loss,
cached renderer recovery, world-space coordinates, flash expiration and owner
cleanup. Existing arrival animation, spawn and equipment checks also pass.

This removes the reported exception flood. No independent FPS benchmark of the
village scene was made; the tests establish pool stability and absence of this
exception, not a general frame-rate guarantee.
