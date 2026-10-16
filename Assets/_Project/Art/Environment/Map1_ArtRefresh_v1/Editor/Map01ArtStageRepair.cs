using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
namespace ShadowVale.EnvironmentReview
{
    public static class Map01ArtStageRepair
    {
        public static void Run()
        {
            var scene=EditorSceneManager.GetActiveScene();if(scene.path!=Map01ArtStage.Stage)throw new Exception("Art stage only.");
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();var log=new List<string>();
            var files=new[]{"C1_ApprovedArt","C2_ApprovedArt","C3_MainGarrison_Art"};
            for(int i=0;i<3;i++)
            {
                var root=all.Single(t=>t.name=="C"+(i+1)+" / Mission art");
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Map01ArtStage.Root+"/Meshes/"+files[i]+".fbx");var originals=asset.GetComponentsInChildren<Transform>(true);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var box=t.GetComponent<BoxCollider>();if(box==null||t.name.StartsWith("COLLIDER_"))continue;
                    var source=originals.FirstOrDefault(s=>s.name==t.name);if(source==null)continue;
                    var renderers=source.GetComponentsInChildren<MeshRenderer>();if(renderers.Length==0)continue;
                    bool first=true;var bounds=new Bounds();
                    foreach(var r in renderers)for(int n=0;n<8;n++)
                    {
                        var p=source.InverseTransformPoint(r.transform.TransformPoint(r.localBounds.center+Vector3.Scale(r.localBounds.extents,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1))));
                        if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                    }
                    box.center=bounds.center;box.size=bounds.size;
                }
            }
            var ground=all.Single(t=>t.name=="Continuous hills — 220 x 200").GetComponent<MeshCollider>();var mesh=ground.sharedMesh;
            if(AssetDatabase.GetAssetPath(mesh)!=Map01ArtStage.Root+"/StageData/StageTerrain.asset")throw new Exception("Refusing to edit a shared ground mesh.");
            var original=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Scenes/Maps/Map 1/Terrain.asset");
            if(original==null||original.vertexCount!=mesh.vertexCount)throw new Exception("Ground topology changed.");
            mesh.vertices=original.vertices;
            var pads=JsonUtility.FromJson<Map01ArtStage.StageReport>(File.ReadAllText(Map01ArtStage.Work+"/Audit/art_stage_report.json")).pads;
            Map01ArtStage.ApplyPads(ground.GetComponent<MeshFilter>(),pads.ToList());ground.sharedMesh=null;ground.sharedMesh=mesh;Physics.SyncTransforms();
            var chest=new Vector3(71,0,59.05f);ground.Raycast(new Ray(chest+Vector3.up*100,Vector3.down),out var groundHit,200);float level=groundHit.point.y;
            var vs=mesh.vertices;for(int i=0;i<vs.Length;i++)
            {var p=ground.transform.TransformPoint(vs[i]);float edge=Mathf.Max(Mathf.Abs(p.x-chest.x)-1.4f,Mathf.Abs(p.z-chest.z)-1.35f);if(edge<2)p.y=Mathf.Lerp(level,p.y,Mathf.SmoothStep(0,1,Mathf.Max(0,edge)/2));vs[i]=ground.transform.InverseTransformPoint(p);}
            mesh.vertices=vs;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);ground.sharedMesh=null;ground.sharedMesh=mesh;
            Physics.SyncTransforms();
            foreach(var header in all.Where(t=>t.name.StartsWith("COLLIDER_DoorHeader")))
            {var modifier=header.GetComponent<NavMeshModifier>()??header.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;}
            var surface=Object.FindFirstObjectByType<NavMeshSurface>();var settings=surface.GetBuildSettings();log.Add($"Agent {settings.agentTypeID}: radius {settings.agentRadius}, height {settings.agentHeight}, climb {settings.agentClimb}, slope {settings.agentSlope}.");
            // Physical door clearance is checked independently of navigation rasterization.
            foreach(var floor in all.SelectMany(t=>t.GetComponents<BoxCollider>()).Where(c=>c.name.StartsWith("COLLIDER_Floor")))
            {
                var doors=floor.transform.parent.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("COLLIDER_OpenDoor")).ToArray();
                foreach(var door in doors)
                {
                    var hinge=door.transform.parent.position;var centre=floor.bounds.center;var right=floor.transform.right;right.y=0;right.Normalize();
                    float sign=Vector3.Dot(centre-hinge,right)>0?1:-1;float width=door.size.x*Mathf.Abs(door.transform.lossyScale.x);
                    var at=hinge+right*(width*.5f*sign);at.y=floor.bounds.max.y+.03f;
                    var hits=Physics.OverlapCapsule(at+Vector3.up*.5f,at+Vector3.up*1.5f,.49f,~0,QueryTriggerInteraction.Ignore);
                    log.Add(floor.transform.parent.name+" test "+at.ToString("F3")+": "+string.Join(",",hits.Select(c=>c.name)));
                }
            }
            File.WriteAllLines(Map01ArtStage.Work+"/Audit/repair_diagnostics.txt",log);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Map01ArtStageValidation.Run();
            ConnectDoorways();
            Map01ArtStageValidation.Run();
        }
        public static void ConnectDoorways()
        {
            var all=EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();var log=new List<string>();
            var player=Object.FindFirstObjectByType<ShadowVale.Gameplay.Player.PlayerController>();
            if(player==null||!NavMesh.SamplePosition(player.transform.position,out var global,5,NavMesh.AllAreas))throw new Exception("No global navigation start.");
            foreach(var t in all.Where(t=>t.name=="Walkable doorway transition").ToArray())Object.DestroyImmediate(t.gameObject);
            foreach(var floor in all.SelectMany(t=>t.GetComponents<BoxCollider>()).Where(c=>c.name.StartsWith("COLLIDER_Floor")))
            {
                var house=floor.transform.parent;var doors=house.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("COLLIDER_OpenDoor")).ToArray();if(doors.Length==0)continue;
                var right=floor.transform.right;right.y=0;right.Normalize();var middle=doors.Aggregate(Vector3.zero,(sum,d)=>sum+d.transform.parent.position)/doors.Length;
                if(doors.Length==1){float sign=Vector3.Dot(floor.bounds.center-middle,right)>0?1:-1;middle+=right*(doors[0].size.x*Mathf.Abs(doors[0].transform.lossyScale.x)*.5f*sign);}
                var outward=middle-floor.bounds.center;outward.y=0;outward-=right*Vector3.Dot(outward,right);outward.Normalize();middle.y=floor.bounds.max.y;
                float porch=house.name.Contains("command house")?2:house.name.Contains("quarters")?1.2f:house.name.Contains("Aid post")?1:0;
                var inside=middle-outward*1.4f;var outside=middle+outward*(porch+1.2f);outside.y=floor.bounds.max.y;
                if(!NavMesh.SamplePosition(inside,out var a,.8f,NavMesh.AllAreas)||!NavMesh.SamplePosition(outside,out var b,1,NavMesh.AllAreas)){log.Add(house.name+": no endpoints");continue;}
                var path=new NavMeshPath();if(NavMesh.CalculatePath(global.position,a.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete){log.Add(house.name+": reachable from player start");continue;}
                var indoors=path.corners.LastOrDefault();
                if(!NavMesh.CalculatePath(global.position,b.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete){log.Add(house.name+": outside also disconnected; inside stop "+indoors+"; outside stop "+path.corners.LastOrDefault()+"; endpoints "+a.position+" → "+b.position);continue;}
                if(Mathf.Abs(a.position.y-b.position.y)>.75f){log.Add(house.name+": too high for a walking link "+(a.position-b.position));continue;}
                bool clear=true;var blocked=new HashSet<string>();
                for(int i=0;i<=24;i++)
                {
                    var foot=Vector3.Lerp(a.position,b.position,i/24f);
                    var support=Physics.RaycastAll(foot+Vector3.up*1.5f,Vector3.down,3,~0,QueryTriggerInteraction.Ignore).Where(h=>h.normal.y>.5f).OrderByDescending(h=>h.point.y).FirstOrDefault();
                    if(support.collider!=null)foot.y=support.point.y+.03f;
                    foreach(var c in Physics.OverlapCapsule(foot+Vector3.up*.5f,foot+Vector3.up*1.5f,.49f,~0,QueryTriggerInteraction.Ignore))
                    {if(c.GetComponentInParent<ShadowVale.Map01.Map01EnemyController>()!=null||c.GetComponentInParent<CharacterController>()!=null)continue;clear=false;blocked.Add(c.name);}
                }
                if(!clear){log.Add(house.name+": physical blockage "+string.Join(",",blocked));continue;}
                var go=new GameObject("Walkable doorway transition");go.transform.SetParent(house);go.transform.position=a.position;
                var link=go.AddComponent<NavMeshLink>();link.agentTypeID=0;link.startPoint=Vector3.zero;link.endPoint=go.transform.InverseTransformPoint(b.position);link.width=.1f;link.bidirectional=true;link.UpdateLink();
                log.Add(house.name+": added physical-clear walking transition "+a.position+" → "+b.position);
            }
            File.WriteAllLines(Map01ArtStage.Work+"/Audit/doorway_transitions.txt",log);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
    }
}
