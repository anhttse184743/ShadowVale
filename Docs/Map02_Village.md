# Map 2 — Ngôi làng (environment blockout)

Scene: `Assets/_Project/Scenes/Maps/Map 2.unity`.

## Accepted scope

220 × 200 metres (44,000 m²), equal to the current expanded Map 1. The river runs straight north–south at X = −36.667 m, one third of the map width from its western edge. Its width is 10.5 m: 1.5 times Map 1's nominal 7 m stream width. Map 1's stream width varies locally; the comparison uses its nominal width.

References: patchwork rice paddies and raised earthen bunds; southern Vietnamese timber/bamboo houses with layered palm-thatch roofs; longer stilt houses with open verandas, cross-braced railings and lotus gardens.

14 houses, 18 paddies, main village bridge, exposed repair ferry, concealed alternate landing, workbenches, civilian assembly courtyard, fields and survey routes. No enemy models, enemy spawners, NPCs or combat controllers are placed in this scene.

## Story layout

Nam and Hùng arrive from the forest via the western fields and bridge. Sơn's briefing anchor is near the northern village centre. The preparation phase is organised around the crafting/weapon-repair yard, material crates, a medicine-search house, an ammunition-search house and the exposed northern ferry where the boat needs repair. Civilian evacuation assembly is close to the ferry; a narrower path along the eastern riverbank leads to a concealed southern landing as an alternate route.

Trap candidates and scouting positions are empty named transforms. Night-attack design anchors identify holding, suppression, flanking and cover-pressure directions. These are planning locations only. Squad coordination, QUBO, adaptive enemy behaviour, dialogue, civilians, inventory, durability, loot, crafting and mission progression are not implemented by this environment blockout.

The optional inactive night-lighting group supports later visual planning; daytime is the default preparation view. To preview night manually, disable the daytime sun and enable the night group; environment ambience still needs a dedicated night art pass.

## Assets and editing

The Map 2 terrain is one continuous mesh with a matching collider to avoid chunk seams. Environment assets are stored in `Assets/_Project/Art/Environment/Map02_Village`. Shared Map 1 materials and house/tree assets remain referenced; new stilt-house and crop source models are in `SourceArt/Map02_Village/Village_Assets.blend` and can be regenerated with `build_village.py` in Blender 5.2.

The scene-generation menu is **ShadowVale > Map 2 > Create village blockout**. Generation refuses to overwrite an existing Map 2 scene, preserving subsequent manual changes. Story positions are under `02 Story anchors • design only • no actors` in the hierarchy. Reports and review renders are in `Tools/Map02Reports`.

This is a scene layout and environment pass, not a playable Map 2 mission. It does not change campaign routing or build settings.
