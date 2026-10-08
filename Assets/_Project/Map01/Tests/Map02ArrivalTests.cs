using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ShadowVale.Gameplay.Combat;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace ShadowVale.Map01.Tests {
public sealed class Map02ArrivalTests:ForestSceneTestBase {
[UnityTest]public IEnumerator CompletedCheckpointTransfersVisibleBoatAndKeepsRowersHandsFree() {
EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;
var mission=Object.FindFirstObjectByType<Map01Mission>();
string storage=Path.GetFullPath("Logs/Map02/saves/"+System.Guid.NewGuid().ToString("N"));Directory.CreateDirectory(storage);
typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,storage);
// A completed checkpoint has not played StartDeparture: Prepare creates the
// seated guards with rifles. Arrival must establish its own prop state.
mission.GetComponent<Map01Quest>().RestoreStage(Map01Quest.CompleteStage);
var extraction=Map01Extraction.Get(mission);extraction.autoContinueToMap2=false;extraction.ContinueToMap2();
float deadline=Time.realtimeSinceStartup+90;
while(SceneManager.GetActiveScene().name!="Map 2"&&Time.realtimeSinceStartup<deadline)yield return null;
var arrival=Object.FindFirstObjectByType<Map02Arrival>();Assert.NotNull(arrival);
while(arrival.CurrentPhase==Map02Arrival.Phase.Loading&&Time.realtimeSinceStartup<deadline)yield return null;
Assert.AreEqual(Map02Arrival.Phase.Approaching,arrival.CurrentPhase);
yield return WaitGameSeconds(.3f);
Directory.CreateDirectory("Logs/Map02/Checkpoint");Capture(Camera.main,"Logs/Map02/Checkpoint/approach.png");
var hull=arrival.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="Moored wooden sampan");
var renderer=hull.GetComponent<MeshRenderer>();
Assert.IsTrue(renderer.enabled&&renderer.gameObject.activeInHierarchy&&!renderer.forceRenderingOff);
Assert.Less(Vector3.Distance(renderer.bounds.center,arrival.transform.position),1,"The visible hull must move with its passengers.");
Assert.IsFalse(arrival.Passengers[2].GetComponentsInChildren<Weapon>(true).Any(w=>w.IsGun&&w.gameObject.activeInHierarchy),"Hung must have two free hands for the paddle, even after loading a completed checkpoint.");
LogAssert.Expect(LogType.Log,"Map 2 arrival complete. Party on dry bank; inventory, health and magazine retained.");arrival.Skip();
yield return WaitGameSeconds(.2f);AssertDryPartySpawn(arrival);VerifyGameplayRifle(arrival.Passengers[0]);
Capture(Camera.main,"Logs/Map02/Checkpoint/gameplay.png");yield return new ExitPlayMode();
}
[Test]public void StairContactsGatherBothFeetAndKeepSupportPlanted() {
for(int side=0;side<2;side++) {
Assert.Less(Vector2.Distance(Map02Arrival.StairContact(0,side),new Vector2(0,-.15f)),.001f);
Assert.Less(Vector2.Distance(Map02Arrival.StairContact(1,side),new Vector2(-2.35f,1.01f)),.001f);
}
for(int step=0;step<8;step++) {
int support=1-step%2;
Assert.Less(Vector2.Distance(Map02Arrival.StairContact((step+.1f)/8,support),Map02Arrival.StairContact((step+.9f)/8,support)),.001f);
}
foreach(var who in new[]{"Nam","Hung"})foreach(var take in new[]{"Arrival_Row_Loop","Arrival_Travel","Boat_Stand","Boat_Turn","Oar_Stow","Standing_Guard"}) {
var clip=Resources.Load<AnimationClip>("Cutscenes/Map02/"+who+"_"+take);Assert.NotNull(clip);Assert.IsTrue(clip.humanMotion);Assert.AreEqual(30,clip.frameRate);
}
}
[Test] public void ShoreAndStairClipsContainLegMovement() {
foreach(var who in new[]{"Nam","Hung"})foreach(var suffix in new[]{"Walk_Ashore","Jetty_StepUp"}) {
var clip=Resources.Load<AnimationClip>("Cutscenes/Map02/"+who+"_"+suffix);Assert.NotNull(clip);
var bindings=UnityEditor.AnimationUtility.GetCurveBindings(clip);
var legs=bindings.Where(b=>b.propertyName.Contains("Leg Front-Back"));
Assert.IsTrue(legs.Any(b=>{var keys=UnityEditor.AnimationUtility.GetEditorCurve(clip,b).keys;return keys.Max(k=>k.value)-keys.Min(k=>k.value)>.1f;}),who+" "+suffix+" must move the legs, not repeat a frozen retarget pose.");
if(suffix=="Walk_Ashore")Assert.IsTrue(clip.isLooping);
}
}
[UnityTest] public IEnumerator NaturalArrivalPreservesPartyAndEquipment() {
EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;
Directory.CreateDirectory("Logs/Map02/frames");
// Reproduce the normal editor setting; arrival itself must guard shader compilation.
UnityEditor.EditorSettings.asyncShaderCompilation=true;UnityEditor.ShaderUtil.allowAsyncCompilation=true;
string storage=Path.GetFullPath("Logs/Map02/saves/"+System.Guid.NewGuid().ToString("N"));Directory.CreateDirectory(storage);
typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,storage);
var mission=Object.FindFirstObjectByType<Map01Mission>();
mission.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ExtractionStage);
var extraction=mission.GetComponentInChildren<Map01Extraction>();extraction.autoContinueToMap2=false;
foreach(var enemy in mission.Enemies)enemy.GetComponent<Health>().TakeDamage(99999,enemy.transform.position,null);
var originalPlayer=mission.player;var originalHung=mission.hung;
var cc=mission.player.GetComponent<CharacterController>();cc.enabled=false;mission.player.position=extraction.approach;cc.enabled=true;
yield return WaitGameSeconds(.3f);extraction.Skip();
mission.GetComponent<Map01Inventory>().Add("ammo_rifle",123);
mission.GetComponent<Map01Inventory>().Add("cloth",4);
mission.ModernCombat.RestoreMagazine(17);mission.ModernHealth.RestoreHealth(72);
int ammo=mission.ModernCombat.RoundsInMagazine;float hp=mission.ModernHealth.Current;
int reserve=mission.GetComponent<Map01Inventory>().Count("ammo_rifle");
extraction.autoContinueToMap2=true;
yield return WaitGameSeconds(2);
Assert.AreEqual("Map 1",SceneManager.GetActiveScene().name,"Completion screen must remain visible before transfer.");
float timeout=Time.realtimeSinceStartup+90;
while(SceneManager.GetActiveScene().name!="Map 2" && Time.realtimeSinceStartup<timeout)yield return null;
var arrival=Object.FindFirstObjectByType<Map02Arrival>();Assert.NotNull(arrival);
while(arrival.CurrentPhase==Map02Arrival.Phase.Loading && Time.realtimeSinceStartup<timeout)yield return null;
Assert.AreEqual(Map02Arrival.Phase.Approaching,arrival.CurrentPhase);
Assert.IsFalse(UnityEditor.EditorSettings.asyncShaderCompilation);
Assert.AreSame(originalPlayer,arrival.Passengers[0]);Assert.AreSame(originalHung,arrival.Passengers[2]);
LogAssert.Expect(LogType.Log,"Map 2 arrival complete. Party on dry bank; inventory, health and magazine retained.");
yield return RecordArrival(arrival);
Assert.AreEqual(Map02Arrival.Phase.Gameplay,arrival.CurrentPhase);
Assert.IsTrue(UnityEditor.EditorSettings.asyncShaderCompilation,"Restore the editor preference after arrival.");
VerifyGameplayRifle(originalPlayer);
Assert.AreEqual(ammo,originalPlayer.GetComponent<PlayerCombat>().RoundsInMagazine);
Assert.AreEqual(hp,originalPlayer.GetComponent<Health>().Current);
Assert.AreEqual(reserve,arrival.Inventory["ammo_rifle"]);
Assert.AreEqual(4,arrival.Inventory["cloth"]);
Assert.AreEqual(1,Object.FindObjectsByType<Map02Arrival>(FindObjectsSortMode.None).Length);
AssertDryPartySpawn(arrival);
Assert.IsTrue(originalPlayer.GetComponent<CharacterController>().enabled);
ShotTracerLifecycleTests.FireAfterArrival(originalPlayer.GetComponent<PlayerCombat>());
yield return WaitGameSeconds(.5f);Capture(Camera.main,"Logs/Map02/gameplay.png");yield return new ExitPlayMode();
}
[UnityTest]public IEnumerator SkippedArrivalUsesSameDrySpawn() {
EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;
var mission=Object.FindFirstObjectByType<Map01Mission>();mission.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ExtractionStage);
var extraction=mission.GetComponentInChildren<Map01Extraction>();extraction.autoContinueToMap2=false;
mission.player.GetComponent<CharacterController>().enabled=false;mission.player.position=extraction.approach;mission.player.GetComponent<CharacterController>().enabled=true;
yield return WaitGameSeconds(.3f);extraction.Skip();extraction.ContinueToMap2();
while(SceneManager.GetActiveScene().name!="Map 2")yield return null;
var arrival=Object.FindFirstObjectByType<Map02Arrival>();while(arrival.CurrentPhase==Map02Arrival.Phase.Loading)yield return null;
var routing=RouteInputToGame();var mouse=InputSystem.AddDevice<Mouse>();
InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));InputSystem.Update();
LogAssert.Expect(LogType.Log,"Map 2 arrival complete. Party on dry bank; inventory, health and magazine retained.");
arrival.Skip();arrival.Skip();
yield return WaitGameSeconds(.2f);
Assert.IsTrue(arrival.Passengers[0].GetComponent<PlayerCombat>().IsAiming,"Held aim must survive cinematic handoff.");
InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();yield return WaitGameSeconds(.2f);
Assert.IsFalse(arrival.Passengers[0].GetComponent<PlayerCombat>().IsAiming);
VerifyGameplayRifle(arrival.Passengers[0]);
InputSystem.RemoveDevice(mouse);RestoreInputRouting(routing);
Assert.AreEqual(Map02Arrival.Phase.Gameplay,arrival.CurrentPhase);
AssertDryPartySpawn(arrival);
ShotTracerLifecycleTests.FireAfterArrival(arrival.Passengers[0].GetComponent<PlayerCombat>());
yield return new ExitPlayMode();
}
static void AssertDryPartySpawn(Map02Arrival arrival) {
for(int i=0;i<3;i++) {
Assert.Less(Vector3.ProjectOnPlane(arrival.Passengers[i].position-arrival.SpawnPoints[i],Vector3.up).magnitude,.1f);
Assert.Greater(arrival.Passengers[i].position.y,.5f);
// Animator roots differ between Nam and Hung. Check actual shoe soles rather
// than assuming every actor's transform origin is at the ground surface.
float sole=float.PositiveInfinity;
foreach(var skin in arrival.Passengers[i].GetComponentsInChildren<SkinnedMeshRenderer>()){
var mesh=new Mesh();skin.BakeMesh(mesh);
foreach(var v in mesh.vertices)sole=Mathf.Min(sole,skin.transform.TransformPoint(v).y);
Object.Destroy(mesh);
}
Assert.Less(Mathf.Abs(sole-arrival.SpawnPoints[i].y),.02f,"Shoe soles must be grounded after arrival, actor "+i);
}
}
static void VerifyGameplayRifle(Transform root) {
var combat=root.GetComponent<PlayerCombat>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
var anchor=(Transform)typeof(PlayerCombat).GetField("handAnchor",flags).GetValue(combat);
var config=(WeaponGripConfig)typeof(PlayerCombat).GetField("gripConfig",flags).GetValue(combat);
var gun=root.GetComponentsInChildren<Weapon>().Single(w=>w.IsGun);
Assert.AreSame(anchor,gun.transform.parent,"Cinematic grip must return to gameplay hand anchor.");
Assert.IsTrue(config.TryGetOffset(WeaponKind.Rifle,out var offset));
Assert.Less(Vector3.Distance(gun.transform.localScale,Vector3.one*WeaponGripConfig.EffectiveScale(offset)),.001f);
var animator=root.GetComponentInChildren<Animator>();
Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName(PlayerCombat.RifleLocomotionState));
Assert.Less(animator.GetLayerWeight(animator.GetLayerIndex("UpperBody")),.01f);
}
static IEnumerator RecordArrival(Map02Arrival arrival) {
float limit=Time.realtimeSinceStartup+180;
var camera=arrival.GetComponentInChildren<Camera>();
Assert.NotNull(camera);
var captureHost=arrival.gameObject;
var pump=captureHost.AddComponent<ExtractionCapturePump>();
Assert.NotNull(pump);
var water=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).First(m=>m.name=="Winding canal water");
var vertices=water.sharedMesh.vertices.Select(v=>water.transform.TransformPoint(v)).ToArray();var triangles=water.sharedMesh.triangles;
var events=new System.Collections.Generic.List<string>();int frame=0;
var metrics=new System.Collections.Generic.List<string>{"time,actor,head_angle,stair_foot_error,shoe_riser_penetration"};
var actors=arrival.Passengers.Select(p=>p.GetComponentInChildren<Animator>()).ToArray();
var neutralHeads=actors.Select(a=>Quaternion.Inverse(a.transform.rotation)*a.GetBoneTransform(HumanBodyBones.Head).rotation).ToArray();
var ankle=(float[,])typeof(Map02Arrival).GetField("ankleOffsets",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(arrival);
while(arrival.IsPlaying && Time.realtimeSinceStartup<limit) {
bool rendered=false;System.Exception captureFailure=null;var phase=arrival.CurrentPhase;float time=arrival.CinematicTime;
pump.draw=()=>{try {
if(arrival.IsPlaying)Assert.IsFalse(arrival.Passengers[2].GetComponentsInChildren<Weapon>(true).Any(w=>w.IsGun&&w.gameObject.activeInHierarchy),"Hung's hands must stay free throughout rowing, stowing and disembarking.");
if(phase==Map02Arrival.Phase.Approaching)foreach(var x in new[]{-.7f,.7f})foreach(var z in new[]{-2.7f,2.7f})
Assert.IsTrue(InWater(arrival.transform.TransformPoint(new Vector3(x,0,z)),vertices,triangles),"Hull must stay inside the actual canal mesh.");
Assert.Greater(arrival.transform.TransformPoint(new Vector3(0,-.15f,0)).y,.07f,"Interior floor must stay clear of the canal water.");
float sampled=arrival.CinematicTime;
for(int i=0;i<3;i++) {
float headAngle=Quaternion.Angle(neutralHeads[i],Quaternion.Inverse(actors[i].transform.rotation)*actors[i].GetBoneTransform(HumanBodyBones.Head).rotation);
Assert.Less(headAngle,35,"Arrival gaze must stay restrained, actor "+i+" time "+sampled);
float footError=0,shoePenetration=0;string penetrationDetail="";
float localTime=sampled-16-i*3,start=i==0?3:5.4f;
if(arrival.CurrentPhase==Map02Arrival.Phase.Disembarking && localTime>=start && localTime<start+4.4f) {
for(int side=0;side<2;side++) {
var upper=actors[i].GetBoneTransform(side==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
var hips=actors[i].GetBoneTransform(HumanBodyBones.Hips);
float lateral=Mathf.Sign(Vector3.Dot(upper.position-hips.position,arrival.transform.forward))*.13f;
var expected=Map02Arrival.StairContact((localTime-start)/4.4f,side);
var target=arrival.transform.TransformPoint(new Vector3(expected.x,expected.y+ankle[i,side],(i==0?0:i==1?.8f:-.8f)+lateral));
var foot=actors[i].GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
footError=Mathf.Max(footError,Vector3.Distance(foot.position,target));
}
Assert.Less(footError,.02f,"Foot contact must stay within 2 cm, actor "+i+" time "+sampled);
// Check the soles against the real timber collider volumes as well as
// checking ankle targets. An ankle-only test misses toes sweeping a riser.
var contacts=(System.Collections.Generic.List<(Transform bone,Vector3 point)>[])typeof(Map02Arrival).GetField("shoeContacts",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(arrival);
foreach(var tread in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c=>c.name.StartsWith("Arrival tread "))) {
foreach(var contact in contacts[i]) {
var point=tread.transform.InverseTransformPoint(contact.bone.TransformPoint(contact.point));
var half=tread.size*.5f;point-=tread.center;
if(Mathf.Abs(point.x)<half.x-.01f&&Mathf.Abs(point.z)<half.z-.01f&&point.y<half.y&&point.y>-half.y) {
float depth=(half.y-point.y)*tread.transform.lossyScale.y;
if(depth>shoePenetration){shoePenetration=depth;penetrationDetail=tread.name+" "+contact.bone.name+" hull point "+arrival.transform.InverseTransformPoint(contact.bone.TransformPoint(contact.point));}
}
}
}
Assert.Less(shoePenetration,.02f,"The shoe must clear the stair riser, actor "+i+" time "+sampled+" "+penetrationDetail);
}
metrics.Add(sampled+","+i+","+headAngle+","+footError+","+shoePenetration);
}
Capture(camera,"Logs/Map02/frames/"+frame.ToString("D4")+".png");
if(frame%2==0 && time>12 && time<34) {
var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;float fov=camera.fieldOfView;
int index=time<16?2:time<19?0:time<22?1:2;
for(int i=0;i<3;i++) {float local=time-16-i*3,start=i==0?3:5.4f;if(local>=start&&local<start+4.4f){index=i;break;}}
var target=arrival.Passengers[index].position+Vector3.up*.85f;
var eye=target+arrival.transform.rotation*new Vector3(3.2f,1.4f,-2.6f);
camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));camera.fieldOfView=48;
Capture(camera,"Logs/Map02/close-"+frame.ToString("D4")+".png");
camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.fieldOfView=fov;
}
rendered=true;}catch(System.Exception exception){captureFailure=exception;rendered=true;}};
yield return new WaitUntil(()=>rendered || Time.realtimeSinceStartup>=limit);
pump.draw=null;if(captureFailure!=null){File.WriteAllText("Logs/Map02/foot-contact-failure.txt",captureFailure.ToString());throw captureFailure;}
events.Add(frame+","+time+","+phase+","+arrival.Passengers[0].position+","+arrival.Passengers[2].position);frame++;
// Capture every rendered frame so the review shows the transitions, not a slideshow.
}
File.WriteAllLines("Logs/Map02/frames.csv",events);
File.WriteAllLines("Logs/Map02/pose-metrics.csv",metrics);
}
static bool InWater(Vector3 point,Vector3[] vertices,int[] triangles) {
var p=new Vector2(point.x,point.z);
for(int i=0;i<triangles.Length;i+=3) {
var av=vertices[triangles[i]];var bv=vertices[triangles[i+1]];var cv=vertices[triangles[i+2]];
var a=new Vector2(av.x,av.z);var b=new Vector2(bv.x,bv.z);var c=new Vector2(cv.x,cv.z);
var v=b-a;var w=c-a;var q=p-a;float det=v.x*w.y-v.y*w.x;
if(Mathf.Abs(det)<.000001f)continue;
float u=(q.x*w.y-q.y*w.x)/det,t=(v.x*q.y-v.y*q.x)/det;
if(u>=-.001f && t>=-.001f && u+t<=1.001f)return true;
}return false;
}
static void Capture(Camera camera,string path){var old=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}}
}}


