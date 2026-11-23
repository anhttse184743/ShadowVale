# Night-watch models

Editable source: `NightWatchProps.blend`, authored in Blender 5.3 at metre scale.
Geometry and wood-grain texture are original procedural project assets; no paid asset pack is required.

Rebuild from the Unity project root:

    "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python SourceArt/NightWatch_20261123/build_nightwatch.py -- <absolute-project-root>

Then run **ShadowVale > Map 2 > Build Blender night watch models** in Unity.

- `VillageGate`: fixed timber posts, woven bamboo wings, true hinge pivot and sliding bolt with receiver.
- `ShieldedLamp`: reservoir, glass chimney, flame, guards, handle, opaque rotating hood and a real spot light.
- `FenceSection`: shared mesh module for the continuous village boundary. Runtime paths preserve existing road crossings.
- Articulation axes and bolt retraction direction are derived from the imported FBX reference pose, rather than assuming FBX local Y is world up.
- Inspection cameras use layer 31 in an isolated studio; their meshes are copies of the in-world prefabs. Physics/navigation are disabled only on those inspection copies.
- The gate interaction target is inside the fence. Night 1 duty closes the two inspected gates; other chapter phases leave those portals open.

Runtime placement: `Map02ChapterDayOne.NightModels.cs` and `Map02ChapterDayOne.NightFence.cs`.
Validation: `Map02NightWatchModelTests`; input playthrough: `Map02DayOnePlayableFlowTests`; video: explicit `Map02DockMovieTests.RecordNightWatchTutorialAndAllThreeChecks`.
