# Map 1: opening briefing

On branch Khoa, New Game now loads Map 1 and requests an in-world briefing. The old Intro.mp4 and VideoPlayer menu path are removed. Continuing a checkpoint does not request the briefing.

## Assets
- Reuses Idle_Tuned.anim and the player's visual mesh, avatar and textures.
- Uses Art/cutsence/Salute.fbx, Talking.fbx and Pointing.fbx directly.
- Commander variation uses a runtime tint, shoulder insignia and a chest nameplate. No second character texture set or duplicated FBX.
- Commander.mp3 and Soldier.mp3 are synthetic Vietnamese voices. Lip sync is not included.
- Resources/Cutscenes/Map01Opening.prefab references those clips. ShadowVale > Cutscene > Prepare opening rebuilds the references after replacing source clips.

## Flow
Movement, combat, interaction, gameplay HUD and saving are blocked while the briefing plays. The camera blends back to the existing third-person rig over 1.2 seconds. Hold Esc for one second to skip. Skip and natural completion use the same cleanup. Salute plays once during the soldier's reply; no extra nod or turn animation.

The supply briefing mentions looking for Hung at the northern jetty so it remains compatible with the existing rescue quest and checkpoint stage numbering.

## Verification
OpeningCutsceneTests checks asset reuse and the complete briefing. ForestMenuIntegrationTests covers starting from the menu and skipping. Logs/Cutscene contains local runtime screenshots and smoke-test results; these are not game assets.

No Timeline/Cinemachine package is added: existing camera code and Unity animation Playables provide this one short sequence.

