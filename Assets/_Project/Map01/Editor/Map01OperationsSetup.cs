using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static class Map01OperationsSetup
    {
        const string Art="Assets/_Project/Art/Environment/Map01Operations/";
        [MenuItem("ShadowVale/Story/Prepare three operation puzzles")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before preparing operations.");
            AssetDatabase.Refresh();Map01OperationAnimationSetup.Import();var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            var layout=Object.FindFirstObjectByType<Map01OperationsLayout>();if(layout==null)throw new Exception("Existing operations layout missing.");
            Physics.SyncTransforms();
            if(layout.supplyProps!=null&&layout.supplyProps.formatVersion<3){
                var replacement=new GameObject("Temporary supply placement reference");replacement.transform.position=layout.supply.position;
                Object.DestroyImmediate(layout.supplyProps.gameObject);layout.supply=replacement.transform;layout.supplyProps=null;BuildStore(layout);Object.DestroyImmediate(replacement);
            }
            if(layout.supplyProps==null)BuildStore(layout);
            layout.chestApproach.position=layout.supplyProps.transform.TransformPoint(new Vector3(0,0,.82f));
            if(Physics.Raycast(layout.chestApproach.position+Vector3.up*2,Vector3.down,out var floor,4,LayerMask.GetMask("Ground"),QueryTriggerInteraction.Ignore))layout.chestApproach.position=floor.point;
            foreach(var labelName in new[]{"Warehouse sign","Chest sign"}){
                var label=Find(layout.supplyProps.transform,labelName);if(label!=null)Object.DestroyImmediate(label.gameObject);
            }
            BuildGuards(layout);
            if(layout.handoff==null){var player=Object.FindFirstObjectByType<ShadowVale.Gameplay.Player.PlayerController>();layout.handoff=Marker(layout.transform,"Supply handoff — shelter",Walkable(player.transform.position,3),player.transform.forward);}
            if(layout.responseSites==null||layout.responseSites.Length<3)layout.responseSites=RingMarkers(layout.transform,"Radio response",layout.radioApproach.position,layout.radioApproach.position,24,36,12,false);
            if(layout.observationSites==null||layout.observationSites.Length<3||layout.observationSites.Any(t=>Vector3.Distance(t.position,layout.commanderPost.position)<25||!RenderedOfficerVisible(t.position,layout.commanderPost.position))){
                foreach(var site in layout.observationSites??Array.Empty<Transform>())if(site!=null)Object.DestroyImmediate(site.gameObject);
                PrepareObservation(layout);
            }
            layout.interactionRange=2.4f;
            Validate(layout);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/Map01Operations");
            File.WriteAllText("Logs/Map01Operations/layout.json",JsonUtility.ToJson(new LayoutReceipt{
                supply=layout.supply.position,door=layout.doorApproach.position,chest=layout.chestApproach.position,radio=layout.radioApproach.position,
                officer=layout.commanderPost.position,observations=layout.observationSites.Select(t=>t.position).ToArray(),responses=layout.responseSites.Select(t=>t.position).ToArray(),
                rosters=new[]{layout.supplyGuards.Length,layout.radioGuards.Length,layout.commanderGuards.Length},keyKeeper=layout.keyKeeper.SaveId
            },true));
            Debug.Log("[Map01Operations] Authored and validated warehouse, chest, 4/5/5 guards, response routes and three observation positions.");
            Capture("warehouse-front",layout.doorApproach.position-layout.doorApproach.forward*3+Vector3.up*1.8f,layout.supplyProps.transform.position+Vector3.up*1.1f);
            Capture("warehouse-side",layout.supplyProps.transform.position+layout.supplyProps.transform.right*5+layout.supplyProps.transform.forward*3+Vector3.up*3,layout.supplyProps.transform.position+Vector3.up);
            File.WriteAllText("Logs/Map01Operations/prop-transforms.txt",string.Join("\n",layout.supplyProps.GetComponentsInChildren<Transform>(true).Where(t=>new[]{"DoorHinge","LidPivot","SupplyChest","SupplyBag","ChestLock"}.Contains(t.name)).Select(t=>t.name+" local="+t.localPosition+" rotation="+t.localEulerAngles+" world="+t.position+" worldRotation="+t.eulerAngles)));
        }
        [Serializable]sealed class LayoutReceipt{public Vector3 supply,door,chest,radio,officer;public Vector3[] observations,responses;public int[] rosters;public string keyKeeper;}
        public static void Probe()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");var l=Object.FindFirstObjectByType<Map01OperationsLayout>();Physics.SyncTransforms();
            Directory.CreateDirectory("Logs/Map01Operations");var text=new System.Text.StringBuilder();
            text.AppendLine("Officer "+l.commanderPost.position+" Radio "+l.radioApproach.position);
            for(int i=0;i<36;i++){
                var point=l.commanderPost.position+Quaternion.Euler(0,i*10,0)*Vector3.forward*32;
                if(!NavMesh.SamplePosition(point,out var n,2,NavMesh.AllAreas)){text.AppendLine(i+" no navmesh");continue;}
                var path=new NavMeshPath();bool reachable=NavMesh.CalculatePath(l.radioApproach.position,n.position,NavMesh.AllAreas,path);
                bool blocked=Physics.Linecast(n.position+Vector3.up*1.6f,l.commanderPost.position+Vector3.up*1.3f,out var hit,LayerMask.GetMask("Default","Obstacle","Cover","VisionBlocker"),QueryTriggerInteraction.Ignore);
                text.AppendLine(i+" "+n.position+" path="+reachable+" "+path.status+" sight="+(blocked?hit.collider.name+" at "+hit.point:"clear"));
            }
            File.WriteAllText("Logs/Map01Operations/world-probe.txt",text.ToString());
            foreach(int i in new[]{1,3}){var c=GameObject.Find("Enemy outpost "+i).transform;Capture("camp-"+i,c.position+new Vector3(-12,12,-14),c.position+Vector3.up);}
        }
        public static void RadioProbe()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");var l=Object.FindFirstObjectByType<Map01OperationsLayout>();
            var renderers=l.radio.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            Directory.CreateDirectory("Logs/Map01Operations");File.WriteAllText("Logs/Map01Operations/radio-probe.txt","Radio "+l.radio.position+" bounds "+bounds+" approach "+l.radioApproach.position+" forward "+l.radioApproach.forward);
            Capture("radio-front",l.radioApproach.position+Vector3.up*1.7f-l.radioApproach.forward*1.2f+l.radioApproach.right*1.2f,bounds.center);
        }
        public static void FinalizeAndVerify()
        {
            Map01OperationAnimationSetup.Import();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            var l=Object.FindFirstObjectByType<Map01OperationsLayout>();var rescue=Resources.Load<Map01RescueLayout>("Rescue/Map01RescueLayout");
            var targets=new[]{l.doorApproach,l.radioApproach,l.commanderPost}.Concat(l.observationSites).Concat(l.responseSites);
            if(rescue!=null)targets=targets.Concat(new[]{rescue.shelterDoor,rescue.safeEntry}).Concat(rescue.guardPosts).Concat(rescue.pursuitSites).Concat(rescue.patrolRoutes.Where(t=>t!=null).SelectMany(t=>t.Cast<Transform>()));
            var rows=new System.Collections.Generic.List<string>();
            if(!NavMesh.SamplePosition(l.handoff.position,out var from,1,NavMesh.AllAreas))throw new Exception("Supply handoff is detached from navigation.");
            foreach(var target in targets.Where(t=>t!=null)){
                var route=new NavMeshPath();bool ok=NavMesh.SamplePosition(target.position,out var to,1,NavMesh.AllAreas)&&NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,route)&&route.status==NavMeshPathStatus.PathComplete;
                rows.Add(target.name+": "+ok);if(!ok)throw new Exception("Navigation regression at "+target.name);
            }
            Directory.CreateDirectory("Logs/Map01Operations");File.WriteAllLines("Logs/Map01Operations/navigation-regression.txt",rows);
        }
        public static void CalibrateRadioReach()
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            var layout=Object.FindFirstObjectByType<Map01OperationsLayout>();
            layout.radio.position=layout.radioWorkbench.position+Vector3.up*.75f-layout.radioWorkbench.forward*.14f;
            layout.radioRightContact.position=layout.radio.TransformPoint(new Vector3(-.12f,.99f,-.285f));
            layout.radioLeftContact.position=layout.radio.TransformPoint(new Vector3(-.36f,1.02f,-.27f));
            foreach(var light in new[]{layout.radioPowered,layout.radioDisabled})if(light!=null)light.transform.position=layout.radio.TransformPoint(new Vector3(.35f,1.18f,-.25f));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        public static void PrepareRadioWorkspace()
        {
            Map01OperationAnimationSetup.Import();
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");var l=Object.FindFirstObjectByType<Map01OperationsLayout>();var camp=GameObject.Find("Enemy outpost 2").transform;
            if(l.radioWorkbench==null){
                var bench=Model("RadioWorkbench",l.transform);bench.transform.SetPositionAndRotation(l.radio.position,Quaternion.identity);l.radioWorkbench=bench.transform;
                var collider=bench.AddComponent<BoxCollider>();collider.center=Vector3.up*.375f;collider.size=new Vector3(1.1f,.75f,.65f);
                Obstacle(bench.transform,"Radio workbench navigation",collider.center,collider.size);
            }
            l.radio.position=l.radioWorkbench.position+Vector3.up*.75f-l.radioWorkbench.forward*.14f;
            foreach(var light in new[]{l.radioPowered,l.radioDisabled})if(light!=null)light.transform.position=l.radio.TransformPoint(new Vector3(.35f,1.18f,-.25f));
            Transform Furniture(Transform node){for(var t=node;t!=null&&t!=camp;t=t.parent)if(new[]{"Table","Chair","Desk"}.Any(word=>t.name.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0))return t;return null;}
            var furniture=camp.GetComponentsInChildren<Renderer>().Select(r=>Furniture(r.transform)).Where(t=>t!=null).Distinct().ToArray();
            foreach(var old in l.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Radio furniture navigation —",StringComparison.Ordinal)).ToArray())Object.DestroyImmediate(old.gameObject);
            {
                foreach(var table in furniture.Where(t=>t.name.IndexOf("Table",StringComparison.OrdinalIgnoreCase)>=0)){
                    float bottom=table.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y);var position=table.position;position.x=l.radio.position.x-3.5f;position.z=l.radio.position.z-1.2f;
                    if(NavMesh.SamplePosition(position,out var ground,3,NavMesh.AllAreas))position.y+=Map01OperationSession.GroundPoint(ground.position,null).y-bottom;
                    table.position=position;
                }l.radioWorkspacePrepared=true;Physics.SyncTransforms();
            }
            var solids=furniture.Select(t=>{var renderers=t.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;}).ToArray();
            Directory.CreateDirectory("Logs/Map01Operations");File.WriteAllText("Logs/Map01Operations/radio-furniture.txt",string.Join("\n",furniture.Select((t,i)=>t.name+" "+solids[i])));
            for(int i=0;i<solids.Length;i++){
                var go=new GameObject("Radio furniture navigation — "+i);go.transform.SetParent(l.transform);go.transform.position=solids[i].center;
                go.AddComponent<BoxCollider>().size=solids[i].size;var obstacle=go.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=solids[i].size;obstacle.carving=true;
            }
            // Rebuild Map 1 navigation after moving furniture; never overwrite Map 2 data.
            var surface=Object.FindFirstObjectByType<NavMeshSurface>();surface.RemoveData();surface.BuildNavMesh();
            string navPath=Art+"OperationsNavigation.asset";var navigation=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if(navigation==null)AssetDatabase.CreateAsset(surface.navMeshData,navPath);else{EditorUtility.CopySerialized(surface.navMeshData,navigation);surface.navMeshData=navigation;EditorUtility.SetDirty(navigation);}
            surface.RemoveData();surface.AddData();EditorUtility.SetDirty(surface);
            Vector3 chosen=default;bool found=false;
            var offsets=new System.Collections.Generic.List<Vector3>{new Vector3(0,0,-.85f),new Vector3(-.85f,0,-.15f),new Vector3(.85f,0,-.15f),new Vector3(-1,0,-.55f),new Vector3(1,0,-.55f)};
            foreach(float radius in new[]{1f,1.25f,1.5f})for(int angle=0;angle<360;angle+=30)offsets.Add(Quaternion.Euler(0,angle,0)*Vector3.forward*radius);
            foreach(var offset in offsets){
                if(!NavMesh.SamplePosition(l.radioWorkbench.position+Vector3.up*.75f+offset,out var at,.7f,NavMesh.AllAreas))continue;
                if(solids.Any(b=>at.position.x>b.min.x-.33f&&at.position.x<b.max.x+.33f&&at.position.z>b.min.z-.33f&&at.position.z<b.max.z+.33f&&b.max.y>at.position.y+.1f))continue;
                chosen=at.position;found=true;break;
            }
            if(!found)throw new Exception("No clear standing position beside the radio cabinet.");
            l.radioApproach.position=chosen;var direction=Vector3.ProjectOnPlane(l.radio.position-chosen,Vector3.up);l.radioApproach.rotation=Quaternion.LookRotation(direction);
            l.radioRightContact=Find(l.transform,"Radio right fingertip contact")??Marker(l.transform,"Radio right fingertip contact",Vector3.zero,Vector3.forward);
            l.radioLeftContact=Find(l.transform,"Radio left palm support")??Marker(l.transform,"Radio left palm support",Vector3.zero,Vector3.forward);
            l.radioRightContact.position=l.radio.TransformPoint(new Vector3(-.12f,.99f,-.285f));l.radioLeftContact.position=l.radio.TransformPoint(new Vector3(-.36f,1.02f,-.27f));
            for(int i=0;i<l.radioGuards.Length;i++){
                var guard=l.radioGuards[i];
                if(solids.Any(b=>guard.transform.position.x>b.min.x-.35f&&guard.transform.position.x<b.max.x+.35f&&guard.transform.position.z>b.min.z-.35f&&guard.transform.position.z<b.max.z+.35f)){
                    var post=PatrolSpot(l.radio.position,i*67+25,4.5f+i%2);guard.transform.position=post;
                    guard.Configure(i==0?Array.Empty<Vector3>():new[]{post,PatrolSpot(post,i*67,3),PatrolSpot(post,i*67+80,3)});EditorUtility.SetDirty(guard);
                }
            }
            var rescue=Object.FindFirstObjectByType<Map01RescueLayout>();var targets=new[]{l.doorApproach,l.radioApproach,l.commanderPost}.Concat(l.observationSites).Concat(l.responseSites);
            if(rescue!=null)targets=targets.Concat(new[]{rescue.shelterDoor,rescue.safeEntry}).Concat(rescue.guardPosts).Concat(rescue.pursuitSites);
            var checks=new System.Collections.Generic.List<string>();
            if(!NavMesh.SamplePosition(l.handoff.position,out var start,1,NavMesh.AllAreas))throw new Exception("Shelter disconnected after navigation update.");
            foreach(var target in targets.Where(t=>t!=null)){
                var route=new NavMeshPath();bool connected=NavMesh.SamplePosition(target.position,out var end,1,NavMesh.AllAreas)&&NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,route)&&route.status==NavMeshPathStatus.PathComplete;
                checks.Add(target.name+": "+connected);if(!connected)throw new Exception("Navigation regression at "+target.name);
            }
            File.WriteAllLines("Logs/Map01Operations/navigation-regression.txt",checks);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Directory.CreateDirectory("Logs/Map01Operations");File.WriteAllText("Logs/Map01Operations/radio-workspace.txt","Stand "+chosen+"\n"+string.Join("\n",solids.Select(b=>b.ToString())));
        }
        public static void Capture(string name,Vector3 eye,Vector3 target)
        {
            var go=new GameObject("Operation validation camera");var camera=go.AddComponent<Camera>();camera.transform.position=eye;camera.transform.LookAt(target);camera.fieldOfView=50;
            var rt=RenderTexture.GetTemporary(1280,720,24);var active=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Directory.CreateDirectory("Logs/Map01Operations");File.WriteAllBytes("Logs/Map01Operations/"+name+".png",image.EncodeToPNG());}
            finally{RenderTexture.active=active;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(go);}
        }
        static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
        static GameObject Model(string name,Transform parent)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Art+name+".fbx");if(source==null)throw new Exception("Missing exported model "+name);
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(source,go.transform);
            visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.Euler(0,180,0)*visual.transform.localRotation;
            foreach(var r in go.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>Material(m!=null?m.name:"SV_Wood")).ToArray();
            return go;
        }
        static Material Material(string name)
        {
            string clean=name.Split(' ')[0];string path=Art+clean+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            Color color=clean.Contains("Iron")?new Color(.09f,.105f,.085f):clean.Contains("Brass")?new Color(.51f,.36f,.13f):clean.Contains("Red")?new Color(.48f,.09f,.055f):clean.Contains("Cloth")?new Color(.19f,.25f,.13f):clean.Contains("Strap")?new Color(.33f,.32f,.21f):clean.Contains("Edge")?new Color(.38f,.25f,.12f):new Color(.26f,.16f,.075f);
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;material.SetFloat("_Smoothness",clean.Contains("Brass")?.35f:.12f);AssetDatabase.CreateAsset(material,path);return material;
        }
        static void BuildStore(Map01OperationsLayout layout)
        {
            var old=layout.supply;var camp=GameObject.Find("Enemy outpost 1").transform;
            var at=StoreSite(old.position,camp.position);var front=Vector3.ProjectOnPlane(layout.supplyApproach.position-old.position,Vector3.up).normalized;if(front.sqrMagnitude<.1f)front=Vector3.back;
            var root=new GameObject("Point 1 — keyed warehouse and combination chest").transform;root.SetParent(layout.transform);root.SetPositionAndRotation(at,Quaternion.LookRotation(front));
            var store=Model("Warehouse",root);store.transform.localPosition=Vector3.zero;store.transform.localRotation=Quaternion.identity;
            var chest=Model("SupplyChest",root);chest.transform.localPosition=new Vector3(0,0,-.28f);chest.transform.localRotation=Quaternion.identity;
            var bag=Model("SupplyBag",root);bag.transform.localPosition=new Vector3(0,.12f,-.28f);
            var controller=root.gameObject.AddComponent<Map01SupplyProps>();controller.formatVersion=3;controller.doorHinge=Find(store.transform,"DoorHinge");controller.lid=Find(chest.transform,"LidPivot");controller.lockBody=Find(chest.transform,"ChestLock");controller.doorLock=Find(store.transform,"DoorLock");controller.bag=bag.transform;
            foreach(var mesh in store.GetComponentsInChildren<MeshFilter>()){
                if(mesh.name.StartsWith("DoorLeaf")||mesh.name.StartsWith("Wall")||mesh.name.StartsWith("Corner")||mesh.name.StartsWith("FrontPanel")||mesh.name.StartsWith("DoorFrame"))mesh.gameObject.AddComponent<BoxCollider>();
            }
            // One door collider is owned by the interaction; walls remain solid.
            var door=new GameObject("Door blocker");door.transform.SetParent(root,false);door.transform.localPosition=new Vector3(0,1.05f,1.14f);var blocker=door.AddComponent<BoxCollider>();blocker.size=new Vector3(1.02f,2.10f,.1f);controller.doorCollider=blocker;
            foreach(var mesh in store.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.StartsWith("DoorLeaf"))){var c=mesh.GetComponent<BoxCollider>();if(c!=null)Object.DestroyImmediate(c);}
            var box=chest.AddComponent<BoxCollider>();box.center=new Vector3(0,.3f,0);box.size=new Vector3(1.2f,.6f,.78f);
            Obstacle(chest.transform,"Chest navigation",box.center,box.size);
            controller.doorObstacle=Obstacle(root,"Door navigation",new Vector3(0,1.05f,1.14f),new Vector3(1.02f,2.1f,.12f));
            Obstacle(root,"Left wall navigation",new Vector3(-1.48f,1.12f,0),new Vector3(.15f,2.24f,2.3f));
            Obstacle(root,"Right wall navigation",new Vector3(1.48f,1.12f,0),new Vector3(.15f,2.24f,2.3f));
            Obstacle(root,"Back wall navigation",new Vector3(0,1.12f,-1.08f),new Vector3(3,2.24f,.15f));
            foreach(float x in new[]{-1.03f,1.03f})Obstacle(root,"Front wall navigation",new Vector3(x,1.1f,1.12f),new Vector3(.76f,2.2f,.15f));
            if(layout.supplyFull!=null)layout.supplyFull.SetActive(false);if(layout.supplyEmpty!=null)layout.supplyEmpty.SetActive(false);
            layout.supply=chest.transform;layout.supplyProps=controller;layout.warehouseDoor=controller.doorHinge;
            layout.doorApproach=Marker(layout.transform,"C1 Door — use guard key",Walkable(root.TransformPoint(new Vector3(0,0,1.9f)),1),-front);
            layout.chestApproach=Marker(layout.transform,"C1 Chest — combination puzzle",Walkable(root.TransformPoint(new Vector3(0,0,.60f)),.7f),-front);
            layout.supplyApproach=layout.chestApproach;
            var hints=Object.FindFirstObjectByType<Map01Mission>().GetComponent<Map01OperationHints>();if(hints==null)Object.FindFirstObjectByType<Map01Mission>().gameObject.AddComponent<Map01OperationHints>();
        }
        static Vector3 StoreSite(Vector3 preferred,Vector3 camp)
        {
            foreach(float radius in new[]{0f,4f,6f,8f})for(int angle=0;angle<360;angle+=30){
                Vector3 p=radius==0?preferred:camp+Quaternion.Euler(0,angle,0)*Vector3.forward*radius;
                if(!NavMesh.SamplePosition(p,out var hit,1,NavMesh.AllAreas))continue;
                p=hit.position;
                bool solid=Physics.OverlapBox(p+Vector3.up*1.2f,new Vector3(1.65f,1.05f,1.45f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
                    .Any(c=>!(c is TerrainCollider)&&c.bounds.max.y>p.y+.4f);
                if(!solid)return p;
            }throw new Exception("No clear warehouse footprint near point 1; adjust a placement marker before authoring.");
        }
        static void BuildGuards(Map01OperationsLayout layout)
        {
            var all=Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None).Where(e=>e.name.StartsWith("Outpost guard ")).OrderBy(e=>e.SaveId,StringComparer.Ordinal).ToArray();
            var camps=Enumerable.Range(1,3).Select(i=>GameObject.Find("Enemy outpost "+i).transform).ToArray();
            Map01EnemyController[] Roster(int number,int total){
                var initial=all.Where(e=>camps.OrderBy(c=>(c.position-e.transform.position).sqrMagnitude).First()==camps[number-1]).ToList();
                for(int slot=initial.Count;slot<total;slot++){
                    string name=$"Operation guard {number}_{slot}";var existing=Find(layout.transform,name)?.GetComponent<Map01EnemyController>();
                    if(existing==null){var at=PatrolSpot(camps[number-1].position,slot*71+number*31,8+slot%3*2);var go=Object.Instantiate(all[0].gameObject,layout.transform);go.name=name;go.transform.position=at;existing=go.GetComponent<Map01EnemyController>();}
                    initial.Add(existing);
                }
                for(int slot=0;slot<initial.Count;slot++){
                    var e=initial[slot];e.operationIndex=number;e.carriesSupplyKey=false;e.suppressOperationLoot=false;
                    var center=e.transform.position;var points=new[]{center,PatrolSpot(center,slot*61+number*31,4),PatrolSpot(center,slot*61+number*31+80,4)};
                    e.Configure(slot==0?Array.Empty<Vector3>():points);e.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(center-camps[number-1].position,Vector3.up).normalized+Vector3.forward*.01f);EditorUtility.SetDirty(e);
                }return initial.ToArray();
            }
            layout.supplyGuards=Roster(1,4);layout.radioGuards=Roster(2,5);layout.commanderGuards=Roster(3,5);
            var store=layout.supplyProps.transform;var offsets=new[]{new Vector3(-2.2f,0,1.9f),new Vector3(2.2f,0,2.6f),new Vector3(-3,0,-2.4f),new Vector3(3,0,-2.4f)};
            for(int i=0;i<4;i++){
                var guard=layout.supplyGuards[i];var post=Walkable(store.TransformPoint(offsets[i]),2);guard.transform.position=post;
                var away=Vector3.ProjectOnPlane(post-store.position,Vector3.up);if(away.sqrMagnitude>.1f)guard.transform.rotation=Quaternion.LookRotation(away);
                guard.Configure(i==0?Array.Empty<Vector3>():new[]{post,PatrolSpot(post,i*75,3),PatrolSpot(post,i*75+70,3)});EditorUtility.SetDirty(guard);
            }
            layout.keyKeeper=layout.supplyGuards[0];layout.keyKeeper.carriesSupplyKey=true;EditorUtility.SetDirty(layout.keyKeeper);
        }
        static Vector3 PatrolSpot(Vector3 center,float angle,float distance)
        {
            for(int n=0;n<16;n++){
                var p=center+Quaternion.Euler(0,angle+n*22.5f,0)*Vector3.forward*distance;
                if(!NavMesh.SamplePosition(p,out var hit,1.8f,NavMesh.AllAreas))continue;
                var path=new NavMeshPath();if(NavMesh.CalculatePath(center,hit.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)return hit.position;
            }return Walkable(center,2);
        }
        static NavMeshObstacle Obstacle(Transform root,string name,Vector3 center,Vector3 size){var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=center;var obstacle=go.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=size;obstacle.carving=true;obstacle.carveOnlyStationary=true;return obstacle;}
        static void Sign(Transform root,string name,string words,Vector3 at,float size)
        {
            var existing=Find(root,name);var go=existing!=null?existing.gameObject:new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=at;go.transform.localRotation=Quaternion.Euler(0,180,0);go.transform.localScale=Vector3.one;
            var text=go.GetComponent<TextMesh>()??go.AddComponent<TextMesh>();text.text=words;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=72;text.characterSize=size;text.color=new Color(.94f,.85f,.63f);var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=text.font.material;
            // Fit the label to the wooden panel, including native FBX axis/scale compensation.
            float width=name=="Chest sign"?.65f:1.8f;float rendered=renderer.bounds.size.x;
            if(rendered>width)go.transform.localScale*=width/rendered;
        }
        static Transform[] RingMarkers(Transform parent,string name,Vector3 target,Vector3 from,float min,float max,int count,bool sight)
        {
            var results=new System.Collections.Generic.List<Transform>();
            for(int sector=0;sector<count;sector++){
                bool found=false;
                for(float radius=min;radius<=max&&!found;radius+=2)for(int step=0;step<23&&!found;step++){
                    float angle=sector*360f/count+(step-11)*5f;
                    if(!sight)angle=sector*360f/count;
                    var p=target+Quaternion.Euler(0,angle,0)*Vector3.forward*radius;
                    if(!NavMesh.SamplePosition(p,out var hit,1.5f,NavMesh.AllAreas))continue;
                    var path=new NavMeshPath();if(!NavMesh.CalculatePath(from,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                    if(sight&&Physics.Linecast(hit.position+Vector3.up*1.6f,target+Vector3.up*1.3f,LayerMask.GetMask("Default","Obstacle","Cover","VisionBlocker"),QueryTriggerInteraction.Ignore))continue;
                    results.Add(Marker(parent,name+" "+sector,hit.position,target-hit.position));found=true;
                }
            }return results.ToArray();
        }
        static void PrepareObservation(Map01OperationsLayout layout)
        {
            var original=GameObject.Find("Enemy outpost 3").transform.TransformPoint(new Vector3(-.3f,0,1.2f));
            for(int attempt=0;attempt<37;attempt++){
                var candidate=attempt==0?original:original+Quaternion.Euler(0,(attempt-1)*30,0)*Vector3.forward*(4+((attempt-1)/12)*4);
                if(!NavMesh.SamplePosition(candidate,out var post,3,NavMesh.AllAreas))continue;
                var positions=new System.Collections.Generic.List<Vector3>();
                foreach(float radius in new[]{36f,38f,40f,42f,44f,34f,32f,30f,28f,26f})for(int angle=0;angle<360;angle+=4){
                    var desired=post.position+Quaternion.Euler(0,angle,0)*Vector3.forward*radius;
                    if(!NavMesh.SamplePosition(desired,out var hit,2,NavMesh.AllAreas))continue;
                    float distance=Vector3.Distance(hit.position,post.position);if(distance<25||distance>45)continue;
                    if(distance<31&&Vector3.Angle(layout.commanderPost.forward,hit.position-post.position)<110)continue;
                    var path=new NavMeshPath();if(!NavMesh.CalculatePath(layout.radioApproach.position,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                    if(Physics.Linecast(hit.position+Vector3.up*1.6f,post.position+Vector3.up*1.3f,LayerMask.GetMask("Default","Obstacle","Cover","VisionBlocker"),QueryTriggerInteraction.Ignore))continue;
                    if(positions.Any(p=>Vector3.Distance(p,hit.position)<6))continue;
                    if(!RenderedOfficerVisible(hit.position,post.position))continue;
                    positions.Add(hit.position);if(positions.Count==3)break;
                }
                if(positions.Count>=3){layout.commanderPost.position=post.position;layout.observationSites=positions.Take(3).Select((p,i)=>Marker(layout.transform,"Officer observation "+i,p,post.position-p)).ToArray();return;}
            }
            throw new Exception("No three clear observation routes: inspect officer placement at point 3.");
        }
        // Foliage has alpha-cutout meshes without physics colliders: validate rendered visibility too.
        static bool RenderedOfficerVisible(Vector3 from,Vector3 post)
        {
            var proxy=GameObject.CreatePrimitive(PrimitiveType.Capsule);proxy.name="Temporary officer visibility proxy";proxy.hideFlags=HideFlags.HideAndDontSave;
            Object.DestroyImmediate(proxy.GetComponent<Collider>());proxy.transform.position=post+Vector3.up*.86f;proxy.transform.localScale=new Vector3(.42f,.86f,.42f);
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",new Color(1,0,1));proxy.GetComponent<Renderer>().sharedMaterial=material;
            var host=new GameObject("Temporary binocular visibility camera");host.hideFlags=HideFlags.HideAndDontSave;var camera=host.AddComponent<Camera>();
            camera.transform.position=from+Vector3.up*1.6f;camera.transform.LookAt(post+Vector3.up*1.3f);camera.fieldOfView=16;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var target=RenderTexture.GetTemporary(160,120,24);var active=RenderTexture.active;var texture=new Texture2D(160,120,TextureFormat.RGB24,false);
            try{
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,160,120),0,0);texture.Apply();
                int visible=texture.GetPixels32().Count(p=>p.r>200&&p.b>200&&p.g<70);
                Directory.CreateDirectory("Logs/Map01Operations");
                if(!File.Exists("Logs/Map01Operations/visibility-probe.png")||visible>=20)File.WriteAllBytes("Logs/Map01Operations/visibility-probe.png",texture.EncodeToPNG());
                File.AppendAllText("Logs/Map01Operations/visibility-probe.txt",from+" -> "+post+": "+visible+"\n");
                return visible>=20;
            }finally{camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);Object.DestroyImmediate(host);Object.DestroyImmediate(proxy);Object.DestroyImmediate(material);}
        }
        static Vector3 Walkable(Vector3 p,float radius){if(!NavMesh.SamplePosition(p,out var hit,radius,NavMesh.AllAreas))throw new Exception("Not on NavMesh: "+p);return hit.position;}
        static Transform Marker(Transform parent,string name,Vector3 p,Vector3 direction){var t=new GameObject(name).transform;t.SetParent(parent);t.position=p;direction=Vector3.ProjectOnPlane(direction,Vector3.up);if(direction.sqrMagnitude>.001f)t.rotation=Quaternion.LookRotation(direction);return t;}
        public static void Validate(Map01OperationsLayout layout)
        {
            if(layout.supplyGuards.Length!=4||layout.radioGuards.Length!=5||layout.commanderGuards.Length!=5)throw new Exception("Invalid guard rosters.");
            if(layout.keyKeeper==null||!layout.keyKeeper.carriesSupplyKey||layout.supplyProps==null||layout.supplyProps.lid==null||layout.supplyProps.doorHinge==null)throw new Exception("Missing keyed supply props.");
            // Closed door carving intentionally removes the small inside landing until unlocked.
            if(!NavMesh.SamplePosition(layout.chestApproach.position,out _,1.2f,NavMesh.AllAreas))throw new Exception("Chest landing is detached from the walkable warehouse.");
            foreach(var t in new[]{layout.doorApproach,layout.radioApproach,layout.handoff}.Concat(layout.observationSites).Concat(layout.responseSites))
                if(t==null||!NavMesh.SamplePosition(t.position,out var hit,.4f,NavMesh.AllAreas)||Vector3.Distance(hit.position,t.position)>.2f)throw new Exception("Unreachable operation marker "+t);
        }
    }
}
