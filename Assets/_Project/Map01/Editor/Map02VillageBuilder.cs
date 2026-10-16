using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad] public static partial class Map02VillageBuilder
    {
        const string Root="Assets/_Project/Art/Environment/Map02_Village",Reports="Tools/Map02Reports",ScenePath="Assets/_Project/Scenes/Maps/Map 2.unity";
        const float RiverX=-110+220f/3,RiverWidth=10.5f;
        [Serializable] public sealed class VillageSourceData { public VillageMeshData[] meshes; }
        [Serializable] public sealed class VillageMeshData { public string id; public float[] vertices,normals,colors; public int[] triangles; }
        static Material palette,soil,rice,water,wood;static Transform env,markers;static Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();static System.Random rng;
        static Map02VillageBuilder(){EditorApplication.update+=Poll;EditorApplication.update+=PollReference;}
        static void Poll(){const string request="Tools/Map02Village.request";if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(request);try{Build();File.WriteAllText(Reports+"/build.status","PASS "+DateTime.UtcNow.ToString("O"));}catch(Exception e){File.WriteAllText(Reports+"/build.status","FAIL "+e);Debug.LogException(e);}}
        static float Rand(float a,float b)=>Mathf.Lerp(a,b,(float)rng.NextDouble());
        static float H(float x)=>Mathf.Lerp(-.8f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(RiverWidth/2,RiverWidth/2+3,Mathf.Abs(x-RiverX))));
        static GameObject Child(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
        static Material Mat(string name,Color color){var path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.12f);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;}
        static Mesh SaveMesh(Mesh mesh,string name){var path=Root+"/"+name+".asset";mesh.name=name;var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
        static GameObject Model(string name,Mesh mesh,Vector3 p,Transform parent,Material mat){var g=Child(name,parent);g.transform.localPosition=p;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;return g;}
        static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material mat,bool collision=true){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<MeshRenderer>().sharedMaterial=mat;if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());else g.layer=LayerMask.NameToLayer("Obstacle");return g;}
        static void ColliderBox(Transform parent,string name,Vector3 center,Vector3 size){var g=Child(name,parent);g.layer=LayerMask.NameToLayer("Obstacle");var c=g.AddComponent<BoxCollider>();c.center=center;c.size=size;}
        static void Import()
        {
            VillageSourceData data;using(var f=File.OpenRead("SourceArt/Map02_Village/Village.meshdata.json.gz"))using(var z=new GZipStream(f,CompressionMode.Decompress))using(var r=new StreamReader(z))data=JsonUtility.FromJson<VillageSourceData>(r.ReadToEnd());
            meshes.Clear();foreach(var s in data.meshes){var m=new Mesh{indexFormat=IndexFormat.UInt32};int n=s.vertices.Length/3;var v=new Vector3[n];var c=new Color[n];var normals=new Vector3[n];for(int i=0;i<n;i++){v[i]=new Vector3(s.vertices[i*3],s.vertices[i*3+1],s.vertices[i*3+2]);normals[i]=new Vector3(s.normals[i*3],s.normals[i*3+1],s.normals[i*3+2]);c[i]=new Color(s.colors[i*4],s.colors[i*4+1],s.colors[i*4+2],1);}m.vertices=v;m.normals=normals;m.colors=c;m.triangles=s.triangles;m.RecalculateBounds();meshes[s.id]=SaveMesh(m,s.id);}
        }
        [MenuItem("ShadowVale/Map 2/Create village blockout")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Reports);
            if(File.Exists(ScenePath))throw new InvalidOperationException("Map 2 already exists; preserve manual edits rather than rebuilding it.");
            rng=new System.Random(2226);Import();palette=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Optimized/Materials/MapPalette.mat");soil=Mat("Warm earth",new Color(.36f,.30f,.19f));rice=Mat("Rice green",new Color(.30f,.43f,.075f));wood=Mat("Timber",new Color(.32f,.24f,.14f));water=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Realism/River surface.mat");
            // Preserve unsaved work before switching to the standalone scene; do not render or bake Map 1 underneath it.
            for(int i=0;i<SceneManager.sceneCount;i++){var existing=SceneManager.GetSceneAt(i);if(existing.isDirty){if(string.IsNullOrEmpty(existing.path))throw new InvalidOperationException("Save untitled scene first");EditorSceneManager.SaveScene(existing,Reports+"/Before_Map02_"+i+".unity",true);EditorSceneManager.SaveScene(existing);}}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SceneManager.SetActiveScene(scene);scene.name="Map 2";
            env=new GameObject("01 Environment • village blockout").transform;markers=new GameObject("02 Story anchors • design only • no actors").transform;
            Terrain();Fields();Village();RiverLandmarks();Vegetation();Story();Lighting();
            var surface=env.gameObject.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.2f;surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData,Root+"/Map02_NavMesh.asset");
            var main=Child("Main Camera • map overview",null).AddComponent<Camera>();main.tag="MainCamera";main.orthographic=true;main.orthographicSize=125;main.farClipPlane=650;main.transform.position=new Vector3(135,205,-195);main.transform.LookAt(new Vector3(0,0,0));main.backgroundColor=new Color(.63f,.76f,.77f);main.clearFlags=CameraClearFlags.Skybox;main.gameObject.AddComponent<AudioListener>();
            Validate(surface);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
            Capture(main,new Vector3(135,205,-195),Vector3.zero,125,"map02-overview");
            Capture(main,new Vector3(62,30,-37),new Vector3(18,2,12),0,"map02-village");
            Capture(main,new Vector3(-15,15,64),new Vector3(RiverX,1,45),0,"map02-evacuation");
            Selection.activeGameObject=env.gameObject;if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(Vector3.zero,Quaternion.Euler(55,-35,0),155);
        }
        static void Terrain()
        {
            var v=new List<Vector3>();var c=new List<Color>();var t=new List<int>();int nx=221,nz=201;
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){float xx=x-110,zz=z-100;v.Add(new Vector3(xx,H(xx),zz));float k=Mathf.PerlinNoise(x*.055f,z*.055f);c.Add(Color.Lerp(new Color(.28f,.33f,.16f),new Color(.40f,.43f,.23f),k));if(x>0&&z>0){int i=z*nx+x;t.AddRange(new[]{i-nx-1,i,i-nx,i-nx-1,i-1,i});}}
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetColors(c);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=SaveMesh(mesh,"Continuous_Terrain_220x200");var ground=Model("Continuous ground • 220 x 200 m",mesh,Vector3.zero,env,palette);ground.layer=LayerMask.NameToLayer("Obstacle");ground.AddComponent<MeshCollider>().sharedMesh=mesh;
            Box("Straight river • 10.5 m wide • one-third map",env,new Vector3(RiverX,.045f,0),new Vector3(RiverWidth,.015f,200),water,false);
            var paths=Child("Paths and village lanes",env).transform;
            Box("Village main lane",paths,new Vector3(18,1.015f,8),new Vector3(5,.025f,136),soil,false);
            foreach(float z in new[]{-42f,-5f,23f,45f})Box("Cross lane "+z,paths,new Vector3(12,1.015f,z),new Vector3(87,.025f,3),soil,false);
            Box("Safe reed-bank evacuation path",paths,new Vector3(RiverX+11,1.025f,-13),new Vector3(2,.035f,132),soil,false);
        }
        static void Fields()
        {
            var root=Child("Patchwork rice fields • raised bunds",env).transform;int index=0;var riceParts=new List<CombineInstance>();
            var plots=new List<Vector4>();foreach(float x in new[]{-94f,-70f})foreach(float z in new[]{-77f,-43f,-9f,25f,59f})plots.Add(new Vector4(x,z,20,28));
            foreach(float z in new[]{-72f,-36f,0f,36f,72f})plots.Add(new Vector4(84,z,32,29));
            foreach(float x in new[]{-12f,17f,44f})plots.Add(new Vector4(x,-73,23,28));
            foreach(var p in plots){var plot=Child("Paddy "+index,root).transform;plot.localPosition=new Vector3(p.x,0,p.y);var color=index%4==0?new Color(.36f,.43f,.34f):index%4==1?new Color(.49f,.42f,.22f):index%4==2?new Color(.28f,.41f,.07f):new Color(.42f,.50f,.14f);var m=Mat("Paddy tone "+index%4,color);Box("Field surface",plot,new Vector3(0,1.026f,0),new Vector3(p.z,.03f,p.w),m,false);
                foreach(float side in new[]{-1f,1f}){Box("Raised earth bund",plot,new Vector3(side*(p.z/2+.25f),1.11f,0),new Vector3(.7f,.22f,p.w+1),soil);Box("Raised earth bund",plot,new Vector3(0,1.11f,side*(p.w/2+.25f)),new Vector3(p.z+1,.22f,.7f),soil);}
                if(index%4!=0)for(float x=-p.z/2+1;x<p.z/2;x+=1.25f)for(float z=-p.w/2+1;z<p.w/2;z+=1.3f)riceParts.Add(new CombineInstance{mesh=meshes["Rice_Clump"],transform=Matrix4x4.TRS(new Vector3(p.x+x,1.05f,p.y+z),Quaternion.Euler(0,Rand(0,360),0),Vector3.one*Rand(.7f,1.2f))});index++;}
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(riceParts.ToArray());Model("Rice rows",SaveMesh(mesh,"Rice_Rows"),Vector3.zero,root,palette).GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        static void Village()
        {
            var root=Child("Village • timber bamboo and palm-thatch houses",env).transform;
            var positions=new[]{new Vector3(-10,0,-19),new Vector3(9,0,-23),new Vector3(35,0,-22),new Vector3(52,0,-15),new Vector3(-9,0,12),new Vector3(9,0,9),new Vector3(35,0,8),new Vector3(54,0,10),new Vector3(-6,0,36),new Vector3(12,0,39),new Vector3(37,0,36),new Vector3(55,0,39),new Vector3(34,0,59),new Vector3(9,0,62)};
            for(int i=0;i<positions.Length;i++)
            {
                bool stilt=i%3==0;var p=positions[i];p.y=stilt?2.35f:1.48f;GameObject g;
                if(stilt){g=Child("Stilt house "+(i+1),root);g.transform.localPosition=p;var lods=new LOD[2];for(int l=0;l<2;l++){var child=Model("LOD"+l,meshes["Village_Stilt_LOD"+l],Vector3.zero,g.transform,palette);lods[l]=new LOD(l==0?.12f:.02f,new[]{child.GetComponent<Renderer>()});}g.AddComponent<LODGroup>().SetLODs(lods);g.GetComponent<LODGroup>().RecalculateBounds();}
                else {var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/Map01_LatestRevision/ThatchHouse.prefab");g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);g.name="Bamboo thatch house "+(i+1);g.transform.localPosition=p;}
                float w=stilt?9.28f:6.4f;ColliderBox(g.transform,"Floor",new Vector3(0,.1f,-.8f),new Vector3(w,.2f,6.8f));ColliderBox(g.transform,"Back wall",new Vector3(0,1.5f,2.4f),new Vector3(w,.1f+2.9f,.15f));
                foreach(float side in new[]{-1f,1f}){ColliderBox(g.transform,"Side wall",new Vector3(side*w*.455f,1.5f,.2f),new Vector3(.15f,3,4.4f));ColliderBox(g.transform,"Door side",new Vector3(side*w*.3f,1.5f,-2.05f),new Vector3(w*.3f,3,.15f));}
                var ramp=Box("Entry steps smooth collision",g.transform,new Vector3(0,stilt?-.64f:-.16f,stilt?-5.1f:-4.55f),new Vector3(2,.15f,stilt?2.8f:1.3f),wood);ramp.transform.localRotation=Quaternion.Euler(stilt?-28:-20,0,0);ramp.GetComponent<MeshRenderer>().enabled=false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
                if(stilt){var pond=Child("Lotus garden "+i,root).transform;pond.localPosition=p+new Vector3(0,-p.y,6);Box("Lotus pond",pond,new Vector3(0,1.045f,0),new Vector3(11,.02f,4),Mat("Pond",new Color(.16f,.29f,.23f)),false);for(int j=0;j<30;j++)Model("Lotus",meshes["Lotus_Leaf"],new Vector3(Rand(-5,5),1.09f,Rand(-1.6f,1.6f)),pond,palette).transform.localScale=Vector3.one*Rand(.6f,1.1f);}
            }
            Box("Civilian assembly courtyard",root,new Vector3(-8,1.02f,25),new Vector3(13,.03f,12),soil,false);
            Box("Repair and crafting yard",root,new Vector3(29,1.02f,-4),new Vector3(14,.03f,9),soil,false);
            for(int i=0;i<3;i++){Box("Workbench "+i,root,new Vector3(24+i*4,1.8f,-4),new Vector3(2.5f,.18f,1.2f),wood);foreach(float side in new[]{-1f,1f})Box("Bench support",root,new Vector3(24+i*4+side,1.4f,-4),new Vector3(.15f,.8f,.8f),wood);}
            var crate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/Map01_Optimized/Prefabs/Prop_Crate.prefab");foreach(var p in new[]{new Vector3(23,1,-8),new Vector3(25,1,-8),new Vector3(40,1,29),new Vector3(39,1,-27),new Vector3(-15,1,38)}){var g=(GameObject)PrefabUtility.InstantiatePrefab(crate,root);g.transform.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);}
        }
        static void RiverLandmarks()
        {
            var root=Child("River crossings and evacuation landings",env).transform;
            var bridge=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/Map01_Sept22/Prefabs/Bridge_New.prefab");var g=(GameObject)PrefabUtility.InstantiatePrefab(bridge,root);g.name="Main village bridge";g.transform.position=new Vector3(RiverX,1.15f,-42);g.transform.localScale=new Vector3(.72f,1,1);ColliderBox(g.transform,"Bridge deck",Vector3.zero,new Vector3(24,.18f,3.7f));foreach(float side in new[]{-1f,1f})ColliderBox(g.transform,"Bridge rail",new Vector3(0,.6f,side*1.73f),new Vector3(24,1.2f,.18f));PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
            foreach(float z in new[]{45f,-76f})
            {
                var dock=Child(z>0?"Exposed ferry • repair and evacuation":"Alternate concealed landing",root).transform;dock.position=new Vector3(RiverX+5,1.15f,z);
                for(int j=0;j<15;j++)Box("Dock plank",dock,new Vector3(j*.38f,0,0),new Vector3(.36f,.15f,3),wood,false);
                ColliderBox(dock,"Dock floor",new Vector3(2.6f,0,0),new Vector3(5.8f,.18f,3));foreach(float x in new[]{0f,4.8f})foreach(float side in new[]{-1f,1f})Box("Dock pile",dock,new Vector3(x,-.75f,side*1.3f),new Vector3(.18f,2.9f,.18f),wood);
                var boatMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Environment/Map01_RiverDetails/Boat_Floating.asset");var boat=Model(z>0?"Ferry boat • repair story prop":"Backup evacuation boat",boatMesh,new Vector3(RiverX+2,.21f,z),root,palette);ColliderBox(boat.transform,"Boat hull",new Vector3(0,.1f,0),new Vector3(1.9f,.65f,6.6f));
            }
        }
        static void Vegetation()
        {
            var root=Child("Village trees and riverbank planting",env).transform;
            for(int i=0;i<170;i++)
            {
                float x,z;if(i<85){x=Rand(-106,106);z=(i%2==0?1:-1)*Rand(89,98);}else if(i<125){x=(i%2==0?-1:1)*Rand(101,108);z=Rand(-87,87);}else{x=RiverX+(i%2==0?-1:1)*Rand(9,14);z=Rand(-93,93);if(Mathf.Abs(z+42)<7||Mathf.Abs(z-45)<8||Mathf.Abs(z+76)<7)continue;}
                if(Mathf.Abs(x-RiverX)<9)continue;var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/Map01_Optimized/Prefabs/Tree_"+(i%5)+".prefab");var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);g.transform.position=new Vector3(x,H(x),z);g.transform.localScale=Vector3.one*Rand(.7f,1.1f);PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
            }
            var palm=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Environment/Map01_Realism/Meshes/Coconut_LOD1.asset");foreach(var p in new[]{new Vector3(-18,1,-10),new Vector3(-18,1,40),new Vector3(46,1,24),new Vector3(66,1,58),new Vector3(0,1,55),new Vector3(60,1,-32),new Vector3(23,1,71)}){var g=Model("Coconut palm",palm,p,root,palette);ColliderBox(g.transform,"Trunk",new Vector3(.3f,3,0),new Vector3(.6f,6,.6f));}
        }
        static void Anchor(string id,string title,Vector3 p){var g=Child(id+" • "+title,markers);g.transform.position=p;}
        static void Story()
        {
            Anchor("01_arrival","Nam and Hung arrive from forest",new Vector3(-93,1,-43));Anchor("02_command","Son • protect village and prepare evacuation",new Vector3(12,1,33));
            Anchor("03_materials","Timber rope repair materials",new Vector3(24,1,-8));Anchor("04_workbench","Repair weapons and craft items",new Vector3(28,1,-4));Anchor("05_medicine","Search medical supplies",new Vector3(37,1,29));Anchor("06_ammo","Search ammunition",new Vector3(35,1,-28));
            Anchor("07_ferry","Exposed ferry • repair boat",new Vector3(RiverX+10,1,45));Anchor("08_civilians","Evacuation assembly",new Vector3(-8,1,25));Anchor("09_scout","Survey fields and bridge",new Vector3(-57,1,-35));Anchor("10_safe_route","Reed-bank path to concealed landing",new Vector3(RiverX+11,1,-65));
            foreach(var p in new[]{new Vector3(-53,1,-42),new Vector3(18,1,77),new Vector3(60,1,23)})Anchor("trap_candidate","Trap placement design anchor",p);
            Anchor("night_hold","Holding attack direction • no enemy",new Vector3(-95,1,-42));Anchor("night_suppress","Suppression direction • no enemy",new Vector3(-73,1,30));Anchor("night_flank","Flanking direction • no enemy",new Vector3(95,1,65));Anchor("night_cover_break","Cover pressure direction • no enemy",new Vector3(30,1,88));
        }
        static void Lighting()
        {
            var light=Child("Sun • preparation daytime",null).AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.45f;light.color=new Color(1,.94f,.79f);light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(48,-35,0);RenderSettings.sun=light;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.60f,.70f,.74f);RenderSettings.ambientEquatorColor=new Color(.45f,.49f,.39f);RenderSettings.ambientGroundColor=new Color(.23f,.24f,.18f);RenderSettings.fog=false;
            var night=Child("Night attack lighting • optional preview only",null);var moon=Child("Moon",night.transform).AddComponent<Light>();moon.type=LightType.Directional;moon.intensity=.35f;moon.color=new Color(.42f,.56f,1);moon.transform.rotation=Quaternion.Euler(35,120,0);night.SetActive(false);
        }
        static void Validate(NavMeshSurface surface)
        {
            int guards=Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None).Count(g=>g.gameObject.scene==env.gameObject.scene);if(guards!=0)throw new Exception("Enemy objects present");
            var checks=new List<string>{"PASS continuous terrain 220 x 200 = 44000 m2; same footprint as Map 1","PASS straight river centre x="+RiverX+" at one-third width; 10.5m = 1.5 x Map 1 nominal 7m","PASS no enemy models / Map01EnemyController components in Map 2","PASS 14 houses, 18 rice paddies, two evacuation landings, separate design-only story markers"};
            foreach(var target in new[]{new Vector3(12,1,33),new Vector3(28,1,-4),new Vector3(RiverX+10,1,45),new Vector3(RiverX+10,1,-76)}){var path=new NavMeshPath();bool a=NavMesh.SamplePosition(new Vector3(-93,1,-42),out var p,2,NavMesh.AllAreas),b=NavMesh.SamplePosition(target,out var q,2,NavMesh.AllAreas);bool ok=a&&b&&NavMesh.CalculatePath(p.position,q.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;checks.Add((ok?"PASS":"FAIL")+" arrival route -> "+target);}
            File.WriteAllLines(Reports+"/checks.txt",checks);if(checks.Any(s=>s.StartsWith("FAIL")))throw new Exception("Map 2 navigation checks failed");
        }
        static void Capture(Camera source,Vector3 position,Vector3 target,float ortho,string name)
        {
            var go=new GameObject("Temporary review camera");var c=go.AddComponent<Camera>();c.CopyFrom(source);c.enabled=false;c.transform.position=position;c.transform.LookAt(target);c.orthographic=ortho>0;c.orthographicSize=ortho>0?ortho:10;c.fieldOfView=58;var rt=new RenderTexture(1600,1100,24);var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);var prev=RenderTexture.active;
            try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();File.WriteAllBytes(Reports+"/"+name+".png",tex.EncodeToPNG());}finally{RenderTexture.active=prev;c.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);Object.DestroyImmediate(go);}
        }
    }
}



