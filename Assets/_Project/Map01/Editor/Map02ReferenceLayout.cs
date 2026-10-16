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
        static float Canal(float z) => -8 + 10 * Mathf.Sin((z + 12) * .027f);
        static float ReferenceHeight(float x,float z) => Mathf.Lerp(-.8f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(5.25f,8.5f,Mathf.Abs(x-Canal(z)))));
        static void PollReference()
        {
            const string request="Tools/Map02Reference.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(request);Directory.CreateDirectory(Reports);
            try { ApplyReference();File.WriteAllText(Reports+"/reference.status","PASS "+DateTime.UtcNow.ToString("O")); }
            catch(Exception e){File.WriteAllText(Reports+"/reference.status","FAIL "+e);Debug.LogException(e);}
        }
        static void Lane(Transform parent,string name,Vector3 a,Vector3 b,float width)
        {
            var g=Box(name,parent,(a+b)*.5f,new Vector3(width,.045f,Vector3.Distance(a,b)),soil,false);
            g.transform.rotation=Quaternion.LookRotation(b-a);
        }
        [MenuItem("ShadowVale/Map 2/Apply Ap Bac reference layout")]
        public static void ApplyReference()
        {
            Directory.CreateDirectory(Reports);
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=ScenePath){if(scene.isDirty)throw new InvalidOperationException("Save the active scene before applying Map 2 layout.");scene=EditorSceneManager.OpenScene(ScenePath);}
            if(scene.GetRootGameObjects().Any(g=>g.GetComponentsInChildren<Transform>().Any(t=>t.name=="Continuous ground with meandering canal")))throw new InvalidOperationException("Reference layout already applied; edit the scene directly or restore its backup before reapplying.");
            EditorSceneManager.SaveScene(scene,Reports+"/Map2_before_reference_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true);
            env=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 Environment")).transform;
            markers=scene.GetRootGameObjects().First(g=>g.name.StartsWith("02 Story")).transform;
            rng=new System.Random(230926);
            palette=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Optimized/Materials/MapPalette.mat");
            soil=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Warm earth.mat");wood=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Timber.mat");
            water=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Map01_Realism/River surface.mat");
            foreach(var s in env.GetComponents<NavMeshSurface>()){s.RemoveData();Object.DestroyImmediate(s);}
            // Retain the authored houses and their interiors; regenerate the surrounding landscape.
            var village=env.Cast<Transform>().First(t=>t.name.StartsWith("Village •"));
            foreach(var child in env.Cast<Transform>().ToArray())if(child!=village)Object.DestroyImmediate(child.gameObject);
            var houses=village.Cast<Transform>().Where(t=>t.name.StartsWith("Stilt house")||t.name.StartsWith("Bamboo thatch house")).ToArray();
            var positions=new[]{new Vector2(-82,44),new Vector2(-56,44),new Vector2(-82,12),new Vector2(-56,12),new Vector2(-82,-20),new Vector2(-56,-20),new Vector2(-82,-52),new Vector2(-56,-52),new Vector2(32,44),new Vector2(64,12),new Vector2(96,44),new Vector2(32,-20),new Vector2(96,-20),new Vector2(64,-52)};
            var oldPositions=houses.Select(t=>t.position).ToArray();
            for(int i=0;i<houses.Length;i++){houses[i].position=new Vector3(positions[i].x,houses[i].position.y,positions[i].y);PrefabUtility.RecordPrefabInstancePropertyModifications(houses[i]);}
            foreach(var child in village.Cast<Transform>().ToArray())
            {
                if(houses.Contains(child))continue;
                if(child.name.StartsWith("Lotus garden")){int n=int.Parse(child.name.Split(' ').Last());child.position+=houses[n].position-oldPositions[n];}
                else child.position+=new Vector3(-85,0,0);
            }
            foreach(var anchor in markers.Cast<Transform>())anchor.position+=new Vector3(-85,0,0);
            ReferenceTerrain();
            var roads=Child("Earth roads linking homes between paddies",env).transform;
            foreach(float z in new[]{-68f,-36f,-4f,28f,60f}){Lane(roads,"West field lane",new Vector3(-106,1.04f,z),new Vector3(Canal(z)-9,1.04f,z),2.5f);Lane(roads,"East field lane",new Vector3(Canal(z)+9,1.04f,z),new Vector3(108,1.04f,z),2.5f);}
            for(int z=-96;z<96;z+=3)foreach(float side in new[]{-1f,1f})Lane(roads,"Canal bank road",new Vector3(Canal(z)+side*10,1.04f,z),new Vector3(Canal(z+3)+side*10,1.04f,z+3),3);
            foreach(float x in new[]{-96f,-69f,-42f,17f,48f,80f,108f})Lane(roads,"Field access",new Vector3(x,1.04f,-70),new Vector3(x,1.04f,63),2);
            ReferenceFields(positions,roads);
            ReferenceBridge(-4);ReferenceBridge(-68);
            ReferenceDetails();
            SetAnchor("01_arrival",new Vector3(-104,1,-4));SetAnchor("07_ferry",new Vector3(Canal(62)-10,1,62));SetAnchor("10_safe_route",new Vector3(Canal(-82)-10,1,-82));SetAnchor("09_scout",new Vector3(-26,1,-4));
            foreach(var anchor in markers.Cast<Transform>())if(anchor.position.x < -108)anchor.position=new Vector3(-103,anchor.position.y,anchor.position.z);
            Physics.SyncTransforms();
            var surface=env.gameObject.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.2f;surface.BuildNavMesh();
            var navPath=Root+"/Map02_Reference_NavMesh.asset";var old=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);if(old!=null)AssetDatabase.DeleteAsset(navPath);AssetDatabase.CreateAsset(surface.navMeshData,navPath);
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();camera.transform.position=new Vector3(0,185,-220);camera.transform.LookAt(new Vector3(0,0,12));camera.orthographic=true;camera.orthographicSize=133;camera.farClipPlane=650;
            var checks=new List<string>{"14 homes interspersed with rice paddies; west bank denser", "Curved central canal, two bridges, northern landing zone, northeast and southeast defensive positions"};
            foreach(var target in new[]{new Vector3(32,1,-4),new Vector3(64,1,-68),new Vector3(-56,1,60)}){var path=new NavMeshPath();bool ok=NavMesh.SamplePosition(new Vector3(-100,1,-4),out var start,3,NavMesh.AllAreas)&&NavMesh.SamplePosition(target,out var end,3,NavMesh.AllAreas)&&NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;checks.Add((ok?"PASS":"FAIL")+" route to "+target);}
            File.WriteAllLines(Reports+"/reference-checks.txt",checks);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Capture(camera,new Vector3(0,185,-220),new Vector3(0,0,12),133,"map02-reference-overview");
            Capture(camera,new Vector3(-112,65,-86),new Vector3(-62,1,8),0,"map02-homes-in-fields");
            Selection.activeGameObject=env.gameObject;if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(Vector3.zero,Quaternion.Euler(48,0,0),155);
            if(checks.Any(c=>c.StartsWith("FAIL")))throw new Exception("Reference navigation check failed; see reference-checks.txt");
        }
        static void SetAnchor(string prefix,Vector3 p){foreach(var t in markers.Cast<Transform>())if(t.name.StartsWith(prefix))t.position=p;}
        static void ReferenceTerrain()
        {
            var v=new List<Vector3>();var colors=new List<Color>();var tris=new List<int>();
            for(int z=0;z<=200;z++)for(int x=0;x<=220;x++){v.Add(new Vector3(x-110,ReferenceHeight(x-110,z-100),z-100));colors.Add(Color.Lerp(new Color(.29f,.34f,.14f),new Color(.46f,.44f,.23f),Mathf.PerlinNoise(x*.06f,z*.06f)));if(x>0&&z>0){int i=z*221+x;tris.AddRange(new[]{i-222,i,i-221,i-222,i-1,i});}}
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetColors(colors);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh=SaveMesh(mesh,"Reference_Curved_Terrain");var ground=Model("Continuous ground with meandering canal",mesh,Vector3.zero,env,palette);ground.layer=LayerMask.NameToLayer("Obstacle");ground.AddComponent<MeshCollider>().sharedMesh=mesh;
            v.Clear();tris.Clear();for(int z=-100;z<=100;z++){v.Add(new Vector3(Canal(z)-5.6f,.045f,z));v.Add(new Vector3(Canal(z)+5.6f,.045f,z));if(z>-100){int i=(z+100)*2;tris.AddRange(new[]{i-2,i,i-1,i-1,i,i+1});}}
            mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();Model("Winding canal water",SaveMesh(mesh,"Reference_Canal_Water"),Vector3.zero,env,water);
        }
        static void ReferenceFields(Vector2[] homes,Transform roads)
        {
            var root=Child("Rice fields surrounding individual homes",env).transform;var parts=new List<CombineInstance>();var crop=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Rice_Clump.asset");int index=0;
            foreach(float x in new[]{-82f,-56f,32f,64f,96f})foreach(float z in new[]{-52f,-20f,12f,44f})
            {
                bool home=homes.Any(p=>Mathf.Abs(p.x-x)<1&&Mathf.Abs(p.y-z)<1);float width=x<0?23:27;
                var p=Child("Paddy "+index+++(home?" with homestead":""),root).transform;p.position=new Vector3(x,0,z);
                var tone=Mat("Reference rice "+index%3,index%3==0?new Color(.54f,.48f,.12f):index%3==1?new Color(.39f,.46f,.10f):new Color(.49f,.52f,.18f));
                Box("Paddy soil",p,new Vector3(0,1.025f,0),new Vector3(width,.03f,28),tone,false);
                foreach(float side in new[]{-1f,1f}){Box("Bund",p,new Vector3(side*width/2,1.10f,0),new Vector3(.6f,.18f,28),soil);Box("Bund",p,new Vector3(0,1.10f,side*14),new Vector3(width,.18f,.6f),soil);}
                if(home){Box("Dry homestead island",p,new Vector3(0,1.065f,1),new Vector3(13,.08f,20),soil,false);Lane(roads,"Home footpath",new Vector3(x,1.09f,z-5),new Vector3(x,1.09f,z-16),2.2f);}
                for(float xx=-width/2+1;xx<width/2-1;xx+=1.4f)for(float zz=-13;zz<13;zz+=1.4f){if(home&&Mathf.Abs(xx)<7&&zz>-10&&zz<12||home&&Mathf.Abs(xx)<1.4f&&zz<0)continue;parts.Add(new CombineInstance{mesh=crop,transform=Matrix4x4.TRS(new Vector3(x+xx,1.06f,z+zz),Quaternion.Euler(0,Rand(0,360),0),Vector3.one*Rand(.75f,1.2f))});}
            }
            var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(parts.ToArray());Model("Golden green rice rows",SaveMesh(m,"Reference_Rice_Rows"),Vector3.zero,root,palette).GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        static void ReferenceBridge(float z)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/Map01_Sept22/Prefabs/Bridge_New.prefab");var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,env);g.name=z>-10?"Central wooden village bridge":"Southern orchard footbridge";g.transform.position=new Vector3(Canal(z),1.15f,z);g.transform.localScale=new Vector3(.88f,1,1);ColliderBox(g.transform,"Walkable bridge deck",Vector3.zero,new Vector3(24,.18f,3.7f));PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
        }
        static void ReferenceDetails()
        {
            var root=Child("Orchards palms and reference landmarks",env).transform;
            for(int i=0;i<200;i++){float z=Rand(-96,96),x;if(i<100)x=Canal(z)+(i%2==0?-1:1)*Rand(13,17);else {x=Rand(-106,106);z=i%2==0?Rand(76,96):Rand(-97,-82);}if(Mathf.Abs(z+4)<6||Mathf.Abs(z+68)<6||z>73&&x<-30&&x>-75)continue;var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environment/Map01_Optimized/Prefabs/Tree_"+(i%5)+".prefab");var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);g.transform.position=new Vector3(x,1,z);g.transform.localScale=Vector3.one*Rand(.65f,1);PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);}
            var palm=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Environment/Map01_Realism/Meshes/Coconut_LOD1.asset");foreach(float x in new[]{-96f,-42f,17f,80f})foreach(float z in new[]{-59f,-27f,5f,37f,67f}){var g=Model("Coconut at field edge",palm,new Vector3(x+2,1,z),root,palette);ColliderBox(g.transform,"Palm trunk",new Vector3(.3f,3,0),new Vector3(.6f,6,.6f));}
            Box("Northern helicopter landing clearing",root,new Vector3(-52,1.025f,83),new Vector3(40,.04f,23),soil,false);Anchor("reference_landing","Helicopter landing area",new Vector3(-52,1,83));
            var sand=Mat("Reference sandbags",new Color(.43f,.40f,.28f));foreach(var p in new[]{new Vector3(83,1,82),new Vector3(81,1,-83)}){var fort=Child("Defensive sandbag position",root).transform;fort.position=p;for(int row=0;row<3;row++)for(int i=0;i<17;i++){float a=(35+i*17+row%2*8)*Mathf.Deg2Rad;var b=Box("Sandbag",fort,new Vector3(Mathf.Cos(a)*4,.22f+row*.38f,Mathf.Sin(a)*4),new Vector3(1.3f,.38f,.65f),sand);b.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);}Anchor("reference_defense","Reference defensive position",p);}
            foreach(float z in new[]{62f,-82f}){float x=Canal(z);Box("Timber evacuation landing",root,new Vector3(x-6,1.15f,z),new Vector3(9,.2f,3),wood);var boat=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Environment/Map01_RiverDetails/Boat_Floating.asset");Model("Canal boat",boat,new Vector3(x-1,.21f,z),root,palette);}
        }
    }
}

