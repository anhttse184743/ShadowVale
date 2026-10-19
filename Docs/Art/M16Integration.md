# M16 integration

Model: M16 gun by hwa021216, https://sketchfab.com/3d-models/m16-gun-b0b838d9d187481f96bd9b60869220e3
License: CC BY 4.0, https://creativecommons.org/licenses/by/4.0/
Imported from the supplied GLB; normalized for the existing rifle mount. Inventory previews rendered from game assets.

Item rifle_m16 uses ammo_556 (5.56 x 45 mm), 30-round magazine, 700 RPM and 90% of AK damage per round. Existing rifle animations and hand mounts are reused. Repair, condition, salvage and requisition values follow AK. Ammunition uses the existing cartridge-box art at a smaller scale, with a separate item name and caliber.

Enemy loadouts are shuffled in groups of ten (seven M16, three AK), using the saved run seed. Remaining enemies use the remainder of the shuffled group. Existing AI burst pauses remain. Enemy magazine/reload logic is unchanged.

Build assets/previews: ShadowVale > Build M16 and inventory previews. The editor-only Tools/M16Integration.request trigger runs the same builder. M16IntegrationTests covers distribution, asset references, enemy loot, caliber separation, reload and inventory persistence.
