using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
 public sealed class BoatAudioRegressionTests:ForestSceneTestBase {
  const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
  const string Folder="Logs/BoatAudioFix_20261028";
  [UnityTest,Timeout(480000)]public IEnumerator RifleStaysSingleAcrossNaturalBoatAndReplacementBody(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");SetPreviewResolution(1280,720);yield return new EnterPlayMode();yield return Run(false);yield return new ExitPlayMode();}
  [UnityTest,Timeout(480000)]public IEnumerator MosinStaysSingleAcrossNaturalBoatAndReplacementBody(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");SetPreviewResolution(1280,720);yield return new EnterPlayMode();yield return Run(true);yield return new ExitPlayMode();}
  IEnumerator Run(bool mosin){
   var m=Object.FindFirstObjectByType<Map01Mission>();float deadline=Time.realtimeSinceStartup+100;
   while(m==null||!m.IsInitialized){Assert.Less(Time.realtimeSinceStartup,deadline);m=Object.FindFirstObjectByType<Map01Mission>();yield return null;}
   if(mosin){var bag=m.GetComponent<Map01Inventory>();bag.Add("rifle_mosin",1);Assert.True(bag.EquipItem("rifle_mosin"));yield return null;Assert.AreEqual(WeaponKind.Sniper,m.ModernCombat.EquippedKind);}
   m.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ExtractionStage);var ex=m.GetComponentInChildren<Map01Extraction>();ex.autoContinueToMap2=true;
   foreach(var e in m.Enemies)e.GetComponent<Health>().TakeDamage(99999,e.transform.position,null);
   while(!ex.Prepared){Assert.Less(Time.realtimeSinceStartup,deadline);yield return null;}
   var cc=m.player.GetComponent<CharacterController>();cc.enabled=false;m.player.position=ex.approach;cc.enabled=true;
   var rows=new List<string>();var seen=new HashSet<string>();int samples=0,c1Samples=0;bool gameplay=false;var errors=new List<string>();
   Directory.CreateDirectory(Folder);deadline=Time.realtimeSinceStartup+340;
   while(Time.realtimeSinceStartup<deadline){
    var arrival=Object.FindFirstObjectByType<Map02Arrival>();var body=arrival!=null?arrival.Passengers?.FirstOrDefault()?.GetComponentInChildren<Map01BoatCarry>():ex!=null?ex.NamAnimator?.GetComponent<Map01BoatCarry>():null;
    if(arrival?.CurrentPhase==Map02Arrival.Phase.Gameplay){gameplay=true;yield return null;var guns=arrival.Passengers[0].GetComponentsInChildren<Weapon>().Where(w=>w.IsGun&&w.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&!r.forceRenderingOff)).ToArray();Assert.AreEqual(1,guns.Length,"Exactly one gameplay gun after handoff");break;}
    string stage=arrival!=null?"Map2-"+arrival.CurrentPhase:ex!=null?"Map1-"+ex.CurrentPhase:"loading";
    if(body!=null){var pump=body.GetComponent<ExtractionCapturePump>()??body.gameObject.AddComponent<ExtractionCapturePump>();var capturedBody=body;var capturedArrival=arrival;var capturedStage=stage;
     pump.draw=()=>{
      if(!capturedBody.Slung)return;
      samples++;int count=capturedBody.VisibleGunCount;if(count!=1)errors.Add(capturedStage+" guns="+count);
      Assert.Greater(capturedBody.Gun.GetComponentsInChildren<MeshRenderer>().Length,0,"Sling must have real meshes");
      bool c1=capturedArrival!=null&&capturedArrival.MisstepActor(0)!=capturedBody.GetComponent<Animator>();
      if(c1){c1Samples++;var torso=capturedArrival.MisstepActor(0).GetBoneTransform(HumanBodyBones.Chest);var renderers=capturedBody.Gun.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);if(Vector3.Distance(bounds.center,torso.position)>1)errors.Add("C1 rifle not following visible chest");}
      string key=capturedStage+(c1?"-C1":"");if(seen.Add(key)){rows.Add(key+" guns="+count+" root="+capturedBody.transform.position);ScreenCapture.CaptureScreenshot(Folder+"/"+(mosin?"Mosin-":"AK-")+key+".png");}
     };
    }yield return null;
   }
   File.WriteAllLines(Folder+"/"+(mosin?"Mosin":"AK")+"-audit.txt",rows.Concat(new[]{"samples="+samples,"replacementBodySamples="+c1Samples}).Concat(errors));
   Assert.True(gameplay,"Natural boat sequence must return control");Assert.Greater(samples,50);Assert.Greater(c1Samples,10,"Must audit the actual replacement body, not only the seated pose");Assert.IsEmpty(errors,string.Join("\n",errors.Take(15)));
  }
  [UnityTest,Timeout(240000)]public IEnumerator QuietSingleWaterBedDucksUnderDialogueAndPauses(){
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();float until=Time.realtimeSinceStartup+100;Map02Mission m=null;
   while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();Object.FindFirstObjectByType<Map02Arrival>()?.Skip();if(m?.Ready==true&&m.Chapter!=null)break;yield return null;}
   Assert.NotNull(m?.Chapter);var c=m.Chapter;c.Cancel();yield return WaitGameSeconds(4);c.enabled=false;
   void Tick(bool paused=false){typeof(Map02ChapterDirector).GetMethod("TickAmbience",F).Invoke(c,new object[]{.05f,paused});}
   for(int i=0;i<120;i++)Tick();float full=c.AmbienceVolume("d1_oars_loop");Assert.That(full,Is.InRange(.06f,.066f));
   var sources=c.GetComponentsInChildren<AudioSource>(true);Assert.AreEqual(1,sources.Count(s=>s.loop&&s.clip!=null&&(s.clip.name=="d1_oars_loop"||s.clip.name=="oars")),"No stacked water loops");
   var voice=DialogueVoice.For(m.gameObject);voice.CanSpeak=()=>true;voice.Play("m2v3_promise_05");Assert.True(voice.HasSpeech);for(int i=0;i<120;i++)Tick();Assert.Less(c.AmbienceVolume("d1_oars_loop"),full*.3f);
   Tick(true);Assert.False(sources.Single(s=>s.clip!=null&&s.clip.name=="d1_oars_loop").isPlaying);Tick();Assert.True(sources.Single(s=>s.clip!=null&&s.clip.name=="d1_oars_loop").isPlaying);
   Directory.CreateDirectory(Folder);File.WriteAllLines(Folder+"/audio.txt",sources.Where(s=>s.clip!=null).Select(s=>s.name+" clip="+s.clip.name+" gain="+s.volume+" spatial="+s.spatialBlend));
   voice.Stop();c.enabled=true;yield return new ExitPlayMode();
  }
 }
}
