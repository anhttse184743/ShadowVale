# Quartermaster proposal — pending visual approval

Run with Blender 5.2: `blender --background --factory-startup --threads 2 --python SourceArt/QuartermasterPreview/build_preview.py`.

Outputs are three studio renders, a packed editable Blender file, and collider dimensions in Blender coordinates (Z up). Nothing is exported into Assets and no Unity prefab or scene is modified.

The rack reuses `Assets/_Project/Art/Items/ak-47.glb`, mesh GUID `56a57ebd8c4aa0340879e6193a191498`, referenced by `W_AK47.prefab`. UVs and source materials are retained. The repeated rifles share one mesh.

Static inspection of existing LayoutTest prefabs:

- EquipmentShelf has two enabled, non-trigger BoxColliders attached to side posts. Each post is only 0.1 m wide; the center of the loaded shelf has no blocking volume.
- RadioDesk has one enabled, non-trigger BoxCollider on the tabletop at Y=0.8, scaled to 1.45 × 0.12 × 0.65 m. It does not provide a full body blocker or a chair collider.

The Blender proposal includes hidden wireframe blocker boxes for the loaded rack, loaded shelf, desk and chair. These are design metadata, not functioning Unity physics components. The new chair back is on negative Y, so the seated direction points toward the table at positive Y.

After approval: fit to the existing prefab footprints, bake procedural materials for Unity, install solid BoxColliders and verify layer interaction and actual CharacterController movement in the target scene. Scene overrides and runtime movement have not yet been validated; the static inspection alone cannot establish every cause of pass-through.
