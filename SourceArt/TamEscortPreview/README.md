# Tâm escort integration for M2_D2_04

Runtime task ID: D2_04. The approved paired Blender pose is applied to the existing Nam and Tâm models in Map 2.

## Applied behavior

- Ensure a usable K54 in the pistol slot; reuse an owned K54 without duplicating it. Supply the quest's initial pistol ammunition once, persisted in chapter state.
- Keep the K54 equipped while escorting. Block other weapons, throwables, binoculars, sprint, prone movement and jumping. Release restrictions when the escort ends or is cancelled.
- Move at 1.8 m/s. Play the approved paired standing/walking poses directly by bone name, independently of the live Animator hierarchy. Advance the gait from actual horizontal displacement, using the Blender loop's distance calibration to prevent stationary feet or sliding. Both actors use the approved upright legs and flat planted shoes; Tâm's visual height is matched during escort only and restored on release, his left hand supports Tâm's left hip, and Tâm's arm rests across his neck on the far shoulder.
- Allow H / medkit use during escort without playing the ordinary kneeling bandage action; retain normal health, item consumption and cooldown rules.
- Carry the pistol low. Raise the right arm on aim or fire, without releasing the left support arm. Align the barrel to the camera through the right arm instead of bending the supporting torso.
- During reload, keep the left arm in place and lower/raise the right arm using the existing magazine's normalized reload progress. Reload duration, ammo consumption and completion remain owned by PlayerCombat/Magazine.
- Wait until Nam moves 4 metres horizontally away from the rescue origin before starting the ambush. Persist the origin and activation flag so reloading within the radius does not spawn soldiers early.
- Spawn five new ambush soldiers, at five fixed points on the two field banks of the rescue-to-bridge approach, alternating left/right; persist these points across save/load. Spawn points sit about 20 m to either side of the road; defer a spawn if either Nam or Tâm is within 20 m of its fixed point. Only one ambush soldier may be alive. Either character reaching the bridge permanently stops unspawned waves; any already living soldier must still be defeated. Older cached spawn plans and obsolete bridge-stop flags are rebuilt when resuming escort. Field positions are validated by paths that do not cross an actual bridge deck; an infinite plane from the rotated bridge must not reject dry field banks. Each spawn requires a reachable NavMesh position on the selected side.
- Ground both characters against actual walkable surfaces from their evaluated soles, including incoming model-root offsets. Keep ordinary camera target, aim distance, shoulder offset and field of view; ignore only the assisted companion in spring-arm collision checks and restore that exception on cleanup.
- Persist the issued-pistol flag, spawn count and enemy snapshots. Reach the return destination after all five ambush soldiers are defeated to complete the escort.

## Authoring

Current editable source: UprightPreview/Nam_Tam_ShoulderAssist.blend.
Approved standing previews: UprightPreview/frames/000.png, side.png and back.png.
Frame 0 is the approved standing review; frames 1–19 contain the seamless 18-frame, 30 fps walking loop.
The source loop is in place, calibrated for 2.5 m/s. Runtime movement uses 1.8 m/s and advances the source gait from actual distance travelled.

Build using Blender 5.2:

    blender -b -t 6 --python SourceArt/TamEscortPreview/build_upright_preview.py -- --render
    blender -b -t 2 --python SourceArt/TamEscortPreview/verify_animation.py

Package the rendered GIF with package_animation.py using Python and Pillow.
Copy the four generated FBX files to Assets/_Project/Art/TamEscort, then use ShadowVale/Map 2/Build approved Tâm escort in Unity. Runtime animation assets and the NamPoses/TamPoses bone-frame JSON files are saved under Resources/Story/TamEscort. Original source models, weights and controllers remain in place.

## Verification

See Tools/TamEscortReports for Unity checks and front/back pose renders. Checks run in a separate small Unity project using compiled gameplay assemblies, the actual animation clips and original models. They cover native curve binding, bone lengths, loop continuity, K54 deduplication/equipping, weapon restrictions, reload timing, checkpoint serialization, five-soldier sequencing and alternating reachable spawn points on a test NavMesh. Full Map 2 traversal and terrain-specific spawning still need an in-game playthrough.
