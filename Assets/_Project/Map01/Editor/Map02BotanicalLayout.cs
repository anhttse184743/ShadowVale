using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    public static partial class Map02VillageBuilder
    {
        static Dictionary<string,Mesh> botanical;
        static void ImportBotanical()
        {
            VillageSourceData data;using(var f=File.OpenRead("SourceArt/Map02_Village/Botanical.meshdata.json.gz"))using(var z=new GZipStream(f,CompressionMode.Decompress))using(var r=new StreamReader(z))data=JsonUtility.FromJson<VillageSourceData>(r.ReadToEnd());
            botanical=new Dictionary<string,Mesh>();
            foreach(var source in data.meshes){var m=new Mesh{indexFormat=IndexFormat.UInt32};int n=source.vertices.Length/3;var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];for(int i=0;i<n;i++){v[i]=new Vector3(source.vertices[i*3],source.vertices[i*3+1],source.vertices[i*3+2]);normals[i]=new Vector3(source.normals[i*3],source.normals[i*3+1],source.normals[i*3+2]);colors[i]=new Color(source.colors[i*4],source.colors[i*4+1],source.colors[i*4+2],1);}m.vertices=v;m.normals=normals;m.colors=colors;m.triangles=source.triangles;m.RecalculateBounds();botanical[source.id]=SaveMesh(m,"Botanical_"+source.id);}
        }
        static void BotanicalTree(Transform parent,string species)
        {
            var lods=new LOD[2];for(int i=0;i<2;i++){var g=Model(species+" LOD"+i,botanical[species+"_LOD"+i],Vector3.zero,parent,palette);lods[i]=new LOD(i==0?.16f:.009f,new[]{g.GetComponent<Renderer>()});}
            var group=parent.gameObject.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
        }
        [MenuItem("ShadowVale/Map 2/Apply Blender rice banyan and flamboyant")]
        public static void ApplyBotanical()
        {
            Directory.CreateDirectory(Reports);var scene=EditorSceneManager.GetActiveScene();if(scene.path!=ScenePath){if(scene.isDirty)throw new Exception("Save the active scene first.");scene=EditorSceneManager.OpenScene(ScenePath);}
            env=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 Environment")).transform;
            if(env.Find("Blender flamboyant trees - exactly three")!=null)throw new Exception("Botanical revision already applied; restore its backup to regenerate.");
            EditorSceneManager.SaveScene(scene,Reports+"/Map2_before_botanical_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
            ImportBotanical();rng=new System.Random(239763);
            var sourcePalette=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Optimized/Materials/MapPalette.mat");
            var materialPath=Root+"/Botanical_Palette.mat";palette=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(palette==null){palette=new Material(sourcePalette);AssetDatabase.CreateAsset(palette,materialPath);}palette.enableInstancing=true;EditorUtility.SetDirty(palette);
            var gardens=env.Find("Natural village gardens");if(gardens==null)throw new Exception("Apply the natural village revision before botanical assets.");
            int trees=0;foreach(var t in gardens.Cast<Transform>().Where(t=>t.name=="Homestead shade tree"||t.name=="Irregular orchard shade").ToArray())
            {
                Object.DestroyImmediate(t.GetComponent<MeshRenderer>());Object.DestroyImmediate(t.GetComponent<MeshFilter>());t.name="Blender banyan "+(++trees);BotanicalTree(t,"Banyan");
                foreach(var box in t.GetComponentsInChildren<BoxCollider>())box.size=new Vector3(1.65f,box.size.y,1.65f);
            }
            Physics.SyncTransforms();var phoenix=Child("Blender flamboyant trees - exactly three",env).transform;
            var candidates=new List<Vector3>();foreach(float z in new[]{21f,39f,-30f,-46f,73f,53f,8f,-18f,-54f,83f})foreach(float side in new[]{-1f,1f})candidates.Add(Rural(new Vector3(Canal(z)+side*15,1,z)));candidates.Add(Rural(new Vector3(-39,1,22)));candidates.Add(Rural(new Vector3(-38,1,-36)));
            for(int i=candidates.Count-1;i>0;i--){int j=rng.Next(i+1);var tmp=candidates[i];candidates[i]=candidates[j];candidates[j]=tmp;}
            var placed=new List<Vector3>();var obstacles=env.GetComponentsInChildren<Collider>().Where(c=>c is BoxCollider).ToArray();
            foreach(var p in candidates)
            {
                if(placed.Count==3)break;if(gardens.Cast<Transform>().Any(t=>t.name.StartsWith("Blender banyan")&&Vector3.Distance(t.position,p)<13))continue;if(placed.Any(q=>Vector3.Distance(q,p)<23))continue;
                if(obstacles.Any(c=>Vector3.Distance(c.ClosestPoint(p+Vector3.up),p+Vector3.up)<2.1f))continue;
                var g=Child("Royal poinciana "+(placed.Count+1)+" - riverside village",phoenix);g.transform.position=p;g.transform.rotation=Quaternion.Euler(0,Rand(0,360),0);g.transform.localScale=Vector3.one*Rand(.85f,1.05f);BotanicalTree(g.transform,"Flamboyant");ColliderBox(g.transform,"Flamboyant trunk",new Vector3(0,2,0),new Vector3(1.25f,4,1.25f));placed.Add(p);
            }
            if(placed.Count!=3)throw new Exception("Could not place exactly three safe flamboyant trees.");
            var fields=env.Find("Rice fields surrounding individual homes");var previous=fields.Find("Irregular dense rice rows");if(previous!=null)Object.DestroyImmediate(previous.gameObject);
            var houses=env.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Stilt house")||t.name.StartsWith("Bamboo thatch house")).ToArray();
            var plots=fields.Cast<Transform>().Where(t=>t.name.StartsWith("Paddy ")).ToArray();var ripe=Enumerable.Range(0,plots.Length).Select(i=>i<plots.Length/2).ToArray();for(int i=ripe.Length-1;i>0;i--){int j=rng.Next(i+1);bool temp=ripe[i];ripe[i]=ripe[j];ripe[j]=temp;}
            int total=0;var report=new List<string>{"Blender source: Map02_Botanical_Models.blend","Banyan replacements: "+trees,"Flamboyant trees: "+placed.Count};
            for(int i=0;i<plots.Length;i++)
            {
                var plot=plots[i];var center=plot.position;float width=center.x<0?23:27;var plants=new List<Vector4>();var sizes=new List<float>();
                for(float x=-width/2+.65f;x<width/2-.65f;x+=.52f)for(float z=-13.3f;z<13.3f;z+=.56f)
                {
                    var original=new Vector3(center.x+x+Rand(-.075f,.075f),1.065f,center.z+z+Rand(-.075f,.075f));var p=Rural(original);
                    if(houses.Any(h=>{var q=h.InverseTransformPoint(new Vector3(p.x,h.position.y,p.z));return q.x*q.x/68+q.z*q.z/114<1.12f;}))continue;
                    bool home=houses.Any(h=>Vector2.Distance(new Vector2(h.position.x,h.position.z),new Vector2(Rural(center).x,Rural(center).z))<4);
                    if(home&&Mathf.Abs(original.x-center.x)<1.7f&&original.z<center.z-4&&original.z>center.z-17)continue;
                    if(rng.NextDouble()<.02)continue;var local=p-Rural(center);plants.Add(new Vector4(local.x,local.y,local.z,Rand(0,360)));sizes.Add(Rand(.9f,1.2f));
                }
                string kind=ripe[i]?"Rice_Ripe":"Rice_Young";var go=Child((ripe[i]?"Golden mature":"Green young")+" rice - instanced",plot);go.transform.position=Rural(center);var instance=go.AddComponent<VillageRiceInstances>();instance.detailedMesh=botanical[kind+"_LOD0"];instance.distantMesh=botanical[kind+"_LOD1"];instance.material=palette;instance.plants=plants.ToArray();instance.sizes=sizes.ToArray();instance.Rebuild();
                var tone=Mat(ripe[i]?"Botanical_Ripe_Field":"Botanical_Young_Field",ripe[i]?new Color(.55f,.46f,.105f):new Color(.20f,.34f,.07f));foreach(var r in plot.GetComponentsInChildren<MeshRenderer>())if(r.name=="Irregular rice parcel")r.sharedMaterial=tone;
                total+=plants.Count;report.Add("Paddy "+i+": "+kind+", "+plants.Count+" clumps");
            }
            report.Add("Total clumps: "+total+" (previous 8452)");if(total<16904)throw new Exception("Rice density must at least double.");
            foreach(var s in env.GetComponents<NavMeshSurface>()){s.RemoveData();Object.DestroyImmediate(s);}Physics.SyncTransforms();var nav=env.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.overrideVoxelSize=true;nav.voxelSize=.2f;nav.BuildNavMesh();var navPath=Root+"/Botanical_NavMesh.asset";if(AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath)!=null)AssetDatabase.DeleteAsset(navPath);AssetDatabase.CreateAsset(nav.navMeshData,navPath);
            var targets=new[]{Rural(new Vector3(32,1,-4)),Rural(new Vector3(64,1,-68)),Rural(new Vector3(-56,1,60))}.Concat(houses.Select(h=>new Vector3(h.position.x,1,h.position.z)-h.forward*12));
            foreach(var target in targets){var np=new NavMeshPath();bool ok=NavMesh.SamplePosition(Rural(new Vector3(-100,1,-4)),out var a,3,NavMesh.AllAreas)&&NavMesh.SamplePosition(target,out var b,3,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,np)&&np.status==NavMeshPathStatus.PathComplete;report.Add((ok?"PASS":"FAIL")+" route "+target);}
            foreach(var p in placed)report.Add("Flamboyant position: "+p);File.WriteAllLines(Reports+"/botanical-checks.txt",report);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();
            Capture(camera,new Vector3(18,175,-200),new Vector3(0,0,8),108,"map02-botanical-overview");Capture(camera,new Vector3(-109,35,-67),new Vector3(-58,2,0),0,"map02-botanical-village");
            var treePos=placed[0];Capture(camera,treePos+new Vector3(17,10,-24),treePos+Vector3.up*4,0,"map02-flamboyant");
            var banyan=gardens.Cast<Transform>().First(t=>t.name.StartsWith("Blender banyan"));Capture(camera,banyan.position+new Vector3(13,6,-15),banyan.position+Vector3.up*4,0,"map02-banyan");
            var first=plots.First(p=>p.GetComponentInChildren<VillageRiceInstances>().name.StartsWith("Golden"));var focus=Rural(first.position+new Vector3(-9,0,-8));Capture(camera,focus+new Vector3(3,2.6f,-4),focus+Vector3.up*.7f,0,"map02-rice-close");
            if(report.Any(s=>s.StartsWith("FAIL")))throw new Exception("Botanical navigation checks failed.");
            var saved=EditorSceneManager.OpenScene(ScenePath);var savedRice=saved.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VillageRiceInstances>()).ToArray();if(savedRice.Length!=20||savedRice.Sum(r=>r.plants.Length)!=total)throw new Exception("Saved rice instances did not round-trip.");
            var savedCamera=saved.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();Capture(savedCamera,new Vector3(18,175,-200),new Vector3(0,0,8),108,"map02-botanical-overview");report.Add("PASS saved scene reload: 20 instanced fields and "+total+" plants");File.WriteAllLines(Reports+"/botanical-checks.txt",report);
        }
    }
}



