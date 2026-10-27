using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad] public static class EscortPlayChecks
{
    const string Key="EscortLiveCheck";static EscortLiveDriver driver;static double started;
    static EscortPlayChecks(){EditorApplication.update+=Tick;}
    public static void Begin(){SessionState.SetInt(Key,1);EditorApplication.isPlaying=true;}
    static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
    static void Prop(object obj,string property,object value)=>obj.GetType().GetProperty(property).SetValue(obj,value);
    static void Tick()
    {
        int phase=SessionState.GetInt(Key,0);if(phase==0)return;
        if(phase==3&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetInt(Key,0);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));return;}
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(phase==1){Setup();SessionState.SetInt(Key,2);started=EditorApplication.timeSinceStartup;return;}
            if(driver!=null&&driver.done)Finish(driver.error);
            else if(EditorApplication.timeSinceStartup-started>45)Finish("Play Mode timed out.");
        }catch(Exception e){Finish(e.ToString());}
    }
    static void Finish(string error)
    {
        File.WriteAllText("live-checks.txt",string.IsNullOrEmpty(error)?"PASS 110 actual Play Mode frames\nPASS both knee joints animate while moving: Nam="+driver.namMotion+" degrees; Tam="+driver.tamMotion+" degrees\nPASS gait remains active when PlanarSpeed is zero\nPASS approved shoulder pose overrides earlier animation layers\nPASS root moved "+driver.travel+" m at configured walk speed "+driver.motor.WalkSpeed:"FAIL "+error);
        if(string.IsNullOrEmpty(error))File.AppendAllText("live-checks.txt","\nPASS actual CharacterController measured normal walk="+driver.measuredWalk+" m/s; assisted walk="+driver.measuredEscort+" m/s (requested 1.8 m/s)\n");
        SessionState.SetInt(Key+"Exit",string.IsNullOrEmpty(error)?0:1);SessionState.SetInt(Key,3);EditorApplication.isPlaying=false;
    }
    static void Setup()
    {
        foreach(var menu in Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None)){Set(menu,"visible",false);Set(menu,"quitAuthorized",true);menu.enabled=false;}
        Time.timeScale=1;Application.targetFrameRate=60;
        var root=new GameObject("Live shoulder assist");var pawn=new GameObject("Player");pawn.transform.SetParent(root.transform);
        var motor=pawn.AddComponent<PlayerController>();motor.enabled=false;Set(motor,"walkSpeed",2.5f);
        var health=pawn.AddComponent<Health>();health.SetMaxHealth(100);var combat=pawn.AddComponent<PlayerCombat>();combat.enabled=false;
        var mission=root.AddComponent<Map02Mission>();mission.enabled=false;Set(mission,"player",motor);Set(mission,"combat",combat);Set(mission,"health",health);Prop(mission,"Ready",true);
        var nam=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Player/Player.fbx"),pawn.transform);EscortRuntimeChecks.NestedAnimator(nam);
        var tam=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/NPCs/Anh Tâm.fbx"));EscortRuntimeChecks.NestedAnimator(tam);
        var actor=new GameObject("Tâm slot").AddComponent<Map02ChapterActor>();actor.role="Tâm";actor.Bind(tam.transform);actor.PairedEscort=true;
        var chapter=root.AddComponent<Map02ChapterDirector>();chapter.enabled=false;Set(chapter,"player",motor);Set(chapter,"combat",combat);Set(chapter,"escort",actor);Prop(chapter,"Mission",mission);Prop(mission,"Chapter",chapter);chapter.State.activeTask="D2_04";
        ((Dictionary<string,Map02ChapterActor>)typeof(Map02ChapterDirector).GetField("roles",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter))["Tâm"]=actor;
        var camera=new GameObject("Camera").AddComponent<Camera>();camera.tag="MainCamera";
        var visual=root.AddComponent<Map02ScoutEscortVisual>();visual.Bind(chapter);
        driver=root.AddComponent<EscortLiveDriver>();driver.player=pawn.transform;driver.motor=motor;
        driver.namKnee=nam.GetComponentsInChildren<Transform>().First(t=>t.name=="LeftLeg");driver.tamKnee=tam.GetComponentsInChildren<Transform>().First(t=>t.name=="LeftLeg");driver.tamArm=tam.GetComponentsInChildren<Transform>().First(t=>t.name=="RightArm");
    }
}
