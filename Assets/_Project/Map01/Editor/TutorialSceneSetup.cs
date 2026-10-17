using System;
using System.IO;
using System.Linq;
using ShadowVale.Editor;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static class TutorialSceneSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/Maps/Map00_Tutorial.unity";
        public const string RootPath = "Assets/_Project/Prefabs/Tutorial/Tutorial Root.prefab";
        private const string DataPath = "Assets/_Project/Scenes/Maps/Map00_Tutorial";
        private const string LayoutPath = "Assets/_Project/Map01/tutorial_layout.json";
        [Serializable] public sealed class Layout
        {
            public Vector2 playMin = new(-69, -55), playMax = new(14, 103);
            public Vector2 sceneryMin = new(-110, -114), sceneryMax = new(50, 125);
            public Vector3 routeStart = new(-60, 4.2f, -47), routeEnd = new(-3, 1.5f, 87);
            public float routeHalfWidth = 8;
            public Vector3[] starts, squad, patrol;
            public Vector3 knifeBush, knifeGuard, shootingGuard, supply, trap, captureEnd;
            public float knifeGuardYaw, shootingGuardYaw, trapYaw = 48.36646f, trapWidth = 17;
            public Layout()
            {
                // Follow the existing land straight to the original jetty ramp.
                // Only the final approach follows the wooden ramp onto the deck.
                var forward = Vector3.ProjectOnPlane(new Vector3(-12, 1.47f, 79) - routeStart, Vector3.up).normalized;
                var right = Vector3.Cross(Vector3.up, forward);
                Vector3 At(float distance, float side = 0) => routeStart + forward * distance + right * side;
                knifeGuardYaw = Quaternion.LookRotation(forward).eulerAngles.y;
                shootingGuardYaw = knifeGuardYaw + 180;
                starts = new[] { 0f, 8, 26, 38, 46, 73, 83, 112 }.Select(d => At(d)).Concat(new[] {
                    new Vector3(-4, 1.54f, 85.6f), new Vector3(-7.484455f, 1.54f, 83.01382f), new Vector3(-11.2215f, 1.54f, 79.692f)
                }).ToArray();
                knifeBush = At(52); knifeGuard = At(55.3f); shootingGuard = At(80, 2);
                squad = new[] { At(121, -2), At(119, 1.5f), At(123, -.5f) };
                patrol = new[] { new Vector3(-11.96891f, 1.54f, 79.02763f), new Vector3(-4.49482f, 1.54f, 85.67127f) };
                supply = new Vector3(-1.3474f, 1.54f, 87.39862f); trap = starts[9]; captureEnd = starts[10];
            }
        }
        [MenuItem("ShadowVale/Tutorial/Tạo (hoặc làm lại) từ Map 1", priority = 20)]
        public static void FromMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Tutorial", "Dựng lại scene từ Map 1? Chỉnh sửa đã apply vào Tutorial Root được giữ; chỉnh tay trong scene sẽ mất.", "Dựng lại", "Huỷ")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Create();
        }
        public static void CreateHeadless() => Create();
        public static void Create()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode before rebuilding tutorial.");
            TutorialCaptureSetup.BuildAssets();
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            if (layout == null || layout.starts.Length != 11) throw new InvalidOperationException("Tutorial needs 11 lesson anchors.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Map01SceneCopy.Create(TutorialDirector.SceneName, true);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var env = scene.GetRootGameObjects().First(g => g.name.StartsWith("Detailed layout"));
            var template = Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None).First(g => !g.IsBoss);
            var templateCopy = Object.Instantiate(template.gameObject); templateCopy.SetActive(false); templateCopy.name = "Tutorial template";
            foreach (var enemy in Object.FindObjectsByType<Map01EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (enemy.gameObject != templateCopy) Object.DestroyImmediate(enemy.gameObject);
            if (mission.hung != null) Object.DestroyImmediate(mission.hung.gameObject);
            mission.hung = null;
            foreach (var point in Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None)) {
                bool useful = (point.kind == ForestPointKind.Hide || point.kind == ForestPointKind.Cover) && Inside(point.transform.position, layout.playMin, layout.playMax);
                if (!useful) Object.DestroyImmediate(point.gameObject);
            }
            Object.DestroyImmediate(mission.GetComponent<Map01Quest>()); Object.DestroyImmediate(mission.GetComponent<Map01SaveSystem>());
            // Map 1's operations story (commander orders, point props, hints) is not the tutorial's: its scenery stays, its logic goes.
            foreach (var story in mission.GetComponents<MonoBehaviour>().Where(c => c is Map01Story || c is Map01OperationHints
                         || c is Map01StoryDialogue || c is Map01OperationPuzzleUI || c is Map01C3Mission).ToArray())
                Object.DestroyImmediate(story);
            foreach (var operations in Object.FindObjectsByType<Map01OperationsLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(operations);
            var oldPlayer = mission.player != null ? mission.player.gameObject : Object.FindFirstObjectByType<PlayerController>().gameObject;
            if (!File.Exists(HungPlayerSetup.PrefabPath)) HungPlayerSetup.Run();
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HungPlayerSetup.PrefabPath), scene);
            player.transform.SetParent(mission.transform, false);
            var spawn = Ground(layout.starts[0]); player.transform.SetPositionAndRotation(spawn, Quaternion.LookRotation(TutorialLinearLayout.Forward(layout)));
            Object.DestroyImmediate(oldPlayer); mission.player = player.transform;
            mission.gameCamera.GetComponent<ThirdPersonCamera>().SetTarget(player.transform);
            var pc = new SerializedObject(player.GetComponent<PlayerController>()); pc.FindProperty("cameraTransform").objectReferenceValue = mission.gameCamera.transform; pc.ApplyModifiedPropertiesWithoutUndo();
            var combat = new SerializedObject(player.GetComponent<PlayerCombat>());
            var cameraProperty = combat.FindProperty("cameraRig"); if (cameraProperty != null) { cameraProperty.objectReferenceValue = mission.gameCamera.GetComponent<ThirdPersonCamera>(); combat.ApplyModifiedPropertiesWithoutUndo(); }
            mission.balanceJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Map01/tutorial_balance.json");
            mission.mapMin = layout.playMin; mission.mapMax = layout.playMax;
            var director = mission.gameObject.AddComponent<TutorialDirector>();
            mission.gameObject.AddComponent<TutorialTimeControl>();
            var scouting = mission.gameObject.AddComponent<TutorialScouting>();
            var capture = mission.gameObject.AddComponent<TutorialCaptureCutscene>();
            GameObject tutorial;
            if (File.Exists(RootPath)) tutorial = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RootPath), scene);
            else { tutorial = BuildRoot(layout, templateCopy); Directory.CreateDirectory(Path.GetDirectoryName(RootPath)); PrefabUtility.SaveAsPrefabAssetAndConnect(tutorial, RootPath, InteractionMode.AutomatedAction); }
            TutorialLinearLayout.ConformRoot(tutorial, layout);
            Object.DestroyImmediate(templateCopy);
            director.lessonStarts = Enumerable.Range(0, 11).Select(i => tutorial.transform.Find("Lesson " + i)).ToArray();
            director.knifeGuard = tutorial.transform.Find("Knife guard").GetComponent<Map01EnemyController>();
            director.knifeBush = tutorial.transform.Find("Knife bush");
            director.shootingGuard = tutorial.transform.Find("Shooting guard").GetComponent<Map01EnemyController>();
            director.squad = tutorial.transform.Find("Squad").GetComponentsInChildren<Map01EnemyController>(true);
            director.patrol = tutorial.transform.Find("Patrol").GetComponentsInChildren<Map01EnemyController>(true);
            director.noiseTrap = tutorial.GetComponentInChildren<TutorialNoiseTrap>(true); director.noiseTrap.director = director;
            PrefabUtility.RecordPrefabInstancePropertyModifications(director.noiseTrap);
            EditorUtility.SetDirty(director.noiseTrap);
            director.noiseTrap.gameObject.SetActive(false); director.promptUI = tutorial.GetComponentInChildren<TutorialPromptUI>(true);
            scouting.supplyCache = tutorial.transform.Find("Supply cache"); capture.endPoint = tutorial.transform.Find("Capture end");
            var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/_Project" }).Select(AssetDatabase.GUIDToAssetPath).SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>().ToArray();
            capture.runClip = Clip("Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_Run.fbx") ?? clips.FirstOrDefault(c => c.name == "Nam_Rifle_Run");
            capture.hitClip = Clip("Assets/_Project/Art/Characters/NPCs/Animations/Hung_TrungDan.fbx") ?? Clip("Assets/_Project/Art/Characters/Animations/Nam/Nam_HitReaction.fbx");
            capture.fallClip = Clip("Assets/_Project/Art/Characters/Animations/Clips/Dying.fbx") ?? capture.hitClip;
            capture.enemyRunClip = Clip("Assets/_Project/Art/Characters/Animations/Enemy/Enemy_Rifle_Run.fbx") ?? capture.runClip;
            // Keep all original scenery. Restrict gameplay with tutorial bounds, never by deleting the map.
            Physics.SyncTransforms();
            var surface = env.GetComponent<NavMeshSurface>(); surface.RemoveData(); surface.navMeshData = null;
            surface.collectObjects = CollectObjects.Volume; surface.center = BoundsFor(layout.playMin, layout.playMax, 20).center;
            surface.size = BoundsFor(layout.playMin, layout.playMax, 20).size; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Tutorial NavMesh bake failed.");
            AssetDatabase.CreateAsset(surface.navMeshData, DataPath + "/TutorialNavMesh.asset");
            foreach (var point in director.lessonStarts.Concat(new[] { capture.endPoint })) point.position = Ground(point.position);
            player.transform.position = director.lessonStarts[0].position;
            EditorSceneManager.SaveScene(scene);
            var graph = TacticalGraphBaker.Bake(TutorialDirector.SceneName, new TacticalGraphBaker.Settings(), out string report);
            if (graph == null) throw new InvalidOperationException(report);
            TacticalGraphBaker.Save(graph);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            int map1 = scenes.FindIndex(s => s.path.EndsWith("/Map 1.unity")); scenes.Insert(map1 < 0 ? scenes.Count : map1 + 1, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray(); AssetDatabase.SaveAssets();
            // Only this generated copy's unreferenced data may be removed.
            var used = AssetDatabase.GetDependencies(ScenePath, true).ToHashSet();
            foreach (string path in Directory.GetFiles(DataPath, "*", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).Where(p => !p.EndsWith(".meta")).ToArray())
                if (!used.Contains(path)) AssetDatabase.DeleteAsset(path);
            Debug.Log($"[Tutorial] Ready: original shelter → wooden jetty, cargo on deck, escape towards shelter; scenery retained. {report}");
        }

        [MenuItem("ShadowVale/Tutorial/Áp dụng toạ độ JSON vào Tutorial Root", priority = 21)]
        public static void ApplyLayoutToPrefab()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode before applying a layout.");
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            var root = PrefabUtility.LoadPrefabContents(RootPath);
            try {
                PlaceLessonObjects(root, layout);
                foreach (var child in root.transform.Cast<Transform>().Where(t => t.name == "Tutorial boundary" || t.name.StartsWith("Boundary fence ") || t.name == "Tutorial nav exclusion").ToArray())
                    Object.DestroyImmediate(child.gameObject);
                TutorialLinearLayout.EnsureKnifeBush(root);
                Boundary(root.transform, layout);
                PrefabUtility.SaveAsPrefabAsset(root, RootPath);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void PlaceLessonObjects(GameObject root, Layout layout)
        {
            for (int i = 0; i < layout.starts.Length; i++) Place(root, "Lesson " + i, layout.starts[i]);
            Place(root, "Knife guard", layout.knifeGuard, layout.knifeGuardYaw);
            Place(root, "Knife bush", layout.knifeBush);
            Place(root, "Shooting guard", layout.shootingGuard, layout.shootingGuardYaw);
            var squad = root.transform.Find("Squad");
            for (int i = 0; i < squad.childCount; i++) {
                squad.GetChild(i).SetPositionAndRotation(Ground(layout.squad[i]), Quaternion.Euler(0, layout.shootingGuardYaw, 0));
                if (PrefabUtility.IsPartOfPrefabInstance(squad.GetChild(i))) PrefabUtility.RecordPrefabInstancePropertyModifications(squad.GetChild(i));
            }
            var patrol = root.transform.Find("Patrol");
            for (int i = 0; i < patrol.childCount; i++) {
                patrol.GetChild(i).position = Ground(layout.patrol[i]);
                if (PrefabUtility.IsPartOfPrefabInstance(patrol.GetChild(i))) PrefabUtility.RecordPrefabInstancePropertyModifications(patrol.GetChild(i));
            }
            Place(root, "Supply cache", layout.supply);
            var supply = root.transform.Find("Supply cache"); supply.position += Vector3.up * .5f;
            if (PrefabUtility.IsPartOfPrefabInstance(supply)) PrefabUtility.RecordPrefabInstancePropertyModifications(supply);
            Place(root, "Capture end", layout.captureEnd);
            Place(root, "Dry branch trap", layout.trap, layout.trapYaw);
            root.GetComponentInChildren<TutorialNoiseTrap>(true).GetComponent<BoxCollider>().size = new Vector3(layout.trapWidth, 20, 1.3f);
        }

        // Repairs only this generated tutorial's scenery, then changes lesson objects in place.
        // It deliberately keeps the mission, player, UI, checkpoints and combat setup.
        public static void RestoreSceneryAndApplyRoute()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved tutorial outside Play Mode before restoring its scenery.");
            if (UnityEngine.SceneManagement.SceneManager.sceneCount != 1)
                throw new InvalidOperationException("Close additional scenes before restoring tutorial scenery.");
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            var source = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity", OpenSceneMode.Additive);
            try {
                foreach (string prefix in new[] { "Detailed layout", "03 Surroundings" }) {
                    var original = source.GetRootGameObjects().Single(g => g.name.StartsWith(prefix));
                    var previous = scene.GetRootGameObjects().Single(g => g.name.StartsWith(prefix));
                    // Move the loaded scene objects to preserve their nested prefab connections.
                    // The source scene is closed without saving and its file stays unchanged.
                    var replacement = original;
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(replacement, scene);
                    var copiedSurface = replacement.GetComponent<NavMeshSurface>();
                    if (copiedSurface != null) { copiedSurface.RemoveData(); copiedSurface.navMeshData = null; }
                    Object.DestroyImmediate(previous);
                }
            } finally { EditorSceneManager.CloseScene(source, true); }
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            Physics.SyncTransforms();
            ApplyLayoutToPrefab();
            var tutorial = scene.GetRootGameObjects().Single(g => g.name == "Tutorial Root");
            PlaceLessonObjects(tutorial, layout);
            var director = Object.FindFirstObjectByType<TutorialDirector>();
            var mission = director.GetComponent<Map01Mission>();
            mission.mapMin = layout.playMin; mission.mapMax = layout.playMax;
            mission.player.SetPositionAndRotation(Ground(layout.starts[0]), Quaternion.LookRotation(TutorialLinearLayout.Forward(layout)));
            director.noiseTrap.director = director;
            PrefabUtility.RecordPrefabInstancePropertyModifications(director.noiseTrap);
            var surface = scene.GetRootGameObjects().Single(g => g.name.StartsWith("Detailed layout")).GetComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = BoundsFor(layout.playMin, layout.playMax, 20).center;
            surface.size = BoundsFor(layout.playMin, layout.playMax, 20).size;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms(); surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Tutorial NavMesh bake failed.");
            string navPath = DataPath + "/TutorialNavMesh.asset";
            var savedNav = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if (savedNav == null) AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            else {
                surface.RemoveData(); var generated = surface.navMeshData;
                EditorUtility.CopySerialized(generated, savedNav); surface.navMeshData = savedNav;
                Object.DestroyImmediate(generated); EditorUtility.SetDirty(savedNav); surface.AddData();
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var graph = TacticalGraphBaker.Bake(TutorialDirector.SceneName, new TacticalGraphBaker.Settings(), out string report);
            if (graph == null) throw new InvalidOperationException(report);
            TacticalGraphBaker.Save(graph); AssetDatabase.SaveAssets();
            Debug.Log("[Tutorial route repair] Original scenery restored; only tutorial lesson objects relocated. " + report);
        }
        private static void Place(GameObject root, string name, Vector3 position, float yaw = 0)
        {
            var target = root.transform.Find(name);
            target.SetPositionAndRotation(Ground(position), Quaternion.Euler(0, yaw, 0));
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
        private static GameObject BuildRoot(Layout layout, GameObject template)
        {
            var root = new GameObject("Tutorial Root");
            for (int i = 0; i < layout.starts.Length; i++) Anchor("Lesson " + i, root.transform, Ground(layout.starts[i]));
            CloneGuard("Knife guard", root.transform, template, layout.knifeGuard, Quaternion.Euler(0, layout.knifeGuardYaw, 0));
            Anchor("Knife bush", root.transform, Ground(layout.knifeBush));
            CloneGuard("Shooting guard", root.transform, template, layout.shootingGuard, Quaternion.Euler(0, layout.shootingGuardYaw, 0));
            var squad = new GameObject("Squad"); squad.transform.SetParent(root.transform);
            for (int i = 0; i < layout.squad.Length; i++) CloneGuard("Tutorial squad " + i, squad.transform, template, layout.squad[i], Quaternion.Euler(0, layout.shootingGuardYaw, 0));
            var patrol = new GameObject("Patrol"); patrol.transform.SetParent(root.transform);
            for (int i = 0; i < layout.patrol.Length; i++) CloneGuard("Capture patrol " + i, patrol.transform, template, layout.patrol[i], Quaternion.Euler(0, 90, 0));
            var supply = GameObject.CreatePrimitive(PrimitiveType.Cube); supply.name = "Supply cache"; supply.transform.SetParent(root.transform); supply.transform.position = Ground(layout.supply) + Vector3.up * .5f; supply.transform.localScale = new Vector3(2, 1, 1.4f);
            supply.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01Detailed/Weathered timber.mat");
            Anchor("Capture end", root.transform, Ground(layout.captureEnd));
            var trap = new GameObject("Dry branch trap", typeof(BoxCollider), typeof(TutorialNoiseTrap)); trap.transform.SetParent(root.transform); trap.transform.position = Ground(layout.trap);
            trap.transform.rotation = Quaternion.Euler(0, layout.trapYaw, 0);
            var box = trap.GetComponent<BoxCollider>(); box.isTrigger = true; box.center = Vector3.up * 2; box.size = new Vector3(layout.trapWidth, 20, 1.3f);
            var branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder); branch.name = "Dry branch"; branch.transform.SetParent(trap.transform, false); branch.transform.SetLocalPositionAndRotation(new Vector3(0, .08f, 0), Quaternion.Euler(90, 0, 25)); branch.transform.localScale = new Vector3(.1f, .6f, .1f); Object.DestroyImmediate(branch.GetComponent<Collider>()); branch.GetComponent<Renderer>().sharedMaterial = supply.GetComponent<Renderer>().sharedMaterial;
            Boundary(root.transform, layout);
            var ui = new GameObject("Tutorial prompts", typeof(RectTransform), typeof(TutorialPromptUI)); ui.transform.SetParent(root.transform, false); ui.GetComponent<TutorialPromptUI>().Build();
            return root;
        }
        private static void CloneGuard(string name, Transform parent, GameObject template, Vector3 at, Quaternion rotation)
        {
            var copy = Object.Instantiate(template, parent); copy.name = name; copy.transform.SetPositionAndRotation(Ground(at), rotation);
            copy.GetComponent<Map01EnemyController>().Configure(Array.Empty<Vector3>()); copy.SetActive(false);
        }
        private static void Anchor(string name, Transform parent, Vector3 position) { var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = position; }
        private static void Boundary(Transform parent, Layout layout)
        {
            var forward = TutorialLinearLayout.Forward(layout); var right = Vector3.Cross(Vector3.up, forward) * layout.routeHalfWidth;
            var start = layout.routeStart - forward * 2; var end = layout.routeEnd + forward * 6;
            var corners = new[] { start - right, start + right, end + right, end - right };
            for (int i = 0; i < 4; i++) {
                Vector3 a = corners[i], b = corners[(i + 1) % 4];
                var go = new GameObject("Tutorial boundary", typeof(BoxCollider)); go.transform.SetParent(parent);
                go.transform.SetPositionAndRotation(new Vector3((a.x+b.x)/2, 8, (a.z+b.z)/2), Quaternion.LookRotation(b-a));
                go.GetComponent<BoxCollider>().size = new Vector3(.5f, 30, Vector3.Distance(a,b));
                int pieces = Mathf.CeilToInt(Vector3.Distance(a,b) / 3);
                for (int j = 0; j < pieces; j++) {
                    Vector3 p = Vector3.Lerp(a, b, j / (float)pieces), q = Vector3.Lerp(a, b, (j+1) / (float)pieces);
                    FencePost(parent, p); FenceRail(parent, p, q);
                }
            }
            TutorialLinearLayout.ExcludeOutsideCorridor(parent, layout);
        }
        private static void FencePost(Transform parent, Vector3 position)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube); post.name = "Boundary fence post"; post.transform.SetParent(parent); post.transform.position = Ground(position) + Vector3.up * .9f; post.transform.localScale = new Vector3(.14f, 1.8f, .14f);
            post.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01Detailed/Weathered timber.mat");
        }
        private static void FenceRail(Transform parent, Vector3 a, Vector3 b)
        {
            a = Ground(a); b = Ground(b);
            foreach (float height in new[] { .65f, 1.35f }) {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube); rail.name = "Boundary fence rail";
                rail.transform.SetParent(parent); rail.transform.SetPositionAndRotation((a+b)/2 + Vector3.up*height, Quaternion.LookRotation(b-a));
                rail.transform.localScale = new Vector3(.09f, .09f, Vector3.Distance(a,b));
                rail.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01Detailed/Weathered timber.mat");
            }
        }
        private static Bounds BoundsFor(Vector2 min, Vector2 max, float height) => new(new Vector3((min.x+max.x)/2, height/2-2, (min.y+max.y)/2), new Vector3(max.x-min.x, height, max.y-min.y));
        private static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        private static bool Inside(Vector3 p, Vector2 min, Vector2 max) => p.x >= min.x && p.x <= max.x && p.z >= min.y && p.z <= max.y;
        internal static Vector3 Ground(Vector3 p)
        {
            // Ignore tutorial actors, cargo and fences so applying the layout is repeatable.
            var hits = Physics.RaycastAll(new Vector3(p.x, 150, p.z), Vector3.down, 300, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits.OrderBy(h => h.distance))
                if (hit.normal.y > .55f && (hit.transform.root.name.StartsWith("Detailed layout") || hit.transform.root.name.StartsWith("03 Surroundings")))
                    return new Vector3(p.x, hit.point.y + .03f, p.z);
            return p;
        }
    }
}
