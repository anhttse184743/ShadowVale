using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    public static partial class Map02VillageBuilder
    {
        // Coordinates precede the shared Rural deformation, keeping both banks and bridges aligned.
        static readonly Vector2[] VillageFieldSeeds = {
            new Vector2(-90,-82),new Vector2(-61,-76),new Vector2(-35,-85),new Vector2(-87,-51),new Vector2(-48,-49),
            new Vector2(-91,84),new Vector2(-62,83),new Vector2(-29,80),
            new Vector2(8,-84),new Vector2(39,-79),new Vector2(80,-88),new Vector2(101,-58),
            new Vector2(13,-51),new Vector2(53,-45),new Vector2(82,-25),new Vector2(26,-18),
            new Vector2(15,14),new Vector2(51,10),new Vector2(99,9),new Vector2(27,42),
            new Vector2(69,39),new Vector2(99,65),new Vector2(43,77),new Vector2(76,88),new Vector2(14,86)
        };
        static readonly List<Vector2[]> VillageLanes = new List<Vector2[]>();
        static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b) { var d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude)); }
        static float VillageRoad(Vector2 q) {
            if(useRoundedVillageRoads) return RoundedRoad(q);
            float d=1000;foreach(var line in VillageLanes)for(int i=1;i<line.Length;i++)d=Mathf.Min(d,SegmentDistance(q,line[i-1],line[i])-1.65f);
            // Village common is the meeting point of the lanes.
            d=Mathf.Min(d,Vector2.Distance(q,new Vector2(-64,11))-9);
            // Preserve the two existing bridge approaches.
            d=Mathf.Min(d,Mathf.Abs(q.y+68)-1.6f);
            if(q.x>-29)d=Mathf.Min(d,Mathf.Abs(q.y+4)-1.7f);
            return d;
        }
        static float VillageParcel(Vector2 q,out int id) {
            float best=float.MaxValue;id=0;
            for(int i=0;i<VillageFieldSeeds.Length;i++){float d=(q-VillageFieldSeeds[i]).sqrMagnitude;if(d<best){best=d;id=i;}}
            float edge=1000;var s=VillageFieldSeeds[id];
            for(int i=0;i<VillageFieldSeeds.Length;i++)if(i!=id){var v=VillageFieldSeeds[i]-s;edge=Mathf.Min(edge,((q-VillageFieldSeeds[i]).sqrMagnitude-best)/(2*v.magnitude));}
            float domain=Mathf.Min(106-Mathf.Abs(q.x),95-Mathf.Abs(q.y));
            domain=Mathf.Min(domain,Mathf.Abs(q.x-Canal(q.y))-10.5f);
            domain=Mathf.Min(domain,(new Vector2((q.x+52)/23f,(q.y-83)/15f).magnitude-1)*15);
            domain=Mathf.Min(domain,Mathf.Min(Vector2.Distance(q,new Vector2(83,82)),Vector2.Distance(q,new Vector2(81,-83)))-7);
            // A single dry village footprint, with fields outside it.
            if(q.x<Canal(q.y))domain=Mathf.Min(domain,Mathf.Max(q.y-69,-43-q.y));
            return Mathf.Min(edge-.85f,Mathf.Min(domain,VillageRoad(q)-.8f));
        }
        [MenuItem("ShadowVale/Map 2/Cluster village and reshape rice parcels")]
        public static void ClusterVillageAndFields() {
            Directory.CreateDirectory(Reports);
            var scene=EditorSceneManager.GetActiveScene();if(scene.isDirty)throw new Exception("Save the active scene first.");
            scene=EditorSceneManager.OpenScene(ScenePath);env=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 Environment")).transform;
            if(env.Find("Clustered village reference layout")!=null)throw new Exception("Layout already applied. Restore the pre-cluster backup before regenerating.");
            EditorSceneManager.SaveScene(scene,Reports+"/Map2_before_cluster.unity",true);
            rng=new System.Random(261001);palette=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Botanical_Palette.mat");
            var village=env.Cast<Transform>().First(t=>t.name.StartsWith("Village"));
            var houses=village.Cast<Transform>().Where(t=>t.name.StartsWith("Stilt house")||t.name.StartsWith("Bamboo thatch house")).ToArray();
            if(houses.Length!=14)throw new Exception("Expected fourteen existing homes.");
            var old=houses.Select(h=>h.position).ToArray();var destinations=new List<Vector3>();var angles=new List<float>();
            for(int row=0;row<5;row++)for(int col=0;col<3;col++) {
                if(row==2&&col==1)continue;
                var q=new Vector3(-88+col*24+Rand(-2,2),1,-31+row*22+Rand(-2,2));destinations.Add(Rural(q));
                angles.Add(col==0?-75+Rand(-12,12):col==2?75+Rand(-12,12):Rand(-15,15));
            }
            var rotations=houses.Select((h,i)=>Quaternion.Euler(0,angles[i],0)*Quaternion.Inverse(h.rotation)).ToArray();
            Action<Transform> moveProp=t=> { int i=Enumerable.Range(0,old.Length).OrderBy(k=>(t.position-old[k]).sqrMagnitude).First();t.position=destinations[i]+rotations[i]*(t.position-old[i]);t.rotation=rotations[i]*t.rotation;if(PrefabUtility.IsPartOfPrefabInstance(t))PrefabUtility.RecordPrefabInstancePropertyModifications(t); };
            foreach(var t in village.Cast<Transform>().Where(t=>!houses.Contains(t)).ToArray())moveProp(t);
            var gardens=env.Find("Natural village gardens");
            foreach(var t in gardens.Cast<Transform>().ToArray()) {
                if(t.name=="Soft-edged homestead yard"){Object.DestroyImmediate(t.gameObject);continue;}
                if(t.name.StartsWith("Blender banyan")||t.name.Contains("Banana")||t.name.Contains("haystack")||t.name.Contains("fence")||t.name.Contains("rail"))moveProp(t);
            }
            for(int i=0;i<houses.Length;i++){houses[i].position=destinations[i];houses[i].rotation=Quaternion.Euler(0,angles[i],0);PrefabUtility.RecordPrefabInstancePropertyModifications(houses[i]);}
            Object.DestroyImmediate(env.Find("Rice fields surrounding individual homes").gameObject);
            Object.DestroyImmediate(env.Find("Earth roads linking homes between paddies").gameObject);
            var root=Child("Clustered village reference layout",env).transform;
            VillageLanes.Clear();
            VillageLanes.Add(new[]{new Vector2(-76,-47),new Vector2(-75,-21),new Vector2(-76,7),new Vector2(-74,37),new Vector2(-74,68)});
            VillageLanes.Add(new[]{new Vector2(-52,-44),new Vector2(-53,-18),new Vector2(-51,12),new Vector2(-51,40),new Vector2(-52,69)});
            VillageLanes.Add(new[]{new Vector2(-101,10),new Vector2(-77,7),new Vector2(-64,11),new Vector2(-50,9),new Vector2(-31,-4),new Vector2(12,-4)});
            foreach(var h in houses){var q=Unwarp(h.position-h.forward*9);float x=Mathf.Abs(q.x+75)<Mathf.Abs(q.x+52)?-75:-52;VillageLanes.Add(new[]{new Vector2(q.x,q.z),new Vector2(x,q.z)});}
            // Replace old paddy depressions and masks together on one continuous surface.
            var ground=env.Find("Continuous ground with meandering canal");var verts=new List<Vector3>();var colors=new List<Color>();var tris=new List<int>();
            const int nx=440,nz=400;
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++) {
                var q=new Vector2(x*.5f-110,z*.5f-100);int id;float inset=VillageParcel(q,out id);float wet=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,2,inset));
                float height=ReferenceHeight(q.x,q.y)-.28f*wet;var p=Rural(new Vector3(q.x,height,q.y));verts.Add(p);
                float grain=Mathf.PerlinNoise(p.x*.29f+300,p.z*.29f+300);
                var c=Color.Lerp(new Color(.25f,.34f,.09f),new Color(.40f,.44f,.16f),grain);
                var field=id%4==0?new Color(.28f,.39f,.08f):new Color(.52f,.45f,.12f);c=Color.Lerp(c,field,wet);
                float road=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.2f,.55f,VillageRoad(q)));if(Mathf.Abs(q.x-Canal(q.y))>8.5f)c=Color.Lerp(c,new Color(.48f,.37f,.22f)*( .9f+grain*.2f),road);
                colors.Add(c);if(x>0&&z>0){int i=z*(nx+1)+x;tris.AddRange(new[]{i-nx-2,i,i-nx-1,i-nx-2,i-1,i});}
            }
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(verts);mesh.SetColors(colors);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=SaveMesh(mesh,"Cluster_Continuous_Terrain");ground.GetComponent<MeshFilter>().sharedMesh=mesh;ground.GetComponent<MeshRenderer>().sharedMaterial=palette;var gc=ground.GetComponent<MeshCollider>();gc.sharedMesh=mesh;
            // Remove trees from crop interiors and village lanes; retain the riverside and perimeter canopy.
            foreach(var lod in env.GetComponentsInChildren<LODGroup>().ToArray()) {
                if(lod.transform.IsChildOf(village))continue;var q=Unwarp(lod.transform.position);int id;
                if(Mathf.Abs(lod.transform.position.x)>101||Mathf.Abs(lod.transform.position.z)>93||VillageParcel(new Vector2(q.x,q.z),out id)>-2||VillageRoad(new Vector2(q.x,q.z))<2||houses.Any(h=>Vector2.Distance(new Vector2(h.position.x,h.position.z),new Vector2(lod.transform.position.x,lod.transform.position.z))<7))Object.DestroyImmediate(lod.gameObject);
            }
            Physics.SyncTransforms();
            foreach(var ins in env.GetComponentsInChildren<VillageRiceInstances>()) {
                var kept=new List<Vector4>();var sizes=new List<float>();
                for(int i=0;i<ins.plants.Length;i++){var p=ins.plants[i];var w=ins.transform.TransformPoint(new Vector3(p.x,p.y,p.z));var q=Unwarp(w);int id;
                    if(VillageParcel(new Vector2(q.x,q.z),out id)>-.4f||VillageRoad(new Vector2(q.x,q.z))<.65f||houses.Any(h=>Vector2.Distance(new Vector2(h.position.x,h.position.z),new Vector2(w.x,w.z))<10))continue;
                    if(gc.Raycast(new Ray(new Vector3(w.x,5,w.z),Vector3.down),out var hit,10)){w.y=hit.point.y+.015f;var local=ins.transform.InverseTransformPoint(w);kept.Add(new Vector4(local.x,local.y,local.z,p.w));sizes.Add(ins.sizes[i]);}
                }ins.plants=kept.ToArray();ins.sizes=sizes.ToArray();ins.Rebuild();EditorUtility.SetDirty(ins);
            }
            var cropLists=VillageFieldSeeds.Select(_=>new List<Vector4>()).ToArray();
            for(float x=-105;x<106;x+=.57f)for(float z=-94;z<95;z+=.60f){var q=new Vector2(x+Rand(-.065f,.065f),z+Rand(-.065f,.065f));int id;if(VillageParcel(q,out id)<.45f)continue;var p=Rural(new Vector3(q.x,1,q.y));if(gc.Raycast(new Ray(p+Vector3.up*4,Vector3.down),out var hit,10)){p.y=hit.point.y+.008f;cropLists[id].Add(new Vector4(p.x,p.y,p.z,Rand(0,360)));}}
            int total=0;for(int i=0;i<cropLists.Length;i++){var go=Child("Connected rice parcel "+(i+1),root);var ins=go.AddComponent<VillageRiceInstances>();string kind=i%4==0?"Rice_Young":"Rice_Ripe";ins.detailedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Botanical_"+kind+"_LOD0.asset");ins.distantMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Botanical_"+kind+"_LOD1.asset");ins.material=palette;ins.plants=cropLists[i].ToArray();ins.sizes=ins.plants.Select(_=>Rand(.9f,1.17f)).ToArray();ins.Rebuild();total+=ins.plants.Length;}
            foreach(var n in env.GetComponents<NavMeshSurface>()){n.RemoveData();Object.DestroyImmediate(n);}Physics.SyncTransforms();var nav=env.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.overrideVoxelSize=true;nav.voxelSize=.2f;nav.BuildNavMesh();AssetDatabase.CreateAsset(nav.navMeshData,Root+"/Cluster_NavMesh.asset");
            var report=new List<string>{"Homes: "+houses.Length,"Irregular connected parcels: "+cropLists.Length,"Rice clumps: "+total};
            var start=Rural(new Vector3(-64,1,11));
            foreach(var target in houses.Select(h=>h.position-h.forward*9).Concat(new[]{Rural(new Vector3(35,1,-4)),Rural(new Vector3(65,1,-68))})) {var path=new NavMeshPath();bool ok=NavMesh.SamplePosition(start,out var a,3,NavMesh.AllAreas)&&NavMesh.SamplePosition(target,out var b,3,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;report.Add((ok?"PASS":"FAIL")+" route "+target);}
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);scene=EditorSceneManager.OpenScene(ScenePath);
            var saved=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VillageRiceInstances>()).Where(r=>r.name.StartsWith("Connected rice parcel")).ToArray();
            if(saved.Length!=cropLists.Length||saved.Sum(r=>r.plants.Length)!=total)throw new Exception("Saved crop layout did not round-trip.");report.Add("PASS saved scene reload and crop counts");File.WriteAllLines(Reports+"/cluster-checks.txt",report);
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();Capture(camera,new Vector3(0,205,-130),new Vector3(0,0,0),112,"map02-cluster-overview");Capture(camera,new Vector3(-105,77,-82),new Vector3(-65,1,10),53,"map02-cluster-village");Capture(camera,new Vector3(0,250,-.1f),Vector3.zero,108,"map02-cluster-plan");
            if(report.Any(s=>s.StartsWith("FAIL")))throw new Exception("Navigation validation failed; see cluster-checks.txt.");
            Debug.Log("CLUSTER_LAYOUT_COMPLETE "+total);
        }
    }
}


