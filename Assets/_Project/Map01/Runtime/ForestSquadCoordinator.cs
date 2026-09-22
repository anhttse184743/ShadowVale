using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ShadowVale.AI.Solvers;
using ShadowVale.AI.Solvers.Classical;
using ShadowVale.Data.Coordination;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    public sealed class ForestSquadCoordinator : MonoBehaviour
    {
        public TextAsset researchConfig;
        public ForestResearchConfig Config { get; private set; } = new ForestResearchConfig();
        public string Variant { get; private set; }
        public string LastOrder { get; private set; } = "Chưa có báo động";
        public int OrdersIssued { get; private set; }
        public string TelemetryDirectory { get; private set; }
        public double LastSolveMs { get; private set; }
        public double LastEnergy { get; private set; }
        public Vector3 LastReportedPosition { get; private set; }
        public float LastReportTime { get; private set; } = -999;
        ForestMission mission; ForestGuard[] guards;
        readonly Dictionary<string,Squad> squads=new Dictionary<string,Squad>();
        readonly StringBuilder log=new StringBuilder();float flushAt;bool showPanel;
        int serial; string session; int snapshotCount;
        sealed class Squad {public Vector3 target;public float seen=-999,next;public bool busy,engaged,lost;public float encounterStart;}
        void Awake()
        {
            if(researchConfig!=null)Config=JsonUtility.FromJson<ForestResearchConfig>(researchConfig.text)??new ForestResearchConfig();
            Config.solverBudgetMs=Mathf.Clamp(Config.solverBudgetMs,1,20);Config.replanSeconds=Mathf.Max(1,Config.replanSeconds);
            Config.burstRounds=Mathf.Clamp(Config.burstRounds,1,5);Config.magazineRounds=Mathf.Max(Config.burstRounds,Config.magazineRounds);
            Config.shotSpacing=Mathf.Max(.12f,Config.shotSpacing);Config.burstPause=Mathf.Max(.5f,Config.burstPause);
            Variant=Config.solver=="greedy"?"greedy":"qiea";
            session=Guid.NewGuid().ToString("N");TelemetryDirectory=Path.Combine(Application.persistentDataPath,"Research",session);
            if(Config.telemetry){Directory.CreateDirectory(TelemetryDirectory);File.WriteAllText(Path.Combine(TelemetryDirectory,"events.csv"),"time,event,squad,solver,instance,agents,nodes,build_ms,solve_ms,pipeline_ms,energy,feasible,deadline,samples,raw_feasible,value,x,y,z\n");File.WriteAllText(Path.Combine(TelemetryDirectory,"config.json"),JsonUtility.ToJson(Config,true));}
        }
        void Start(){mission=FindFirstObjectByType<ForestMission>();guards=FindObjectsByType<ForestGuard>(FindObjectsSortMode.None).OrderBy(g=>g.id,StringComparer.Ordinal).ToArray();}
        public void Report(ForestGuard observer,Vector3 position)
        {
            if(observer==null||!observer.Alive)return;
            LastReportedPosition=position;LastReportTime=Time.time;
            if(string.IsNullOrEmpty(observer.squadId))return;
            var leader=guards?.FirstOrDefault(g=>g.Alive&&g.isCommander&&g.squadId==observer.squadId);
            if(leader==null||Vector3.Distance(leader.transform.position,observer.transform.position)>Config.radioRange)return;
            if(!squads.TryGetValue(observer.squadId,out var squad)){squad=new Squad();squads.Add(observer.squadId,squad);}
            squad.target=position;squad.seen=Time.time;squad.lost=false;
            if(!squad.engaged){squad.engaged=true;squad.encounterStart=Time.time;Event("contact",observer.squadId,position);}
        }
        void Update()
        {
            var kb=Keyboard.current;
            if(kb!=null&&kb.f7Key.wasPressedThisFrame)showPanel=!showPanel;
            if(kb!=null&&kb.f6Key.wasPressedThisFrame){Variant=Variant=="qiea"?"greedy":"qiea";Event("variant_changed","",Vector3.zero);}
            if(mission==null||!mission.IsInitialized||mission.Stopped)return;
            foreach(var entry in squads)
            {
                var s=entry.Value;
                if(Time.time-s.seen>Config.memorySeconds)
                {
                    if(s.engaged&&!s.lost){s.lost=true;Event("contact_lost",entry.Key,s.target,Time.time-s.encounterStart);}
                    var survivors=guards.Where(g=>g.Alive&&g.squadId==entry.Key).ToArray();
                    if(s.engaged&&survivors.All(g=>g.state==ForestGuardState.Patrol))
                    {s.engaged=false;Event(survivors.Length==0?"squad_eliminated":"escape_to_patrol",entry.Key,s.target,Time.time-s.encounterStart);}
                    continue;
                }
                if(s.busy||Time.time<s.next)continue;
                var members=guards.Where(g=>g.Alive&&g.squadId==entry.Key).ToArray();
                var leader=members.FirstOrDefault(g=>g.isCommander);
                if(leader==null)continue;
                members=members.Where(g=>Vector3.Distance(g.transform.position,leader.transform.position)<Config.radioRange).Take(12).ToArray();
                if(members.Length<2)continue;
                s.busy=true;s.next=Time.time+Config.replanSeconds;
                StartCoroutine(Plan(entry.Key,s,members,leader));
            }
            if(Time.unscaledTime>flushAt){flushAt=Time.unscaledTime+5;Flush();}
        }
        IEnumerator Plan(string squadId,Squad squad,ForestGuard[] members,ForestGuard leader)
        {
            Vector3 threat=squad.target;float observedAt=squad.seen;string variant=Variant;
            var pipeline=System.Diagnostics.Stopwatch.StartNew();var build=System.Diagnostics.Stopwatch.StartNew();
            var nodes=new List<Vector3>();
            foreach(var guard in members)nodes.Add(guard.transform.position);
            for(int ring=0;ring<2;ring++)for(int k=0;k<8;k++)
            {
                float angle=k*Mathf.PI/4;Vector3 p=threat+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(ring==0?8:14);
                // Sample around the actual terrain surface rather than another floor/river elevation.
                var groundHits=Physics.RaycastAll(p+Vector3.up*25,Vector3.down,60,mission.ObstructionMask,QueryTriggerInteraction.Ignore);
                var ground=groundHits.Where(h=>h.collider.name.StartsWith("Collision_Walk")||h.collider.name.StartsWith("Hill ")||h.collider.name=="Cube").OrderByDescending(h=>h.point.y).FirstOrDefault();
                if(ground.collider!=null)p.y=ground.point.y;
                if(NavMesh.SamplePosition(p,out var nav,2.5f,NavMesh.AllAreas)&&nodes.All(n=>(n-nav.position).sqrMagnitude>2.25f))nodes.Add(nav.position);
            }
            int a=members.Length,n=nodes.Count,size=a*n;var q=new double[size*size];var valid=new bool[a,n];
            float penalty=Mathf.Max(100,Config.assignmentPenalty);var front=(leader.transform.position-threat).normalized;
            for(int ag=0;ag<a;ag++)
            {
                for(int j=0;j<n;j++)
                {
                    var path=new NavMeshPath();valid[ag,j]=NavMesh.CalculatePath(members[ag].transform.position,nodes[j],NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                    float distance=0;for(int p=1;p<path.corners.Length;p++)distance+=Vector3.Distance(path.corners[p-1],path.corners[p]);
                    bool clear=!Physics.Linecast(nodes[j]+Vector3.up*1.3f,threat+Vector3.up,mission.ObstructionMask,QueryTriggerInteraction.Ignore);
                    float flank=1-Mathf.Abs(Vector3.Dot((nodes[j]-threat).normalized,front));
                    float range=Vector3.Distance(nodes[j],threat);
                    double reward=(clear?Config.coverageWeight:-2)+flank*Config.flankWeight-distance*Config.travelWeight-Mathf.Abs(range-11)*.12f;
                    q[(ag*n+j)*size+ag*n+j]=-penalty-reward+(valid[ag,j]?0:penalty*100);
                }
                build.Stop();yield return null;build.Start();
            }
            for(int i=0;i<size;i++)for(int j=i+1;j<size;j++)
            {
                if(i/n==j/n)q[i*size+j]=2*penalty;
                else {float dist=Vector3.Distance(nodes[i%n],nodes[j%n]);q[i*size+j]=i%n==j%n?penalty*2:Config.overlapPenalty*Mathf.Max(0,1-dist/5);}
            }
            build.Stop();int sequence=++serial;
            var request=new QuboRequest{InstanceId=session+"-"+sequence,Seed=Config.seed+sequence,AgentCount=a,NodeCount=n,QMatrix=q,Offset=a*penalty,LatencyBudgetMs=Config.solverBudgetMs,Variant=variant};
            // Only immutable numeric request data crosses to the worker; Unity/NavMesh calls stay above.
            Task<CoordinationPlan> task=Task.Run(()=>variant=="qiea"?new QieaSolver(Config.solverBudgetMs,Config.iterations,Config.population).Solve(request):new GreedySolver(Config.solverBudgetMs).Solve(request));
            while(!task.IsCompleted)yield return null;
            squad.busy=false;
            if(task.IsFaulted){Debug.LogWarning("Squad solve failed: "+task.Exception?.GetBaseException().Message);yield break;}
            if(leader==null||!leader.Alive||members.Any(g=>g==null||!g.Alive)||Time.time-observedAt>Config.memorySeconds||Vector3.Distance(threat,squad.target)>6)yield break;
            var plan=task.Result;bool feasible=plan.AgentToTarget!=null&&plan.AgentToTarget.Length==a;
            if(feasible)for(int ag=0;ag<a;ag++)if(plan.AgentToTarget[ag]<0||plan.AgentToTarget[ag]>=n||!valid[ag,plan.AgentToTarget[ag]])feasible=false;
            if(!feasible){Event("unreachable_plan_rejected",squadId,threat);yield break;}
            for(int ag=0;ag<a;ag++)members[ag].ReceiveOrder(nodes[plan.AgentToTarget[ag]],threat,observedAt,Config.memorySeconds);
            OrdersIssued++;LastSolveMs=plan.SolveLatencyMs;LastEnergy=plan.Energy;
            LastOrder=squadId+": áp chế / vòng sườn / chiếm vị trí";
            if(Vector3.Distance(mission.player.position,leader.transform.position)<35)mission.Say("Chỉ huy địch: Phát hiện mục tiêu! Giữ hỏa lực, tản ra hai cánh!",2);
            Append("plan",squadId,variant,request.InstanceId,a,n,build.Elapsed.TotalMilliseconds,plan.SolveLatencyMs,pipeline.Elapsed.TotalMilliseconds,plan.Energy,1,plan.HitLatencyBudget?1:0,plan.SamplesEvaluated,plan.RawFeasibleSamples,0,threat);
            if(Config.telemetry&&Config.captureSnapshots&&snapshotCount++<100)
            {
                var snapshot=new Snapshot{instance=request.InstanceId,seed=request.Seed,agents=a,nodes=n,q=q,offset=request.Offset,budgetMs=request.LatencyBudgetMs,positions=nodes.ToArray()};
                File.AppendAllText(Path.Combine(TelemetryDirectory,"instances.jsonl"),JsonUtility.ToJson(snapshot)+"\n");
            }
        }
        [Serializable] public class Snapshot {public string instance;public int seed,agents,nodes,budgetMs;public double[] q;public double offset;public Vector3[] positions;}
        public void Event(string kind,string squad,Vector3 position,float value=0)=>Append(kind,squad,Variant,"",0,0,0,0,0,0,0,0,0,0,value,position);
        void Append(string kind,string squad,string variant,string instance,int agents,int nodes,double build,double solve,double total,double energy,int feasible,int deadline,int samples,int raw,float value,Vector3 p)
        {
            if(!Config.telemetry)return;
            log.AppendLine(string.Join(",",new[]{Time.time.ToString("F3",CultureInfo.InvariantCulture),kind,squad,variant,instance,agents.ToString(),nodes.ToString(),build.ToString("F4",CultureInfo.InvariantCulture),solve.ToString("F4",CultureInfo.InvariantCulture),total.ToString("F4",CultureInfo.InvariantCulture),energy.ToString("F6",CultureInfo.InvariantCulture),feasible.ToString(),deadline.ToString(),samples.ToString(),raw.ToString(),value.ToString("F3",CultureInfo.InvariantCulture),p.x.ToString("F3",CultureInfo.InvariantCulture),p.y.ToString("F3",CultureInfo.InvariantCulture),p.z.ToString("F3",CultureInfo.InvariantCulture)}));
        }
        void Flush(){if(log.Length==0||!Config.telemetry)return;try{File.AppendAllText(Path.Combine(TelemetryDirectory,"events.csv"),log.ToString());log.Clear();}catch(IOException e){Debug.LogWarning("Research telemetry: "+e.Message);}}
        void OnDestroy(){Flush();}
        void OnGUI()
        {
            if(!showPanel)return;
            GUI.Box(new Rect(18,180,460,135),"RBL • F6 đổi Greedy / QIEA • F7 ẩn");
            GUI.Label(new Rect(30,205,440,100),$"Solver: {Variant} | Lệnh: {OrdersIssued}\nSolve: {LastSolveMs:F2} ms | Energy: {LastEnergy:F2}\n{LastOrder}\nCSV + snapshot: persistentDataPath/Research");
        }
    }
}
