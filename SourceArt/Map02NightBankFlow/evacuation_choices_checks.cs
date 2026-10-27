using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

[InitializeOnLoad] public static class EvacuationChoicesChecks
{
    const string LiveKey="EvacuationChoicesLive";
    static EvacuationChoicesChecks(){EditorApplication.update+=LiveTick;}
    public static void BeginLive(){SessionState.SetInt(LiveKey,1);EditorApplication.isPlaying=true;}
    static void LiveTick()
    {
        int phase=SessionState.GetInt(LiveKey,0);if(phase==0)return;
        if(phase==3&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetInt(LiveKey,0);EditorApplication.Exit(SessionState.GetInt(LiveKey+"Exit",1));return;}
        if(phase!=1||!EditorApplication.isPlaying)return;
        SessionState.SetInt(LiveKey,2);
        try{Run();SessionState.SetInt(LiveKey+"Exit",0);}catch(Exception error){Debug.LogException(error);SessionState.SetInt(LiveKey+"Exit",1);}
        SessionState.SetInt(LiveKey,3);EditorApplication.isPlaying=false;
    }
    static readonly List<string> report=new List<string>();
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);report.Add("PASS "+text);}
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
    static void Prop(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
    static Map02ChapterState State()
    {
        var state=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){Check(state.Complete(t.id),"setup "+t.id);if(t.id=="N2_03")break;}
        state.night2EvacuationStarted=true;return state;
    }
    public static void Run()
    {
        var root=new GameObject("Evacuation actual NavMesh and physics checks");NavMeshDataInstance nav=default;
        try
        {
            var state=State();Check(state.SelectEvacuationRoute(3)&&state.ResolveEvacuationReport()&&state.Done("N2_04"),"village first completes without visiting either rejected route");state.Validate();
            state=State();state.SelectEvacuationRoute(1);Check(!state.CanReportEvacuationRoute&&!state.ResolveEvacuationReport(),"field report requires observation");
            state.night2FieldObserved=true;Check(state.ResolveEvacuationReport()&&!state.Done("N2_04")&&!state.SelectEvacuationRoute(1),"field is rejected and cannot complete mission");
            Check(state.SelectEvacuationRoute(2)&&state.ResolveEvacuationReport()&&!state.Done("N2_04"),"river is rejected without ending mission");
            Check(state.night2RejectedRoutes==3&&state.SelectEvacuationRoute(3)&&state.ResolveEvacuationReport(),"both rejected then village succeeds");state.Validate();
            state=State();state.SelectEvacuationRoute(2);Check(state.ResolveEvacuationReport()&&!state.night2FieldObserved&&state.night2RejectedRoutes==2,"river can be first without field observation");
            state.SelectEvacuationRoute(1);state.night2FieldObserved=true;state.night2EvacuationPatrolVersion=1;state.night2EvacuationPatrolTriggered=true;state.night2EvacuationPatrolDirection=-1;state.night2EvacuationPatrolAtEnd=true;state.night2EvacuationPatrolClock=1.5f;
            var saved=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(state));saved.Validate();
            Check(saved.CanReportEvacuationRoute&&saved.night2RejectedRoutes==2&&saved.night2EvacuationPatrolAtEnd&&saved.night2EvacuationPatrolClock==1.5f&&saved.night2EvacuationPatrolDirection==-1&&saved.night2EvacuationPatrolTriggered,"save roundtrip retains selection observation rejection and patrol phase");
            saved.ResetPatrolRoute();saved.Validate();Check(!saved.night2EvacuationStarted&&!saved.night2FieldObserved&&saved.night2EvacuationChoice==0&&!saved.night2EvacuationPatrolAtEnd,"old patrol retry clears new route state consistently");
            Check(Map02ChapterDirector.InEvacuationLightCone(Vector3.zero,Vector3.forward,Vector3.forward*9.99f)&&!Map02ChapterDirector.InEvacuationLightCone(Vector3.zero,Vector3.forward,Vector3.forward*10.01f),"flashlight range boundary 10 metres");
            Check(Map02ChapterDirector.InEvacuationLightCone(Vector3.zero,Vector3.forward,Quaternion.Euler(0,29,0)*Vector3.forward*9)&&!Map02ChapterDirector.InEvacuationLightCone(Vector3.zero,Vector3.forward,Quaternion.Euler(0,31,0)*Vector3.forward*9),"flashlight half angle is 30 degrees, full cone 60");
            nav=NavMesh.AddNavMeshData(AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Map2NavMesh.asset"));
            var actorAssets=ScriptableObject.CreateInstance<Map01ActorAssets>();actorAssets.enemyController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Characters/Animations/Controllers/AC_Enemy.controller");
            const string assetPath="Assets/_Project/Map01/Resources/Characters/Map01ActorAssets.asset";
            AssetDatabase.DeleteAsset(assetPath);AssetDatabase.CreateAsset(actorAssets,assetPath);AssetDatabase.SaveAssets();
            var layout=ScriptableObject.CreateInstance<Map02ChapterLayout>();
            var pawn=new GameObject("Nam");pawn.transform.SetParent(root.transform);var body=pawn.AddComponent<CharacterController>();body.height=1.8f;body.center=Vector3.up*.9f;body.radius=.3f;
            var motor=pawn.AddComponent<PlayerController>();motor.enabled=false;
            var mission=root.AddComponent<Map02Mission>();mission.enabled=false;Set(mission,"player",motor);
            var chapter=root.AddComponent<Map02ChapterDirector>();chapter.enabled=false;Prop(chapter,"Mission",mission);Prop(mission,"Chapter",chapter);Prop(chapter,"Layout",layout);Set(chapter,"player",motor);Set(chapter,"root",root);
            Prop(chapter,"State",State());Call(chapter,"BuildEvacuationChoices");
            Check((bool)typeof(Map02ChapterDirector).GetField("evacuationConfigured",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter),"all choice and five formation points are on actual Map 2 NavMesh with complete patrol paths");
            for(int i=0;i<4;i++)report.Add("POINT "+i+" "+chapter.EvacuationChoicePoint(i).ToString("F3"));
            Call(chapter,"SyncEvacuationPatrol");
            var units=chapter.Enemies.Where(e=>e.group==Map02ChapterDirector.EvacuationPatrolGroup).ToArray();
            Check(units.Length==5,"five distinct real NPCs spawn for field route");Call(chapter,"SyncEvacuationPatrol");Check(chapter.Enemies.Count==5,"refresh does not duplicate patrol");
            foreach(var unit in units){unit.enabled=false;var agent=unit.GetComponent<NavMeshAgent>();Check(agent.isOnNavMesh,"guard on native NavMesh "+unit.id);}
            var guard=units[0];var guardAgent=guard.GetComponent<NavMeshAgent>();
            var view=new GameObject("Actual observation camera");view.transform.SetParent(root.transform);var cam=view.AddComponent<Camera>();cam.fieldOfView=16;
            var rig=view.AddComponent<ThirdPersonCamera>();rig.enabled=false;Set(chapter,"rig",rig);Set(chapter,"binocular",true);
            pawn.transform.position=chapter.EvacuationChoicePoint(1);view.transform.position=pawn.transform.position+Vector3.up*1.6f;
            view.transform.LookAt(guard.transform.position+Vector3.up*1.3f);Physics.SyncTransforms();
            Check(!(bool)Call(chapter,"ObserveEvacuationPatrol",.9f),"binocular observation below one second cannot report");
            Check((bool)Call(chapter,"ObserveEvacuationPatrol",.11f),"one second of continuous binocular view from bush detects patrol");
            Set(chapter,"binocular",false);Check(!(bool)Call(chapter,"ObserveEvacuationPatrol",2f),"lowering binocular resets incomplete observation");Set(chapter,"binocular",true);
            Prop(motor,"IsProne",true);Check(!(bool)Call(chapter,"ObserveEvacuationPatrol",2f),"prone cannot accumulate binocular observation");Prop(motor,"IsProne",false);
            Prop(motor,"IsSneaking",true);Check((bool)Call(chapter,"ObserveEvacuationPatrol",1.01f),"crouching can complete binocular observation");Prop(motor,"IsSneaking",false);
            pawn.transform.position+=Vector3.right*4;Check(!(bool)Call(chapter,"ObserveEvacuationPatrol",2f),"observing outside bush cannot qualify");
            pawn.transform.position=chapter.EvacuationChoicePoint(1);view.transform.position=pawn.transform.position+Vector3.up*1.6f;view.transform.LookAt(guard.transform.position+Vector3.up*1.3f);
            var screen=GameObject.CreatePrimitive(PrimitiveType.Cube);screen.transform.SetParent(root.transform);screen.transform.position=(view.transform.position+guard.transform.position+Vector3.up*1.3f)*.5f;screen.transform.localScale=new Vector3(20,6,4);Physics.SyncTransforms();
            Check(!(bool)Call(chapter,"ObserveEvacuationPatrol",2f),"opaque screen blocks binocular observation");Object.DestroyImmediate(screen);
            body.enabled=false;pawn.transform.position=guard.transform.position+Vector3.forward*5;body.enabled=true;Physics.SyncTransforms();
            var from=guard.transform.position+Vector3.up*1.5f;var target=pawn.transform.position+Vector3.up;
            Check((bool)Call(chapter,"EvacuationBeamSeesPlayer",guard,from,(target-from).normalized),"real player collider detected in unobstructed cone");
            var tree=GameObject.CreatePrimitive(PrimitiveType.Cylinder);tree.name="Blender Banyan · visual tree cover";tree.transform.SetParent(root.transform);
            tree.transform.position=(from+target)*.5f;tree.transform.localScale=new Vector3(1.2f,2,1.2f);Object.DestroyImmediate(tree.GetComponent<Collider>());
            var cover=root.AddComponent<Map02SightCover>();
            Check(tree.GetComponent<Collider>()==null&&cover.Blocks(from,target),"visible tree mesh blocks sight even without a physics collider");
            Check(!(bool)Call(chapter,"EvacuationBeamSeesPlayer",guard,from,(target-from).normalized),"standing behind a colliderless tree cannot trigger detection");
            tree.transform.position+=Vector3.right*3;Physics.SyncTransforms();
            Check(!cover.Blocks(from,target)&&(bool)Call(chapter,"EvacuationBeamSeesPlayer",guard,from,(target-from).normalized),"stepping out from tree cover restores actual line of sight");
            tree.transform.position=target+(target-from).normalized*3;
            Check(!cover.Blocks(from,target),"tree behind Nam does not block sight before Nam");
            Object.DestroyImmediate(tree);
            var gapTree=new GameObject("RoadTree with open gap");gapTree.transform.SetParent(root.transform);gapTree.transform.position=(from+target)*.5f;
            var probe=GameObject.CreatePrimitive(PrimitiveType.Cube);var cube=probe.GetComponent<MeshFilter>().sharedMesh;
            var gapMesh=new Mesh();gapMesh.CombineMeshes(new[]{new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(Vector3.left*1.2f,Quaternion.identity,new Vector3(.5f,3,1))},new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(Vector3.right*1.2f,Quaternion.identity,new Vector3(.5f,3,1))}});
            Object.DestroyImmediate(probe);gapTree.AddComponent<MeshFilter>().sharedMesh=gapMesh;gapTree.AddComponent<MeshRenderer>();cover.Build();
            Check(!cover.Blocks(from,target),"open gap inside a tree bounding box remains visible, no oversized invisible cover");
            Object.DestroyImmediate(gapTree);Object.DestroyImmediate(gapMesh);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform);wall.transform.position=(from+target)*.5f;wall.transform.localScale=new Vector3(3,3,.4f);Physics.SyncTransforms();
            Check(!(bool)Call(chapter,"EvacuationBeamSeesPlayer",guard,from,(target-from).normalized),"solid cover blocks flashlight detection");Object.DestroyImmediate(wall);
            Prop(motor,"IsProne",true);body.enabled=false;pawn.transform.position=guard.transform.position+Vector3.forward*4;body.enabled=true;Physics.SyncTransforms();
            target=pawn.transform.position+Vector3.up*.4f;
            Check(!(bool)Call(chapter,"EvacuationBeamSeesPlayer",guard,from,(target-from).normalized),"lying prone avoids patrol light detection");Prop(motor,"IsProne",false);
            pawn.transform.position=new Vector3(-70,1,20);Physics.SyncTransforms();
            chapter.State.night2EvacuationPatrolTriggered=true;
            chapter.ControlsEvacuationUnit(guard,guardAgent);
            Check(guardAgent.hasPath||guardAgent.pathPending,"field NPC marches toward its formation endpoint");
            var ends=(Vector3[])typeof(Map02ChapterDirector).GetField("evacuationMarchEnd",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter);
            for(int i=0;i<5;i++)units[i].GetComponent<NavMeshAgent>().Warp(ends[i]);
            chapter.ControlsEvacuationUnit(guard,guardAgent);
            Call(chapter,"TickEvacuationFieldPatrol",0f);
            Check(chapter.State.night2EvacuationPatrolAtEnd,"formation stops when all five arrive");
            for(int i=0;i<5;i++)chapter.ControlsEvacuationUnit(units[i],units[i].GetComponent<NavMeshAgent>());
            var lights=(Light[])typeof(Map02ChapterDirector).GetField("evacuationLights",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter);
            Check(lights.All(l=>l!=null&&l.range==10&&l.spotAngle==60),"five real spotlights use specified range and cone");
            Check(units.All(u=>u.GetComponent<NavMeshAgent>().isStopped),"all five stay stationary while scanning");
            // One walking guard scans sideways; all other walking guards keep detection off.
            foreach(var menu in Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None)){Set(menu,"visible",false);menu.enabled=false;}
            Prop(mission,"Ready",true);mission.World.story.stage=Map02Stage.QuietPreparation;
            var hp=pawn.AddComponent<Health>();var combat=pawn.AddComponent<PlayerCombat>();Set(chapter,"health",hp);Set(chapter,"combat",combat);
            var sideGuard=units[Map02ChapterDirector.EvacuationSideGuard];
            chapter.State.night2EvacuationPatrolDirection=-1;
            chapter.State.night2EvacuationPatrolAtEnd=false;
            foreach(var unit in units)chapter.ControlsEvacuationUnit(unit,unit.GetComponent<NavMeshAgent>());
            chapter.SyncEvacuationPatrolLights();
            Check(lights.Count(l=>l.enabled)==1&&lights[Map02ChapterDirector.EvacuationSideGuard].enabled,"exactly one guard scans while marching");
            var sideDirection=(Vector3)Call(chapter,"EvacuationFacing",Map02ChapterDirector.EvacuationSideGuard);
            var towardBush=chapter.EvacuationChoicePoint(1)-(layout.nightEvacuationPatrolStart+layout.nightEvacuationPatrolEnd)*.5f;
            Check(Vector3.Dot(sideDirection.normalized,towardBush.normalized)>.6f,"walking sweep faces the side containing Nam's observation bush");
            foreach(var other in units)if(other!=guard)other.gameObject.SetActive(false);
            guardAgent.Warp(ends[0]);var facing=(layout.nightEvacuationPatrolEnd-layout.nightEvacuationPatrolStart).normalized;
            pawn.transform.position=guard.transform.position+facing*5;Physics.SyncTransforms();
            chapter.State.night2EvacuationPatrolAtEnd=false;
            chapter.ControlsEvacuationUnit(guard,guardAgent);chapter.SyncEvacuationPatrolLights();
            Check(!mission.World.story.failed&&!lights[0].enabled,"walking phase cannot detect player before scanning starts");
            chapter.State.night2EvacuationPatrolAtEnd=true;chapter.ControlsEvacuationUnit(guard,guardAgent);guardAgent.velocity=Vector3.zero;
            var emitter=guard.GetComponent<Map02PatrolFlashlight>()?.Emitter;
            Check(emitter!=null,"detection regression uses real hand-held flashlight emitter");
            emitter.SetPositionAndRotation(guard.transform.position+Vector3.up*1.5f,Quaternion.LookRotation(-facing));
            chapter.SyncEvacuationPatrolLights();
            Check(lights[0].enabled&&!mission.World.story.failed,"desired scan heading cannot detect while visible flashlight still points away");
            Check(Vector3.Angle(lights[0].transform.forward,emitter.forward)<.01f&&Vector3.Distance(lights[0].transform.position,emitter.position)<.001f,"spotlight position and direction exactly match visible emitter");
            emitter.rotation=Quaternion.LookRotation(facing);chapter.SyncEvacuationPatrolLights();
            Check(chapter.EvacuationCaptureActive&&!mission.World.story.failed&&chapter.Busy&&!chapter.MaySave,"visible beam starts capture cutscene and blocks input/save instead of failing immediately");
            Call(chapter,"CancelEvacuationCapture");
            chapter.State.Complete("N2_04");Call(chapter,"SyncEvacuationPatrol");Check(units.All(u=>!u.gameObject.activeSelf),"field patrol and lights hide after mission completion");
        }
        catch(Exception error){report.Add("FAIL "+error);throw;}
        finally
        {
            if(root!=null){var chapter=root.GetComponent<Map02ChapterDirector>();if(chapter!=null)Object.DestroyImmediate(chapter);Object.DestroyImmediate(root);}
            if(nav.valid)nav.Remove();File.WriteAllLines("evacuation-choices-checks.txt",report);
        }
    }
}
