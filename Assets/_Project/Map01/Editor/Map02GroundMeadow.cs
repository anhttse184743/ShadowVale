using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;
namespace ShadowVale.Map01.Editor
{
    public static partial class Map02VillageBuilder
    {
        static Transform[] reliefHomes, reliefPlots;
        [InitializeOnLoadMethod] static void RegisterMeadow() { EditorApplication.update += PollMeadow; }
        static void PollMeadow() { const string request="Tools/Map02Meadow.request"; if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return; File.Delete(request); try { ApplyGroundMeadow(); File.WriteAllText(Reports+"/meadow.status","PASS"); } catch(Exception e) { File.WriteAllText(Reports+"/meadow.status","FAIL "+e); Debug.LogException(e); } }
        static Vector3 Unwarp(Vector3 p)
        {
            var q=p;
            for(int i=0;i<16;i++){var e=p-Rural(q);q+=e*.8f;if(e.sqrMagnitude<.000001f)break;}
            return q;
        }
        static float FieldInset(Vector3 q)
        {
            float inset=-100;
            foreach(var plot in reliefPlots){var c=plot.position;float w=c.x<0?23:27;inset=Mathf.Max(inset,Mathf.Min(w*.5f-Mathf.Abs(q.x-c.x),14-Mathf.Abs(q.z-c.z)));}
            return inset;
        }
        static float HomeMask(Vector3 p)
        {
            float mask=1;
            foreach(var h in reliefHomes){var q=h.InverseTransformPoint(new Vector3(p.x,h.position.y,p.z));float d=Mathf.Sqrt(q.x*q.x/81+q.z*q.z/144);mask=Mathf.Min(mask,Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,1.3f,d)));}
            return mask;
        }
        static float Relief(Vector3 p)
        {
            var q=Unwarp(p);float inset=FieldInset(q);
            float depression=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.4f,2.6f,inset));
            float noise=(Mathf.PerlinNoise(p.x*.46f+300,p.z*.46f+300)-.5f)*.11f+(Mathf.PerlinNoise(p.x*1.7f+300,p.z*1.7f+300)-.5f)*.035f;
            float canal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(7,10,Mathf.Abs(q.x-Canal(q.z))));
            return (-.62f*depression+noise*(1-depression*.75f))*HomeMask(p)*canal;
        }
        static Mesh ReliefMesh(Mesh source,Transform owner,string id,int subdivisions)
        {
            var v=source.vertices.Select(owner.TransformPoint).ToList();var colors=source.colors.ToList();if(colors.Count!=v.Count)colors=Enumerable.Repeat(Color.white,v.Count).ToList();var tri=source.triangles.ToList();
            for(int pass=0;pass<subdivisions;pass++)
            {
                var edges=new Dictionary<ulong,int>();var next=new List<int>(tri.Count*4);
                Func<int,int,int> midpoint=(a,b)=>{ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);if(edges.TryGetValue(key,out var index))return index;index=v.Count;v.Add((v[a]+v[b])*.5f);colors.Add((colors[a]+colors[b])*.5f);edges[key]=index;return index;};
                for(int i=0;i<tri.Count;i+=3){int a=tri[i],b=tri[i+1],c=tri[i+2],ab=midpoint(a,b),bc=midpoint(b,c),ca=midpoint(c,a);next.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});}tri=next;
            }
            for(int i=0;i<v.Count;i++){var p=v[i];p.y+=Relief(p);v[i]=owner.InverseTransformPoint(p);}
            var m=new Mesh{indexFormat=IndexFormat.UInt32};m.SetVertices(v);m.SetColors(colors);m.SetTriangles(tri,0);m.RecalculateNormals();m.RecalculateBounds();return SaveMesh(m,id);
        }
        static Mesh MeadowBlade(string id,float height,int blades)
        {
            var v=new List<Vector3>();var c=new List<Color>();var t=new List<int>();
            for(int b=0;b<blades;b++)
            {
                float angle=Rand(0,Mathf.PI*2),h=height*Rand(.65f,1.25f),w=height*.065f;var root=new Vector3(Rand(-.16f,.16f),0,Rand(-.16f,.16f));var side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*w;var bend=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*h*.32f;int k=v.Count;
                v.AddRange(new[]{root-side,root+side,root+bend*.45f+Vector3.up*h*.55f-side*.55f,root+bend*.45f+Vector3.up*h*.55f+side*.55f,root+bend+Vector3.up*h});
                var col=Color.Lerp(new Color(.13f,.25f,.035f),new Color(.38f,.48f,.12f),Rand(0,1));c.AddRange(new[]{col*.72f,col*.72f,col,col,col*1.12f});
                int[] faces={k,k+2,k+1,k+1,k+2,k+3,k+2,k+4,k+3};t.AddRange(faces);for(int j=0;j<faces.Length;j+=3)t.AddRange(new[]{faces[j+2],faces[j+1],faces[j]});
            }
            var m=new Mesh();m.SetVertices(v);m.SetColors(c);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return SaveMesh(m,id);
        }
        [MenuItem("ShadowVale/Map 2/Apply natural ground and meadow")]
        public static void ApplyGroundMeadow()
        {
            var active=EditorSceneManager.GetActiveScene();if(active.isDirty)throw new Exception("Save active scene first.");
            var scene=EditorSceneManager.OpenScene(ScenePath);env=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 Environment")).transform;
            if(env.Find("Natural meadow and additional trees")!=null)throw new Exception("Ground meadow already applied. Restore its scene backup before regenerating.");
            Directory.CreateDirectory(Reports);EditorSceneManager.SaveScene(scene,Reports+"/Map2_before_ground_meadow.unity",true);
            reliefHomes=env.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Stilt house")||t.name.StartsWith("Bamboo thatch house")).ToArray();
            reliefPlots=env.Find("Rice fields surrounding individual homes").Cast<Transform>().Where(t=>t.name.StartsWith("Paddy ")).ToArray();
            palette=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Botanical_Palette.mat");rng=new System.Random(250926);
            int meshId=0;var ground=env.Find("Continuous ground with meandering canal");
            var surfaces=env.GetComponentsInChildren<MeshFilter>().Where(f=>f.transform==ground||f.name.StartsWith("Winding ")&&f.name!="Winding canal water"||f.name=="Irregular rice parcel"||f.name=="Uneven planted bund"||f.name=="Soft-edged homestead yard").ToArray();
            foreach(var f in surfaces){f.sharedMesh=ReliefMesh(f.sharedMesh,f.transform,"Meadow_Surface_"+meshId++,f.transform==ground?1:2);var collider=f.GetComponent<MeshCollider>();if(collider==null)collider=f.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=f.sharedMesh;}
            var riceInstances=env.GetComponentsInChildren<VillageRiceInstances>();int riceCount=riceInstances.Sum(r=>r.plants.Length);
            foreach(var instance in riceInstances){for(int i=0;i<instance.plants.Length;i++){var p=instance.plants[i];var world=instance.transform.TransformPoint(new Vector3(p.x,p.y,p.z));world.y+=Relief(world);var local=instance.transform.InverseTransformPoint(world);instance.plants[i]=new Vector4(local.x,local.y,local.z,p.w);}instance.Rebuild();EditorUtility.SetDirty(instance);}
            var root=Child("Natural meadow and additional trees",env).transform;
            var shortMesh=MeadowBlade("Meadow_Short_LOD0",.23f,7);var shortFar=MeadowBlade("Meadow_Short_LOD1",.20f,3);var tallMesh=MeadowBlade("Meadow_Tall_LOD0",.68f,9);var tallFar=MeadowBlade("Meadow_Tall_LOD1",.6f,3);
            Physics.SyncTransforms();var obstacles=env.GetComponentsInChildren<BoxCollider>();
            int grassCount=0;
            for(int cx=-110;cx<110;cx+=20)for(int cz=-100;cz<100;cz+=20)
            {
                var shortP=new List<Vector4>();var tallP=new List<Vector4>();var origin=new Vector3(cx+10,0,cz+10);
                for(float x=cx+.25f;x<cx+20;x+=.62f)for(float z=cz+.25f;z<cz+20;z+=.62f)
                {
                    var p=new Vector3(x+Rand(-.24f,.24f),1,z+Rand(-.24f,.24f));var q=Unwarp(p);if(Mathf.Abs(q.x)>108||Mathf.Abs(q.z)>98||Mathf.Abs(q.x-Canal(q.z))<8.6f||FieldInset(q)>1.1f||HomeMask(p)<.8f)continue;
                    float cross=new[]{-68f,-36f,-4f,28f,60f}.Min(v=>Mathf.Abs(q.z-v));float along=new[]{-96f,-69f,-42f,17f,48f,80f,108f}.Min(v=>Mathf.Abs(q.x-v));float bank=Mathf.Abs(Mathf.Abs(q.x-Canal(q.z))-10);
                    float road=Mathf.Min(cross-1.25f,Mathf.Min(along-1,bank-1.5f));if(road<.1f)continue;
                    if(obstacles.Any(o=>o.bounds.Contains(p)))continue;
                    float density=Mathf.PerlinNoise(p.x*.075f+400,p.z*.075f+400);if(Rand(0,1)>Mathf.Lerp(.45f,.95f,density))continue;
                    p.y=ReferenceHeight(q.x,q.z)+Relief(p)+.025f;var local=p-origin;bool tall=road>.55f&&Rand(0,1)<(road<2.5f?.55f:.24f);(tall?tallP:shortP).Add(new Vector4(local.x,local.y,local.z,Rand(0,360)));
                }
                for(int type=0;type<2;type++){var plants=type==0?shortP:tallP;if(plants.Count==0)continue;var go=Child((type==0?"Short grass ":"Tall verge grass ")+cx+"_"+cz,root);go.transform.position=origin;var ins=go.AddComponent<VillageRiceInstances>();ins.detailedMesh=type==0?shortMesh:tallMesh;ins.distantMesh=type==0?shortFar:tallFar;ins.material=palette;ins.plants=plants.ToArray();ins.sizes=plants.Select(_=>Rand(.72f,1.3f)).ToArray();ins.Rebuild();grassCount+=plants.Count;}
            }
            botanical=new Dictionary<string,Mesh>();foreach(string species in new[]{"Banyan","Flamboyant"})for(int lod=0;lod<2;lod++)botanical[species+"_LOD"+lod]=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Botanical_"+species+"_LOD"+lod+".asset");
            var treePositions=env.GetComponentsInChildren<LODGroup>().Select(g=>g.transform.position).ToList();int added=0;
            for(int attempt=0;attempt<3000&&added<46;attempt++)
            {
                var q=new Vector3(Rand(-105,105),1,Rand(-94,94));var p=Rural(q);if(FieldInset(q)>-3||Mathf.Abs(q.x-Canal(q.z))<15||HomeMask(p)<.99f||treePositions.Any(v=>Vector3.Distance(p,v)<10))continue;
                if(new[]{-68f,-36f,-4f,28f,60f}.Min(v=>Mathf.Abs(q.z-v))<4||new[]{-96f,-69f,-42f,17f,48f,80f,108f}.Min(v=>Mathf.Abs(q.x-v))<4)continue;
                if(obstacles.Any(o=>Vector3.Distance(o.ClosestPoint(p),p)<3))continue;
                p.y+=Relief(p);var go=Child("Additional banyan "+(++added),root);go.transform.position=p;go.transform.rotation=Quaternion.Euler(0,Rand(0,360),0);go.transform.localScale=Vector3.one*Rand(.48f,.82f);BotanicalTree(go.transform,"Banyan");ColliderBox(go.transform,"Trunk",new Vector3(0,2,0),new Vector3(1.2f,4,1.2f));treePositions.Add(p);
            }
            foreach(var s in env.GetComponents<NavMeshSurface>()){s.RemoveData();Object.DestroyImmediate(s);}Physics.SyncTransforms();var nav=env.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.overrideVoxelSize=true;nav.voxelSize=.2f;nav.BuildNavMesh();AssetDatabase.CreateAsset(nav.navMeshData,Root+"/Meadow_NavMesh.asset");
            var report=new List<string>{"Depression: 0.62m; smooth banks: 3m; shared height function for ground, parcels, roads and rice.","Grass clumps: "+grassCount,"Additional trees: "+added,"Rice preserved: "+riceCount,"Surface meshes: "+meshId};
            foreach(var target in new[]{Rural(new Vector3(32,1,-4)),Rural(new Vector3(64,1,-68)),Rural(new Vector3(-56,1,60))}.Concat(reliefHomes.Select(h=>h.position-h.forward*12)))
            {var path=new NavMeshPath();bool ok=NavMesh.SamplePosition(Rural(new Vector3(-100,1,-4)),out var a,3,NavMesh.AllAreas)&&NavMesh.SamplePosition(target,out var b,3,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;report.Add((ok?"PASS":"FAIL")+" route "+target);}
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);File.WriteAllLines(Reports+"/ground-meadow-checks.txt",report);
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();Capture(camera,new Vector3(-109,35,-67),new Vector3(-58,2,0),0,"map02-meadow-village");Capture(camera,Rural(new Vector3(-94,3,-38)),Rural(new Vector3(-66,.5f,-35)),0,"map02-meadow-path");Capture(camera,new Vector3(18,175,-200),new Vector3(0,0,8),108,"map02-meadow-overview");
            if(report.Any(r=>r.StartsWith("FAIL")))throw new Exception("Navigation validation failed; see ground-meadow-checks.txt");
            Debug.Log("GROUND_MEADOW_COMPLETE "+grassCount+" grass, "+added+" trees");
        }
    }
}

