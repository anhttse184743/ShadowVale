using System.Collections.Generic;
using System.IO;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Builds the greybox prototype map into <c>Map Test.unity</c>: a 100 x 100 m gridded ground,
    /// boundary walls, a few hundred scattered obstacles and four landmarks, the Mixamo character
    /// as a walkable player, and a Minecraft-style third-person camera.
    /// Idempotent — everything generated lives under a single <c>GREYBOX_Sandbox</c> root that is
    /// wiped and rebuilt on each run, and the scatter is seeded so the layout is reproducible.
    /// Menu <b>ShadowVale ▸ Build Greybox Sandbox (Map Test)</b>.
    /// </summary>
    public static class GreyboxSandboxBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Maps/Map Test.unity";
        private const string TexturePath = "Assets/_Project/Art/Textures/ProtoGrid.png";
        private const string BlockMatPath = "Assets/_Project/Art/Materials/M_ProtoGrid.mat";
        private const string GroundMatPath = "Assets/_Project/Art/Materials/M_ProtoGround.mat";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        /// <summary>
        /// Resolved, not hardcoded: the character's filename has changed twice already, and a
        /// mismatch produces a mangled character rather than a clean failure.
        /// </summary>
        private static string CharacterModelPath => CharacterImportSetup.ResolvePlayerModelPath();

        /// <summary>
        /// Hip height the movement is tuned around, in metres. Scaling by hips rather than overall
        /// height keeps leg length — and therefore stride and step height — consistent when the
        /// art is swapped.
        /// </summary>
        private const float TargetHipHeight = 0.99f;

        /// <summary>Ankle joint height above the sole, for a roughly 1.8 m character.</summary>
        private const float AnkleHeight = 0.1f;

        /// <summary>Rescales closer to 1 than this are left alone — art that is already right.</summary>
        private const float ScaleDeadzone = 0.06f;

        /// <summary>
        /// The test bot deliberately uses the plain Mixamo dummy, not the player's character, so
        /// the two are never confused in a screenshot or a bug report.
        /// </summary>
        private const string BotModelPath = "Assets/_Project/Art/Characters/Player/Y Bot.fbx";

        /// <summary>Extra seconds after the death animation before a body stands back up.</summary>
        private const float RespawnBuffer = 1f;
        private const string WeaponFolder = "Assets/_Project/Prefabs/Items";
        private const string TargetMatPath = "Assets/_Project/Art/Materials/M_ProtoTarget.mat";
        private const string TracerMatPath = "Assets/_Project/Art/Materials/M_Tracer.mat";
        private const string TracerPrefabPath = "Assets/_Project/Prefabs/VFX/FX_ShotTracer.prefab";

        /// <summary>
        /// Resting camera distance. Set explicitly on rebuild because the Main Camera lives
        /// outside the generated root, so its component survives and would keep old values.
        /// </summary>
        private const float CameraDistance = 3.2f;
        private const float CameraMinDistance = 1.2f;


        private const string RootName = "GREYBOX_Sandbox";
        private const string PlayerLayerName = "Player";

        private const float GroundSize = 100f;
        private const float WallHeight = 4f;
        private const float WallThickness = 1f;

        /// <summary>Radius around the origin kept free so the player never spawns inside a block.</summary>
        private const float SpawnClearRadius = 10f;

        /// <summary>Fixed seed: the same layout every rebuild, so bug reports stay reproducible.</summary>
        private const int ScatterSeed = 20260921;

        private const float CellSize = 7f;

        [MenuItem("ShadowVale/Build Greybox Sandbox (Map Test)")]
        public static void Build()
        {
            Texture2D tex = EnsureProtoTexture();
            Material blockMat = EnsureMaterial(BlockMatPath, tex, Vector2.one, new Color(0.62f, 0.62f, 0.64f));
            Material groundMat = EnsureMaterial(GroundMatPath, tex, new Vector2(GroundSize, GroundSize), new Color(0.5f, 0.5f, 0.52f));
            Material targetMat = EnsureMaterial(TargetMatPath, tex, Vector2.one, new Color(0.75f, 0.25f, 0.22f));
            int playerLayer = EnsurePlayerLayer();
            GameObject playerPrefab = EnsurePlayerPrefab(blockMat, playerLayer);

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
            BuildScatter(root.transform, blockMat);
            BuildLandmarks(root.transform, blockMat);
            BuildTargetDummies(root.transform, targetMat);

            GameObject player = InstantiatePlayer(playerPrefab, root.transform, new Vector3(0f, 0.2f, 0f));
            BuildBot(root.transform, new Vector3(0f, 0f, 7f), targetMat);
            ConfigureCamera(player.transform, playerLayer);
            ConfigureLight();
            HudBuilder.CreateHud(root.transform, player.GetComponent<PlayerCombat>());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Greybox] Built {GroundSize} x {GroundSize} sandbox in {ScenePath}. " +
                      "Play: WASD move, Shift toggles sprint, Ctrl toggles sneak, Space jump, " +
                      "1 rifle / 2 knife / 3 fists, LMB attack, RMB aim (rifle), mouse look, wheel zoom.");
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
            Transform h = NewHolder("Walls", parent);

            CreateBox("Wall_North", h, new Vector3(0f, y, half), Vector3.zero,
                new Vector3(GroundSize, WallHeight, WallThickness), mat);
            CreateBox("Wall_South", h, new Vector3(0f, y, -half), Vector3.zero,
                new Vector3(GroundSize, WallHeight, WallThickness), mat);
            CreateBox("Wall_East", h, new Vector3(half, y, 0f), Vector3.zero,
                new Vector3(WallThickness, WallHeight, GroundSize), mat);
            CreateBox("Wall_West", h, new Vector3(-half, y, 0f), Vector3.zero,
                new Vector3(WallThickness, WallHeight, GroundSize), mat);
        }

        /// <summary>
        /// Walks a <see cref="CellSize"/> grid over the map and drops up to three obstacles per
        /// cell, jittered inside it. Seeded, so the same map comes out every time.
        /// </summary>
        private static void BuildScatter(Transform parent, Material mat)
        {
            Transform h = NewHolder("Obstacles", parent);
            var rng = new System.Random(ScatterSeed);
            float half = GroundSize / 2f - CellSize / 2f;
            int index = 0;

            for (float cz = -half; cz <= half; cz += CellSize)
            {
                for (float cx = -half; cx <= half; cx += CellSize)
                {
                    int count = Roll(rng) switch
                    {
                        < 0.12f => 0,
                        < 0.60f => 1,
                        < 0.90f => 2,
                        _ => 3,
                    };

                    for (int i = 0; i < count; i++)
                    {
                        float x = cx + Range(rng, -CellSize * 0.4f, CellSize * 0.4f);
                        float z = cz + Range(rng, -CellSize * 0.4f, CellSize * 0.4f);
                        var xz = new Vector2(x, z);

                        // Keep the spawn area and the landmark plots clear.
                        if (xz.magnitude < SpawnClearRadius || IsInsideLandmarkPlot(xz))
                        {
                            continue;
                        }

                        SpawnObstacle(h, rng, x, z, mat, index++);
                    }
                }
            }
        }

        private static void SpawnObstacle(Transform parent, System.Random rng, float x, float z,
            Material mat, int index)
        {
            float yaw = Range(rng, 0f, 360f);
            float roll = Roll(rng);

            if (roll < 0.35f)
            {
                // Crate — jumpable, roughly Minecraft block sized.
                float s = Range(rng, 0.8f, 2f);
                CreateBox($"Crate_{index}", parent, new Vector3(x, s / 2f, z),
                    new Vector3(0f, yaw, 0f), new Vector3(s, s, s), mat);
            }
            else if (roll < 0.6f)
            {
                // Low block — cover you can see over while sprinting past.
                float w = Range(rng, 2f, 5f);
                float d = Range(rng, 2f, 5f);
                float hgt = Range(rng, 1f, 3f);
                CreateBox($"Block_{index}", parent, new Vector3(x, hgt / 2f, z),
                    new Vector3(0f, yaw, 0f), new Vector3(w, hgt, d), mat);
            }
            else if (roll < 0.8f)
            {
                // Pillar — breaks up sightlines and gives the camera something to collide with.
                float w = Range(rng, 1f, 2.5f);
                float hgt = Range(rng, 4f, 12f);
                CreateBox($"Pillar_{index}", parent, new Vector3(x, hgt / 2f, z),
                    new Vector3(0f, yaw, 0f), new Vector3(w, hgt, w), mat);
            }
            else if (roll < 0.92f)
            {
                // Free-standing wall segment.
                float len = Range(rng, 4f, 12f);
                float hgt = Range(rng, 2f, 4f);
                CreateBox($"WallSeg_{index}", parent, new Vector3(x, hgt / 2f, z),
                    new Vector3(0f, Mathf.Round(yaw / 45f) * 45f, 0f),
                    new Vector3(len, hgt, 0.6f), mat);
            }
            else
            {
                // Ramp — the only way up onto some of the taller blocks.
                float len = Range(rng, 4f, 7f);
                CreateBox($"Ramp_{index}", parent, new Vector3(x, 0.4f, z),
                    new Vector3(Range(rng, -25f, -15f), yaw, 0f),
                    new Vector3(3f, 0.3f, len), mat);
            }
        }

        // ---- Landmarks ------------------------------------------------------

        /// <summary>Plot centres for the four landmarks; the scatter avoids these.</summary>
        private static readonly Vector2[] LandmarkPlots =
        {
            new(30f, 30f),   // stepped pyramid
            new(-32f, -32f), // tower
            new(-30f, 30f),  // wall maze
            new(32f, -30f),  // raised platform + ramp
        };

        private const float LandmarkPlotRadius = 14f;

        private static bool IsInsideLandmarkPlot(Vector2 xz)
        {
            foreach (Vector2 plot in LandmarkPlots)
            {
                if ((xz - plot).magnitude < LandmarkPlotRadius)
                {
                    return true;
                }
            }
            return false;
        }

        private static void BuildLandmarks(Transform parent, Material mat)
        {
            Transform h = NewHolder("Landmarks", parent);
            BuildPyramid(h, LandmarkPlots[0], mat);
            BuildTower(h, LandmarkPlots[1], mat);
            BuildMaze(h, LandmarkPlots[2], mat);
            BuildPlatform(h, LandmarkPlots[3], mat);
        }

        /// <summary>Stepped pyramid — each tier is one jump up, so it is climbable without ramps.</summary>
        private static void BuildPyramid(Transform parent, Vector2 centre, Material mat)
        {
            Transform h = NewHolder("Pyramid", parent);
            const int tiers = 6;
            const float tierHeight = 1.1f;

            for (int i = 0; i < tiers; i++)
            {
                float size = 18f - i * 3f;
                float y = tierHeight * i + tierHeight / 2f;
                CreateBox($"Tier_{i}", h, new Vector3(centre.x, y, centre.y), Vector3.zero,
                    new Vector3(size, tierHeight, size), mat);
            }
        }

        /// <summary>Tall tower with a spiral of ledges — a vertical test for the camera arm.</summary>
        private static void BuildTower(Transform parent, Vector2 centre, Material mat)
        {
            Transform h = NewHolder("Tower", parent);
            const float towerHeight = 24f;

            CreateBox("Shaft", h, new Vector3(centre.x, towerHeight / 2f, centre.y), Vector3.zero,
                new Vector3(5f, towerHeight, 5f), mat);

            // Ledges spiral upward, each one a jump above the last.
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                float y = 1.4f + i * 1.8f;
                var offset = new Vector3(Mathf.Sin(angle) * 3.6f, 0f, Mathf.Cos(angle) * 3.6f);
                CreateBox($"Ledge_{i}", h, new Vector3(centre.x, y, centre.y) + offset,
                    new Vector3(0f, i * 60f, 0f), new Vector3(3f, 0.4f, 3f), mat);
            }
        }

        /// <summary>A loose grid of wall segments with gaps — somewhere to lose line of sight.</summary>
        private static void BuildMaze(Transform parent, Vector2 centre, Material mat)
        {
            Transform h = NewHolder("Maze", parent);
            var rng = new System.Random(ScatterSeed + 1);
            const int lines = 5;
            const float spacing = 5f;

            for (int i = 0; i < lines; i++)
            {
                float offset = (i - (lines - 1) / 2f) * spacing;

                // Horizontal run with a gap punched through it.
                if (Roll(rng) < 0.8f)
                {
                    float gap = Range(rng, -6f, 6f);
                    CreateBox($"MazeH_{i}a", h, new Vector3(centre.x - 6f + gap / 2f, 1.5f, centre.y + offset),
                        Vector3.zero, new Vector3(10f + gap, 3f, 0.6f), mat);
                    CreateBox($"MazeH_{i}b", h, new Vector3(centre.x + 7f + gap / 2f, 1.5f, centre.y + offset),
                        Vector3.zero, new Vector3(6f - gap, 3f, 0.6f), mat);
                }

                // Vertical run.
                if (Roll(rng) < 0.7f)
                {
                    CreateBox($"MazeV_{i}", h, new Vector3(centre.x + offset, 1.5f, centre.y + Range(rng, -4f, 4f)),
                        Vector3.zero, new Vector3(0.6f, 3f, Range(rng, 6f, 14f)), mat);
                }
            }
        }

        /// <summary>Raised platform reached by a long ramp — tests slope walking and fall damage hooks.</summary>
        private static void BuildPlatform(Transform parent, Vector2 centre, Material mat)
        {
            Transform h = NewHolder("Platform", parent);
            const float deckHeight = 5f;

            CreateBox("Deck", h, new Vector3(centre.x, deckHeight, centre.y), Vector3.zero,
                new Vector3(16f, 0.6f, 16f), mat);

            // Four legs so it reads as a structure rather than a floating slab.
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -7f : 7f;
                float sz = (i < 2) ? -7f : 7f;
                CreateBox($"Leg_{i}", h, new Vector3(centre.x + sx, deckHeight / 2f, centre.y + sz),
                    Vector3.zero, new Vector3(1.2f, deckHeight, 1.2f), mat);
            }

            // Ramp up from ground level to the deck, long enough to walk rather than jump.
            const float rampLength = 18f;
            float angle = Mathf.Atan2(deckHeight, rampLength) * Mathf.Rad2Deg;
            CreateBox("Ramp", h, new Vector3(centre.x, deckHeight / 2f, centre.y - 8f - rampLength / 2f),
                new Vector3(-angle, 0f, 0f), new Vector3(4f, 0.4f, rampLength / Mathf.Cos(angle * Mathf.Deg2Rad)), mat);

            // Guard rail so you have something to vault.
            CreateBox("Rail", h, new Vector3(centre.x, deckHeight + 0.8f, centre.y + 8f), Vector3.zero,
                new Vector3(16f, 1f, 0.4f), mat);
        }

        /// <summary>
        /// A ring of shootable dummies near the spawn, so every weapon has something to hit the
        /// moment you press Play. Each carries Health and topples out of the way when killed.
        /// </summary>
        private static void BuildTargetDummies(Transform parent, Material mat)
        {
            Transform h = NewHolder("Targets", parent);
            const int count = 8;
            const float radius = 13f;

            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count) * Mathf.Deg2Rad;
                var position = new Vector3(Mathf.Sin(angle) * radius, 1f, Mathf.Cos(angle) * radius);

                GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = $"Target_{i}";
                dummy.transform.SetParent(h, false);
                dummy.transform.localPosition = position;
                dummy.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
                dummy.GetComponent<MeshRenderer>().sharedMaterial = mat;

                // Non-static: they disappear when killed, which is the hit feedback for now.
                var dummyHealth = dummy.AddComponent<Health>();
                var so = new SerializedObject(dummyHealth);
                so.FindProperty("maxHealth").floatValue = 60f;
                so.FindProperty("despawnDelay").floatValue = 0.4f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// A stationary humanoid built from the same character and animator as the player, so
        /// shots, swings and the death animation can be checked against a real rig rather than a
        /// capsule. No controller — it just stands in its idle and faces the spawn.
        /// </summary>
        private static void BuildBot(Transform parent, Vector3 position, Material markerMat)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(BotModelPath);
            if (model == null)
            {
                Debug.LogWarning($"[Greybox] {BotModelPath} missing — skipping the test bot.");
                return;
            }

            var bot = new GameObject("Bot_Dummy");
            bot.transform.SetParent(parent, false);
            bot.transform.localPosition = position;
            // Face the player spawn at the origin.
            bot.transform.localRotation = Quaternion.LookRotation(-position.normalized, Vector3.up);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            instance.name = "Model";
            instance.transform.SetParent(bot.transform, false);

            Animator animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.avatar = FindAvatar(BotModelPath);
            NormalizeCharacterModel(instance);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorBuilder.ControllerPath);
            if (ac != null)
            {
                // Speed stays 0, so the locomotion blend tree holds the idle pose.
                animator.runtimeAnimatorController = ac;
            }

            // A skinned mesh has no collider of its own — without this nothing can hit it.
            var capsule = bot.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            var health = bot.AddComponent<Health>();
            var so = new SerializedObject(health);
            so.FindProperty("maxHealth").floatValue = 100f;
            so.FindProperty("animator").objectReferenceValue = animator;
            // Respawn once the corpse has finished falling, so the bot is always shootable again
            // without a scene reload. despawnDelay stays 0 — it revives instead of disappearing.
            so.FindProperty("respawnDelay").floatValue = DeathClipLength() + RespawnBuffer;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Marker disc so the bot reads as a target rather than a second player.
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Marker";
            disc.transform.SetParent(bot.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            disc.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);
            disc.GetComponent<MeshRenderer>().sharedMaterial = markerMat;
            Object.DestroyImmediate(disc.GetComponent<CapsuleCollider>());
        }

        // ---- Primitives -----------------------------------------------------

        private static Transform NewHolder(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
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
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);
            return go;
        }

        private static float Roll(System.Random rng) => (float)rng.NextDouble();

        private static float Range(System.Random rng, float min, float max) =>
            min + (float)rng.NextDouble() * (max - min);

        // ---- Player ---------------------------------------------------------

        /// <summary>
        /// Builds the player prefab around the imported Mixamo character: CharacterController on
        /// the root, model as a child, Animator wired to AC_Player. Rebuilt on every run so the
        /// prefab tracks changes to the model or the controller.
        /// </summary>
        private static GameObject EnsurePlayerPrefab(Material fallbackMat, int layer)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath)!);

            var rootGo = new GameObject("Player") { layer = layer };

            CharacterController cc = rootGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f); // Root sits at the feet.
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;
            // The default 0.08 m skin is enough to read as the character hovering.
            cc.skinWidth = 0.02f;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            Animator animator = null;

            if (model != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                // Unpack: the prefab must own the model's transform hierarchy outright.
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                instance.name = "Model";
                instance.transform.SetParent(rootGo.transform, false);
                SetLayerRecursively(instance, layer);

                animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
                animator.avatar = FindAvatar(CharacterModelPath);
                // After the avatar: normalisation reads humanoid bones through it.
                NormalizeCharacterModel(instance);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    PlayerAnimatorBuilder.ControllerPath);
                if (ac != null)
                {
                    animator.runtimeAnimatorController = ac;
                }
                else
                {
                    Debug.LogWarning("[Greybox] No AC_Player controller — run " +
                                     "'ShadowVale ▸ Build Player Animator' for animation.");
                }
            }
            else
            {
                // No character imported: fall back to a capsule so the map is still testable.
                Debug.LogWarning($"[Greybox] {CharacterModelPath} not found — using a capsule.");
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(capsule.GetComponent<CapsuleCollider>());
                capsule.name = "Model";
                capsule.transform.SetParent(rootGo.transform, false);
                capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                capsule.GetComponent<MeshRenderer>().sharedMaterial = fallbackMat;
                SetLayerRecursively(capsule, layer);
            }

            var controller = rootGo.AddComponent<PlayerController>();
            if (animator != null)
            {
                var so = new SerializedObject(controller);
                so.FindProperty("animator").objectReferenceValue = animator;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Health only revives on a non-zero delay and nothing else here brings the player
            // back, so leaving this at the default meant dying once and lying face-down forever.
            var playerHealth = rootGo.AddComponent<Health>();
            var healthSo = new SerializedObject(playerHealth);
            healthSo.FindProperty("respawnDelay").floatValue = DeathClipLength() + RespawnBuffer;
            healthSo.ApplyModifiedPropertiesWithoutUndo();
            AddCombat(rootGo, animator, layer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PlayerPrefabPath);
            Object.DestroyImmediate(rootGo);
            Debug.Log($"[Greybox] Rebuilt {PlayerPrefabPath}");
            return prefab;
        }

        /// <summary>
        /// Adds PlayerCombat and the hand anchor the weapons hang from. The anchor is a child of
        /// the right hand bone, so it follows the animation; its offset is the one thing that
        /// normally needs eyeballing after an art change.
        /// </summary>
        private static void AddCombat(GameObject rootGo, Animator animator, int layer)
        {
            var combat = rootGo.AddComponent<PlayerCombat>();
            WeaponGripConfig grip = WeaponGripTool.EnsureConfig();
            Transform anchor = null;

            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null)
                {
                    var anchorGo = new GameObject("WeaponAnchor") { layer = layer };
                    anchorGo.transform.SetParent(hand, false);
                    // World-space align: +Z (the barrel/blade) points where the character faces,
                    // whatever roll the bind-pose hand happens to have.
                    anchorGo.transform.rotation =
                        rootGo.transform.rotation * Quaternion.Euler(grip.AnchorEuler);
                    anchorGo.transform.localPosition = grip.AnchorPosition;
                    // Cancel the character rescale, so a weapon keeps the real-world size it was
                    // authored at instead of shrinking with the model.
                    Vector3 lossy = hand.lossyScale;
                    anchorGo.transform.localScale = new Vector3(
                        lossy.x != 0f ? 1f / lossy.x : 1f,
                        lossy.y != 0f ? 1f / lossy.y : 1f,
                        lossy.z != 0f ? 1f / lossy.z : 1f);
                    anchor = anchorGo.transform;
                }
                else
                {
                    Debug.LogWarning("[Greybox] No right hand bone — weapons will parent to the root.");
                }
            }

            var weapons = new List<Object>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { WeaponFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponent<Weapon>() != null)
                {
                    weapons.Add(go.GetComponent<Weapon>());
                }
            }

            if (weapons.Count == 0)
            {
                Debug.LogWarning("[Greybox] No weapon prefabs found — run " +
                                 "'ShadowVale ▸ Build Weapon Prefabs'. The player starts unarmed.");
            }

            var so = new SerializedObject(combat);
            SerializedProperty array = so.FindProperty("weaponPrefabs");
            array.arraySize = weapons.Count;
            for (int i = 0; i < weapons.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
            }
            so.FindProperty("handAnchor").objectReferenceValue = anchor;
            so.FindProperty("tracerPrefab").objectReferenceValue = EnsureTracerPrefab();
            so.FindProperty("gripConfig").objectReferenceValue = grip;
            so.FindProperty("animator").objectReferenceValue = animator;
            // Never let a shot or a swing hit the player who fired it.
            so.FindProperty("hitMask").intValue = ~(1 << layer);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Builds the bullet-streak prefab. Unlit and shadowless: it is a flash, not geometry.
        /// </summary>
        private static ShotTracer EnsureTracerPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TracerPrefabPath)!);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(TracerMatPath);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TracerMatPath)!);
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.35f));
                AssetDatabase.CreateAsset(mat, TracerMatPath);
                Debug.Log($"[Greybox] Created {TracerMatPath}");
            }

            var go = new GameObject("FX_ShotTracer");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = mat;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 0;
            line.widthMultiplier = 0.035f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            go.AddComponent<ShotTracer>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, TracerPrefabPath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ShotTracer>();
        }

        /// <summary>
        /// Matches a character's scale to the rig the movement is tuned for and puts its soles on
        /// the root's origin. Measured from humanoid bones, never from renderer bounds: a
        /// SkinnedMeshRenderer's bounds cover the whole animation range, so this character reports
        /// 2.27 m when it is really about 1.75 m — measuring that way shrank it and left it
        /// hovering. A model that is already the right size is left untouched.
        /// </summary>
        private static float NormalizeCharacterModel(GameObject instance)
        {
            var animator = instance.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogWarning($"[Greybox] {instance.name} has no humanoid avatar — left as imported.");
                return 1f;
            }

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips == null || leftFoot == null || rightFoot == null)
            {
                return 1f;
            }

            float rootY = instance.transform.position.y;
            float hipHeight = hips.position.y - rootY;
            if (hipHeight <= 0.01f)
            {
                return 1f;
            }

            float scale = TargetHipHeight / hipHeight;
            if (Mathf.Abs(scale - 1f) < ScaleDeadzone)
            {
                scale = 1f;
            }
            instance.transform.localScale = Vector3.one * scale;

            // Re-read after scaling: the ankle has moved. The sole sits an ankle's height below it.
            rootY = instance.transform.position.y;
            float ankle = Mathf.Min(leftFoot.position.y, rightFoot.position.y) - rootY;
            float sole = ankle - AnkleHeight * scale;
            if (Mathf.Abs(sole) > 0.005f)
            {
                instance.transform.localPosition -= new Vector3(0f, sole, 0f);
            }

            Debug.Log($"[Greybox] {instance.name}: hip {hipHeight:F3} m -> scale {scale:F3}, " +
                      $"sole correction {-sole:F3} m");
            return scale;
        }

        private static Avatar FindAvatar(string modelPath)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (asset is Avatar avatar)
                {
                    return avatar;
                }
            }
            Debug.LogWarning($"[Greybox] No avatar on {modelPath} — run " +
                             "'ShadowVale ▸ Setup Character Import'.");
            return null;
        }

        /// <summary>
        /// Length of the death clip as the animator will play it, so the respawn wait is derived
        /// from the art rather than guessed. Falls back to a sane default when the clip is absent.
        /// </summary>
        private static float DeathClipLength()
        {
            AnimationClip die = CharacterClipLibrary.LoadClip(
                CharacterClipLibrary.ResolveRoleToPath(), "Die");
            // The animator plays it faster than authored, so the wait has to shrink with it.
            float length = die != null ? die.length : 3f;
            return length / PlayerAnimatorBuilder.DiePlaybackSpeed;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static GameObject InstantiatePlayer(GameObject prefab, Transform parent, Vector3 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Player";
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            return instance;
        }

        /// <summary>
        /// Finds or creates the "Player" layer. The camera arm masks it out, otherwise the
        /// spring arm would collide with the character it is framing.
        /// </summary>
        private static int EnsurePlayerLayer()
        {
            int existing = LayerMask.NameToLayer(PlayerLayerName);
            if (existing >= 0)
            {
                return existing;
            }

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            // 0-7 are Unity's own; user layers start at 8.
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = PlayerLayerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"[Greybox] Created layer '{PlayerLayerName}' at index {i}");
                    return i;
                }
            }

            Debug.LogWarning("[Greybox] No free user layer — player stays on Default, so the " +
                             "camera arm may clip on the character.");
            return 0;
        }

        // ---- Camera & light -------------------------------------------------

        private static void ConfigureCamera(Transform target, int playerLayer)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            // Third person, not the project's isometric rig — this scene is a movement testbed.
            cam.orthographic = false;
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;

            // Only one follow script should drive the transform.
            var iso = cam.GetComponent<IsoFollowCamera>();
            if (iso != null)
            {
                Object.DestroyImmediate(iso);
            }

            var follow = cam.GetComponent<ThirdPersonCamera>() ?? cam.gameObject.AddComponent<ThirdPersonCamera>();
            follow.SetTarget(target);

            var so = new SerializedObject(follow);
            so.FindProperty("obstructionMask").intValue = ~(1 << playerLayer);
            so.FindProperty("distance").floatValue = CameraDistance;
            so.FindProperty("minDistance").floatValue = CameraMinDistance;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Frame it in edit mode too (the script drives it at runtime).
            cam.transform.SetPositionAndRotation(
                target.position + new Vector3(0f, 1.6f, 0f)
                    - Quaternion.Euler(15f, 0f, 0f) * Vector3.forward * CameraDistance,
                Quaternion.Euler(15f, 0f, 0f));
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
            const int border = 10; // outer frame thickness → the grid line.
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
