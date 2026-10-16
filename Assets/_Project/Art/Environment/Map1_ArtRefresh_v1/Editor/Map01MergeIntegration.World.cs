using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using ShadowVale.Map01;
using Object=UnityEngine.Object;
namespace ShadowVale.EnvironmentReview
{
    public static partial class Map01MergeIntegration
    {
        static Transform[] All()=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        static Transform Camp(int n)=>All().Single(t=>t.name=="C"+n+" / Mission art");
        static void Detail()
        {
            var rows=new List<string>();
            foreach(var c in Enumerable.Range(1,3).Select(Camp)){
                rows.Add("CAMP "+T(c));
                foreach(var t in c.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("ENTRY")||t.name.Contains("radio")||t.name.Contains("Radio")||t.name.Contains("Desk")||t.name.Contains("Floor")||t.name.StartsWith("HQ")||t.name.Contains("command house")||t.name.StartsWith("Open wooden door")))
                    rows.Add("NODE "+T(t)+" local="+V(t.localPosition)+" rot="+V(t.localEulerAngles)+" scale="+V(t.localScale));
                rows.Add("MODEL "+c.GetChild(0).name+" local="+V(c.GetChild(0).localPosition)+" rot="+V(c.GetChild(0).localEulerAngles)+" scale="+V(c.GetChild(0).localScale));
            }
            var l=Object.FindFirstObjectByType<Map01OperationsLayout>();
            foreach(var t in l.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==l.transform||t.name.Contains("Door")||t.name.Contains("Lid")||t.name.Contains("SupplyBag")))rows.Add("OP "+T(t));
            foreach(var code in new[]{"C1","C2"}){
                var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Meshes/"+code+"_ApprovedArt.fbx"));
                var original=Camp(code=="C1"?1:2).GetChild(0);source.transform.SetParent(original.parent,false);source.transform.SetLocalPositionAndRotation(original.localPosition,original.localRotation);source.transform.localScale=original.localScale;
                foreach(var t in source.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Open wooden door")||t.name.Contains("radio")||t.name.Contains("Radio")||t.name.Contains("Desk")))
                {rows.Add("SOURCE "+T(t)+" local="+V(t.localPosition)+" rot="+V(t.localEulerAngles));foreach(var r in t.GetComponentsInChildren<MeshRenderer>())rows.Add("MESH "+r.name+" bounds="+V(r.bounds.center)+" size="+V(r.bounds.size)+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m.name)));}
                Object.DestroyImmediate(source);
            }
            File.WriteAllLines(Work+"/world_detail.txt",rows);
        }
        static void Adapt()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit mode required");
            if(File.Exists(Stage))File.Copy(Stage,Work+"/stage_before_retry_"+DateTime.UtcNow.Ticks+".unity");
            EditorSceneManager.OpenScene(Map);Directory.CreateDirectory(Path.GetDirectoryName(Stage));
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),Stage,true);EditorSceneManager.OpenScene(Stage);
            var l=Object.FindFirstObjectByType<Map01OperationsLayout>();
            AdaptSupply(l);AdaptRadio(l);AdaptGarrison(l);
            try{BakeAndValidate(l);}finally{EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();}
        }
        static Transform Marker(Transform parent,string name,Vector3 at,Vector3 forward)
        {var t=new GameObject(name).transform;t.SetParent(parent);t.SetPositionAndRotation(at,Quaternion.LookRotation(forward,Vector3.up));return t;}
        static GameObject Source(int n)
        {
            var c=Camp(n);var original=c.GetChild(0);var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Meshes/C"+n+"_ApprovedArt.fbx"));
            source.transform.SetParent(c,false);source.transform.SetLocalPositionAndRotation(original.localPosition,original.localRotation);source.transform.localScale=original.localScale;
            foreach(var r in source.GetComponentsInChildren<MeshRenderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+m.name+".mat")??m).ToArray();
            return source;
        }
        // Remove only exact source triangles from copies of the derived meshes; shared art assets remain intact.
        static void ExtractFromBatches(Transform camp,IEnumerable<MeshRenderer> extracted,string label)
        {
            var triangles=new Dictionary<Vector3Int,List<Vector3[]>>();int expected=0;
            Vector3Int Cell(Vector3 p)=>Vector3Int.FloorToInt(p/.02f);
            foreach(var r in extracted){var mesh=r.GetComponent<MeshFilter>().sharedMesh;var matrix=camp.worldToLocalMatrix*r.transform.localToWorldMatrix;var vs=mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();var ts=mesh.triangles;expected+=ts.Length/3;for(int i=0;i<ts.Length;i+=3){var tri=new[]{vs[ts[i]],vs[ts[i+1]],vs[ts[i+2]]};var key=Cell((tri[0]+tri[1]+tri[2])/3);if(!triangles.TryGetValue(key,out var list))triangles[key]=list=new List<Vector3[]>();list.Add(tri);}}
            bool Match(Vector3 a,Vector3 b,Vector3 d){var cell=Cell((a+b+d)/3);var tri=new[]{a,b,d};for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(triangles.TryGetValue(cell+new Vector3Int(x,y,z),out var list))foreach(var other in list)if(tri.All(v=>other.Any(w=>(v-w).sqrMagnitude<.000009f))&&other.All(v=>tri.Any(w=>(v-w).sqrMagnitude<.000009f)))return true;return false;}
            int nearRemoved=0,farRemoved=0;Directory.CreateDirectory(Root+"/MergeReview/Meshes");
            foreach(var mf in camp.GetComponentsInChildren<MeshFilter>().Where(m=>m.transform.parent!=null&&m.transform.parent.name.Contains("static material batches")).ToArray()){
                var mesh=mf.sharedMesh;var matrix=camp.worldToLocalMatrix*mf.transform.localToWorldMatrix;var vs=mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();var ts=mesh.triangles;var keep=new List<int>();int removed=0;
                for(int i=0;i<ts.Length;i+=3){if(Match(vs[ts[i]],vs[ts[i+1]],vs[ts[i+2]])){removed++;continue;}keep.Add(ts[i]);keep.Add(ts[i+1]);keep.Add(ts[i+2]);}
                if(removed==0)continue;
                var copy=Object.Instantiate(mesh);copy.name=mesh.name+"_"+label;copy.SetTriangles(keep,0);copy.RecalculateBounds();var path=AssetDatabase.GenerateUniqueAssetPath(Root+"/MergeReview/Meshes/"+copy.name+".asset");AssetDatabase.CreateAsset(copy,path);mf.sharedMesh=copy;
                var collider=mf.GetComponent<MeshCollider>();if(collider!=null)collider.sharedMesh=copy;
                if(mf.transform.parent.name.StartsWith("LOD0"))nearRemoved+=removed;else farRemoved+=removed;
            }
            File.AppendAllText(Work+"/geometry_adaptation.txt",label+" expected="+expected+" near removed="+nearRemoved+" far removed="+farRemoved+"\n");
            if(nearRemoved!=expected)throw new Exception(label+" source triangles differ from static batches: "+nearRemoved+" / "+expected);
        }
        static Transform RetainVisual(Transform source,Transform parent,string name)
        {
            var pivot=Marker(parent,name,source.position,Vector3.forward);source.SetParent(pivot,true);
            foreach(var c in source.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
            return pivot;
        }
        static void AdaptSupply(Map01OperationsLayout l)
        {
            var c=Camp(1);var source=Source(1);var doors=source.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Open wooden door")).OrderBy(t=>t.name).ToArray();
            if(doors.Length!=2)throw new Exception("Expected double warehouse doors");
            ExtractFromBatches(c,doors.SelectMany(t=>t.GetComponentsInChildren<MeshRenderer>()),"DynamicDoors");
            var props=l.supplyProps;var root=props.transform;var front=c.forward;var floor=c.GetComponentsInChildren<BoxCollider>().First(b=>b.name=="COLLIDER_Floor");
            var center=floor.bounds.center;center.y=floor.bounds.max.y;var doorCenter=(doors[0].position+doors[1].position)/2;
            root.SetPositionAndRotation(center,Quaternion.LookRotation(front));
            var oldDoorLock=props.doorLock;
            var first=RetainVisual(doors[0],root,"C1 left leaf hinge");var second=RetainVisual(doors[1],root,"C1 right leaf hinge");
            first.Rotate(Vector3.up,90,Space.World);second.Rotate(Vector3.up,-90,Space.World);
            props.doorHinge=first;props.secondDoorHinge=second;props.doorOpenAngle=-90;props.secondDoorOpenAngle=90;
            foreach(var col in c.GetComponentsInChildren<Collider>().Where(b=>b.name.StartsWith("COLLIDER_OpenDoor")))col.enabled=false;
            // Both open leaves rest against the side walls; the mission owns the doorway blocker.
            var oldStore=root.Cast<Transform>().Single(t=>t.name=="Warehouse");oldDoorLock.SetParent(root,true);oldStore.gameObject.SetActive(false);
            foreach(var obstacle in root.GetComponentsInChildren<NavMeshObstacle>(true))obstacle.enabled=false;
            foreach(var t in root.Cast<Transform>().Where(t=>t.name.EndsWith("wall navigation",StringComparison.Ordinal)||t.name=="Front wall navigation").ToArray())t.gameObject.SetActive(false);
            l.supply.SetLocalPositionAndRotation(new Vector3(0,0,-.20f),Quaternion.identity);props.bag.SetPositionAndRotation(l.supply.position+Vector3.up*.12f,root.rotation);
            var blocker=props.doorCollider as BoxCollider;blocker.transform.SetPositionAndRotation(doorCenter+Vector3.up*1.015f,root.rotation);blocker.center=Vector3.zero;blocker.size=new Vector3(2,2.03f,.10f);blocker.enabled=true;
            props.doorObstacle.transform.SetPositionAndRotation(blocker.transform.position,root.rotation);props.doorObstacle.center=Vector3.zero;props.doorObstacle.size=blocker.size;props.doorObstacle.carving=true;props.doorObstacle.enabled=true;
            oldDoorLock.SetPositionAndRotation(doorCenter+front*.085f+Vector3.up,root.rotation);oldDoorLock.gameObject.SetActive(true);
            l.warehouseDoor=first;l.doorApproach.SetPositionAndRotation(doorCenter+front*.95f,Quaternion.LookRotation(-front));
            l.chestApproach.SetPositionAndRotation(l.supply.position+front*1.05f,Quaternion.LookRotation(-front));l.supplyApproach=l.chestApproach;props.formatVersion=4;
            Object.DestroyImmediate(source);
        }
        static void AdaptRadio(Map01OperationsLayout l)
        {
            var c=Camp(2);var source=Source(2);var nodes=source.GetComponentsInChildren<Transform>();var hero=nodes.Single(t=>t.name=="Hero analog radio / fictional design");var table=hero.parent;var front=c.forward;
            var chair=nodes.Where(t=>t.name.StartsWith("Chair")&&t.parent==table.parent).OrderBy(t=>Vector3.Distance(t.position,table.position+front*.8f)).First();
            ExtractFromBatches(c,chair.GetComponentsInChildren<MeshRenderer>(),"RadioChairMoved");
            var art=Camp(2).GetChild(0);var originalChair=art.GetComponentsInChildren<Transform>().Single(t=>t.name==chair.name&&t.parent.name==chair.parent.name);
            foreach(var col in originalChair.GetComponentsInChildren<Collider>())col.enabled=false;
            var chairPivot=RetainVisual(chair,c,"C2 chair set aside for standing operator");chairPivot.position+=c.right*1.0f;
            l.radio.gameObject.SetActive(false);l.radioWorkbench.gameObject.SetActive(false);
            foreach(var t in l.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Radio furniture navigation")))t.gameObject.SetActive(false);
            var floor=c.GetComponentsInChildren<BoxCollider>().First(b=>b.name.StartsWith("COLLIDER_Floor"));float height=floor.bounds.max.y;
            var device=Marker(l.transform,"C2 active radio on approved art desk",hero.position,-front);l.radio=device;
            var stand=table.position+front*.88f;stand.y=height;l.radioApproach.SetPositionAndRotation(stand,Quaternion.LookRotation(-front));
            l.radioRightContact.position=hero.position+front*.25f+c.right*.10f+Vector3.up*.22f;
            l.radioLeftContact.position=hero.position+front*.27f-c.right*.20f+Vector3.up*.12f;
            foreach(var light in new[]{l.radioPowered,l.radioDisabled})if(light!=null)light.transform.position=hero.position+front*.265f+c.right*.23f+Vector3.up*.31f;
            l.radioWorkbench=table; // Replaced below with the retained, non-rendering source table marker.
            l.radioWorkbench=art.GetComponentsInChildren<Transform>().Single(t=>t.name==table.name&&t.parent.name==table.parent.name);l.radioWorkspacePrepared=true;
            Object.DestroyImmediate(source);
        }
        static void AdaptGarrison(Map01OperationsLayout l)
        {
            l.commanderPost.SetPositionAndRotation(new Vector3(68,8.59f,62),Quaternion.LookRotation(Vector3.back));
            var sets=new[]{l.supplyGuards,l.radioGuards,l.commanderGuards};
            var positions=new[]{new[]{new Vector3(34,3.5f,-75),new Vector3(36,3.5f,-83),new Vector3(43,3.5f,-75),new Vector3(44,3.5f,-82)},new[]{new Vector3(60,6.3f,-18),new Vector3(69,6.3f,-16),new Vector3(61,6.3f,-8),new Vector3(68,6.3f,-8),new Vector3(71,6.3f,-20)},new[]{new Vector3(66,8.4f,45),new Vector3(73,8.4f,53),new Vector3(82,8.4f,61),new Vector3(70.5f,8.59f,62),new Vector3(54,8.4f,52)}};
            for(int i=0;i<3;i++){
                if(sets[i].Length!=positions[i].Length)throw new Exception("Approved guard roster mismatch C"+(i+1));
                for(int j=0;j<sets[i].Length;j++){
                    var guard=sets[i][j];guard.GetComponent<NavMeshAgent>().enabled=false;guard.transform.position=positions[i][j];guard.transform.rotation=Quaternion.LookRotation(j%2==0?Camp(i+1).forward:-Camp(i+1).forward);
                    // Preserve dev's post-versus-patrol roles; local routes are authored after baking.
                    if(j==0)guard.Configure(Array.Empty<Vector3>());
                }
            }
            // Keep the approved roster and key keeper; no changes to damage, sight or puzzle difficulty.
        }
        static void BakeAndValidate(Map01OperationsLayout l)
        {
            var surface=Object.FindFirstObjectByType<NavMeshSurface>();var agents=Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            foreach(var a in agents)a.enabled=false;
            var blocker=l.supplyProps.doorCollider;var obstacle=l.supplyProps.doorObstacle;blocker.enabled=false;obstacle.enabled=false;
            surface.RemoveData();NavMesh.RemoveAllNavMeshData();surface.navMeshData=null;surface.collectObjects=CollectObjects.All;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.08f;surface.minRegionArea=.2f;
            Physics.SyncTransforms();surface.BuildNavMesh();var navPath=AssetDatabase.GenerateUniqueAssetPath(Root+"/MergeReview/IntegratedNavMesh.asset");AssetDatabase.CreateAsset(surface.navMeshData,navPath);
            foreach(var link in Object.FindObjectsByType<NavMeshLink>(FindObjectsSortMode.None))link.UpdateLink();
            var rows=new List<string>();int failures=0;
            Vector3 Snap(string label,Vector3 p,float range=2.5f){if(!NavMesh.SamplePosition(p,out var hit,range,NavMesh.AllAreas)){failures++;rows.Add("FAIL no navmesh "+label+" "+V(p));return p;}rows.Add("POINT "+label+" "+V(hit.position)+" delta="+Vector3.Distance(p,hit.position));return hit.position;}
            foreach(var a in agents){a.transform.position=Snap(a.name,a.transform.position);}
            foreach(var roster in new[]{l.supplyGuards,l.radioGuards,l.commanderGuards})for(int i=1;i<roster.Length;i++){
                var guard=roster[i];var points=new List<Vector3>{guard.transform.position};
                for(int angle=0;angle<360&&points.Count<3;angle+=30){var desired=guard.transform.position+Quaternion.Euler(0,angle,0)*Vector3.forward*3;var path=new NavMeshPath();if(!NavMesh.SamplePosition(desired,out var hit,.6f,NavMesh.AllAreas)||Vector3.Distance(hit.position,guard.transform.position)<1.5f)continue;
                    if(!NavMesh.CalculatePath(guard.transform.position,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                    float length=0;for(int k=1;k<path.corners.Length;k++)length+=Vector3.Distance(path.corners[k-1],path.corners[k]);if(length>7||BodyCollisions(hit.position).Length>0)continue;points.Add(hit.position);}
                guard.Configure(points.ToArray());if(points.Count<3){failures++;rows.Add("FAIL patrol needs three reachable points "+guard.name);}else rows.Add("PASS patrol "+guard.name+" "+string.Join(" -> ",points.Select(V)));
            }
            foreach(var marker in new[]{l.doorApproach,l.chestApproach,l.radioApproach,l.commanderPost,l.handoff})marker.position=Snap(marker.name,marker.position,.45f);
            var startup=Object.FindFirstObjectByType<Map01ArtNavigationReady>();startup.surface=surface;startup.initiallyEnabled=agents;EditorUtility.SetDirty(startup);
            var mission=Object.FindFirstObjectByType<Map01Mission>();var player=mission.player;
            if(player==null)player=Object.FindFirstObjectByType<ShadowVale.Gameplay.Player.PlayerController>()?.transform;
            if(player==null)throw new Exception("No authored player start");var start=player.position;
            void Route(string label,Vector3 from,Vector3 to){var path=new NavMeshPath();var a=Snap(label+" from",from,4);var b=Snap(label+" to",to,.5f);bool good=NavMesh.CalculatePath(a,b,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;rows.Add((good?"PASS ":"FAIL ")+label+" "+path.status);if(!good)failures++;}
            foreach(var marker in new[]{l.doorApproach,l.chestApproach,l.radioApproach,l.commanderPost,l.handoff})Route("Player -> "+marker.name,start,marker.position);
            foreach(var a in agents)Route("Guard -> objective "+a.name,a.transform.position,a.GetComponent<Map01EnemyController>()?.operationIndex==1?l.doorApproach.position:a.GetComponent<Map01EnemyController>()?.operationIndex==2?l.radioApproach.position:l.commanderPost.position);
            foreach(var marker in l.responseSites){marker.position=Snap(marker.name,marker.position,3);Route("Response -> radio "+marker.name,marker.position,l.radioApproach.position);}
            foreach(var marker in l.observationSites){marker.position=Snap(marker.name,marker.position,3);rows.Add("OBS "+marker.name+" commander distance="+Vector3.Distance(marker.position,l.commanderPost.position));}
            var all=All();var aBase=all.Single(t=>t.name=="A_BaseReport");var captive=all.Single(t=>t.name=="B_HungCaptive");Route("C3 -> rescue jetty",l.commanderPost.position,captive.position);Route("Escort B -> A",captive.position,aBase.position);
            blocker.enabled=true;obstacle.enabled=true;rows.Insert(0,"FAILURES "+failures+"; authored agents "+agents.Length+"; surface "+navPath);File.WriteAllLines(Work+"/integrated_navigation.txt",rows);
            if(failures>0)throw new Exception("Navigation review has "+failures+" failures; stage retained for diagnosis");
        }
        static void Publish()
        {
            if(!File.ReadAllLines(Work+"/integrated_navigation.txt")[0].StartsWith("FAILURES 0;"))throw new Exception("Resolve navigation failures before publication");
            if(SceneManager.GetActiveScene().path!=Stage)EditorSceneManager.OpenScene(Stage);
            if(!File.Exists(Work+"/Map1.before_world_adaptation.unity"))File.Copy(Map,Work+"/Map1.before_world_adaptation.unity");
            var scene=SceneManager.GetActiveScene();EditorSceneManager.SaveScene(scene,Map,true);EditorSceneManager.SaveScene(scene,Root+"/Map1_ArtStage.unity",true);EditorSceneManager.OpenScene(Map);
            File.WriteAllText(Work+"/world_published.txt",DateTime.UtcNow.ToString("O")+" integrated stage copied to gameplay Map 1 and art preview; gameplay tests pending");
        }
        static void ReviewRoster()
        {
            EditorSceneManager.OpenScene(Stage);var l=Object.FindFirstObjectByType<Map01OperationsLayout>();var roster=l.supplyGuards.Concat(l.radioGuards).Concat(l.commanderGuards).ToArray();var notes=new List<string>();
            foreach(var name in new[]{"rbl_east_2","rbl_east_3"}){
                var legacy=All().Single(t=>t.name==name&&t.GetComponent<Map01EnemyController>()!=null);
                if(roster.Contains(legacy.GetComponent<Map01EnemyController>()))throw new Exception("Legacy guard belongs to approved roster: "+name);
                notes.Add("Disabled overlapping legacy camp actor "+PathOf(legacy)+"; approved operation roster retained");legacy.gameObject.SetActive(false);
            }
            l.commanderGuards[3].transform.position=new Vector3(68,8.28f,58.8f);
            l.radioGuards[1].transform.position=new Vector3(72,6.1f,-14);
            // Retain the authored FBX axis when moving the padlock to the new double door.
            var padlock=l.supplyProps.doorLock;padlock.rotation=l.supplyProps.transform.rotation*Quaternion.Euler(-90,0,0);
            try{BakeAndValidate(l);}finally{EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();}
            Physics.SyncTransforms();int failed=0;
            foreach(var t in new[]{l.doorApproach,l.chestApproach,l.radioApproach,l.commanderPost,l.handoff}.Concat(roster.Select(g=>g.transform))){
                var p=t.position;var collisions=BodyCollisions(p);
                notes.Add((collisions.Length==0?"PASS ":"FAIL ")+t.name+" body clearance "+string.Join("; ",collisions.Select(c=>PathOf(c.transform))));if(collisions.Length>0)failed++;
            }
            notes.Insert(0,"clearance failures="+failed+"; approved operation guards="+roster.Length+"; active scene agents="+Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None).Length);File.WriteAllLines(Work+"/world_clearance.txt",notes);
            if(failed>0)throw new Exception("Actor/interaction clearance requires correction: "+failed);
            Publish();
        }
        static Collider[] BodyCollisions(Vector3 p)=>Physics.OverlapCapsule(p+Vector3.up*.36f,p+Vector3.up*1.45f,.27f,~0,QueryTriggerInteraction.Ignore).Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&c.GetComponentInParent<Map01EnemyController>()==null&&c.GetComponentInParent<ShadowVale.Gameplay.Player.PlayerController>()==null&&!(c is TerrainCollider)&&!(c is CharacterController)&&c.bounds.max.y>p.y+.2f).ToArray();
    }
}
