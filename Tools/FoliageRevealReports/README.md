# Map 2 foliage reveal

Bushes and Blender Hedge LOD groups now fade to 28% of their original opacity in about 0.35 seconds when Nam enters their bounds, and restore over about 0.45 seconds on exit. The inactive Night 2 observation bush is registered before it becomes visible. Tree trunks, buildings, rice and crates are not included.

Map01FoliageReveal is reused by Map02Mission. Only renderer-local material variants/property blocks change; shared assets, colliders and detection rules stay intact. Map 1 retains its existing hide-spot requirement and 62% opacity. The vertex-color shader now supports configurable blending/depth writing with unchanged opaque defaults.

Validation: runtime assembly compiled; 12 Play Mode checks passed in the isolated validation project, covering gradual alpha, restore, shared-material isolation, original tint/property blocks, LODs, inactive quest bush and collider preservation. Source: SourceArt/FoliageReveal/validate.cs. These checks use a diagnostic shader; the full Map 2 scene and its final URP rendering have not been visually reviewed in this run.