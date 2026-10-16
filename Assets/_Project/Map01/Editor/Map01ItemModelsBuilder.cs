using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShadowVale.Map01.Editor
{
    /// <summary>
    /// ShadowVale > Map 1 > Nhập mô hình vật phẩm: brings in the iron chest and the item models built in
    /// Blender (SourceArt/Map01_Items/build_map01_items.py writes them to Art/Environment/Map01Items/Meshes),
    /// makes their materials (the colours the script gives them) or reuses the FieldCamp ones by name, and
    /// fills Map01/Resources/Items/Map01ItemCatalog.asset, which Map01CampChests and the item photographs
    /// read. The starting rifle, the knife and the stone keep the game's own prefabs. Map 1.unity is not touched.
    /// </summary>
    public static class Map01ItemModelsBuilder
    {
        public const string CatalogPath = "Assets/_Project/Map01/Resources/Items/Map01ItemCatalog.asset";
        private const string ModelFolder = "Assets/_Project/Art/Environment/Map01Items/Meshes";
        private const string MaterialFolder = "Assets/_Project/Art/Environment/Map01Items/Materials";
        private const string FieldCampFolder = "Assets/_Project/Art/Environment/FieldCamp/Materials";

        private static readonly string[] FieldCamp = { "Worn timber", "Weathered olive canvas", "Hemp rope", "Aged paper" };

        /// <summary>Name, linear colour (as in Blender), roughness, metallic, two-sided: kept in step with build_map01_items.py.</summary>
        private static readonly (string name, float r, float g, float b, float rough, float metal, bool twoSided)[] Own =
        {
            ("Chest steel", .15f, .17f, .13f, .5f, .6f, false),
            ("Chest interior", .035f, .038f, .03f, .9f, 0f, false),
            ("Steel", .55f, .55f, .56f, .35f, 1f, false),
            ("Blued steel", .07f, .075f, .085f, .35f, .85f, false),
            ("Brass", .72f, .52f, .20f, .3f, 1f, false),
            ("Copper jacket", .70f, .36f, .18f, .3f, 1f, false),
            ("Bakelite brown", .15f, .065f, .03f, .4f, 0f, false),
            ("Birch stock", .30f, .13f, .055f, .45f, 0f, false),
            ("Stencil yellow", .62f, .47f, .12f, .75f, 0f, false),
            ("Cardboard", .52f, .40f, .25f, .9f, 0f, false),
            ("Bandage gauze", .84f, .81f, .70f, .95f, 0f, false),
            ("Khaki cloth", .45f, .40f, .28f, .95f, 0f, true),
            ("Rust", .26f, .12f, .06f, .8f, .35f, false),
            ("Oil can metal", .13f, .14f, .12f, .45f, .7f, false),
            ("Ink", .12f, .18f, .30f, .8f, 0f, false),
            ("Strap leather", .22f, .13f, .07f, .75f, 0f, false),
            ("Lens glass", .18f, .32f, .38f, .05f, .2f, false),
            ("Black panel", .03f, .03f, .03f, .5f, 0f, false),
            ("Herb leaf", .10f, .28f, .07f, .6f, 0f, true),
            ("Herb stem", .20f, .28f, .10f, .7f, 0f, false),
            ("Smoke grey", .45f, .46f, .45f, .6f, .3f, false),
            ("Smoke orange", .85f, .35f, .05f, .6f, 0f, false),
            ("Charge red", .48f, .07f, .05f, .55f, 0f, false),
            ("Gunpowder black", .045f, .045f, .042f, 1f, 0f, false),
            ("Grenade olive", .10f, .12f, .06f, .6f, .25f, false),
        };

        /// <summary>Items that keep a model the game already has.</summary>
        private static readonly (string id, string path, float scale)[] Existing =
        {
            ("rifle_standard", "Assets/_Project/Prefabs/Items/W_AK47.prefab", 1f),
            ("knife", "Assets/_Project/Prefabs/Items/W_Knife.prefab", 1f),
            ("stone", "Assets/_Project/Art/Environment/AnGiangRocks/Prefabs/Pebble_0.prefab", .45f),  // as the stone sites show it
        };

        [MenuItem("ShadowVale/Map 1/Nhập mô hình vật phẩm")]
        public static void Build()
        {
            var materials = PrepareMaterials();
            foreach (var path in Directory.GetFiles(ModelFolder, "*.fbx").Select(p => p.Replace('\\', '/')))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.addCollider = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                foreach (var pair in materials) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
                importer.SaveAndReimport();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            var catalog = AssetDatabase.LoadAssetAtPath<Map01ItemCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<Map01ItemCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.chest = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/IronChest.fbx");
            var entries = new List<Map01ItemCatalog.Entry>();
            foreach (var (id, path, scale) in Existing)
                entries.Add(new Map01ItemCatalog.Entry { id = id, model = AssetDatabase.LoadAssetAtPath<GameObject>(path), scale = scale });
            foreach (var path in Directory.GetFiles(ModelFolder, "Item_*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p))
                entries.Add(new Map01ItemCatalog.Entry { id = Path.GetFileNameWithoutExtension(path).Substring("Item_".Length), model = AssetDatabase.LoadAssetAtPath<GameObject>(path) });
            catalog.items = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            var missing = entries.Where(e => e.model == null).Select(e => e.id).ToArray();
            Debug.Log($"[ItemModels] chest {(catalog.chest != null ? "ok" : "MISSING")}, {entries.Count} items" + (missing.Length > 0 ? ", missing: " + string.Join(", ", missing) : "") + ".");
        }

        /// <summary>The materials, made or brought up to date, by the names the models use.</summary>
        private static Dictionary<string, Material> PrepareMaterials()
        {
            Directory.CreateDirectory(MaterialFolder);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var materials = new Dictionary<string, Material>();
            foreach (var name in FieldCamp)
            {
                var existing = AssetDatabase.LoadAssetAtPath<Material>($"{FieldCampFolder}/{name}.mat");
                if (existing != null) materials[name] = existing;
            }
            foreach (var spec in Own)
            {
                string path = $"{MaterialFolder}/{spec.name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(lit); AssetDatabase.CreateAsset(material, path); }
                material.shader = lit;
                // Blender's colours are linear; a material's colour is entered as it is seen.
                material.SetColor("_BaseColor", new Color(Mathf.LinearToGammaSpace(spec.r), Mathf.LinearToGammaSpace(spec.g), Mathf.LinearToGammaSpace(spec.b)));
                material.SetFloat("_Metallic", spec.metal);
                material.SetFloat("_Smoothness", 1 - spec.rough);
                material.SetFloat("_Cull", spec.twoSided ? 0 : 2);
                material.doubleSidedGI = spec.twoSided;
                EditorUtility.SetDirty(material);
                materials[spec.name] = material;
            }
            AssetDatabase.SaveAssets();
            return materials;
        }
    }
}
