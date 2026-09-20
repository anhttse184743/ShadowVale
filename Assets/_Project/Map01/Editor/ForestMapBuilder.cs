using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace ShadowVale.Map01.Editor
{
    public static class ForestMapBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Maps/Map01_ForestFootprints.unity";
        private static string Root = "Assets/_Project/Map01";
        private static string Reports = "Tools/Map01Reports";
        private static Transform environment, detail;
        private static Material soil, trail, grass, bark, leaf, stone, wood, canvas, water, metal, paper, gold;
        private static readonly List<Vector3[]> routes = new List<Vector3[]>();

        [MenuItem("ShadowVale/Map 1/Build Forest Scene")]
        public static void Build() => BuildScene(ScenePath, "Assets/_Project/Map01", false);

        [MenuItem("ShadowVale/Map 1/Build Reference Wetland Blockout")]
        public static void BuildReference() => BuildScene("Assets/_Project/Scenes/Maps/Map01_ReferenceBlockout.unity", "Assets/_Project/Map01/ReferenceBlockout", true);

        private static void BuildScene(string destination, string assetRoot, bool reference)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Root = assetRoot;
            Reports = reference ? "Tools/Map01ReferenceReports" : "Tools/Map01Reports";
            if (File.Exists(destination))
            {
                // Keep the previous authored scene as a timestamped backup before explicit rebuilds.
                Directory.CreateDirectory("Tools/Map01Backups");
                File.Copy(destination, "Tools/Map01Backups/Map01_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
            }
            Directory.CreateDirectory(Root + "/Generated/Materials");
            Directory.CreateDirectory(Root + "/Generated/Prefabs");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UnityEngine.Random.InitState(1707);
            environment = new GameObject("01 • Terrain & collision").transform;
            detail = new GameObject("02 • Foliage & set dressing").transform;
            Materials();
            Ground();
            Paths();
            Woodland();
            var mission = new GameObject("00 • Map 1 mission").AddComponent<ForestMission>();
            mission.balanceJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Map01/Map01Balance.json");
            // StreamingAssets are imported as raw files; copy the JSON to a normal TextAsset.
            string bundlePath = Root + "/Generated/Map01Content.json";
            File.Copy("Assets/StreamingAssets/Content/fallback_bundle.json", bundlePath, true);
            AssetDatabase.ImportAsset(bundlePath, ImportAssetOptions.ForceSynchronousImport);
            mission.contentBundle = AssetDatabase.LoadAssetAtPath<TextAsset>(bundlePath);
            mission.trailMaterial = Material("Tracer", new Color(1, .8f, .3f), true);
            StartCamp();
            PatrolCrossing();
            RestCamp();
            if (reference) ReferenceWetlandDressing.Build(environment, detail);
            else AbandonedBase();
            RiverExit();
            Lighting();
            var surface = environment.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true; surface.voxelSize = .18f;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Map 1 NavMesh bake failed");
            string navPath = Root + "/Generated/Map01_NavMesh.asset";
            var oldNav = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if (oldNav == null) AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            else
            {
                EditorUtility.CopySerialized(surface.navMeshData, oldNav);
                surface.RemoveData(); surface.navMeshData = oldNav; surface.AddData(); EditorUtility.SetDirty(oldNav);
            }
            Actors(mission);
            var cam = new GameObject("Isometric camera").AddComponent<Camera>();
            cam.tag = "MainCamera"; cam.orthographic = true; cam.orthographicSize = 12;
            cam.transform.rotation = Quaternion.Euler(30, 45, 0);
            cam.transform.position = mission.player.position - cam.transform.forward * 30;
            cam.nearClipPlane = .1f; cam.farClipPlane = 250;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.12f, .19f, .18f);
            cam.gameObject.AddComponent<AudioListener>(); mission.gameCamera = cam;
            EditorSceneManager.SaveScene(scene, destination);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == destination)) scenes.Add(new EditorBuildSettingsScene(destination, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Validate();
            Capture();
            if (reference && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,14), Quaternion.Euler(58,0,0), 100);
            Debug.Log("[Map01] Built forest scene, baked navigation, validated routes and captured previews: " + destination);
        }

        private static Material Material(string name, Color color, bool unlit = false)
        {
            string path = Root + "/Generated/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", .08f);
            EditorUtility.SetDirty(mat); return mat;
        }
        private static void Materials()
        {
            soil = Material("Earth", new Color(.24f, .29f, .17f));
            trail = Material("Ochre path", new Color(.49f, .39f, .24f));
            grass = Material("Low foliage", new Color(.25f, .40f, .22f));
            bark = Material("Bark", new Color(.25f, .20f, .12f));
            leaf = Material("Canopy", new Color(.15f, .31f, .23f));
            stone = Material("Moss stone", new Color(.35f, .40f, .33f));
            wood = Material("Weathered timber", new Color(.38f, .29f, .18f));
            canvas = Material("Khaki canvas", new Color(.46f, .49f, .30f));
            water = Material("River jade", new Color(.12f, .39f, .40f));
            metal = Material("Oxidized metal", new Color(.25f, .31f, .29f));
            paper = Material("Old charts", new Color(.82f, .77f, .56f));
            gold = Material("Objective amber", new Color(1, .69f, .20f), true);
        }
        private static GameObject Shape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Transform parent, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent);
            go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            else go.layer = LayerMask.NameToLayer("Obstacle");
            return go;
        }
        private static GameObject Box(string name, Vector3 position, Vector3 scale, Material material, bool solid = true)
            => Shape(name, PrimitiveType.Cube, position, scale, material, solid ? environment : detail, solid);

        private static void Ground()
        {
            var ground = Box("Forest floor • 90 × 148 m", new Vector3(0, -.6f, 14), new Vector3(90, 1.2f, 148), soil);
            ground.layer = 0;
            // Impassable edge colliders sit behind the rock and tree perimeter.
            Box("West boundary", new Vector3(-45, 2, 14), new Vector3(2, 6, 148), stone);
            Box("East boundary", new Vector3(45, 2, 14), new Vector3(2, 6, 148), stone);
            Box("South boundary", new Vector3(0, 2, -60), new Vector3(90, 6, 2), stone);
            Box("North river bank", new Vector3(0, 2, 88), new Vector3(90, 6, 2), stone);
        }
        private static void InitializeRoutes()
        {
            routes.Clear();
            routes.Add(new[] { V(0,-52), V(-3,-39), V(0,-25), V(1,-13), V(0,2), V(3,17), V(0,31), V(-3,40), V(3,49), V(4,62), V(17,72), V(27,79) });
            routes.Add(new[] { V(0,-25), V(-13,-19), V(-23,-7), V(-24,8), V(-18,21), V(0,31) });
            routes.Add(new[] { V(0,-25), V(16,-20), V(24,-8), V(23,9), V(17,23), V(0,31) });
        }
        private static void Paths()
        {
            InitializeRoutes();
            foreach (var route in routes)
                for (int i = 0; i < route.Length - 1; i++)
                {
                    Vector3 a = route[i], b = route[i+1]; int steps = Mathf.CeilToInt(Vector3.Distance(a,b));
                    for (int j = 0; j <= steps; j++)
                    {
                        var p = Vector3.Lerp(a,b,(float)j/steps); p.y = .012f;
                        Shape("Worn trail", PrimitiveType.Cylinder, p, new Vector3(route == routes[0] ? 6 : 4, .012f, 5), trail, detail, false);
                    }
                }
        }
        private static Vector3 V(float x, float z) => new Vector3(x, 0, z);
        private static float DistanceToRoutes(Vector3 p)
        {
            float distance = float.MaxValue;
            foreach (var route in routes)
                for (int i = 0; i < route.Length - 1; i++)
                {
                    var a = route[i]; var d = route[i+1] - a;
                    distance = Mathf.Min(distance, Vector3.Distance(p, a + d * Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude)));
                }
            return distance;
        }
        private static bool Clearing(Vector3 p) => Vector3.Distance(p,V(0,-49)) < 11 || Vector3.Distance(p,V(-3,36)) < 11 || (Mathf.Abs(p.x) < 19 && p.z > 47 && p.z < 70) || Vector3.Distance(p,V(26,78)) < 11;
        private static void Woodland()
        {
            for (int i = 0; i < 570; i++)
            {
                var p = V(UnityEngine.Random.Range(-42f,42f), UnityEngine.Random.Range(-57f,85f));
                if (DistanceToRoutes(p) < 5 || Clearing(p)) continue;
                float h = UnityEngine.Random.Range(4.3f,8f);
                Shape("Forest trunk", PrimitiveType.Cylinder, p + Vector3.up * h/2, new Vector3(.65f,h/2,.65f), bark, environment);
                // Small, lifted canopies preserve the isometric view of the navigable paths.
                Shape("Faceted canopy", PrimitiveType.Sphere, p + Vector3.up * h, new Vector3(3.8f,2.4f,3.8f), leaf, detail, false);
                if (i % 3 == 0) Shape("Moss rock", PrimitiveType.Sphere, p + new Vector3(1,.45f,1), new Vector3(2.4f,1.2f,1.6f), stone, environment);
            }
            for (int i = 0; i < 230; i++)
            {
                var p = V(UnityEngine.Random.Range(-40f,40f), UnityEngine.Random.Range(-55f,83f));
                if (DistanceToRoutes(p) < 3 || Clearing(p)) continue;
                Shape("Ground fern", PrimitiveType.Sphere, p + Vector3.up * .25f, new Vector3(1.5f,.5f,1.2f), grass, detail, false);
            }
            for (int i = 0; i < 9; i++) Hide(V(-19 - Mathf.Sin(i)*4, -15+i*5));
            for (int i = 0; i < 5; i++) Hide(V(23,-14+i*7));
        }
        private static ForestPoint Point(string id, string label, ForestPointKind kind, Vector3 p)
        {
            var go = new GameObject(id + " • " + label); go.transform.position = p; go.transform.SetParent(detail);
            var point = go.AddComponent<ForestPoint>(); point.id = id; point.label = label; point.kind = kind;
            return point;
        }
        private static void Hide(Vector3 p)
        {
            var hide = Point("hide_"+p.x+"_"+p.z, "Bụi rậm • C đi khom", ForestPointKind.Hide,p); hide.radius = 3.8f;
            for (int i = 0; i < 7; i++)
            {
                var offset = UnityEngine.Random.insideUnitCircle * 2.4f;
                Shape("Tall fern", PrimitiveType.Sphere, p + new Vector3(offset.x,.45f,offset.y), new Vector3(2,.9f,1.4f), grass, detail, false);
            }
        }
        private static void Cover(string id, Vector3 p, Vector3 scale)
        {
            var go = Box(id, p + Vector3.up * scale.y/2, scale, stone); go.layer = LayerMask.NameToLayer("Cover");
            Point(id+"_cover", "Vật chắn", ForestPointKind.Cover,p);
            for (int i = 0; i < 3; i++) Box("Moss on cover",p + new Vector3((i-1)*scale.x*.28f,scale.y+.05f,0),new Vector3(scale.x*.28f,.13f,scale.z*.85f),grass,false);
        }
        private static ForestPoint Crate(string id, string label, ForestPointKind kind, Vector3 p)
        {
            Box(label, p + Vector3.up*.6f,new Vector3(1.6f,1.2f,1.1f),wood);
            Box("Crate strap",p+Vector3.up*1.22f,new Vector3(.16f,.08f,1.15f),metal,false);
            Shape("Interaction marker",PrimitiveType.Sphere,p+Vector3.up*2,new Vector3(.22f,.22f,.22f),gold,detail,false);
            return Point(id,label,kind,p);
        }
        private static void Sign(string text, Vector3 p)
        {
            Box("Signpost",p+Vector3.up*.9f,new Vector3(.15f,1.8f,.15f),wood,false);
            Box("Trail sign • "+text,p+Vector3.up*1.7f,new Vector3(2.4f,.55f,.12f),canvas,false);
        }
        private static void StartCamp()
        {
            Crate("supplies", "Nhận hàng tiếp tế", ForestPointKind.Supplies,V(-2,-49));
            var loot = Crate("tutorial_loot","Túi vật tư bên đường",ForestPointKind.Loot,V(3,-37));
            loot.items = new[] { Item("cloth",4),Item("herb",2),Item("ammo_rifle",18) };
            Box("Supply shelter",new Vector3(-7,2.5f,-51),new Vector3(5,.2f,5),canvas,false);
            foreach (float x in new[] {-9f,-5f}) foreach (float z in new[] {-53f,-49f}) Box("Shelter pole",new Vector3(x,1.25f,z),new Vector3(.15f,2.5f,.15f),wood);
            Cover("Tutorial fallen log",V(-5,-31),new Vector3(5,1.4f,1.3f));
            Sign("BẾN SÔNG ↑", V(5,-25));
        }
        private static ForestIngredient Item(string id,int count) => new ForestIngredient {item_id=id,count=count};
        private static void PatrolCrossing()
        {
            Cover("Crossing west rock",V(-5,-8),new Vector3(4,1.6f,2.1f));
            Cover("Crossing east rock",V(6,3),new Vector3(4,1.7f,2));
            Cover("Northern sandbags",V(-4,17),new Vector3(5,1.3f,1.4f));
            Cover("Eastern fallen tree",V(17,5),new Vector3(1.6f,1.4f,6));
            Box("Abandoned supply cart",new Vector3(9,.7f,-5),new Vector3(2.5f,1.4f,3.5f),wood);
            for(int i=0;i<3;i++) Shape("Hollow metal distraction cans",PrimitiveType.Cylinder,new Vector3(15+i,.5f,-11),new Vector3(.6f,.5f,.6f),metal,detail,false);
            var loot=Crate("east_cache","Vật tư lối vòng",ForestPointKind.Loot,V(28,13));loot.items=new[]{Item("cloth",2),Item("herb",1),Item("ammo_rifle",12)};
            Sign("ĐƯỜNG CŨ ←",V(-11,-20));
        }
        private static void RestCamp()
        {
            Box("Field workbench",new Vector3(-6,.6f,36),new Vector3(3,1.2f,1.4f),wood);
            Point("workbench","Bàn chế tạo • B làm băng cứu thương",ForestPointKind.Workbench,V(-6,36));
            for(int i=0;i<9;i++)
            {
                float a=i*Mathf.PI*2/9;
                Shape("Cold fire ring",PrimitiveType.Sphere,new Vector3(2+Mathf.Cos(a),.2f,35+Mathf.Sin(a)),new Vector3(.6f,.4f,.6f),stone,detail,false);
            }
            Box("Bedroll",new Vector3(-1,.15f,39),new Vector3(1.2f,.3f,2.5f),canvas,false);
            var loot=Crate("rest_cache","Hộp y tế bỏ lại",ForestPointKind.Loot,V(-8,40));loot.items=new[]{Item("herb",2),Item("cloth",2)};
        }
        private static void AbandonedBase()
        {
            Box("Base courtyard",new Vector3(0,.03f,58),new Vector3(29,.06f,22),trail,false);
            // Broken perimeter, wide south entry and northeast exit, open roof for isometric readability.
            Box("West ruined wall",new Vector3(-14,1.2f,58),new Vector3(1,2.4f,21),stone);
            Box("East ruined wall",new Vector3(14,1.2f,55),new Vector3(1,2.4f,15),stone);
            Box("South wall left",new Vector3(-9,1,47),new Vector3(10,2,1),stone);
            Box("South wall right",new Vector3(10,1,47),new Vector3(8,2,1),stone);
            Box("North broken wall",new Vector3(-4,1.2f,69),new Vector3(21,2.4f,1),stone);
            Box("Command room north",new Vector3(-5,1.5f,65),new Vector3(12,3,.5f),wood);
            Box("Command room west",new Vector3(-11,1.5f,61),new Vector3(.5f,3,8),wood);
            Box("Roofless command floor",new Vector3(-5,.04f,61),new Vector3(12,.08f,8),wood,false);
            Box("Map table",new Vector3(-5,.75f,61),new Vector3(3,1.5f,1.8f),wood);
            Box("River topographic chart",new Vector3(-5,1.51f,61),new Vector3(2.6f,.02f,1.45f),paper,false);
            for(int i=0;i<5;i++)
            {
                var line=Box("Marked secret river route",new Vector3(-5.8f+i*.4f,1.53f,61),new Vector3(.04f,.02f,1.1f),metal,false);
                line.transform.rotation=Quaternion.Euler(0,i*13,0);
            }
            Point("documents","Thu thập bản đồ và ghi chép bến sông",ForestPointKind.Documents,V(-5,61));
            Shape("Evidence marker",PrimitiveType.Sphere,new Vector3(-5,2.7f,61),new Vector3(.28f,.28f,.28f),gold,detail,false);
            Box("Empty weapon rack",new Vector3(-10,1,63),new Vector3(.4f,2,3),metal);
            for(int i=0;i<5;i++) Box("Empty ammunition crate",new Vector3(6+i%2*2,.35f,57+i*1.3f),new Vector3(1.4f,.7f,1),wood);
            Cover("Collapsed masonry",V(8,65),new Vector3(3,1.1f,2));
            Box("Watch platform",new Vector3(-17,3,65),new Vector3(4,.3f,4),wood,false);
            for(int i=0;i<4;i++) Box("Watchtower support",new Vector3(-18+(i%2)*2,1.5f,64+(i/2)*2),new Vector3(.25f,3,.25f),wood);
            Sign("TRẠM CŨ",V(7,44));
        }
        private static void RiverExit()
        {
            Box("River beyond playable bank",new Vector3(0,-.1f,98),new Vector3(140,.1f,25),water,false);
            for(int i=0;i<10;i++) Box("River glint",new Vector3(UnityEngine.Random.Range(-45,45),0,92+i*1.6f),new Vector3(UnityEngine.Random.Range(5,20),.02f,.08f),paper,false);
            for(int i=0;i<12;i++) Box("Landing jetty plank",new Vector3(27,.15f,74+i*.7f),new Vector3(4,.3f,.6f),wood,false);
            Box("Moored boat",new Vector3(32,.3f,84),new Vector3(2.5f,.7f,6),wood,false);
            Point("river_exit","Giao hàng và rời rừng cùng Hùng",ForestPointKind.Exit,V(27,80));
            Shape("Exit marker",PrimitiveType.Cylinder,new Vector3(27,.08f,80),new Vector3(3,.035f,3),gold,detail,false);
        }
        private static GameObject Actor(string name, Vector3 position, Material uniform)
        {
            var root = new GameObject(name); root.transform.position=position;
            var visual = new GameObject("VisualRoot");visual.transform.SetParent(root.transform,false);
            Shape("Body placeholder",PrimitiveType.Capsule,position+Vector3.up*.95f,new Vector3(.65f,.8f,.65f),uniform,visual.transform,false);
            Shape("Helmet placeholder",PrimitiveType.Sphere,position+Vector3.up*1.65f,new Vector3(.62f,.4f,.62f),uniform,visual.transform,false);
            Shape("Backpack placeholder",PrimitiveType.Cube,position+new Vector3(0,1,-.34f),new Vector3(.5f,.65f,.24f),canvas,visual.transform,false);
            Shape("Rifle placeholder",PrimitiveType.Cube,position+new Vector3(.3f,1,.45f),new Vector3(.12f,.15f,.9f),metal,visual.transform,false);
            return root;
        }
        private static NavMeshAgent Agent(GameObject go)
        {
            var agent=go.AddComponent<NavMeshAgent>();agent.radius=.4f;agent.height=1.8f;agent.angularSpeed=240;agent.acceleration=14;agent.stoppingDistance=.4f;return agent;
        }
        private static void Actors(ForestMission mission)
        {
            var nam=Actor("Nam • replace VisualRoot with character asset",V(0,-52),Material("Nam teal",new Color(.15f,.48f,.48f)));
            nam.layer=LayerMask.NameToLayer("Player");
            var controller=nam.AddComponent<CharacterController>();controller.center=Vector3.up*.9f;controller.height=1.8f;controller.radius=.35f;controller.stepOffset=.3f;
            mission.player=nam.transform;
            var hung=Actor("Hùng • narrative companion",V(2,-51),Material("Hung ochre",new Color(.68f,.57f,.25f)));Agent(hung);mission.hung=hung.transform;
            var red=Material("Patrol muted rust",new Color(.57f,.29f,.23f));
            var paths=new[]{new[]{V(-2,-8),V(4,-2),V(1,7),V(-6,0)},new[]{V(6,11),V(-1,16),V(-8,10),V(0,5)},new[]{V(13,-9),V(20,-2),V(14,6),V(9,-1)}};
            for(int i=0;i<paths.Length;i++)
            {
                var go=Actor("Patrol "+(i+1),paths[i][0],red);go.layer=LayerMask.NameToLayer("Enemy");
                var collider=go.AddComponent<CapsuleCollider>();collider.center=Vector3.up*.9f;collider.height=1.8f;collider.radius=.4f;
                Agent(go);var guard=go.AddComponent<ForestGuard>();guard.id="patrol_"+i;guard.patrol=paths[i];
            }
            PrefabUtility.SaveAsPrefabAsset(nam,Root+"/Generated/Prefabs/Nam_Placeholder.prefab");
            PrefabUtility.SaveAsPrefabAsset(hung,Root+"/Generated/Prefabs/Hung_Placeholder.prefab");
        }
        private static void Lighting()
        {
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.51f,.60f,.54f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogDensity=.003f;RenderSettings.fogColor=new Color(.43f,.55f,.48f);
            var sun=new GameObject("Late afternoon sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.color=new Color(1,.88f,.66f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-35,0);
        }

        [MenuItem("ShadowVale/Map 1/Validate Forest Routes")]
        public static void Validate()
        {
            Reports = EditorSceneManager.GetActiveScene().path.Contains("ReferenceBlockout") ? "Tools/Map01ReferenceReports" : "Tools/Map01Reports";
            InitializeRoutes();
            var triangles=NavMesh.CalculateTriangulation();
            if(triangles.vertices.Length==0) throw new InvalidOperationException("No navigation data");
            var checks=new List<string>();
            foreach(var route in routes)
                for(int i=0;i<route.Length-1;i++) AssertPath(route[i],route[i+1],checks);
            AssertPath(V(0,-52),V(-2,-49),checks);
            AssertPath(V(0,31),V(-5,59),checks);
            AssertPath(V(-5,59),V(27,80),checks);
            foreach(var guard in UnityEngine.Object.FindObjectsByType<ForestGuard>(FindObjectsSortMode.None))
                for(int i=0;i<guard.patrol.Length;i++) AssertPath(guard.patrol[i],guard.patrol[(i+1)%guard.patrol.Length],checks);
            var mission=UnityEngine.Object.FindFirstObjectByType<ForestMission>();
            if(mission==null || mission.balanceJson==null || mission.contentBundle==null || mission.player==null || mission.hung==null) throw new InvalidOperationException("Missing mission references");
            Directory.CreateDirectory(Reports);
            File.WriteAllLines(Reports + "/navigation.txt",checks);
            Debug.Log("[Map01] PASS: "+checks.Count+" complete navigation paths; all mission references present.");
        }
        private static void AssertPath(Vector3 a,Vector3 b,List<string> checks)
        {
            var path=new NavMeshPath();
            if(!NavMesh.SamplePosition(a,out var start,3,NavMesh.AllAreas)||!NavMesh.SamplePosition(b,out var end,3,NavMesh.AllAreas)||!NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Unreachable Map 1 route: "+a+" → "+b);
            checks.Add("PASS "+a+" → "+b+" ("+path.corners.Length+" corners)");
        }
        [MenuItem("ShadowVale/Map 1/Capture Forest Previews")]
        public static void Capture()
        {
            Reports = EditorSceneManager.GetActiveScene().path.Contains("ReferenceBlockout") ? "Tools/Map01ReferenceReports" : "Tools/Map01Reports";
            Directory.CreateDirectory(Reports);
            var cam=Camera.main; var position=cam.transform.position;var rotation=cam.transform.rotation;float size=cam.orthographicSize;
            try
            {
                Shot(cam,V(0,12),new Vector3(62,0,0),82,"overview");
                Shot(cam,V(0,-5),new Vector3(38,45,0),23,"patrol");
                Shot(cam,V(-1,57),new Vector3(40,45,0),22,"base");
            }
            finally{cam.transform.SetPositionAndRotation(position,rotation);cam.orthographicSize=size;}
        }
        private static void Shot(Camera cam,Vector3 target,Vector3 angles,float size,string name)
        {
            cam.transform.rotation=Quaternion.Euler(angles);cam.transform.position=target-cam.transform.forward*130;cam.orthographicSize=size;
            var rt=new RenderTexture(1600,1000,24);cam.targetTexture=rt;cam.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            File.WriteAllBytes(Reports + "/"+name+".png",image.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=previous;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
