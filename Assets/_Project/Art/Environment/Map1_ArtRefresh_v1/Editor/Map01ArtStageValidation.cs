using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using ShadowVale.Map01;
using Object=UnityEngine.Object;

namespace ShadowVale.EnvironmentReview
{
    public static class Map01ArtStageValidation
    {
        [Serializable] public class Check {public string name;public bool passed;public string detail;public Vector3 position;}
        [Serializable] public class Report {public string scene,geometryBefore,geometryAfter;public Check[] checks;public int passed,failed;}
        [MenuItem("ShadowVale/Art Review/Bake and validate stage navigation")]
        public static void Run()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=Map01ArtStage.Stage||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Open the clean art stage in Edit mode first.");
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var surface=all.SelectMany(t=>t.GetComponents<NavMeshSurface>()).Single();string geometryBefore=surface.useGeometry.ToString();
            surface.RemoveData();NavMesh.RemoveAllNavMeshData();surface.navMeshData=null;surface.collectObjects=CollectObjects.All;
            surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.08f;surface.minRegionArea=.2f;
            Physics.SyncTransforms();surface.BuildNavMesh();
            string path=AssetDatabase.GenerateUniqueAssetPath(Map01ArtStage.Root+"/StageData/StageNavMesh.asset");
            AssetDatabase.CreateAsset(surface.navMeshData,path);
            foreach(var link in Object.FindObjectsByType<NavMeshLink>(FindObjectsSortMode.None))link.UpdateLink();
            var checks=new List<Check>();var mission=Object.FindFirstObjectByType<Map01Mission>();
            bool Point(Vector3 at,out Vector3 found,float radius=2.5f)
            {bool valid=NavMesh.SamplePosition(at,out var hit,radius,NavMesh.AllAreas);found=hit.position;return valid;}
            void Route(string name,Vector3 start,Vector3 end,float endRadius=3)
            {
                var p=new NavMeshPath();bool s=Point(start,out var from,5),e=Point(end,out var to,endRadius);bool calculated=s&&e&&NavMesh.CalculatePath(from,to,NavMesh.AllAreas,p);bool okay=calculated&&p.status==NavMeshPathStatus.PathComplete;
                checks.Add(new Check{name=name,passed=okay,position=end,detail=$"Start {s}: {from:F3}; end {e}: {to:F3}; calculated {calculated}; {p.status}; corners {p.corners.Length}."});
            }
            var player=mission.player!=null?mission.player:Object.FindFirstObjectByType<ShadowVale.Gameplay.Player.PlayerController>()?.transform;
            if(player==null)player=Object.FindFirstObjectByType<CharacterController>()?.transform;
            if(player==null)throw new Exception("No player controller or start position in art stage.");
            var start=player.position;
            foreach(string anchor in new[]{"A_BaseReport","B_HungCaptive","C1_MissionAnchor","C2_MissionAnchor","C3_MissionAnchor"})
            {var target=all.Single(t=>t.name==anchor);Route("Player → "+anchor,start,target.position);}
            var b=all.Single(t=>t.name=="B_HungCaptive");var a=all.Single(t=>t.name=="A_BaseReport");Route("Escort B → A",b.position,a.position);
            var anchors=Enumerable.Range(1,3).Select(i=>all.Single(t=>t.name=="C"+i+"_MissionAnchor").position).ToArray();
            Route("C1 → C2",anchors[0],anchors[1]);Route("C2 → C3",anchors[1],anchors[2]);Route("C3 → B extraction",anchors[2],b.position);
            var catalog=Map01ItemCatalog.Load();
            for(int i=1;i<=3;i++)
            {
                if(!Map01CampChests.PlaceFor(i,out var at,out var facing)||catalog==null||catalog.chest==null)
                {checks.Add(new Check{name="C"+i+" legacy chest",passed=false,detail="No placement or catalog."});continue;}
                var chest=Object.Instantiate(catalog.chest,at,facing);
                try
                {
                    var rs=chest.GetComponentsInChildren<Renderer>();var bounds=new Bounds(at,Vector3.zero);foreach(var r in rs)bounds.Encapsulate(r.bounds);
                    var extents=bounds.extents*.94f-Vector3.up*.05f;extents.y=Mathf.Max(.02f,extents.y);
                    var inside=Physics.OverlapBox(bounds.center+Vector3.up*.055f,extents,facing,~0,QueryTriggerInteraction.Ignore)
                        .Where(c=>!c.transform.IsChildOf(chest.transform)&&c.GetComponentInParent<Map01EnemyController>()==null&&!(c is TerrainCollider)).ToArray();
                    checks.Add(new Check{name="C"+i+" chest clearance",passed=inside.Length==0,position=at,detail=string.Join(", ",inside.Select(c=>c.name).Distinct())});
                    var front=at+facing*Vector3.forward*(bounds.extents.z+.7f);Route("C"+i+" chest interaction",start,front);
                }
                finally{Object.DestroyImmediate(chest);}
            }
            var guards=all.SelectMany(t=>t.GetComponents<Map01EnemyController>()).ToArray();
            foreach(var guard in guards.Where(g=>g.name.StartsWith("Outpost guard ")))
            {
                for(int i=0;i<guard.PatrolPoints.Count;i++)Route(guard.name+" patrol "+i,guard.PatrolPoints[i],guard.PatrolPoints[(i+1)%guard.PatrolPoints.Count]);
            }
            var floorCols=all.SelectMany(t=>t.GetComponents<BoxCollider>()).Where(c=>c.name.StartsWith("COLLIDER_Floor")).ToArray();
            foreach(var floor in floorCols)checks.Add(new Check{name="Floor thickness / "+floor.transform.parent.name,passed=floor.bounds.size.y<.45f,position=floor.bounds.center,detail=floor.bounds.size.ToString("F3")});
            foreach(var floor in floorCols.Where(c=>!c.transform.parent.name.StartsWith("Screened latrine")&&!c.transform.parent.name.StartsWith("Gate duty shelter")))
            {
                bool reached=false;Vector3 best=floor.bounds.center;int samples=0;
                for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++)
                {
                    var candidate=floor.bounds.center+new Vector3(floor.bounds.extents.x*x/4f,floor.bounds.extents.y+.1f,floor.bounds.extents.z*z/4f);
                    if(!Point(candidate,out var hit,.35f))continue;
                    var flat=new Vector3(hit.x,floor.bounds.center.y,hit.z);if((floor.ClosestPoint(flat)-flat).sqrMagnitude>.001f)continue;
                    samples++;var p=new NavMeshPath();
                    if(Point(start,out var from,5)&&NavMesh.CalculatePath(from,hit,NavMesh.AllAreas,p)&&p.status==NavMeshPathStatus.PathComplete){reached=true;best=hit;}
                }
                checks.Add(new Check{name="Interior / "+floor.transform.parent.name,passed=reached,position=best,detail=$"{samples} interior walkable samples; at least one reachable interior floor point required."});
            }
            foreach(var camp in all.Where(t=>t.name.EndsWith(" / Mission art")))
            {
                var floor=camp.GetComponentsInChildren<BoxCollider>().First(c=>c.name.StartsWith("COLLIDER_Floor"));
                var ray=new Ray(floor.bounds.center+Vector3.up*30,Vector3.down);
                bool roof=camp.GetComponentsInChildren<MeshCollider>().Where(c=>c.name.StartsWith("ROOF /")).Any(c=>c.Raycast(ray,out var hit,35)&&hit.normal.y>.2f);
                checks.Add(new Check{name=camp.name+" solid roof",passed=roof,position=floor.bounds.center,detail="Downward ray against the reduced static roof collision mesh."});
            }
            var report=new Report{scene=scene.path,geometryBefore=geometryBefore,geometryAfter=surface.useGeometry.ToString(),checks=checks.ToArray(),passed=checks.Count(c=>c.passed),failed=checks.Count(c=>!c.passed)};
            File.WriteAllText(Map01ArtStage.Work+"/Audit/navigation_validation.json",JsonUtility.ToJson(report,true));
            Diagnose();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("MAP ART NAVIGATION VALIDATION: "+report.passed+" passed, "+report.failed+" failed. Enemy components: "+guards.Length);
        }
        [Serializable] class Solid {public string name,path;public Vector3 center,size;public Vector3[] corners;}
        [Serializable] class Diagnostics {public Solid[] solids;public Vector3[] vertices;public int[] indices;public string[] navigationComponents;}
        public static void Diagnose()
        {
            var camps=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.EndsWith(" / Mission art")).ToArray();
            string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
            var region=new Bounds(new Vector3(68,12,58),new Vector3(60,30,60));
            var solids=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c=>c.enabled&&c.bounds.Intersects(region)).Select(c=>new Solid{name=c.name,path=PathOf(c.transform),center=c.bounds.center,size=c.bounds.size,
                corners=c is BoxCollider b?Enumerable.Range(0,8).Select(i=>b.transform.TransformPoint(b.center+Vector3.Scale(b.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)))).ToArray():Array.Empty<Vector3>()}).ToArray();
            var nav=NavMesh.CalculateTriangulation();var extra=Object.FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None).Select(o=>PathOf(o.transform)+" obstacle carving="+o.carving+" size="+o.size+" position="+o.transform.position)
                .Concat(Object.FindObjectsByType<NavMeshModifierVolume>(FindObjectsSortMode.None).Select(o=>PathOf(o.transform)+" area="+o.area+" size="+o.size+" position="+o.transform.position))
                .Concat(Object.FindObjectsByType<NavMeshModifier>(FindObjectsSortMode.None).Select(o=>PathOf(o.transform)+" modifier area="+o.area+" override="+o.overrideArea+" ignore="+o.ignoreFromBuild)).ToArray();
            File.WriteAllText(Map01ArtStage.Work+"/Audit/nav_geometry.json",JsonUtility.ToJson(new Diagnostics{solids=solids,vertices=nav.vertices,indices=nav.indices,navigationComponents=extra}));
        }
    }
}
