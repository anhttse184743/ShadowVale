# Isolated river runtime check

This small Unity 6000.3.24f1 project tests the three river runtime components without ShadowVale's Boot/menu routing. It enters actual Play Mode for 12 seconds, checks trigger-based buoyancy, downstream drift, two moored hull proxies and ripple events, then exits with code 0 or 1. It does not test the shader, Map 2 bank collisions or gameplay controls.

Before rerunning, copy the latest `RiverWater.cs`, `RiverBuoyantBody.cs` and `RiverWaterTrigger.cs` from the main project's `Assets/_Project/Map01/Runtime` into this project's `Assets`. The committed copies are the tested snapshot.

From the repository root in PowerShell:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe' -batchmode -nographics -projectPath "$PWD/Tools/WaterRuntimeValidation" -executeMethod WaterRuntimeValidation.Run -logFile "$PWD/Tools/Map02Reports/water-runtime.log"
```

The runner writes `Tools/Map02Reports/river-runtime-checks.txt`. Do not add `-quit`: the runner exits after the asynchronous Play Mode check finishes.

## Shoreline geometry check

`ShorelineValidation.Run` builds the render overlap with the production `RiverShorelineMesh` helper and raycasts 3,200 boundary samples against the actual Map 2 terrain. It requires `Assets/ShorelineInput.asset` (the original narrow mesh backed up at `Tools/Map02Reports/PhysicalRiver_Surface_before_shoreline.asset`) and `Assets/ShorelineTerrain.asset` (a copy of `Roadside_Continuous_Terrain.asset`). Refresh the helper copy from the main Editor directory before rerunning. These temporary mesh copies are ignored by Git. Remove an earlier `Assets/ShorelineOutput.asset` before rerunning.

Run the same Unity command with `-executeMethod ShorelineValidation.Run`. The output is `Assets/ShorelineOutput.asset`; the report is `Tools/Map02Reports/shoreline-checks.txt`. This check is geometric and does not render the URP shader.

## Map 1 character interaction

Use **ShadowVale > Map 1 > Enable footsteps and river interaction** in the main
project first. It saves 50 river sensors and exports their actual transforms and
the fitted banks to `Tools/Map01WaterReports/interaction-layout.json`.

Run the Unity command above with `-executeMethod CharacterRippleValidation.Run`
and a log path under `Tools/Map01WaterReports`. This runs an actual player-layer
CharacterController through the exported sensors in Play Mode. Entry and walking
must emit ripples; standing still, dry banks and bridge-height movement must not.
The test checks timestamps in the ripple buffer, so reaching its 16-entry limit
cannot hide unwanted emissions. Results go to
`Tools/Map01WaterReports/character-ripples.txt`. It checks the production trigger
callbacks and ripple data, rather than the rendered appearance of the shader.
