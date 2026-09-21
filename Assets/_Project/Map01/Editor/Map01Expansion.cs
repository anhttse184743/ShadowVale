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
    [InitializeOnLoad]
    public static class Map01Expansion
    {
        const string Root = "Assets/_Project/Art/Environment/Map01_Expansion";
        const string Request = "Tools/Map01Expansion.request";
        const string Report = "Tools/Map01OptimizedReports/expansion.status";
        static OptimizedMapBuilder.SourceData source;
        static Material wood, roof, leaf, grass, earth;
        static readonly List<Vector3> camps = new List<Vector3>();
        static System.Random random;
        static Map01Expansion() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string action = File.ReadAllText(Request).Trim(); File.Delete(Request);
            try { if(action == "capture") Capture(); else Apply(); File.WriteAllText(Report, "PASS " + DateTime.UtcNow.ToString("O")); }
            catch (Exception e) { File.WriteAllText(Report, "FAIL " + e); Debug.LogException(e); }
        }
        static void Capture()
        {
            var camera = Object.FindFirstObjectByType<ForestMission>().gameCamera;
            var rt = RenderTexture.GetTemporary(1280,720,24); var previous = RenderTexture.active;
            var oldTarget = camera.targetTexture;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                File.WriteAllBytes("Tools/Map01OptimizedReports/third-person.png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
            finally{camera.targetTexture=oldTarget;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        }
        static float Rand(float min, float max) => min + (float)random.NextDouble() * (max - min);
        static GameObject Child(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go;
        }
        static Material Mat(string name, Color color)
        {
            var path = Root + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", .05f); mat.SetFloat("_Cull", 0); mat.enableInstancing = true; EditorUtility.SetDirty(mat); return mat;
        }
        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material mat, bool collision = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = mat;
            go.layer = LayerMask.NameToLayer("Obstacle"); if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        static bool Ground(float x, float z, out Vector3 p)
        {
            // Only the authored walk mesh counts as ground, never roofs or tree crowns.
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 80, z), Vector3.down, 160, LayerMask.GetMask("Obstacle")).OrderByDescending(h => h.point.y))
                if (hit.collider.name.Contains("Collision_Walk") || hit.collider.name.StartsWith("Hill ")) { p = hit.point; return true; }
            p = default; return false;
        }
        static float RouteDistance(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var route in source.routes.Concat(source.patrols))
                for (int i = 3; i < route.positions.Length; i += 3)
                {
                    var a = new Vector2(route.positions[i-3], route.positions[i-1]); var b = new Vector2(route.positions[i], route.positions[i+2]);
                    var q = new Vector2(p.x, p.z); var d = b-a;
                    best = Mathf.Min(best, Vector2.Distance(q, a + d * Mathf.Clamp01(Vector2.Dot(q-a,d)/Mathf.Max(.001f,d.sqrMagnitude))));
                }
            return best;
        }
        static Mesh SaveMesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            string path = Root + "/" + name + ".asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); } else mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh); return mesh;
        }
        static GameObject Visual(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = Child(name, parent, Vector3.zero); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat; return go;
        }
        static void Blade(List<Vector3> v, List<int> t, Vector3 start, Vector3 end, float width)
        {
            int n = v.Count; var side = Vector3.Cross(end-start, Vector3.up).normalized;
            if (side.sqrMagnitude < .01f) side = Vector3.right;
            var mid = Vector3.Lerp(start,end,.48f) + Vector3.up * width * .3f;
            v.Add(start); v.Add(mid-side*width); v.Add(end); v.Add(mid+side*width);
            t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        static Mesh Leaves(bool banana)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for(int i=0;i<(banana?8:11);i++)
            {
                float angle=i*Mathf.PI*2/(banana?8:11); var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var start=Vector3.up*(banana?3.1f:7.1f); float length=banana?2.5f:3.7f;
                for(int j=0;j<7;j++)
                {
                    float a=j/7f,b=(j+1)/7f;
                    var p=start+direction*length*a+Vector3.up*(Mathf.Sin(a*Mathf.PI)*.8f-a*.9f);
                    var q=start+direction*length*b+Vector3.up*(Mathf.Sin(b*Mathf.PI)*.8f-b*.9f);
                    if(banana) Blade(v,t,p,q,.5f*Mathf.Sin((a+.1f)*Mathf.PI));
                    else for(int side=-1;side<=1;side+=2)
                        Blade(v,t,p,q+Vector3.Cross(direction,Vector3.up)*side*(.65f*(1-a)+.1f)-Vector3.up*.22f,.10f);
                }
            }
            return SaveMesh(banana?"BananaLeaves":"CoconutFronds",v,t);
        }
        static void Plant(Transform parent, Vector3 p, bool banana, Mesh leaves, int index)
        {
            var root=Child((banana?"Banana ":"Coconut ")+index,parent,p); root.transform.localRotation=Quaternion.Euler(0,Rand(0,360),0); root.transform.localScale=Vector3.one*Rand(.85f,1.15f);
            var trunk=GameObject.CreatePrimitive(PrimitiveType.Cylinder); trunk.name="Trunk"; trunk.transform.SetParent(root.transform,false);
            float height=banana?3.2f:7; trunk.transform.localPosition=Vector3.up*height*.5f; trunk.transform.localScale=new Vector3(banana?.32f:.45f,height*.5f,banana?.32f:.45f); trunk.layer=LayerMask.NameToLayer("Obstacle"); trunk.GetComponent<Renderer>().sharedMaterial=banana?leaf:wood;
            var crown=Visual("Crown",root.transform,leaves,leaf); var lod=root.AddComponent<LODGroup>(); lod.SetLODs(new[]{new LOD(.012f,new[]{trunk.GetComponent<Renderer>(),crown.GetComponent<Renderer>()})}); lod.RecalculateBounds();
            if(!banana) for(int i=0;i<3;i++)
            {
                var fruit=GameObject.CreatePrimitive(PrimitiveType.Sphere); fruit.name="Coconut fruit";fruit.transform.SetParent(root.transform,false);fruit.transform.localPosition=new Vector3(Mathf.Cos(i*2)*.3f,6.85f,Mathf.Sin(i*2)*.3f);fruit.transform.localScale=Vector3.one*.35f;fruit.GetComponent<Renderer>().sharedMaterial=wood;Object.DestroyImmediate(fruit.GetComponent<Collider>());
            }
        }
        static void House(Transform parent, Vector3 p, int index)
        {
            var root=Child("House "+index,parent,p);
            Box("Floor",root.transform,new Vector3(0,.12f,0),new Vector3(6,.24f,5),wood);
            Box("Back wall",root.transform,new Vector3(0,1.65f,2.4f),new Vector3(6,3,.18f),wood);
            foreach(float x in new[]{-2.9f,2.9f}) Box("Side wall",root.transform,new Vector3(x,1.65f,0),new Vector3(.18f,3,5),wood);
            foreach(float x in new[]{-2f,2f}) Box("Door side",root.transform,new Vector3(x,1.65f,-2.4f),new Vector3(2,3,.18f),wood);
            Box("Door lintel",root.transform,new Vector3(0,2.85f,-2.4f),new Vector3(2,.6f,.18f),wood);
            foreach(int side in new[]{-1,1}) { var panel=Box("Sloping roof",root.transform,new Vector3(side*1.65f,3.55f,0),new Vector3(3.8f,.18f,6),roof); panel.transform.localRotation=Quaternion.Euler(0,0,-side*23); }
        }
        [MenuItem("ShadowVale/Map 1/Apply Tropical Expansion")]
        public static void Apply()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.isDirty) throw new InvalidOperationException("Save the open scene before applying expansion.");
            if(scene.path!=OptimizedMapBuilder.ScenePath) scene=EditorSceneManager.OpenScene(OptimizedMapBuilder.ScenePath);
            Directory.CreateDirectory(Root); AssetDatabase.Refresh(); random=new System.Random(210926); camps.Clear();
            using(var file=File.OpenRead("SourceArt/Map01_Optimized/Map01.meshdata.json.gz")) using(var gz=new GZipStream(file,CompressionMode.Decompress)) using(var reader=new StreamReader(gz)) source=JsonUtility.FromJson<OptimizedMapBuilder.SourceData>(reader.ReadToEnd());
            var mission=Object.FindFirstObjectByType<ForestMission>(); var surface=Object.FindFirstObjectByType<NavMeshSurface>();
            var old=GameObject.Find("03 Tropical expansion"); if(old!=null) Object.DestroyImmediate(old);
            var root=Child("03 Tropical expansion",surface.transform,Vector3.zero);
            wood=Mat("Weathered timber",new Color(.28f,.19f,.10f)); roof=Mat("Palm thatch",new Color(.43f,.39f,.20f)); leaf=Mat("Tropical leaves",new Color(.19f,.38f,.10f)); grass=Mat("Tall grass",new Color(.32f,.43f,.16f)); earth=Mat("Hill ground",new Color(.27f,.33f,.16f));
            Physics.SyncTransforms();
            int hills=0;
            foreach(var center in new[]{new Vector3(-85,0,30),new Vector3(78,0,-55),new Vector3(-60,0,70),new Vector3(80,0,62)})
            {
                float radius=Mathf.Min(19,RouteDistance(center)-5); if(radius<8 || !Ground(center.x,center.z,out var basePoint)) continue;
                var v=new List<Vector3>();var t=new List<int>();const int steps=24; bool valid=true;
                for(int z=0;z<=steps;z++)for(int x=0;x<=steps;x++)
                {
                    float px=center.x+(x/(float)steps*2-1)*radius,pz=center.z+(z/(float)steps*2-1)*radius;
                    if(!Ground(px,pz,out var p)){valid=false;p=new Vector3(px,basePoint.y,pz);}
                    float r=new Vector2(px-center.x,pz-center.z).magnitude/radius;
                    p.y+=Mathf.Pow(Mathf.Max(0,1-r*r),2)*4.5f-.03f;v.Add(p);
                    if(x<steps&&z<steps){int n=z*(steps+1)+x;t.AddRange(new[]{n,n+steps+1,n+1,n+1,n+steps+1,n+steps+2});}
                }
                if(!valid)continue;
                var mesh=SaveMesh("Hill_"+hills,v,t); var hill=Visual("Hill "+hills++,root.transform,mesh,earth);hill.layer=LayerMask.NameToLayer("Obstacle");hill.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
            Physics.SyncTransforms();
            // Spread outposts around the map, keeping authored mission corridors open.
            foreach(var candidate in new[]{new Vector3(-63,0,-14),new Vector3(-50,0,38),new Vector3(52,0,-42),new Vector3(67,0,7),new Vector3(-30,0,66),new Vector3(55,0,69)})
            {
                if(!Ground(candidate.x,candidate.z,out var p)||RouteDistance(p)<7)continue;
                camps.Add(p);var camp=Child("Enemy outpost "+camps.Count,root.transform,p);House(camp.transform,Vector3.zero,camps.Count);
                for(int i=0;i<4;i++) Box("Supply crate",camp.transform,new Vector3(-4-i%2*1.1f,.5f,1+i/2*1.1f),Vector3.one,wood);
                foreach(float x in new[]{-5f,5f})Box("Defensive barricade",camp.transform,new Vector3(x,.65f,-5),new Vector3(2.8f,1.3f,.6f),wood);
            }
            var vegetation=Child("Evenly distributed tropical vegetation",root.transform,Vector3.zero);var palmMesh=Leaves(false);var bananaMesh=Leaves(true);
            int plants=0,patches=0;Physics.SyncTransforms();
            for(float z=-87;z<90;z+=9)for(float x=-98;x<99;x+=9)
            {
                float px=x+Rand(-2,2),pz=z+Rand(-2,2);
                if(!Ground(px,pz,out var p)||RouteDistance(p)<3.2f||camps.Any(c=>Vector3.Distance(c,p)<10)||Vector3.Distance(p,mission.player.position)<9)continue;
                if(plants%3!=2)Plant(vegetation.transform,p,plants%2==0,plants%2==0?bananaMesh:palmMesh,plants);
                plants++;
                var v=new List<Vector3>();var t=new List<int>();
                for(int i=0;i<85;i++)
                {
                    float gx=p.x+Rand(-3.8f,3.8f),gz=p.z+Rand(-3.8f,3.8f);
                    if(!Ground(gx,gz,out var q)||RouteDistance(q)<2.5f)continue;
                    for(int b=0;b<3;b++)Blade(v,t,q,q+new Vector3(Rand(-.4f,.4f),Rand(.8f,1.5f),Rand(-.4f,.4f)),.065f);
                }
                var patch=Visual("Tall grass patch "+patches,vegetation.transform,SaveMesh("Grass_"+patches,v,t),grass);patch.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
                var lod=patch.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.025f,new[]{patch.GetComponent<Renderer>()})});lod.RecalculateBounds();
                var hide=Child("Grass concealment "+patches,vegetation.transform,p).AddComponent<ForestPoint>();hide.id="expansion_hide_"+patches++;hide.kind=ForestPointKind.Hide;hide.radius=3.5f;
            }
            Physics.SyncTransforms();surface.BuildNavMesh();
            string navPath=Root+"/ExpansionNavMesh.asset";var nav=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if(nav==null)AssetDatabase.CreateAsset(surface.navMeshData,navPath);else{EditorUtility.CopySerialized(surface.navMeshData,nav);surface.RemoveData();surface.navMeshData=nav;surface.AddData();EditorUtility.SetDirty(nav);}
            var template=Object.FindObjectsByType<ForestGuard>(FindObjectsSortMode.None).First(g=>!g.transform.IsChildOf(root.transform));
            int guardCount=0;
            foreach(var camp in camps)
            {
                var route=new List<Vector3>();foreach(var delta in new[]{new Vector3(-7,0,-7),new Vector3(7,0,-7),new Vector3(7,0,6),new Vector3(-7,0,6)})
                {if(!NavMesh.SamplePosition(camp+delta,out var hit,5,NavMesh.AllAreas))throw new Exception("No patrol position near outpost "+camp);route.Add(hit.position);}
                for(int i=0;i<route.Count;i++){var path=new NavMeshPath();if(!NavMesh.CalculatePath(route[i],route[(i+1)%route.Count],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Disconnected outpost patrol");}
                for(int i=0;i<2;i++){var guard=Object.Instantiate(template,route[i*2],Quaternion.identity,root.transform);guard.name="Outpost guard "+guardCount;guard.id="expansion_guard_"+guardCount++;guard.patrol=Enumerable.Range(0,4).Select(j=>route[(j+i*2)%4]).ToArray();}
            }
            var cam=mission.gameCamera;cam.name="Third person shoulder camera";cam.orthographic=false;cam.fieldOfView=62;cam.nearClipPlane=.08f;
            var follow=cam.GetComponent<ForestThirdPersonCamera>();if(follow==null)follow=cam.gameObject.AddComponent<ForestThirdPersonCamera>();follow.target=mission.player;follow.mission=mission;
            cam.transform.rotation=Quaternion.Euler(16,mission.player.eulerAngles.y,0);cam.transform.position=mission.player.position+Vector3.up*1.55f+cam.transform.rotation*new Vector3(.55f,.15f,-4.5f);
            OptimizedMapBuilder.Validate();AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Tools/Map01OptimizedReports/expansion-summary.txt",$"Hills: {hills}\nHouses / enemy outposts: {camps.Count}\nAdditional guards: {guardCount}\nVegetation cells: {plants}\nTall grass patches: {patches}\nCamera: third person, 62 FOV, obstruction sphere cast\nOriginal routes and new patrol loops: PASS\n");
            if(File.Exists(TropicalRealismBuilder.Source) && File.Exists("SourceArt/Map01_Realism/StaticRefinement.meshdata.json.gz")) TropicalRealismBuilder.Apply();
        }
    }
}
