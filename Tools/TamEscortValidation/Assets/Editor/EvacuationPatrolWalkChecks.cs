using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

[InitializeOnLoad] public static class EvacuationPatrolWalkChecks
{
    const string Key="EvacuationPatrolWalk";
    static GameObject root;
    static NavMeshDataInstance nav;
    static Map02ChapterDirector chapter;
    static Map02ChapterEnemy[] units;
    static Vector3 center,axis;
    static Texture2D road;
    static float began,stoppedAt;
    static int lastFrame=-1,stops;
    static bool wasStopped;
    static readonly List<string> report=new List<string>();
    static EvacuationPatrolWalkChecks(){EditorApplication.update+=Tick;}
    public static void BeginLive(){SessionState.SetInt(Key,1);EditorApplication.isPlaying=true;}
    static void Set(object o,string n,object value)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
    static void Prop(object o,string n,object value)=>o.GetType().GetProperty(n).SetValue(o,value);
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);report.Add("PASS "+text);}
    static void Tick()
    {
        int phase=SessionState.GetInt(Key,0);if(phase==0)return;
        if(phase==3&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetInt(Key,0);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));return;}
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(phase==1){EvacuationChoicesChecks.Run();Setup();SessionState.SetInt(Key,2);return;}
            if(phase!=2||Time.frameCount==lastFrame)return;
            lastFrame=Time.frameCount;Step();
        }
        catch(Exception error){report.Add("FAIL "+error);Debug.LogException(error);Finish(1);}
    }
    static void Setup()
    {
        root=new GameObject("Live 20 m field patrol test");
        nav=NavMesh.AddNavMeshData(AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Map2NavMesh.asset"));
        var layout=ScriptableObject.CreateInstance<Map02ChapterLayout>();center=(layout.nightEvacuationPatrolStart+layout.nightEvacuationPatrolEnd)*.5f;
        axis=(layout.nightEvacuationPatrolEnd-layout.nightEvacuationPatrolStart).normalized;
        Check(Mathf.Abs(Vector3.ProjectOnPlane(center-layout.nightEvacuationFieldBush,Vector3.up).magnitude-10)<.01f,"patrol midpoint is ten metres from the field objective");
        Check(Mathf.Abs(Vector3.Distance(layout.nightEvacuationPatrolStart,layout.nightEvacuationPatrolEnd)-20)<.01f,"route has minus-ten and plus-ten metre endpoints");
        road=new Texture2D(2,2);road.LoadImage(File.ReadAllBytes("../../Assets/_Project/Art/Environment/Map02_Village/Roadside_Rounded_Lanes.png"));
        for(int i=-10;i<=10;i++)
        {
            var p=center+axis*i;Check(road.GetPixelBilinear((p.x+110)/220,(p.z+100)/200).r>.5f,"road mask contains centre line at "+i+" m");
        }
        var pawn=new GameObject("Nam");pawn.transform.SetParent(root.transform);pawn.AddComponent<CharacterController>();var motor=pawn.AddComponent<PlayerController>();motor.enabled=false;
        var mission=root.AddComponent<Map02Mission>();mission.enabled=false;Set(mission,"player",motor);
        chapter=root.AddComponent<Map02ChapterDirector>();chapter.enabled=false;Prop(chapter,"Mission",mission);Prop(chapter,"Layout",layout);Set(chapter,"player",motor);Set(chapter,"root",root);
        foreach(var t in Map02ChapterDefinition.Load().tasks){chapter.State.Complete(t.id);if(t.id=="N2_03")break;}
        chapter.State.night2EvacuationStarted=true;chapter.State.SelectEvacuationRoute(1);
        Call(chapter,"BuildEvacuationChoices");Call(chapter,"SyncEvacuationPatrol");
        units=chapter.Enemies.Where(u=>u.group==Map02ChapterDirector.EvacuationPatrolGroup).ToArray();Check(units.Length==5,"five guards waiting at middle formation");
        foreach(var unit in units){unit.enabled=false;chapter.ControlsEvacuationUnit(unit,unit.GetComponent<NavMeshAgent>());}
        Check(!chapter.State.night2EvacuationPatrolTriggered&&units.All(u=>u.GetComponent<NavMeshAgent>().isStopped),"patrol waits until Nam reaches the objective");
        Check(Vector3.ProjectOnPlane(units[0].transform.position-center,Vector3.up).magnitude<.1f,"leader initially at zero, not either endpoint");
        pawn.transform.position=chapter.EvacuationChoicePoint(1);Call(chapter,"TickEvacuationFieldPatrol",0f);
        Check(chapter.State.night2EvacuationPatrolTriggered,"reaching field objective triggers departure from zero");
        began=Time.time;
    }
    static void Step()
    {
        if(Time.time-began>65)throw new Exception("Real patrol did not complete three endpoint visits within 65 s: "+string.Join(";",units.Select(u=>u.id+"="+u.transform.position+" velocity="+u.GetComponent<NavMeshAgent>().velocity+" remaining="+u.GetComponent<NavMeshAgent>().remainingDistance)));
        Call(chapter,"TickEvacuationFieldPatrol",Time.deltaTime);
        foreach(var unit in units)chapter.ControlsEvacuationUnit(unit,unit.GetComponent<NavMeshAgent>());
        float along=Vector3.Dot(units[0].transform.position-center,axis);
        if(Mathf.Abs(along)>10.35f)throw new Exception("Patrol escaped twenty-metre segment");
        if(chapter.State.night2EvacuationPatrolAtEnd&&!wasStopped)
        {
            stoppedAt=Time.time;stops++;
            Check(Mathf.Abs(along-10*chapter.State.night2EvacuationPatrolDirection)<.35f,"native agent reached endpoint "+stops+" at "+along.ToString("F2")+" m");
            Check(units.All(u=>u.GetComponent<NavMeshAgent>().isStopped),"all five stop to scan at endpoint "+stops);
            var snapshot=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(chapter.State));snapshot.Validate();
            Check(snapshot.night2EvacuationPatrolTriggered&&snapshot.night2EvacuationPatrolAtEnd&&snapshot.night2EvacuationPatrolDirection==chapter.State.night2EvacuationPatrolDirection,"checkpoint preserves endpoint scan state");
        }
        if(!chapter.State.night2EvacuationPatrolAtEnd&&wasStopped)
        {
            float waited=Time.time-stoppedAt;Check(waited>=2.99f&&waited<3.2f,"scan lasts three gameplay seconds before reversing: "+waited.ToString("F3"));
            Check(units.All(u=>!u.GetComponent<NavMeshAgent>().isStopped),"five real agents resume after reversing");
            if(stops>=3){Finish(0);return;}
        }
        if(!chapter.State.night2EvacuationPatrolAtEnd&&units.Any(u=>Vector3.ProjectOnPlane(u.GetComponent<NavMeshAgent>().velocity,Vector3.up).magnitude>1.7f))throw new Exception("Patrol exceeded walking speed: "+string.Join(";",units.Select(u=>u.id+" velocity="+u.GetComponent<NavMeshAgent>().velocity+" speed="+u.GetComponent<NavMeshAgent>().speed)));
        wasStopped=chapter.State.night2EvacuationPatrolAtEnd;
    }
    static void Finish(int code)
    {
        File.WriteAllLines("evacuation-patrol-walk-checks.txt",report);
        if(root!=null){if(chapter!=null)Object.DestroyImmediate(chapter);Object.DestroyImmediate(root);}
        if(nav.valid)nav.Remove();if(road!=null)Object.DestroyImmediate(road);
        SessionState.SetInt(Key+"Exit",code);SessionState.SetInt(Key,3);EditorApplication.isPlaying=false;
    }
}
