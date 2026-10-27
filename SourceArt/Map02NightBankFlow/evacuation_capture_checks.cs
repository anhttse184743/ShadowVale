using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

[InitializeOnLoad] public static class EvacuationCaptureChecks
{
    const string Key="EvacuationCaptureLive";
    static readonly List<string> report=new List<string>();
    static GameObject root;static NavMeshDataInstance nav;static Map02ChapterDirector c;static Map02Mission m;
    static Animator nam;static Health health;static float began;static int frame=-1;static Vector3 cameraStart;static bool checkedCamera,checkedPin;
    static EvacuationCaptureChecks(){EditorApplication.update+=Tick;}
    public static void BeginLive(){SessionState.SetInt(Key,1);EditorApplication.isPlaying=true;}
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
    static object Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
    static void Prop(object o,string n,object v)=>o.GetType().GetProperty(n).SetValue(o,v);
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);report.Add("PASS "+text);}
    static void Tick()
    {
        int phase=SessionState.GetInt(Key,0);if(phase==0)return;
        if(phase==3&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetInt(Key,0);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));return;}
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(phase==1){EvacuationChoicesChecks.Run();Setup();SessionState.SetInt(Key,2);return;}
            if(phase!=2||frame==Time.frameCount)return;frame=Time.frameCount;Step();
        }
        catch(Exception error){report.Add("FAIL "+error);Debug.LogException(error);Finish(1);}
    }
    static void Setup()
    {
        root=new GameObject("Actual capture scene validation");
        nav=NavMesh.AddNavMeshData(AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Map2NavMesh.asset"));
        var layout=ScriptableObject.CreateInstance<Map02ChapterLayout>();
        var pawn=new GameObject("Nam");pawn.transform.SetParent(root.transform);pawn.AddComponent<CharacterController>();var motor=pawn.AddComponent<PlayerController>();motor.enabled=false;
        var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Player/Player.fbx"),pawn.transform);nam=model.GetComponentInChildren<Animator>();
        var controller=AnimatorController.CreateAnimatorControllerAtPath("Assets/CaptureNam.controller");controller.AddMotion(Resources.Load<AnimationClip>("Story/TamEscort/NamIdle"));
        var tie=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Animations/Nam/Nam_Untie.fbx").OfType<AnimationClip>().First(clip=>!clip.name.StartsWith("__"));controller.AddMotion(tie);nam.runtimeAnimatorController=controller;nam.applyRootMotion=false;nam.Rebind();nam.Update(0);
        var temp=ScriptableObject.CreateInstance<NamProneAnimationData>();var script=MonoScript.FromScriptableObject(temp);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script,out string guid,out long fileId);Object.DestroyImmediate(temp);
        string prone=File.ReadAllText("../../Assets/_Project/Map01/Resources/Animations/NamProne.asset");prone=Regex.Replace(prone,@"m_Script: \{[^}]+\}","m_Script: {fileID: "+fileId+", guid: "+guid+", type: 3}");
        Directory.CreateDirectory("Assets/Resources/Animations");File.WriteAllText("Assets/Resources/Animations/NamProne.asset",prone);AssetDatabase.ImportAsset("Assets/Resources/Animations/NamProne.asset",ImportAssetOptions.ForceSynchronousImport);
        health=pawn.AddComponent<Health>();health.SetMaxHealth(200);var combat=pawn.AddComponent<PlayerCombat>();
        m=root.AddComponent<Map02Mission>();m.enabled=false;Set(m,"player",motor);
        c=root.AddComponent<Map02ChapterDirector>();c.enabled=false;Prop(c,"Mission",m);Prop(m,"Chapter",c);Prop(c,"Layout",layout);Set(c,"player",motor);Set(c,"health",health);Set(c,"combat",combat);Set(c,"root",root);
        foreach(var task in Map02ChapterDefinition.Load().tasks){c.State.Complete(task.id);if(task.id=="N2_03")break;}
        Call(c,"BuildEvacuationChoices");Call(c,"SyncEvacuationPatrol");foreach(var unit in c.Enemies)unit.enabled=false;
        pawn.transform.position=c.EvacuationChoicePoint(1);
        var camera=new GameObject("Capture camera");camera.transform.SetParent(root.transform);var rig=camera.AddComponent<ThirdPersonCamera>();rig.enabled=false;
        rig.SetTarget(pawn.transform);cameraStart=pawn.transform.position+new Vector3(-2,1.8f,3);camera.transform.position=cameraStart;camera.transform.LookAt(pawn.transform.position+Vector3.up);Set(c,"rig",rig);
        Call(c,"BeginEvacuationCapture");
        Check(c.EvacuationCaptureActive&&c.Busy&&!c.MaySave&&!m.World.story.failed,"capture starts with input/save lock and no immediate failure");
        Check((AnimationClip)Field(c,"evacuationTieClip")!=null&&(NamProneAnimationData.Take)Field(c,"evacuationProneTake")!=null,"capture resolves actual humanoid knot-working clip and Nam prone pose data");
        Check(Vector3.Distance(rig.transform.position,cameraStart)<.001f,"cutscene begins at current camera without a cut");began=Time.time;
    }
    static void Step()
    {
        Call(c,"TickEvacuationCapture",Time.deltaTime);Call(c,"PresentEvacuationCapture");float elapsed=Time.time-began;
        var rig=(ThirdPersonCamera)Field(c,"rig");Call(rig,"LateUpdate");
        if(!checkedCamera&&elapsed>.8f&&elapsed<2)
        {
            checkedCamera=true;Check(Vector3.Distance(rig.transform.position,cameraStart)>.1f&&Vector3.Distance(rig.transform.position,(Vector3)Field(c,"evacuationCaptureRear"))>.1f,"camera has an intermediate position during slow travel");
            Check(c.Enemies.All(e=>e.GetComponent<NavMeshAgent>().isStopped),"captors wait until camera reaches enemy side");
        }
        float pinAt=(float)Field(c,"evacuationCapturePinAt");
        if(!checkedPin&&pinAt>=0)
        {
            checkedPin=true;Check(pinAt>=3,"pinning begins only after camera travel and actual approach");
            var captors=(Map02ChapterEnemy[])Field(c,"evacuationCaptors");var targets=(Vector3[])Field(c,"evacuationCaptorTargets");
            Check(captors.Select((e,i)=>Vector3.Distance(e.transform.position,targets[i])<.4f).All(v=>v),"captors reach Nam through native NavMesh movement before restraint");
        }
        if(m.World.story.failed)
        {
            Check(checkedCamera&&checkedPin,"camera travel and approach both occurred before failure");
            Check((float)Field(c,"evacuationCaptureClock")-pinAt>=5,"pin and tying beats finish before failure screen");
            var ropes=(GameObject)Field(c,"evacuationRopes");Check(ropes!=null&&ropes.activeSelf&&ropes.GetComponentsInChildren<LineRenderer>().Length==2,"two wrist bindings visible at final restrained pose");
            Check(!health.IsDead&&health.Current==200,"capture failure does not kill or damage Nam");
            Check(nam.GetBoneTransform(HumanBodyBones.LeftHand)!=null,"restrained pose uses real humanoid wrists");
            report.Add("POSE head height="+(nam.GetBoneTransform(HumanBodyBones.Head).position.y-m.Player.position.y).ToString("F3"));
            CapturePosePreview();
            Call(c,"CancelEvacuationCapture");Check(!c.EvacuationCaptureActive&&!health.CinematicInvulnerable,"retry/cancel releases pose and invulnerability");Finish(0);
        }
        else if(elapsed>28)throw new Exception("Capture did not finish within approach timeout");
    }
    static void CapturePosePreview()
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
        var shader=Shader.Find("Unlit/Color");if(shader==null)return;
        var namMaterial=new Material(shader){color=new Color(.2f,.55f,.8f)};var enemyMaterial=new Material(shader){color=new Color(.5f,.55f,.25f)};
        foreach(var renderer in nam.GetComponentsInChildren<SkinnedMeshRenderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>namMaterial).ToArray();
        foreach(var unit in c.Enemies)foreach(var renderer in unit.GetComponentsInChildren<SkinnedMeshRenderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>enemyMaterial).ToArray();
        var camera=((ThirdPersonCamera)Field(c,"rig")).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.15f,.17f);
        var rt=new RenderTexture(1000,750,24);var previous=RenderTexture.active;
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1000,750,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1000,750),0,0);image.Apply();image.EncodeToPNG();File.WriteAllBytes("evacuation-capture-pose.png",image.EncodeToPNG());Object.DestroyImmediate(image);
        }
        finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(rt);Object.Destroy(namMaterial);Object.Destroy(enemyMaterial);}
    }
    static void Finish(int code)
    {
        File.WriteAllLines("evacuation-capture-checks.txt",report);
        if(root!=null){if(c!=null)Object.DestroyImmediate(c);Object.DestroyImmediate(root);}if(nav.valid)nav.Remove();
        SessionState.SetInt(Key+"Exit",code);SessionState.SetInt(Key,3);EditorApplication.isPlaying=false;
    }
}
