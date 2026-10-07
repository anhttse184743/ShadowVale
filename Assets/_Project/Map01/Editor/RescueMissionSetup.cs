using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01.Editor
{
    public static class RescueMissionSetup
    {
        [MenuItem("ShadowVale/Rescue/Bake character ground contacts")]
        public static void BakeSkinContacts()
        {
            var result=ScriptableObject.CreateInstance<Map01SkinContactData>();
            var records=new System.Collections.Generic.List<Map01SkinContactData.Skin>();
            foreach(var path in new[]{"Assets/_Project/Art/Characters/Player/Player.fbx","Assets/_Project/Art/Characters/NPCs/hung.fbx"})
                foreach(var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>()){
                    // Editor mesh access is permitted outside Play even when runtime Read/Write is off.
                    var source=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes;
                    if(weights.Length!=source.Length||bind.Length==0)continue;
                    var samples=new Map01SkinContactData.Vertex[source.Length];
                    for(int i=0;i<samples.Length;i++){
                        var w=weights[i];var p=source[i];
                        samples[i]=new Map01SkinContactData.Vertex{a=w.boneIndex0,b=w.boneIndex1,c=w.boneIndex2,d=w.boneIndex3,
                            wa=w.weight0,wb=w.weight1,wc=w.weight2,wd=w.weight3,
                            pa=bind[w.boneIndex0].MultiplyPoint3x4(p),pb=bind[w.boneIndex1].MultiplyPoint3x4(p),
                            pc=bind[w.boneIndex2].MultiplyPoint3x4(p),pd=bind[w.boneIndex3].MultiplyPoint3x4(p)};
                    }
                    records.Add(new Map01SkinContactData.Skin{mesh=mesh,vertices=samples});
                }
            result.skins=records.ToArray();if(records.Count<2)throw new Exception("Character skin references missing.");
            const string target="Assets/Resources/Rescue/SkinContactData.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Map01SkinContactData>(target);
            if(existing==null)AssetDatabase.CreateAsset(result,target);
            else{EditorUtility.CopySerialized(result,existing);UnityEngine.Object.DestroyImmediate(result);EditorUtility.SetDirty(existing);}
            AssetDatabase.SaveAssets();Debug.Log("Rescue ground contacts cached for "+records.Count+" original meshes; models/import settings unchanged.");
        }
        [MenuItem("ShadowVale/Rescue/Prepare stealth rescue markers")]
        public static void Prepare()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            var mission=UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
            var root=new GameObject("Map 1 stealth rescue markers");
            var layout=root.AddComponent<Map01RescueLayout>();
            Transform Marker(string name,Vector3 position,Transform parent=null,float yaw=0)
            {
                var t=new GameObject(name).transform;t.SetParent(parent!=null?parent:root.transform);
                t.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));return t;
            }
            Vector3 Land(Vector3 near,float radius=3)
            {
                if(!NavMesh.SamplePosition(near,out var hit,radius,NavMesh.AllAreas))throw new Exception("Marker outside navigation: "+near);
                return hit.position;
            }
            var captive=mission.hung.position;
            var centers=new[]{new Vector3(-4.4f,1.47f,84.6f),new Vector3(-27,3,87),new Vector3(-19,3,65),new Vector3(-23,3,96)};
            layout.guardPosts=new Transform[4];layout.patrolRoutes=new Transform[4];
            for(int i=0;i<4;i++){
                var center=Land(centers[i]);layout.guardPosts[i]=Marker(Map01Rescue.SquadNames[i]+" post",center,null,i==0?225:270);
                var route=Marker("Patrol route "+i,Vector3.zero);layout.patrolRoutes[i]=route;
                if(i==0)continue;
                foreach(var offset in new[]{new Vector3(-2,0,-2),new Vector3(-2,0,2),new Vector3(2,0,2),new Vector3(2,0,-2)})
                    Marker("Waypoint "+route.childCount,Land(center+offset,2),route);
                var loop=route.Cast<Transform>().ToArray();
                for(int j=0;j<loop.Length;j++)if(Map01Rescue.Path(loop[j].position,loop[(j+1)%loop.Length].position).Length<2)throw new Exception("Disconnected patrol "+i);
            }
            layout.safeEntry=Marker("Safe area before shelter",Land(new Vector3(-63.54f,3.36f,-78.12f),1));
            layout.shelterDoor=Marker("Shelter cinematic trigger",Land(new Vector3(-64.44f,2.64f,-77.4f),1));
            layout.reportPoint=Marker("Nam delivery inside shelter",Land(new Vector3(-61,.25f,-63),1));
            layout.shelterRun=new[]{
                Marker("Run 0 ramp top",layout.shelterDoor.position),
                Marker("Run 1 doorway",Land(new Vector3(-64.44f,.21f,-69.66f),1)),
                Marker("Run 2 inside",Land(new Vector3(-64.08f,.21f,-68.58f),1)),
                Marker("Run 3 report",layout.reportPoint.position)};
            var approaches=Marker("Three approach directions",Vector3.zero);
            foreach(var near in new[]{new Vector3(-39,4,77),new Vector3(-39,4,87),new Vector3(-39,4,97)}){
                var p=Land(near,3);if(Map01Rescue.Path(p,captive).Length<2)throw new Exception("Approach disconnected "+p);
                Marker("Approach "+approaches.childCount,p,approaches);
            }
            var sites=new System.Collections.Generic.List<Transform>();
            var path=Map01Rescue.Path(captive,layout.shelterDoor.position);
            if(path.Length<2)throw new Exception("No escort route.");
            var dense=new System.Collections.Generic.List<Vector3>();
            for(int i=1;i<path.Length;i++){
                int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(path[i-1],path[i])/12));
                for(int j=0;j<count;j++)dense.Add(Vector3.Lerp(path[i-1],path[i],j/(float)count));
            }
            foreach(var origin in dense){
                if(Vector3.Distance(origin,layout.safeEntry.position)<30||Vector3.Distance(origin,captive)<20)continue;
                foreach(float x in new[]{-28f,-22f,22f,28f}){
                    if(!NavMesh.SamplePosition(origin+Vector3.right*x,out var hit,4,NavMesh.AllAreas)||hit.position.y<.5f)continue;
                    if(Map01Rescue.Path(hit.position,origin).Length<2)continue;
                    if(sites.Any(t=>Vector3.Distance(t.position,hit.position)<7))continue;
                    sites.Add(Marker("Pursuit bank "+sites.Count,hit.position));
                }
            }
            if(sites.Count<8)throw new Exception("Not enough land pursuit sites.");layout.pursuitSites=sites.ToArray();
            const string folder="Assets/Resources/Rescue";
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            AnimationClip Reuse(string path,string name)
            {
                var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
                string target=folder+"/"+name+".anim";
                var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(target);
                if(saved==null){saved=UnityEngine.Object.Instantiate(clip);saved.name=name;AssetDatabase.CreateAsset(saved,target);}
                // Existing editable copies may contain the designer's hand adjustments.
                return saved;
            }
            layout.namRun=Reuse("Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_Run.fbx","Nam_Shelter_Run");
            layout.namIdle=Reuse("Assets/_Project/Art/Characters/Animations/Nam/Nam_Rifle_Idle.fbx","Nam_Shelter_Idle");
            layout.hungRun=Reuse("Assets/_Project/Art/Characters/Animations/Generated/Run_Tuned.anim","Hung_Shelter_Run");
            layout.hungIdle=Reuse("Assets/_Project/Art/Characters/Animations/Generated/Idle_Tuned.anim","Hung_Shelter_Idle");
            layout.gunshot=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/Weapons/gun.mp3");
            PrefabUtility.SaveAsPrefabAsset(root,folder+"/Map01RescueLayout.prefab");
            UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/Rescue");
            File.WriteAllText("Logs/Rescue/layout-validation.txt","Four guards; three closed patrol loops and approach routes connected.\n"+sites.Count+" pursuit sites validated. Shelter entrance connected.\nNo scene, terrain or navigation assets modified.");
            Debug.Log("Rescue markers prepared: "+sites.Count+" pursuit sites. Original scenes unchanged.");
        }
        public static void Survey()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            var mission = UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
            var shelter = GameObject.Find("A — underground friendly shelter");
            var output = new System.Text.StringBuilder();
            output.AppendLine("HUNG " + mission.hung.position);
            output.AppendLine("PLAYER " + GameObject.Find("A_PlayerStart").transform.position);
            foreach (var t in shelter.GetComponentsInChildren<Transform>())
                if (t.GetComponent<Collider>() != null || t.name.Contains("Step") || t.name.Contains("Entrance"))
                    output.AppendLine(t.name + " " + t.position + " " + t.localScale);
            var from = mission.hung.position;
            for (int x = -36; x <= 0; x += 6)
                for (int z = -30; z <= 30; z += 10)
                {
                    var p = from + new Vector3(x, 0, z);
                    if (NavMesh.SamplePosition(p, out var hit, 4, NavMesh.AllAreas))
                        output.AppendLine("LAND " + x + "," + z + " " + hit.position);
                }
            var path = new NavMeshPath();
            var report = GameObject.Find("A_BaseReport").transform.position;
            NavMesh.SamplePosition(from, out var start, 4, NavMesh.AllAreas);
            NavMesh.SamplePosition(report, out var end, 4, NavMesh.AllAreas);
            NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path);
            output.AppendLine("PATH " + path.status);
            foreach (var p in path.corners) output.AppendLine("CORNER " + p);
            Directory.CreateDirectory("Logs/Rescue");
            File.WriteAllText("Logs/Rescue/survey.txt", output.ToString());
            Debug.Log("Rescue terrain survey complete.");
        }
    }
}
