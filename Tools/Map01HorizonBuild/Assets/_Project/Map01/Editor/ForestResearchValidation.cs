using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using ShadowVale.AI.Solvers;
using ShadowVale.AI.Solvers.Classical;
using ShadowVale.Data.Coordination;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Editor
{
    [InitializeOnLoad] public static class ForestResearchValidation
    {
        const string Reports="Tools/Map01OptimizedReports/";
        static int phase;static double due;static ForestGuard[] team;static ForestMission mission;static ForestSquadCoordinator coordinator;
        static float health,reportTime;static int orders;
        static ForestResearchValidation(){EditorApplication.playModeStateChanged+=Changed;}
        public static void RunPlayOnly() { EditorSceneManager.OpenScene(OptimizedMapBuilder.ScenePath); PlayCheck(); }
        public static void RunBatch()
        {
            try { EditorSceneManager.OpenScene(OptimizedMapBuilder.ScenePath); ForestResearchSetup.Apply(); Benchmark(); PlayCheck(); }
            catch(Exception e) { File.WriteAllText(Reports+"research-batch-failure.txt", e.ToString()); EditorApplication.Exit(1); }
        }
        static void Assert(bool value,string message){if(!value)throw new Exception(message);}
        public static QuboRequest Instance(int agents,int nodes,int seed)
        {
            int size=agents*nodes;var q=new double[size*size];var rng=new System.Random(seed);
            for(int i=0;i<size;i++)
            {q[i*size+i]=-100-rng.NextDouble()*8;for(int j=i+1;j<size;j++)q[i*size+j]=i/nodes==j/nodes?200:i%nodes==j%nodes?200:rng.NextDouble()*3;}
            return new QuboRequest{InstanceId="seed-"+seed,Seed=seed,AgentCount=agents,NodeCount=nodes,QMatrix=q,Offset=agents*100,LatencyBudgetMs=4};
        }
        static double Exact(QuboRequest req)
        {
            double best=double.PositiveInfinity;var assigned=new int[req.AgentCount];var used=new bool[req.NodeCount];
            void Visit(int a){if(a==assigned.Length){best=Math.Min(best,QieaSolver.AssignmentEnergy(req,assigned));return;}for(int n=0;n<used.Length;n++)if(!used[n]){used[n]=true;assigned[a]=n;Visit(a+1);used[n]=false;}}
            Visit(0);return best;
        }
        [MenuItem("ShadowVale/Research/Run seeded Greedy versus QIEA benchmark")]
        public static void Benchmark()
        {
            // Warm JIT before either measured solver. Both receive the identical matrix and 4 ms cap.
            var warm=Instance(3,5,0);new GreedySolver(4).Solve(warm);new QieaSolver(4).Solve(warm);
            var csv=new StringBuilder("seed,agents,nodes,solver,energy,exact_energy,absolute_gap,solve_ms,deadline,samples,raw_feasible\n");
            int checkedPlans=0,improved=0;
            foreach(var shape in new[]{new[]{3,5},new[]{4,20},new[]{8,40},new[]{12,100}})
            for(int seed=0;seed<10;seed++)
            {
                var req=Instance(shape[0],shape[1],1729+seed);double optimum=shape[0]==3?Exact(req):double.NaN;
                CoordinationPlan greedy,qiea;
                if(seed%2==0){greedy=new GreedySolver(4).Solve(req);qiea=new QieaSolver(4).Solve(req);}
                else{qiea=new QieaSolver(4).Solve(req);greedy=new GreedySolver(4).Solve(req);}
                foreach(var plan in new[]{greedy,qiea})
                {
                    Assert(plan.AgentToTarget.Length==shape[0]&&plan.AgentToTarget.All(n=>n>=0&&n<shape[1])&&plan.AgentToTarget.Distinct().Count()==shape[0],"Infeasible assignment");
                    Assert(Math.Abs(QieaSolver.AssignmentEnergy(req,plan.AgentToTarget)-plan.Energy)<1e-8,"Reported QUBO energy mismatch");
                    if(!double.IsNaN(optimum))Assert(plan.Energy>=optimum-1e-8,"Solver reported energy below exact optimum");
                    csv.AppendLine(string.Join(",",new[]{req.Seed.ToString(),shape[0].ToString(),shape[1].ToString(),plan.SolverVariantId,plan.Energy.ToString("F6",CultureInfo.InvariantCulture),double.IsNaN(optimum)?"":optimum.ToString("F6",CultureInfo.InvariantCulture),double.IsNaN(optimum)?"":(plan.Energy-optimum).ToString("F6",CultureInfo.InvariantCulture),plan.SolveLatencyMs.ToString("F4",CultureInfo.InvariantCulture),plan.HitLatencyBudget?"1":"0",plan.SamplesEvaluated.ToString(),plan.RawFeasibleSamples.ToString()}));checkedPlans++;
                }
                Assert(qiea.Energy<=greedy.Energy+1e-8,"QIEA discarded its greedy incumbent");if(qiea.Energy<greedy.Energy-1e-8)improved++;
            }
            var deterministic=Instance(3,5,17);var p1=new QieaSolver(1000,12).Solve(deterministic);var p2=new QieaSolver(1000,12).Solve(deterministic);
            Assert(p1.AgentToTarget.SequenceEqual(p2.AgentToTarget),"Fixed-seed fixed-iteration reproducibility failed");
            File.WriteAllText(Reports+"research-benchmark.csv",csv.ToString());
            File.WriteAllText(Reports+"research-benchmark.txt",$"PASS: {checkedPlans} feasible plans; exact optimum checked for 3x5 instances; QIEA improved {improved}/40 seeded instances; fixed-iteration seed reproducible. Deadline results include best feasible incumbent. This is a synthetic benchmark, not a human playtest or evidence of quantum advantage.");
        }
        [MenuItem("ShadowVale/Research/Run combat and commander Play Mode checks")]
        public static void PlayCheck()
        {
            if(EditorSceneManager.GetActiveScene().isDirty||EditorApplication.isPlaying)throw new Exception("Save scene and exit Play Mode before research checks.");
            SessionState.SetBool("SV.RBL.Check",true);EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange change)
        {
            if(change==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("SV.RBL.Check",false))
            {phase=0;due=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Tick;}
        }
        static void BallisticsCheck()
        {
            var owner=new GameObject("Ballistics test shooter");var target=GameObject.CreatePrimitive(PrimitiveType.Capsule);var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var origin=new Vector3(9000,2,9000);target.transform.position=origin+Vector3.forward*10;target.layer=LayerMask.NameToLayer("Enemy");blocker.transform.position=origin+Vector3.forward*4;blocker.transform.localScale=new Vector3(3,4,1);blocker.layer=LayerMask.NameToLayer("Obstacle");
            Physics.SyncTransforms();Assert(ForestBallistics.Cast(origin,Vector3.forward,20,owner.transform,out var hit)&&hit.collider.gameObject==blocker,"Wall did not block nearest bullet hit");
            Assert(ForestBallistics.MuzzleBlocked(blocker.transform.position,owner.transform),"Embedded muzzle bypasses obstacle");
            blocker.GetComponent<Collider>().enabled=false;Physics.SyncTransforms();Assert(ForestBallistics.Cast(origin,Vector3.forward,20,owner.transform,out hit)&&hit.collider.gameObject==target,"Unobstructed bullet did not reach target");
            var trunk=blocker.AddComponent<CapsuleCollider>();trunk.height=3;trunk.radius=.2f;blocker.transform.localScale=Vector3.one;Physics.SyncTransforms();Assert(ForestBallistics.Cast(origin,Vector3.forward,20,owner.transform,out hit)&&hit.collider==trunk,"Tree capsule did not block bullet");
            trunk.enabled=false;var terrain=blocker.AddComponent<MeshCollider>();var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-3,-3,0),new Vector3(0,3,0),new Vector3(3,-3,0)};mesh.triangles=new[]{0,1,2,2,1,0};mesh.RecalculateBounds();terrain.sharedMesh=mesh;Physics.SyncTransforms();Assert(ForestBallistics.Cast(origin,Vector3.forward,20,owner.transform,out hit)&&hit.collider==terrain,"Terrain mesh did not block bullet");
            Object.Destroy(owner);Object.Destroy(target);Object.Destroy(blocker);Object.Destroy(mesh);
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){EditorApplication.update-=Tick;SessionState.SetBool("SV.RBL.Check",false);return;}
            EditorApplication.isPaused=false;Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.timeSinceStartup<due)return;
            try
            {
                if(phase==0)
                {
                    mission=Object.FindFirstObjectByType<ForestMission>();coordinator=Object.FindFirstObjectByType<ForestSquadCoordinator>();var all=Object.FindObjectsByType<ForestGuard>(FindObjectsSortMode.None);
                    Assert(mission.IsInitialized&&all.Length==12&&all.Count(g=>g.isCommander)==2&&all.All(g=>g.Alive&&g.hp>0),"12 guards / 2 commanders did not initialize");BallisticsCheck();
                    // Isolated deterministic arena, created only during Play Mode; the saved forest is untouched.
                    var arena=new GameObject("RBL temporary test arena");var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(arena.transform);floor.transform.position=new Vector3(5000,-.5f,5000);floor.transform.localScale=new Vector3(45,1,45);floor.layer=LayerMask.NameToLayer("Obstacle");
                    var surface=arena.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
                    foreach(var g in all){g.enabled=false;if(g.GetComponent<NavMeshAgent>().isOnNavMesh)g.GetComponent<NavMeshAgent>().isStopped=true;}
                    team=all.Where(g=>g.squadId=="Bridge patrol").OrderBy(g=>g.id).ToArray();
                    for(int i=0;i<team.Length;i++){var p=new Vector3(4996+i*2.5f,.1f,5007);Assert(team[i].GetComponent<NavMeshAgent>().Warp(p),"Arena guard warp failed");team[i].patrol=new[]{p};team[i].transform.forward=Vector3.back;team[i].enabled=true;}
                    mission.hung.GetComponent<NavMeshAgent>().enabled=false;
                    var cc=mission.player.GetComponent<CharacterController>();cc.enabled=false;mission.player.position=new Vector3(5000,.1f,4995);cc.enabled=true;
                    coordinator.Config.guardDamageScale=.01f;coordinator.Config.aimSpreadDegrees=0;
                    phase=1;due=EditorApplication.timeSinceStartup+7;return;
                }
                if(phase==1)
                {
                    File.WriteAllText(Reports+"research-diagnostic.txt", $"t={Time.time} frame={Time.frameCount} stopped={mission.Stopped} player={mission.player.position} report={coordinator.LastReportTime} orders={coordinator.OrdersIssued} telemetry={coordinator.TelemetryDirectory}\n"+string.Join("\n",team.Select(g=>$"{g.id} state={g.state} suspect={g.suspicion} pos={g.transform.position} forward={g.transform.forward} known={g.LastKnownPosition} shots={g.ShotsFired} nav={g.GetComponent<NavMeshAgent>().isOnNavMesh}")));
                    Assert(coordinator.OrdersIssued>0,"Commander issued no QUBO orders");Assert(team.Sum(g=>g.ShotsFired)>=12,"Burst fire did not exceed old single-shot cadence");Assert(mission.PlayerHealth<100,"Clear bullet hits did not damage player");
                    for(int i=0;i<team.Length;i++)team[i].GetComponent<NavMeshAgent>().Warp(new Vector3(4996+i*2.5f,.1f,5007));
                    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="RBL temporary LOS wall";wall.transform.position=new Vector3(5000,2,5000);wall.transform.localScale=new Vector3(60,4,1);wall.layer=LayerMask.NameToLayer("Obstacle");var carve=wall.AddComponent<NavMeshObstacle>();carve.shape=NavMeshObstacleShape.Box;carve.carving=true;carve.size=Vector3.one;
                    Physics.SyncTransforms();phase=2;due=EditorApplication.timeSinceStartup+1;return;
                }
                if(phase==2)
                {
                    health=mission.PlayerHealth;reportTime=coordinator.LastReportTime;orders=coordinator.OrdersIssued;
                    team.Single(g=>g.isCommander).Hit(100000);
                    var cc=mission.player.GetComponent<CharacterController>();cc.enabled=false;mission.player.position+=Vector3.right*4;cc.enabled=true;
                    phase=3;due=EditorApplication.timeSinceStartup+3;return;
                }
                Assert(Mathf.Abs(mission.PlayerHealth-health)<.001f,"Damage penetrated wall");
                Assert(coordinator.LastReportTime==reportTime,"Guards tracked player through wall");Assert(coordinator.OrdersIssued==orders,"Dead commander issued new orders");
                File.WriteAllText(Reports+"research-playcheck.txt",$"PASS: 12 guards / 2 commanders initialized; wall, tree capsule and terrain mesh block bullets; embedded muzzle blocked; clear hits damage player; burst shots={team.Sum(g=>g.ShotsFired)}; QUBO orders={orders}; no damage/observation through wall; no new orders after commander death. Controlled temporary Play Mode arena, not a full-map human playtest. Telemetry: {coordinator.TelemetryDirectory}");Finish();
            }
            catch(Exception e){File.WriteAllText(Reports+"research-playcheck.txt","FAIL "+e);Finish();}
        }
        static void Finish(){EditorApplication.update-=Tick;SessionState.SetBool("SV.RBL.Check",false);EditorApplication.isPlaying=false; if(Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(File.ReadAllText(Reports+"research-playcheck.txt").StartsWith("PASS")?0:1);}
    }
}
