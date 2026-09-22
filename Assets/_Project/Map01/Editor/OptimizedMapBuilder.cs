using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    // One-shot requests let the already-open editor perform asset import/baking.
    // No periodic rebuild: a request is consumed once and reports success/failure.
    [InitializeOnLoad]
    public static class OptimizedMapBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Maps/Map 1.unity";
        const string Root = "Assets/_Project/Art/Environment/Map01_Optimized";
        const string Source = "SourceArt/Map01_Optimized/Map01.meshdata.json.gz";
        const string Reports = "Tools/Map01OptimizedReports";
        const string Request = "Tools/Map01Optimization.request";
        static double nextCheck;
        static bool busy;
        static int playPhase, nextFrame;
        static OptimizedMapBuilder() { Directory.CreateDirectory(Reports);File.WriteAllText(Reports+"/builder.version","3");EditorApplication.update += Poll; }
        static void Poll()
        {
            if (busy || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck=EditorApplication.timeSinceStartup+1;
            if (!File.Exists(Request)) return;
            string action=File.ReadAllText(Request).Trim();
            File.Delete(Request); busy=true; Directory.CreateDirectory(Reports);
            try
            {
                if (action=="build") Build();
                else if (action=="validate") Validate();
                else if (action=="capture") Capture();
                else if (action=="playcheck") BeginPlayCheck();
                else if (action=="audit") Audit();
                else throw new InvalidOperationException("Unknown map request: "+action);
                File.WriteAllText(Reports+"/"+action+".status","PASS "+DateTime.UtcNow.ToString("O"));
            }
            catch(Exception e) { File.WriteAllText(Reports+"/"+action+".status","FAIL\n"+e); Debug.LogException(e); }
            finally { busy=false; }
        }
        [Serializable] public class SourceData { public MeshData[] meshes; public TreeData[] trees; public PropData[] props; public BoxData[] boxes; public RouteData[] routes,patrols; public PointData[] points; public float[] spawn,hung,encounterExit; }
        [Serializable] public class PropData { public string id; public float[] position,scale;public float yaw; }
        [Serializable] public class MeshData { public string id,kind; public float[] vertices,normals,colors; public int[] triangles; }
        [Serializable] public class TreeData { public int type; public float[] position,scale; public float yaw; }
        [Serializable] public class BoxData { public string name; public float[] position,size; }
        [Serializable] public class RouteData { public string id; public float[] positions; }
        [Serializable] public class PointData { public string id; public int kind; public float[] position; }
        static Vector3 V(float[] a,int i=0) => new Vector3(a[i],a[i+1],a[i+2]);
        static SourceData Read()
        {
            using(var stream=File.OpenRead(Source)) using(var gz=new GZipStream(stream,CompressionMode.Decompress)) using(var reader=new StreamReader(gz)) return JsonUtility.FromJson<SourceData>(reader.ReadToEnd());
        }
        static GameObject Child(string name,Transform parent) { var go=new GameObject(name);go.transform.SetParent(parent,false);return go; }
        static Material MaterialAsset(string name,Shader shader,Color color)
        {
            string path=Root+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);} else mat.shader=shader;
            mat.SetColor("_BaseColor",color);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
        }
        [MenuItem("ShadowVale/Map 1/Build Optimized Blender Map")]
        public static void Build()
        {
            if(EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before building Map 1; unsaved work has been preserved.");
            var data=Read();
            Directory.CreateDirectory(Root+"/Meshes");Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(Reports);AssetDatabase.Refresh();
            var shader=Shader.Find("ShadowVale/Map Vertex Color");if(shader==null)throw new InvalidOperationException("Map Vertex Color shader was not imported.");
            var opaque=MaterialAsset("MapPalette",shader,new Color(.75f,.82f,.74f));
            var water=MaterialAsset("StreamPalette",shader,new Color(.85f,1.08f,1.08f));
            var meshAssets=new Dictionary<string,Mesh>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach(var src in data.meshes)
                {
                    string path=Root+"/Meshes/"+src.id+".asset";
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=mesh==null;
                    if(create)mesh=new Mesh{name=src.id};else mesh.Clear();
                    mesh.indexFormat=IndexFormat.UInt32;int n=src.vertices.Length/3;
                    var vertices=new Vector3[n];var normals=new Vector3[n];var colors=new Color32[n];
                    for(int i=0;i<n;i++){vertices[i]=V(src.vertices,i*3);normals[i]=V(src.normals,i*3);colors[i]=(Color32)new Color(src.colors[i*4],src.colors[i*4+1],src.colors[i*4+2],1);}
                    mesh.vertices=vertices;mesh.normals=normals;mesh.colors32=colors;mesh.triangles=src.triangles;mesh.RecalculateBounds();
                    if(create) AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
                    meshAssets[src.id]=mesh;
                }
            }
            finally {AssetDatabase.StopAssetEditing();}
            AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var env=new GameObject("01 Environment • Blender optimized");
            var ground=Child("Ground & structures • 24m chunks",env.transform);
            var trees=Child("Forest • shared meshes + 3 LODs",env.transform);
            var collision=Child("Collision • simplified",env.transform);
            int blockLayer=LayerMask.NameToLayer("Obstacle");
            foreach(var src in data.meshes)
            {
                if(src.kind=="tree"||src.kind=="prop")continue;
                bool col=src.kind=="walk"||src.kind=="block";
                var go=Child(src.id,col?collision.transform:ground.transform);
                if(col){go.layer=blockLayer;go.AddComponent<MeshCollider>().sharedMesh=meshAssets[src.id];}
                else{go.AddComponent<MeshFilter>().sharedMesh=meshAssets[src.id];var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=src.kind=="water"?water:opaque;r.shadowCastingMode=src.kind=="water"?ShadowCastingMode.Off:ShadowCastingMode.On;r.receiveShadows=true;}
            }
            var treePrefabs=new GameObject[5];
            for(int variant=0;variant<5;variant++)
            {
                var go=new GameObject("Tree_"+variant);go.layer=blockLayer;
                var collider=go.AddComponent<CapsuleCollider>();collider.radius=.29f;collider.height=7.5f;collider.center=new Vector3(0,3.75f,0);
                var lods=new LOD[3];float[] heights={.13f,.035f,.006f};
                for(int lod=0;lod<3;lod++)
                {
                    var visual=Child("LOD"+lod,go.transform);visual.AddComponent<MeshFilter>().sharedMesh=meshAssets["Tree_"+variant+"_LOD"+lod];
                    var renderer=visual.AddComponent<MeshRenderer>();renderer.sharedMaterial=opaque;renderer.shadowCastingMode=lod<2?ShadowCastingMode.On:ShadowCastingMode.Off;
                    lods[lod]=new LOD(heights[lod],new Renderer[]{renderer});
                }
                var group=go.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
                go.AddComponent<ForestInstanceTint>();
                treePrefabs[variant]=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/Tree_"+variant+".prefab");Object.DestroyImmediate(go);
            }
            int index=0;
            foreach(var src in data.trees)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(treePrefabs[src.type],trees.transform);go.name="Tree "+index++;go.transform.position=V(src.position);go.transform.localScale=V(src.scale);go.transform.rotation=Quaternion.Euler(0,src.yaw,0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            }
            var propPrefabs=new Dictionary<string,GameObject>();
            foreach(var src in data.meshes.Where(m=>m.kind=="prop"))
            {
                var go=new GameObject(src.id);go.layer=blockLayer;var mesh=meshAssets[src.id];go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=opaque;
                var c=go.AddComponent<BoxCollider>();c.center=mesh.bounds.center;c.size=mesh.bounds.size;
                go.AddComponent<ForestInstanceTint>();
                propPrefabs[src.id]=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+src.id+".prefab");Object.DestroyImmediate(go);
            }
            var props=Child("Props • shared prefab instances",env.transform);
            foreach(var src in data.props)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(propPrefabs[src.id],props.transform);go.transform.position=V(src.position);go.transform.localScale=V(src.scale);go.transform.rotation=Quaternion.Euler(0,src.yaw,0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            }
            foreach(var src in data.boxes){var go=Child(src.name,collision.transform);go.layer=blockLayer;go.transform.position=V(src.position);go.AddComponent<BoxCollider>().size=V(src.size);}
            var surface=env.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.2f;
            surface.BuildNavMesh();if(surface.navMeshData==null)throw new InvalidOperationException("Navigation bake returned no data.");
            string navPath=Root+"/Map01_NavMesh.asset";var previous=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if(previous==null)AssetDatabase.CreateAsset(surface.navMeshData,navPath);else {EditorUtility.CopySerialized(surface.navMeshData,previous);surface.RemoveData();surface.navMeshData=previous;surface.AddData();EditorUtility.SetDirty(previous);}
            var gameplay=new GameObject("02 Mission • Nam & Hung");var mission=gameplay.AddComponent<ForestMission>();
            mission.balanceJson=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Map01/Map01Balance.json");
            mission.contentBundle=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Map01/Generated/Map01Content.json");
            mission.mapMin=new Vector2(-110,-100);mission.mapMax=new Vector2(110,100);
            mission.encounterExit=Child("Encounter exit • east bridge",gameplay.transform).transform;mission.encounterExit.position=Snap(V(data.encounterExit));
            var player=Actor("Nam",Snap(V(data.spawn)),new Color(.13f,.45f,.44f),gameplay.transform);
            player.layer=LayerMask.NameToLayer("Player");var controller=player.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.35f;controller.center=Vector3.up*.9f;controller.stepOffset=.35f;mission.player=player.transform;
            var hung=Actor("Hung",Snap(V(data.hung)),new Color(.66f,.52f,.25f),gameplay.transform);AddAgent(hung);mission.hung=hung.transform;
            foreach(var patrol in data.patrols)
            {
                var positions=Enumerable.Range(0,patrol.positions.Length/3).Select(i=>Snap(V(patrol.positions,i*3))).ToArray();
                var go=Actor(patrol.id,positions[0],new Color(.5f,.25f,.16f),gameplay.transform);go.layer=LayerMask.NameToLayer("Enemy");
                var c=go.AddComponent<CapsuleCollider>();c.height=1.8f;c.radius=.4f;c.center=Vector3.up*.9f;AddAgent(go);
                var guard=go.AddComponent<ForestGuard>();guard.id=patrol.id;guard.patrol=positions;
            }
            foreach(var point in data.points)
            {
                var go=Child(point.id,gameplay.transform);go.transform.position=Snap(V(point.position));var p=go.AddComponent<ForestPoint>();p.id=point.id;p.kind=(ForestPointKind)point.kind;p.radius=4;
                p.label=point.kind==0?"Nhận hàng tiếp tế":point.kind==1?"Nhặt vật tư":point.kind==2?"Bàn chế tạo":point.kind==3?"Bản đồ và ghi chép tuyến bí mật":point.kind==4?"Giao hàng và rời rừng":"Vật che chắn";
                if(p.kind==ForestPointKind.Loot)p.items=new[]{new ForestIngredient{item_id="cloth",count=4},new ForestIngredient{item_id="herb",count=2},new ForestIngredient{item_id="ammo_rifle",count=24}};
            }
            var cam=new GameObject("Isometric camera").AddComponent<Camera>();cam.tag="MainCamera";cam.orthographic=true;cam.orthographicSize=15;cam.nearClipPlane=.1f;cam.farClipPlane=500;
            cam.transform.rotation=Quaternion.Euler(45,-38,0);cam.transform.position=mission.player.position-cam.transform.forward*60;cam.backgroundColor=new Color(.2f,.28f,.26f);cam.clearFlags=CameraClearFlags.SolidColor;cam.gameObject.AddComponent<AudioListener>();mission.gameCamera=cam;
            mission.trailMaterial=MaterialAsset("Tracer",Shader.Find("Universal Render Pipeline/Unlit"),new Color(1,.78f,.25f));
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;sun.color=new Color(1,.94f,.8f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-35,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.45f,.52f,.56f);RenderSettings.ambientEquatorColor=new Color(.25f,.31f,.22f);RenderSettings.ambientGroundColor=new Color(.17f,.19f,.12f);RenderSettings.fog=false;
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.22f,.27f,.2f));RenderSettings.ambientProbe=ambient;
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
            var build=EditorBuildSettings.scenes.Where(s=>!s.path.EndsWith("Map01_ForestFootprints.unity")&&!s.path.EndsWith("Map01_ReferenceBlockout.unity")&&s.path!=ScenePath).ToList();build.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=build.ToArray();
            Selection.activeGameObject=env;
            if(SceneView.lastActiveSceneView!=null){SceneView.lastActiveSceneView.LookAt(new Vector3(0,3,0),Quaternion.Euler(55,-38,0),145,true,true);}
            Map01Expansion.Apply();
            File.WriteAllText(Reports+"/summary.json",JsonUtility.ToJson(new Summary{treeInstances=data.trees.Length,propInstances=data.props.Length,prefabAssets=treePrefabs.Length+propPrefabs.Count,sharedTreeMeshes=15,staticChunks=data.meshes.Count(m=>m.kind=="opaque"||m.kind=="water"),trianglesInUniqueMeshes=data.meshes.Sum(m=>m.triangles.Length/3),scene=ScenePath},true));
        }
        [Serializable] class Summary {public int treeInstances,propInstances,prefabAssets,sharedTreeMeshes,staticChunks,trianglesInUniqueMeshes;public string scene;}
        static Vector3 Snap(Vector3 p){if(!NavMesh.SamplePosition(p,out var hit,4,NavMesh.AllAreas))throw new InvalidOperationException("No walkable surface near "+p);return hit.position+Vector3.up*.03f;}
        static void Audit()
        {
            var data=Read();var lines=new List<string>();
            foreach(var src in data.meshes.Where(m=>m.kind=="opaque"||m.kind=="water"))
            {
                var go=GameObject.Find(src.id);var mesh=go==null?null:go.GetComponent<MeshFilter>().sharedMesh;
                float minX=float.MaxValue,maxX=float.MinValue;for(int i=0;i<src.vertices.Length;i+=3){minX=Mathf.Min(minX,src.vertices[i]);maxX=Mathf.Max(maxX,src.vertices[i]);}
                lines.Add(src.id+" source="+(src.vertices.Length/3)+"/"+(src.triangles.Length/3)+" X="+minX+".."+maxX+" mesh="+(mesh==null?"MISSING":mesh.vertexCount+"/"+mesh.triangles.Length/3+" X="+mesh.bounds.min.x+".."+mesh.bounds.max.x));
            }
            var renderers=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            lines.Add("Missing materials: "+renderers.Count(r=>r.sharedMaterials.Any(m=>m==null)));
            lines.Add("Connected prefab roots: "+Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)));
            File.WriteAllLines(Reports+"/mesh-audit.txt",lines);
        }
        static void AddAgent(GameObject go){var a=go.AddComponent<NavMeshAgent>();a.radius=.35f;a.height=1.8f;a.angularSpeed=240;a.acceleration=14;a.stoppingDistance=.5f;}
        static GameObject Actor(string name,Vector3 position,Color color,Transform parent)
        {
            var root=Child(name,parent);root.transform.position=position;var visual=Child("VisualRoot",root.transform);
            var material=MaterialAsset("Actor_"+name,Shader.Find("Universal Render Pipeline/Lit"),color);
            var body=GameObject.CreatePrimitive(PrimitiveType.Capsule);body.name="Body";body.transform.SetParent(visual.transform,false);body.transform.localPosition=Vector3.up*.95f;body.transform.localScale=new Vector3(.65f,.8f,.65f);body.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(body.GetComponent<Collider>());
            var helmet=GameObject.CreatePrimitive(PrimitiveType.Sphere);helmet.name="Helmet";helmet.transform.SetParent(visual.transform,false);helmet.transform.localPosition=Vector3.up*1.65f;helmet.transform.localScale=new Vector3(.62f,.38f,.62f);helmet.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(helmet.GetComponent<Collider>());
            return root;
        }
        [MenuItem("ShadowVale/Map 1/Validate Optimized Routes")]
        public static void Validate()
        {
            var data=Read();var lines=new List<string>();int failures=0;
            foreach(var route in data.routes.Concat(data.patrols))
            {
                var positions=Enumerable.Range(0,route.positions.Length/3).Select(i=>Snap(V(route.positions,i*3))).ToArray();
                for(int i=1;i<positions.Length;i++){var path=new NavMeshPath();bool valid=NavMesh.CalculatePath(positions[i-1],positions[i],NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;lines.Add((valid?"PASS":"FAIL")+" "+route.id+" "+(i-1)+" -> "+i);if(!valid)failures++;}
            }
            var m=Object.FindFirstObjectByType<ForestMission>();
            if(m==null||m.contentBundle==null||m.balanceJson==null||m.player==null||m.hung==null||m.encounterExit==null)throw new InvalidOperationException("Missing mission references");
            File.WriteAllLines(Reports+"/routes.txt",lines);if(failures>0)throw new InvalidOperationException(failures+" disconnected navigation segments. See routes.txt");
        }
        [MenuItem("ShadowVale/Map 1/Capture Optimized Map")]
        public static void Capture()
        {
            var cam=Camera.main;var pos=cam.transform.position;var rot=cam.transform.rotation;float size=cam.orthographicSize;
            try{Shot(cam,new Vector3(-63,3,-55),new Vector3(45,-38,0),15,"foliage-start");Shot(cam,new Vector3(0,3,0),new Vector3(52,-38,0),136,"overview");Shot(cam,new Vector3(-27,3,-8),new Vector3(55,-38,0),23,"patrol");Shot(cam,new Vector3(46,4,32),new Vector3(65,-20,0),26,"base");Shot(cam,new Vector3(20,3,85),new Vector3(58,-25,0),24,"landing");}
            finally{cam.transform.SetPositionAndRotation(pos,rot);cam.orthographicSize=size;}
        }
        static void Shot(Camera cam,Vector3 target,Vector3 angles,float size,string name)
        {
            cam.transform.rotation=Quaternion.Euler(angles);cam.transform.position=target-cam.transform.forward*250;cam.orthographicSize=size;
            var rt=new RenderTexture(1600,1100,24);var previous=RenderTexture.active;var image=new Texture2D(1600,1100,TextureFormat.RGB24,false);
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(Reports+"/"+name+".png",image.EncodeToPNG());}
            finally{cam.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);}
        }
        static void BeginPlayCheck()
        {
            SessionState.SetBool("SV.Map01.PlayCheck",true);EditorApplication.playModeStateChanged-=PlayState;EditorApplication.playModeStateChanged+=PlayState;EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void RestorePlayCheck(){EditorApplication.playModeStateChanged-=PlayState;EditorApplication.playModeStateChanged+=PlayState;}
        static void PlayState(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("SV.Map01.PlayCheck",false))return;
            Application.runInBackground=true;playPhase=0;nextFrame=Time.frameCount+5;EditorApplication.update-=AdvancePlayCheck;EditorApplication.update+=AdvancePlayCheck;
        }
        static void AdvancePlayCheck()
        {
            if(!EditorApplication.isPlaying||Time.frameCount<nextFrame)return;
            try
            {
                var m=Object.FindFirstObjectByType<ForestMission>();if(m==null||!m.IsInitialized)throw new InvalidOperationException("Mission failed initialization");
                var points=Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None);
                if(playPhase==0)
                {
                    var guards=Object.FindObjectsByType<ForestGuard>(FindObjectsSortMode.None);if(guards.Length<3||guards.Any(g=>!g.enabled||g.hp<=0))throw new InvalidOperationException("Guard startup failed");
                    m.Interact(points.Single(p=>p.id=="river_exit"));if(m.Stage!=0)throw new InvalidOperationException("Exit skipped mission");
                    m.Interact(points.Single(p=>p.id=="supplies"));if(m.Stage!=1)throw new InvalidOperationException("Supply interaction failed");
                    var controller=m.player.GetComponent<CharacterController>();controller.enabled=false;m.player.position=m.encounterExit.position;controller.enabled=true;
                    playPhase=1;nextFrame=Time.frameCount+5;return;
                }
                if(m.Stage!=2)throw new InvalidOperationException("Crossing the bridge did not unlock evidence");
                m.Interact(points.Single(p=>p.id=="documents"));if(m.Stage!=3||m.Count("river_documents")!=1)throw new InvalidOperationException("Evidence interaction failed");
                var exit=points.Single(p=>p.id=="river_exit").transform.position;var cc=m.player.GetComponent<CharacterController>();cc.enabled=false;m.player.position=exit;cc.enabled=true;m.hung.GetComponent<NavMeshAgent>().Warp(exit);
                m.Interact(points.Single(p=>p.id=="river_exit"));if(m.Stage!=4)throw new InvalidOperationException("Delivery at the expanded landing did not complete mission");
                File.WriteAllText(Reports+"/playcheck-result.txt","PASS: mission/all guards initialized; exit gate; supplies; bridge progression; evidence; delivery with Hung; complete stage 4. Navigation tested separately; this flow check teleports between objectives.");
            }
            catch(Exception e){File.WriteAllText(Reports+"/playcheck-result.txt","FAIL: "+e);}
            EditorApplication.update-=AdvancePlayCheck;SessionState.SetBool("SV.Map01.PlayCheck",false);EditorApplication.isPlaying=false;
        }
    }
}

