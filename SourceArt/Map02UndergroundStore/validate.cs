using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using ShadowVale.Map01;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Combat;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
[InitializeOnLoad] public static class UndergroundStoreChecks
{
 const string Key="UndergroundStoreLive";static List<string> report=new();
 static UndergroundStoreChecks(){EditorApplication.update+=Tick;}
 public static void Begin(){SessionState.SetInt(Key,1);EditorApplication.isPlaying=true;}
 static void Tick()
 {
  int step=SessionState.GetInt(Key,0);if(step==0)return;
  if(step==3&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetInt(Key,0);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));return;}
  if(step!=1||!EditorApplication.isPlaying)return;SessionState.SetInt(Key,2);
  var driver=new GameObject("Real gameplay input validation").AddComponent<UndergroundStoreInputDriver>();driver.Scenario=Run();
  driver.Completed=error=>{if(error!=null){Debug.LogException(error);File.AppendAllText("underground-store-checks.txt","\nFAIL "+error);}SessionState.SetInt(Key+"Exit",error==null?0:1);SessionState.SetInt(Key,3);EditorApplication.isPlaying=false;};
 }
 static void Check(bool ok,string s){if(!ok)throw new Exception(s);report.Add("PASS "+s);}
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
 static void Prop(object o,string n,object v)=>o.GetType().GetProperty(n).SetValue(o,v);
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,a);
 static void Place(CharacterController b,Vector3 p){bool on=b.enabled;b.enabled=false;b.transform.position=p;b.enabled=on;Physics.SyncTransforms();if(on)b.Move(Vector3.down*.12f);}
 public static IEnumerator Run()
 {
  Time.timeScale=1;foreach(var menu in Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None)){Set(menu,"visible",false);Set(menu,"quitAuthorized",true);menu.enabled=false;}
  var nav=NavMesh.AddNavMeshData(AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Map2NavMesh.asset"));var root=new GameObject("Blender store live validation");
  try
  {
   var ground=new GameObject("Continuous ground with meandering canal");ground.transform.SetParent(root.transform);var original=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Map2Ground.asset");ground.AddComponent<MeshFilter>().sharedMesh=original;var gc=ground.AddComponent<MeshCollider>();gc.sharedMesh=original;
   var pawn=new GameObject("Nam");pawn.transform.SetParent(root.transform);var cc=pawn.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.3f;cc.stepOffset=.3f;
   var visual=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Player/Player.fbx"),pawn.transform);var animator=visual.GetComponentInChildren<Animator>();Check(animator!=null&&animator.isHuman,"actual Nam humanoid rig attached to traversal probe");
   var motor=pawn.AddComponent<PlayerController>();motor.enabled=false;var hp=pawn.AddComponent<Health>();hp.SetMaxHealth(100);
   var mission=root.AddComponent<Map02Mission>();mission.enabled=false;Set(mission,"player",motor);Set(mission,"health",hp);Prop(mission,"Ready",true);mission.World.story.stage=Map02Stage.QuietPreparation;Set(mission,"dialogue",new Map02ConversationSession(mission.World.story));
   var c=root.AddComponent<Map02ChapterDirector>();c.enabled=false;Prop(c,"Mission",mission);Prop(mission,"Chapter",c);Set(c,"root",root);Set(c,"player",motor);Set(c,"health",hp);
   var layout=ScriptableObject.CreateInstance<Map02ChapterLayout>();Prop(c,"Layout",layout);
   foreach(var t in Map02ChapterDefinition.Load().tasks){Check(c.State.Complete(t.id),"fixture progression "+t.id);if(t.id=="N2_02")break;}
   var sites=(Dictionary<string,Transform>)typeof(Map02ChapterDirector).GetField("sites",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);
   foreach(var name in Map02ChapterDefinition.Load().tasks.Select(t=>t.site).Concat(new[]{"nightHatch","nightPatrol","dayOneFamily","nightOneRest","family0","family1","family2","scout","southStore","arrival"}).Distinct()){var g=new GameObject(name).transform;g.SetParent(root.transform);g.position=name=="nightHatch"?new Vector3(-3.96654f,.809f,-51.858f):Vector3.zero;sites[name]=g;}
   var roles=(Dictionary<string,Map02ChapterActor>)typeof(Map02ChapterDirector).GetField("roles",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);foreach(string name in new[]{"Tâm","Hùng","Anh Thành","Anh Lực","Chị Lành"}){var a=new GameObject(name).AddComponent<Map02ChapterActor>();a.transform.SetParent(root.transform);a.role=name;roles[name]=a;}
   var markers=(Dictionary<string,Map02ObjectiveObject>)typeof(Map02ChapterDirector).GetField("markers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);foreach(var task in Map02ChapterDefinition.Load().tasks){var m=new GameObject(task.id).AddComponent<Map02ObjectiveObject>();m.transform.SetParent(root.transform);m.chapterId=task.id;m.standPoint=m.transform;m.radius=3;markers[task.id]=m;}
   var field=new GameObject("Young rice around hatch").AddComponent<VillageRiceInstances>();field.transform.SetParent(root.transform);field.detailedMesh=new Mesh{name="Rice_Young_Validation"};var origin=sites["nightHatch"].position;field.plants=new[]{new Vector4(origin.x,origin.y,origin.z,0),new Vector4(origin.x+5,origin.y,origin.z,0)};field.sizes=new[]{1f,1f};field.Rebuild();
   var prefab=Resources.Load<GameObject>("Story/NightStore/UndergroundStore");Check(prefab!=null,"actual Blender prefab available through Resources");var go=Object.Instantiate(prefab,root.transform);go.transform.position=sites["nightHatch"].position;var store=go.AddComponent<Map02UndergroundStore>();store.enabled=false;Prop(c,"UndergroundStore",store);store.Initialize(c);Check(field.plants.Length==1,"rice cleared from aperture without clearing surrounding field");
   Check(store.GetComponentsInChildren<Light>().Length==2,"exactly two ceiling oil lights");Check(store.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Shelf"))==3,"exactly three Blender shelf units");
   var leaf=store.GetComponentsInChildren<Renderer>().First(r=>r.name=="HatchLeaf");var cover=store.GetComponentsInChildren<BoxCollider>().First(b=>b.name=="Single wooden hatch leaf");report.Add("lid mesh="+leaf.bounds+" collider="+cover.bounds);Check(Vector3.ProjectOnPlane(leaf.bounds.center-cover.bounds.center,Vector3.up).magnitude<.1f,"single wooden lid collider aligns with imported mesh");
   Check(gc.sharedMesh!=original,"runtime terrain has independent aperture mesh");Check(!gc.Raycast(new Ray(go.transform.position+Vector3.up,Vector3.down),out _,2),"actual soil removed only under 2 x 3 m mouth");Check(gc.Raycast(new Ray(go.transform.position+Vector3.right*2+Vector3.up*2,Vector3.down),out _,4),"soil outside mouth remains intact");Check(gc.sharedMesh.uv.Length==gc.sharedMesh.vertexCount&&gc.sharedMesh.colors.Length==gc.sharedMesh.vertexCount,"terrain UVs and soil colours preserved");
   Place(cc,store.TopStand);Check(!store.TryEnterLadder(),"closed hatch rejects Space entry");Check(!store.CanInspect,"surface interaction cannot finish warehouse inspection");
   c.State.completed.Remove("N2_02");store.TryDoorInteraction();Check(!store.IsOpen,"hatch locked before observation radio report");c.State.completed.Add("N2_02");Check(store.TryDoorInteraction()&&store.IsOpen,"E opens one wooden hatch after mission gate");
   Check(!store.TryEnterLadder(),"Space waits until hatch has physically opened");for(int i=0;i<250;i++)Call(store,"Update");Physics.SyncTransforms();Place(cc,go.transform.position+Vector3.up*.7f);
   for(int i=0;i<100;i++)cc.Move(Vector3.down*.06f);
   Check(pawn.transform.position.y>go.transform.position.y-.2f&&!store.Inside,"open mouth blocks dropping/jumping without Space");
   Place(cc,store.TopStand+Vector3.forward*2);Check(!store.TryEnterLadder(),"Space outside fixed entry spot cannot start ladder");Place(cc,store.TopStand);Check(store.TryEnterLadder()&&store.IsClimbing&&!cc.enabled,"Space at marked spot takes exclusive ladder control");Check(store.FallGuard.enabled,"permanent fall guard remains active during controlled traversal");Check(!mission.CanSave(),"saving blocked halfway down ladder");
   Call(store,"LateUpdate");var hand=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation;Call(store,"LateUpdate");Check(Quaternion.Angle(hand,animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation)<.01f,"actual Nam climb pose does not accumulate rotation when stationary");
   for(int i=0;i<7;i++)store.StepTraversal(.1f,false,false);Call(store,"LateUpdate");var grip=store.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftHand).position);Check(Mathf.Abs(grip.x+.72f)<.18f&&Mathf.Abs(grip.z+.29f)<.18f,"actual Nam hand reaches ladder rung rather than empty space");float start=pawn.transform.position.y;for(int i=0;i<10;i++)store.StepTraversal(.1f,false,false);Check(Mathf.Abs(pawn.transform.position.y-start)<.001f,"no automatic descent without S");
   for(int i=0;i<90&&store.IsClimbing;i++)store.StepTraversal(.1f,true,false);
   Check(store.Inside&&!store.IsClimbing&&cc.enabled,"S descends seven metres and releases control on floor");Check(Mathf.Abs(pawn.transform.position.y-(go.transform.position.y-7))<.15f,"landing is exactly seven metres below surface");Check(!c.State.Done("N2_03"),"opening/descending alone does not complete inspection");
   int recoveries=mission.BoundaryRecoveryCount;Call(mission,"TickMapSafety");Check(mission.BoundaryRecoveryCount==recoveries&&store.Inside,"map safety does not pull authorized underground player to surface");
   Place(cc,store.InspectionPoint);Check(store.CanInspect,"inspection requires reaching actual shelves inside room");var snapshot=Map02Mission.Parse(JsonUtility.ToJson(mission.Capture()));Check(snapshot.chapter.night2InsideStore&&snapshot.position.y< -5,"complete mission snapshot parser accepts underground save");var saved=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(c.State));saved.Validate();Check(saved.night2InsideStore&&saved.night2HatchOpen,"interior/hatch states survive serialized save round-trip");Prop(c,"State",saved);store.SyncState();Check(store.Inside,"loading inside restores authorized underground position");
   Place(cc,store.transform.TransformPoint(new Vector3(-.5f,-7,.1f)));var oldEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;var oldBackground=InputSystem.settings.backgroundBehavior;var oldUpdate=InputSystem.settings.updateMode;
   InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
   var keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.EnableDevice(keyboard);
   try
   {
    keyboard.MakeCurrent();var warmup=keyboard.spaceKey.wasPressedThisFrame;
    InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;warmup=keyboard.spaceKey.wasPressedThisFrame;
    InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Space));yield return null;keyboard.MakeCurrent();
    uint jumped=motor.JumpSequence;Call(motor,"Update");Check(store.IsClimbing&&!cc.enabled,"real Space at position against ladder attaches before player jump");Check(motor.JumpSequence==jumped,"Space consumed by ladder does not trigger jump");
    InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.W));yield return null;keyboard.MakeCurrent();
    for(int i=0;i<2500&&store.IsClimbing;i++){Call(store,"Update");Call(store,"LateUpdate");yield return null;}
    Check(!store.IsClimbing&&!store.Inside&&cc.enabled&&Vector3.Distance(pawn.transform.position,store.TopStand)<.15f,"real W input climbs back to marked surface exit");
   }
   finally{InputSystem.RemoveDevice(keyboard);InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorInput;InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.updateMode=oldUpdate;}
   Check(store.TryDoorInteraction()&&!store.IsOpen,"E closes hatch after exiting");Check(store.FallGuard.enabled,"fall prevention stays active with hatch closed");
   Check(c.State.Complete("N2_03"),"inspection task can unlock onward evacuation route");Check(!c.NightBankMovementRestricted,"completed store inspection releases river-bank movement bounds");
   c.State.ResetPatrolRoute();store.SyncState();Check(!store.IsOpen&&!store.Inside&&c.NightBankMovementRestricted,"patrol retry closes hatch and restores mission bounds");
   Capture(store,ground,pawn);
   var before=gc.sharedMesh;Object.DestroyImmediate(go);Check(gc.sharedMesh==original,"destroying store restores original terrain asset reference");Check(field.plants.Length==2,"destroying store restores crop instances");Check(motor.JumpInteraction==null,"destroying store restores normal Space jump interaction");Object.DestroyImmediate(layout);
  }
  finally{File.WriteAllLines("underground-store-checks.txt",report);Object.DestroyImmediate(root);nav.Remove();}
 }
 static void Capture(Map02UndergroundStore store,GameObject ground,GameObject pawn)
 {
  if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
  foreach(var r in pawn.GetComponentsInChildren<Renderer>())r.enabled=false;
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.13f,.12f,.10f);
  var mr=ground.AddComponent<MeshRenderer>();mr.sharedMaterial=new Material(Shader.Find("Standard")){color=new Color(.24f,.20f,.12f)};
  var camera=new GameObject("Unity authored store preview").AddComponent<Camera>();camera.fieldOfView=78;camera.nearClipPlane=.05f;camera.backgroundColor=new Color(.06f,.065f,.07f);camera.clearFlags=CameraClearFlags.SolidColor;
  var rt=new RenderTexture(1280,900,24);camera.targetTexture=rt;var prior=RenderTexture.active;
  try
  {
   camera.transform.position=store.transform.TransformPoint(new Vector3(.05f,-5.2f,1));camera.transform.LookAt(store.transform.TransformPoint(new Vector3(3.5f,-4.8f,1)));camera.Render();RenderTexture.active=rt;
   var texture=new Texture2D(1280,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,900),0,0);texture.Apply();File.WriteAllBytes("store-unity-interior.png",texture.EncodeToPNG());Object.DestroyImmediate(texture);report.Add("PASS Unity framebuffer captured from actual imported Blender model");
  }
  finally{RenderTexture.active=prior;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);}
 }
}
