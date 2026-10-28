using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using ShadowVale.Gameplay.Combat;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
 [DefaultExecutionOrder(11000)]public sealed class BoatMotionProbe:MonoBehaviour {
  public Action sample;void LateUpdate()=>sample?.Invoke();
 }
 public sealed class BoatMotionHullTests:ForestSceneTestBase {
  static object Gpu(MeshRenderer r)=>typeof(MeshRenderer).GetProperty("allowGPUDrivenRendering",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(r)??"unknown";
  const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
  [Test]public void RecordedReviewHasNoLargeWristFlips(){
   const string file="Logs/BoatMotionHull_20261028/After/motion.csv";
   if(!File.Exists(file))Assert.Ignore("Run NaturalLiveCoverAndArrivalMotionReview with SV_BOAT_REVIEW_TAKE=After first.");
   var rows=File.ReadAllLines(file).Skip(1).Select(l=>l.Split(',')).Where(x=>x[1]=="Map1-Boarding"||x[1]=="Map1-Seating"||x[1]=="Map1-Departing").ToArray();
   Assert.Greater(rows.Length,100,"Review needs enough rendered poses to detect a flip.");
   float max=rows.Max(x=>Mathf.Max(float.Parse(x[4],CultureInfo.InvariantCulture),float.Parse(x[5],CultureInfo.InvariantCulture)));
   Assert.Less(max,45,"No single recorded pose may flip the wrist by 45 degrees or more.");
  }
  [UnityTest,Timeout(1200000)]public IEnumerator NaturalLiveCoverAndArrivalMotionReview(){
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");SetPreviewResolution(1280,720);yield return new EnterPlayMode();yield return Record();yield return new ExitPlayMode();
  }
  IEnumerator Record(){
   var m=Object.FindFirstObjectByType<Map01Mission>();float until=Time.realtimeSinceStartup+100;while(m==null||!m.IsInitialized){Assert.Less(Time.realtimeSinceStartup,until);m=Object.FindFirstObjectByType<Map01Mission>();yield return null;}
   m.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ExtractionStage);var ex=m.GetComponentInChildren<Map01Extraction>();ex.autoContinueToMap2=true;
   var guards=m.Enemies.Where(e=>e.name=="Extraction guard 0"||e.name=="Extraction guard 1").ToArray();Assert.AreEqual(2,guards.Length);
   foreach(var e in m.Enemies.Except(guards))e.GetComponent<Health>().TakeDamage(99999,e.transform.position,null);
   for(int i=0;i<guards.Length;i++){Assert.True(UnityEngine.AI.NavMesh.SamplePosition(new Vector3(-4,2,90+i*2),out var hit,5,UnityEngine.AI.NavMesh.AllAreas));guards[i].GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(hit.position);}
   string folder="Logs/BoatMotionHull_20261028/"+Environment.GetEnvironmentVariable("SV_BOAT_REVIEW_TAKE");if(folder.EndsWith("/"))folder+="Current";Directory.CreateDirectory(folder+"/frames");
   var rows=new List<string>{"frame,phase,clock,dt,rightDeg,leftDeg,barrelError,shots,hullCount,hullGpu,hullVisible"};var photos=new HashSet<string>();int frame=0,shots=0;float nextPhoto=0,previousTime=-1;Quaternion previousR=default,previousL=default;bool same=false;float maxBarrel=0,maxShotError=0;int aimedSamples=0;var issues=new List<string>();bool after=Environment.GetEnvironmentVariable("SV_BOAT_REVIEW_TAKE")=="After";
   var probe=ex.gameObject.AddComponent<BoatMotionProbe>();
   probe.sample=()=>{
    var arrival=Object.FindFirstObjectByType<Map02Arrival>();if(arrival?.CurrentPhase==Map02Arrival.Phase.Gameplay)return;
    var a=ex.NamAnimator;if(a==null)return;float dt=Time.unscaledTime-previousTime;var r=a.GetBoneTransform(HumanBodyBones.RightHand).localRotation;var l=a.GetBoneTransform(HumanBodyBones.LeftHand).localRotation;
    string phase=arrival!=null?"Map2-"+arrival.CurrentPhase:"Map1-"+ex.CurrentPhase;
    float clock=arrival!=null?(float)typeof(Map02Arrival).GetField("clock",F).GetValue(arrival):(float)typeof(Map01Extraction).GetField("clock",F).GetValue(ex);
    var gun=a.GetComponent<Map01Rifle>();float error=-1;
    if(arrival==null&&ex.CurrentPhase==Map01Extraction.Phase.Departing){var targets=(Transform[])typeof(Map01Extraction).GetField("coverTargets",F).GetValue(ex);var acquired=(float[])typeof(Map01Extraction).GetField("coverAcquiredAt",F).GetValue(ex);
     if(targets[0]!=null&&clock-acquired[0]>.35f){var grip=gun.weapon.GetComponentsInChildren<Renderer>().First(x=>x.name.StartsWith("bl_grip_low"));var barrel=(gun.weapon.Muzzle.position-grip.bounds.center).normalized;error=Vector3.Angle(barrel,targets[0].position+Vector3.up*1.3f-gun.weapon.Muzzle.position);maxBarrel=Mathf.Max(maxBarrel,error);aimedSamples++;if(ex.NamCoverShots>shots)maxShotError=Mathf.Max(maxShotError,error);}}
    var hulls=(arrival!=null?arrival.transform:ex.BoatRoot).GetComponentsInChildren<MeshRenderer>(true).Where(x=>x.name=="Moored wooden sampan").ToArray();
    if(after&&hulls.Any(h=>Equals(Gpu(h),true)))issues.Add(phase+" hull still uses GPU driven rendering");
    rows.Add(string.Join(",",frame,phase,clock.ToString("F4",CultureInfo.InvariantCulture),dt.ToString("F4",CultureInfo.InvariantCulture),same?Quaternion.Angle(previousR,r).ToString("F3",CultureInfo.InvariantCulture):"0",same?Quaternion.Angle(previousL,l).ToString("F3",CultureInfo.InvariantCulture):"0",error.ToString("F3",CultureInfo.InvariantCulture),ex.NamCoverShots,hulls.Length,string.Join("|",hulls.Select(h=>Gpu(h))),string.Join("|",hulls.Select(h=>h.enabled&&!h.forceRenderingOff&&h.gameObject.activeInHierarchy))));
    previousR=r;previousL=l;previousTime=Time.unscaledTime;same=true;shots=ex.NamCoverShots;
    if(Time.unscaledTime>=nextPhoto&&ex.CurrentPhase>=Map01Extraction.Phase.Approach){nextPhoto=Time.unscaledTime+1f/12;ScreenCapture.CaptureScreenshot(folder+"/frames/frame-"+(frame++).ToString("D5")+".png");}
    string key=phase+"-"+Mathf.FloorToInt(clock/3);if(photos.Add(key)){
     ScreenCapture.CaptureScreenshot(folder+"/"+key+".png");
     File.WriteAllLines(folder+"/"+key+"-render.txt",hulls.Select(h=>h.name+" active="+h.gameObject.activeInHierarchy+" enabled="+h.enabled+" hidden="+h.forceRenderingOff+" gpu="+Gpu(h)+" visible="+h.isVisible+" bounds="+h.bounds+" scene="+h.gameObject.scene.name+" shader="+h.sharedMaterial.shader.name));
    }
   };
   var cc=m.player.GetComponent<CharacterController>();m.ModernHealth.CinematicInvulnerable=true;cc.enabled=false;m.player.position=ex.approach;cc.enabled=true;
   until=Time.realtimeSinceStartup+900;bool done=false;
   while(Time.realtimeSinceStartup<until){var arrival=Object.FindFirstObjectByType<Map02Arrival>();
    if(after&&arrival?.CurrentPhase==Map02Arrival.Phase.Mooring&&(float)typeof(Map02Arrival).GetField("clock",F).GetValue(arrival)>2)arrival.Skip();
    if(arrival?.CurrentPhase==Map02Arrival.Phase.Gameplay){done=true;break;}yield return null;}
   probe.sample=null;File.WriteAllLines(folder+"/motion.csv",rows);File.WriteAllText(folder+"/summary.txt",$"complete={done}\nshots={shots}\nmaxBarrelError={maxBarrel}\nmaxShotError={maxShotError}\naimedSamples={aimedSamples}\nframes={frame}\n");
   Assert.True(done);Assert.Greater(shots,0);Assert.Greater(aimedSamples,0);if(after){Assert.IsEmpty(issues);Assert.Less(maxShotError,3f,"Actual barrel must point at the enemy on every recorded shot.");}
  }
 }
}
