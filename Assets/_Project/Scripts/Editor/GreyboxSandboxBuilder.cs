using System.IO;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Builds a minimal greybox / prototype "sandbox" into <c>Map Test.unity</c>: a gridded
    /// ground, boundary walls, a few outlined blocks, a walkable player and the 2.5D iso camera.
    /// Idempotent — everything generated lives under a single <c>GREYBOX_Sandbox</c> root that is
    /// wiped and rebuilt on each run. Menu <b>ShadowVale ▸ Build Greybox Sandbox (Map Test)</b>.
    /// </summary>
    public static class GreyboxSandboxBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Maps/Map Test.unity";
        private const string TexturePath = "Assets/_Project/Art/Textures/ProtoGrid.png";
        private const string BlockMatPath = "Assets/_Project/Art/Materials/M_ProtoGrid.mat";
        private const string GroundMatPath = "Assets/_Project/Art/Materials/M_ProtoGround.mat";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";

        private const string RootName = "GREYBOX_Sandbox";
        private const float GroundSize = 20f; // 20 x 20 m play area centred on origin.
        private const float WallHeight = 2.5f;
        private const float WallThickness = 0.5f;

        [MenuItem("ShadowVale/Build Greybox Sandbox (Map Test)")]
        public static void Build()
        {
            Texture2D tex = EnsureProtoTexture();
            Material blockMat = EnsureMaterial(BlockMatPath, tex, Vector2.one, new Color(0.62f, 0.62f, 0.64f));
            Material groundMat = EnsureMaterial(GroundMatPath, tex, new Vector2(GroundSize, GroundSize), new Color(0.5f, 0.5f, 0.52f));
            GameObject playerPrefab = EnsurePlayerPrefab(blockMat);

            Scene scene = OpenMapTest();

            // Idempotency: wipe any previous generated content.
            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var root = new GameObject(RootName);

            BuildGround(root.transform, groundMat);
            BuildWalls(root.transform, blockMat);
            BuildBlocks(root.transform, blockMat);

            GameObject player = InstantiatePlayer(playerPrefab, root.transform, new Vector3(0f, 1f, 0f));
            ConfigureCamera(player.transform);
            ConfigureLight();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Greybox] Built sandbox in {ScenePath}. Press Play and move with WASD / arrows.");
        }

        // ---- Scene ----------------------------------------------------------

        private static Scene OpenMapTest()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.path == ScenePath)
            {
                return active;
            }
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // ---- Geometry -------------------------------------------------------

        private static void BuildGround(Transform parent, Material mat)
        {
            GameObject ground = CreateBox("Ground", parent, new Vector3(0f, -0.25f, 0f),
                Vector3.zero, new Vector3(GroundSize, 0.5f, GroundSize), mat);
            GameObjectUtility.SetStaticEditorFlags(ground,
                StaticEditorFlags.NavigationStatic | StaticEditorFlags.ContributeGI);
        }

        private static void BuildWalls(Transform parent, Material mat)
        {
            float half = GroundSize / 2f;
            float y = WallHeight / 2f;
            var wallHolder = new GameObject("Walls");
            wallHolder.transform.SetParent(parent, false);
            Transform h = wallHolder.transform;

            CreateBox("Wall_North", h, new Vector3(0f, y, half), Vector3.zero,
                new Vector3(GroundSize, WallHeight, WallThickness), mat);
            CreateBox("Wall_South", h, new Vector3(0f, y, -half), Vector3.zero,
                new Vector3(GroundSize, WallHeight, WallThickness), mat);
            CreateBox("Wall_East", h, new Vector3(half, y, 0f), Vector3.zero,
                new Vector3(WallThickness, WallHeight, GroundSize), mat);
            CreateBox("Wall_West", h, new Vector3(-half, y, 0f), Vector3.zero,
                new Vector3(WallThickness, WallHeight, GroundSize), mat);
        }

        private static void BuildBlocks(Transform parent, Material mat)
        {
            var holder = new GameObject("Blocks");
            holder.transform.SetParent(parent, false);
            Transform h = holder.transform;

            // Low cover (~1 m — crouch/peek height).
            CreateBox("Cover_Low", h, new Vector3(3f, 0.5f, 2f), Vector3.zero,
                new Vector3(1f, 1f, 1f), mat);

            // Tall view-blocker (~2.5 m).
            CreateBox("Blocker_Tall", h, new Vector3(-3f, 1.25f, 3f), Vector3.zero,
                new Vector3(1.5f, 2.5f, 1.5f), mat);

            // Angled crate (~0.8 m).
            CreateBox("Crate", h, new Vector3(2f, 0.4f, -3f), new Vector3(0f, 30f, 0f),
                new Vector3(0.8f, 0.8f, 0.8f), mat);

            // Walkable ramp up to the low cover.
            CreateBox("Ramp", h, new Vector3(-3f, 0.35f, -3f), new Vector3(-18f, 0f, 0f),
                new Vector3(3f, 0.3f, 3f), mat);
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 position,
            Vector3 euler, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ---- Player ---------------------------------------------------------

        private static GameObject EnsurePlayerPrefab(Material mat)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath)!);

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            temp.name = "Player";
            // CharacterController is its own collider — drop the capsule collider to avoid conflicts.
            Object.DestroyImmediate(temp.GetComponent<CapsuleCollider>());

            CharacterController cc = temp.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0f, 0f, 0f); // capsule pivot is already at its centre.

            temp.AddComponent<PlayerController>();
            temp.GetComponent<MeshRenderer>().sharedMaterial = mat;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PlayerPrefabPath);
            Object.DestroyImmediate(temp);
            Debug.Log($"[Greybox] Created {PlayerPrefabPath}");
            return prefab;
        }

        private static GameObject InstantiatePlayer(GameObject prefab, Transform parent, Vector3 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Player";
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            return instance;
        }

        // ---- Camera & light -------------------------------------------------

        private static void ConfigureCamera(Transform target)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            cam.orthographic = true;
            cam.orthographicSize = 10f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            var follow = cam.GetComponent<IsoFollowCamera>();
            if (follow == null)
            {
                follow = cam.gameObject.AddComponent<IsoFollowCamera>();
            }
            follow.SetTarget(target);

            // Frame it correctly in edit mode too (script drives it at runtime).
            cam.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            cam.transform.position = target.position + new Vector3(-12f, 14f, -12f);
        }

        private static void ConfigureLight()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                    light.shadows = LightShadows.Soft;
                    return;
                }
            }
        }

        // ---- Prototype material / texture ----------------------------------

        private static Material EnsureMaterial(string path, Texture2D tex, Vector2 tiling, Color tint)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log($"[Greybox] Created {path}");
            return mat;
        }

        private static Texture2D EnsureProtoTexture()
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(TexturePath)!);

            const int size = 256;
            const int border = 10; // outer frame thickness → the "viền".
            const int subGrid = 4; // faint quarter lines.
            var baseCol = new Color(0.60f, 0.60f, 0.62f);
            var lineCol = new Color(0.28f, 0.28f, 0.30f);
            var faintCol = new Color(0.50f, 0.50f, 0.52f);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onBorder = x < border || x >= size - border || y < border || y >= size - border;
                    bool onQuarter = (x % (size / subGrid)) < 2 || (y % (size / subGrid)) < 2;

                    Color c = baseCol;
                    if (onQuarter) c = faintCol;
                    if (onBorder) c = lineCol;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();

            File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();

            Debug.Log($"[Greybox] Created {TexturePath}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }
    }
}
