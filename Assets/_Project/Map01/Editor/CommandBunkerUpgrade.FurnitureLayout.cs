using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    public static partial class CommandBunkerUpgrade
    {
        const string FurnitureScene="Assets/_Project/Scenes/Maps/Map 1.unity";
        static void RequireFurnitureScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play Mode first.");
            if(EditorSceneManager.GetActiveScene().path!=FurnitureScene)EditorSceneManager.OpenScene(FurnitureScene);
        }
        static void InspectFurnitureLayout()
        {
            RequireFurnitureScene();var report=new StringBuilder();
            var table=All<Transform>().Single(t=>t.name=="BriefingTable");
            foreach(var t in table.parent.GetComponentsInChildren<Transform>().Where(t=>t.parent==table.parent||t.GetComponent<Collider>()!=null))
            {
                report.AppendLine(PathOf(t)+" pos="+t.position.ToString("F3")+" rotation="+t.eulerAngles);
                foreach(var c in t.GetComponents<Collider>().Where(c=>c.enabled))report.AppendLine("  COLLIDER "+c.bounds);
            }
            foreach(var p in All<ForestPoint>().Where(p=>p.kind==ForestPointKind.Workbench))report.AppendLine("WORKBENCH "+p.transform.position+" radius="+p.radius);
            File.WriteAllText(Reports+"/furniture_layout_inspection.txt",report.ToString());
        }
        static Bounds VisibleFurniture(Transform t)
        {
            var rs=t.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            if(rs.Length==0)throw new Exception("No furniture renderers: "+t.name);
            var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
        }
        static void ApplyFurnitureLayout()
        {
            RequireFurnitureScene();
            Directory.CreateDirectory("Library/FurnitureLayoutBackup");
            foreach(var path in new[]{FurnitureScene,Layout+"EquipmentShelf.prefab"})
            {
                var backup="Library/FurnitureLayoutBackup/"+Path.GetFileName(path);
                if(!File.Exists(backup))File.Copy(path,backup);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var models=JsonUtility.FromJson<Export>(File.ReadAllText(Root+"/ApprovedFurnitureLayout.meshdata.json")).models.ToDictionary(m=>m.name,Import);
            // Build a dedicated crafting prefab; the old BriefingTable remains the briefing anchor.
            var craft=new GameObject("CraftingWorkbenchEntrance");
            try
            {
                var v=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Workbench.prefab"));v.transform.SetParent(craft.transform,false);
                Block(craft,"Crafting table solid blocker",new Vector3(0,.47f,0),new Vector3(2.8f,.94f,1.08f));
                int i=0;foreach(float x in new[]{-.70f,.70f})
                {
                    var chair=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Chair.prefab"));
                    chair.name="Crafting Chair "+(++i);chair.transform.SetParent(craft.transform,false);chair.transform.localPosition=new Vector3(x,0,-1.03f);
                    // Chair prefab already contains a collider and carving obstacle.
                    if(!chair.GetComponentsInChildren<Collider>().Any(c=>c.enabled&&!c.isTrigger))Block(chair,"Chair solid blocker",new Vector3(0,.46f,0),new Vector3(.46f,.93f,.46f));
                }
                PrefabUtility.SaveAsPrefabAsset(craft,Root+"/CraftingWorkbenchEntrance.prefab");
            }finally{Object.DestroyImmediate(craft);}
            var old=All<Transform>().Single(t=>t.name=="BriefingTable");var shelter=old.parent;
            if(PrefabUtility.IsPartOfPrefabInstance(old))PrefabUtility.UnpackPrefabInstance(old.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            foreach(var child in old.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
            foreach(var c in old.GetComponents<Collider>())Object.DestroyImmediate(c);
            foreach(var c in old.GetComponents<NavMeshObstacle>())Object.DestroyImmediate(c);
            old.rotation=Quaternion.identity;
            var table=(GameObject)PrefabUtility.InstantiatePrefab(models["MeetingTableV2"],old.gameObject.scene);table.name="Approved horizontal meeting table";table.transform.SetParent(old,false);
            var seats=(GameObject)PrefabUtility.InstantiatePrefab(models["MeetingBenchesV2"],old.gameObject.scene);seats.name="Approved two full-length benches";seats.transform.SetParent(old,false);
            Block(old.gameObject,"Meeting table solid blocker",new Vector3(0,.40f,0),new Vector3(3.3f,.805f,1.168f));
            foreach(float z in new[]{-1.07f,1.07f})Block(old.gameObject,"Meeting bench solid blocker "+z,new Vector3(0,.245f,z),new Vector3(3.3f,.49f,.40f));
            foreach(float x in new[]{-.75f,.85f})
            {
                var lamp=new GameObject("Oil lamp warm practical");lamp.transform.SetParent(old,false);lamp.transform.localPosition=new Vector3(x,1.02f,0);
                var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.60f,.26f);light.intensity=.65f;light.range=2;light.shadows=LightShadows.None;
            }
            foreach(var r in table.GetComponentsInChildren<MeshRenderer>())
            {
                var m=r.sharedMaterial;if(m.name=="Warm lamp flame"){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(3,1.3f,.1f));EditorUtility.SetDirty(m);}
            }
            var existing=shelter.Find("CraftingWorkbenchEntrance");if(existing!=null)Object.DestroyImmediate(existing.gameObject);
            var placed=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/CraftingWorkbenchEntrance.prefab"),old.gameObject.scene);
            placed.name="CraftingWorkbenchEntrance";placed.transform.SetParent(shelter,false);
            placed.transform.SetPositionAndRotation(new Vector3(shelter.position.x-4,old.position.y,shelter.position.z-5.15f),Quaternion.Euler(0,180,0));
            FitCraftingWall(placed.transform,shelter);
            var point=All<ForestPoint>().Single(p=>p.kind==ForestPointKind.Workbench);
            point.transform.position=placed.transform.position;PrefabUtility.RecordPrefabInstancePropertyModifications(point.transform);
            // Replace only the shared shelf's visual. Existing cabinet placements and blockers remain.
            var shelf=PrefabUtility.LoadPrefabContents(Layout+"EquipmentShelf.prefab");
            try
            {
                var visual=shelf.transform.Find("Approved revision 2");if(visual==null)throw new Exception("Expected approved shelf visual.");
                Object.DestroyImmediate(visual.gameObject);
                var v=(GameObject)PrefabUtility.InstantiatePrefab(models["EquipmentShelfHeadSizedFinal"],shelf.scene);v.name="Approved revision 2";v.transform.SetParent(shelf.transform,false);
                PrefabUtility.SaveAsPrefabAsset(shelf,Layout+"EquipmentShelf.prefab");
            }finally{PrefabUtility.UnloadPrefabContents(shelf);}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(old.gameObject.scene);EditorSceneManager.SaveScene(old.gameObject.scene);
            EditorSceneManager.OpenScene(FurnitureScene);MoveCraftingDoorThirty();
        }
        static void FitCraftingWall(Transform craft,Transform shelter)
        {
            // Measure visible triangles, including structural timber, instead of the coarse wall collider.
            var probes=new System.Collections.Generic.List<GameObject>();float plane=float.NegativeInfinity;
            try
            {
                foreach(var f in shelter.Find("ShelterShell").GetComponentsInChildren<MeshFilter>())
                {
                    var r=f.GetComponent<MeshRenderer>();if(r==null||!r.enabled||f.sharedMesh==null)continue;
                    if(r.bounds.max.z<craft.position.z-3||r.bounds.min.z>craft.position.z+1)continue;
                    var go=new GameObject("Temporary entrance wall measurement"){hideFlags=HideFlags.HideAndDontSave};probes.Add(go);
                    go.transform.SetPositionAndRotation(f.transform.position,f.transform.rotation);go.transform.localScale=f.transform.lossyScale;
                    var mc=go.AddComponent<MeshCollider>();mc.sharedMesh=f.sharedMesh;
                    foreach(float x in new[]{-1.35f,-.7f,0,.7f,1.35f})foreach(float y in new[]{.32f,.70f,.94f})
                    {
                        var origin=craft.position+new Vector3(x,y,1.4f);
                        if(mc.Raycast(new Ray(origin,Vector3.back),out var hit,4)&&hit.normal.z>.5f)plane=Mathf.Max(plane,hit.point.z);
                    }
                }
                if(float.IsInfinity(plane))throw new Exception("No visible cot-side entrance wall found.");
                var filters=craft.Find("Workbench").GetComponentsInChildren<MeshFilter>();
                float rear=filters.SelectMany(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v).z)).Min();
                craft.position+=Vector3.forward*(plane+.03f-rear);PrefabUtility.RecordPrefabInstancePropertyModifications(craft);
                File.WriteAllText(Reports+"/crafting_wall_fit.txt","Visible wall plane Z="+plane.ToString("F5")+"; rear Z="+(rear+plane+.03f-rear).ToString("F5")+"; gap=0.030 m; root="+craft.position.ToString("F4"));
            }finally{foreach(var p in probes)Object.DestroyImmediate(p);}
        }
        static void VerifyFurnitureLayout()
        {
            RequireFurnitureScene();Physics.SyncTransforms();var report=new StringBuilder();
            var drawer=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Rendering.GPUResidentDrawer",false)).FirstOrDefault(t=>t!=null);
            drawer?.GetMethod("Reinitialize",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)?.Invoke(null,null);
            var table=All<Transform>().Single(t=>t.name=="BriefingTable");var craft=All<Transform>().Single(t=>t.name=="CraftingWorkbenchEntrance");
            var point=All<ForestPoint>().Single(p=>p.kind==ForestPointKind.Workbench);
            if(Vector3.Distance(point.transform.position,craft.position)>.01f||point.id!="workbench")throw new Exception("Craft interaction anchor not relocated.");
            var chairs=craft.Cast<Transform>().Where(t=>t.name.StartsWith("Crafting Chair ")).ToArray();
            if(chairs.Length!=2||chairs.Any(c=>Vector3.Dot(c.forward,craft.position-c.position)<.5f))throw new Exception("Crafting chairs invalid.");
            report.AppendLine("PASS two inward-facing crafting chairs; crafting ForestPoint moved to cot-side entrance: "+craft.position);
            var b=VisibleFurniture(table.Find("Approved horizontal meeting table"));var seats=VisibleFurniture(table.Find("Approved two full-length benches"));
            if(Mathf.Abs(b.size.x-3.3f)>.02f||b.size.z>1.3f||Mathf.Abs(seats.size.x-3.3f)>.02f)throw new Exception("Meeting table orientation/bench length invalid.");
            report.AppendLine("PASS horizontal 3.30m meeting table and two 3.30m backless benches at original briefing position: "+table.position);
            foreach(var box in table.GetComponentsInChildren<BoxCollider>().Concat(craft.GetComponentsInChildren<BoxCollider>()).Where(c=>c.enabled&&!c.isTrigger))ProbeCharacterMovement(box,report);
            var cb=VisibleFurniture(craft);
            foreach(var cot in table.parent.Cast<Transform>().Where(t=>t.name=="Cot"||t.name.StartsWith("Cot Row ")))
                if(cb.Intersects(VisibleFurniture(cot)))throw new Exception("Crafting furniture overlaps cot "+cot.name);
            report.AppendLine("PASS crafting furniture does not overlap any of the five cots.");
            var front=craft.position-craft.forward*1.9f;
            if(NavMesh.SamplePosition(front,out var a,1.2f,NavMesh.AllAreas)&&NavMesh.SamplePosition(craft.position+craft.forward*1.9f,out var dest,1.2f,NavMesh.AllAreas))
            {
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(a.position,dest.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("No navigation path around moved crafting table.");
                report.AppendLine("PASS complete navigation path around crafting furniture.");
            }
            var shelves=table.parent.Cast<Transform>().Where(t=>t.name=="EquipmentShelf"||t.name.StartsWith("EquipmentShelf Row ")).ToArray();
            if(shelves.Length!=5)throw new Exception("Expected five ammunition shelves.");
            foreach(var s in shelves)
            {
                if(!s.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh!=null&&f.sharedMesh.name.StartsWith("EquipmentShelfHeadSizedFinal_")))throw new Exception("Shelf has old helmet geometry: "+s.name);
                report.AppendLine("PASS resized helmet model on "+s.name);
            }
            File.WriteAllText(Reports+"/furniture_layout_validation.txt",report.ToString());
            Render(table.position+new Vector3(4.3f,3.2f,-5.6f),table.position+new Vector3(-.8f,.6f,-1.7f),"layout_applied_overview",64);
            Render(craft.position+new Vector3(2.7f,2.2f,3.7f),craft.position+Vector3.up*.7f,"layout_applied_crafting",60);
            Render(table.position+new Vector3(3.7f,2.7f,-3.7f),table.position+Vector3.up*.65f,"layout_applied_meeting",58);
            var s0=shelves[2];Render(s0.position-s0.forward*2.2f+Vector3.up*1.9f,s0.position+Vector3.up*1.45f,"layout_applied_helmets",50);
        }
        static void MoveCraftingDoorThirty()
        {
            RequireFurnitureScene();var craft=All<Transform>().Single(t=>t.name=="CraftingWorkbenchEntrance");var shell=craft.parent.Find("ShelterShell");
            var front=shell.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Front wall"&&c.bounds.center.x<craft.parent.position.x).Single();
            // The cot-side wall terminates at the left edge of the entrance opening.
            var post=shell.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Entrance timber"&&c.bounds.center.x<craft.parent.position.x).Single();
            float edge=Mathf.Min(front.bounds.max.x,post.bounds.min.x);
            var bench=VisibleFurniture(craft.Find("Workbench"));craft.position+=Vector3.right*(edge-.30f-bench.max.x);
            PrefabUtility.RecordPrefabInstancePropertyModifications(craft);
            var point=All<ForestPoint>().Single(p=>p.kind==ForestPointKind.Workbench);point.transform.position=craft.position;PrefabUtility.RecordPrefabInstancePropertyModifications(point.transform);
            Physics.SyncTransforms();bench=VisibleFurniture(craft.Find("Workbench"));
            File.WriteAllText(Reports+"/crafting_door_clearance.txt","PASS table edge to cot-side doorway edge = "+(edge-bench.max.x).ToString("F4")+" m; doorway X="+edge.ToString("F4")+"; tabletop edge X="+bench.max.x.ToString("F4")+"; root="+craft.position.ToString("F4")+"; rear wall gap remains 0.030m.");
            EditorSceneManager.MarkSceneDirty(craft.gameObject.scene);EditorSceneManager.SaveScene(craft.gameObject.scene);
            EditorApplication.delayCall+=VerifyFurnitureLayout;
        }
        static double furnitureProbeAt;
        static void RefreshFurnitureShelf()
        {
            RequireFurnitureScene();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var model=JsonUtility.FromJson<Export>(File.ReadAllText(Root+"/ApprovedFurnitureLayout.meshdata.json")).models.Single(m=>m.name=="EquipmentShelfHeadSizedFinal");
            var imported=Import(model);
            var shelf=PrefabUtility.LoadPrefabContents(Layout+"EquipmentShelf.prefab");
            try
            {
                var old=shelf.transform.Find("Approved revision 2");if(old!=null)Object.DestroyImmediate(old.gameObject);
                var v=(GameObject)PrefabUtility.InstantiatePrefab(imported,shelf.scene);v.name="Approved revision 2";v.transform.SetParent(shelf.transform,false);PrefabUtility.SaveAsPrefabAsset(shelf,Layout+"EquipmentShelf.prefab");
            }finally{PrefabUtility.UnloadPrefabContents(shelf);}
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(FurnitureScene);EditorApplication.delayCall+=VerifyFurnitureLayout;
        }
        static void StartFurnitureRuntime()
        {
            RequireFurnitureScene();SessionState.SetString("FurniturePreviousStartScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(FurnitureScene);
            SessionState.SetInt("FurnitureRuntimeProbe",1);EditorApplication.isPlaying=true;
        }
        static void ProbeFurnitureRuntime()
        {
            var state=SessionState.GetInt("FurnitureRuntimeProbe",0);if(state==0)return;
            if(state==4&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("FurniturePreviousStartScene",""));SessionState.SetInt("FurnitureRuntimeProbe",0);return;
            }
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try
            {
                var mission=Object.FindFirstObjectByType<Map01Mission>();if(mission==null||!mission.IsInitialized)return;
                var craft=All<Transform>().Single(t=>t.name=="CraftingWorkbenchEntrance");
                var inventory=mission.GetComponent<Map01Inventory>();var point=mission.Interactables.Single(p=>p.kind==ForestPointKind.Workbench);
                if(state==1)
                {
                    foreach(var e in mission.Enemies){e.enabled=false;var nav=e.GetComponent<NavMeshAgent>();if(nav!=null)nav.enabled=false;}
                    var cc=mission.player.GetComponent<CharacterController>();if(cc!=null)cc.enabled=false;
                    var at=craft.position+Vector3.forward*1.95f;at.y=.21f;mission.player.SetPositionAndRotation(at,Quaternion.LookRotation(craft.position-at));if(cc!=null)cc.enabled=true;
                    Physics.SyncTransforms();furnitureProbeAt=EditorApplication.timeSinceStartup+2;SessionState.SetInt("FurnitureRuntimeProbe",2);return;
                }
                if(EditorApplication.timeSinceStartup<furnitureProbeAt||state==4)return;
                if(state==2)
                {
                    if(!inventory.NearWorkbench())throw new Exception("Moved workbench fails runtime proximity.");
                    mission.GetComponent<Map01PlayerInteraction>().Interact(point);
                    if(!mission.BenchOpen||!mission.InventoryOpen)throw new Exception("Crafting interaction did not open bench UI.");
                    File.WriteAllText(Reports+"/furniture_runtime_validation.txt","PASS runtime initialized Map 1; moved workbench registered; player near new entrance workbench; real Interact handler opens BenchOpen and InventoryOpen.\n");
                    Render(craft.position+new Vector3(2.2f,2,3.8f),craft.position+Vector3.up*.7f,"layout_final_crafting",60);
                    var table=All<Transform>().Single(t=>t.name=="BriefingTable");Render(table.position+new Vector3(3.8f,2.5f,-3.8f),table.position+Vector3.up*.65f,"layout_final_meeting",58);
                    var shelf=All<Transform>().Single(t=>t.name=="EquipmentShelf Row 3");Render(shelf.position-shelf.forward*2.4f+Vector3.up*2.15f,shelf.position+Vector3.up*1.4f,"layout_final_helmets",52);
                    furnitureProbeAt=EditorApplication.timeSinceStartup+1;SessionState.SetInt("FurnitureRuntimeProbe",3);return;
                }
                mission.SetBenchOpen(false);File.AppendAllText(Reports+"/furniture_runtime_validation.txt","PASS crafting UI closes; runtime captures complete.\n");
                SessionState.SetInt("FurnitureRuntimeProbe",4);EditorApplication.isPlaying=false;
            }
            catch(Exception e){File.WriteAllText(Reports+"/furniture_runtime_validation.txt","FAIL "+e);Debug.LogException(e);SessionState.SetInt("FurnitureRuntimeProbe",4);EditorApplication.isPlaying=false;}
        }
    }
}
