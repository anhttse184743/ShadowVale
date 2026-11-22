using System.Collections;
using System.Linq;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
public sealed class CommanderRifleVisualAuditTests:ForestSceneTestBase {
 [UnityTest,Timeout(240000)]public IEnumerator CaptureGripAndSling(){
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
  Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}Assert.NotNull(m?.Chapter);
  var preview=System.Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyToMission");
  Directory.CreateDirectory("Logs/CommanderRifleAudit");
  foreach(var stage in new[]{1,2,5}){
   if(stage==2)preview.Invoke(null,new object[]{m,stage});
   if(stage==5){var snap=m.Capture();var state=new Map02ChapterState();foreach(var task in Map02ChapterDefinition.Load().tasks){if(task.id=="N3_04")break;state.Complete(task.id);}snap.chapter=state;m.Restore(snap);}
   yield return WaitGameSeconds(2);
   var role=m.Chapter.Actor("Anh Thành");var a=role.visual.GetComponentInChildren<Animator>();var gun=a.GetComponent<Map01Rifle>();Assert.NotNull(gun?.weapon);var tr=gun.weapon.transform;
   var hand=a.GetBoneTransform(HumanBodyBones.RightHand);var chest=a.GetBoneTransform(HumanBodyBones.Chest);
   if(stage==5){Assert.IsTrue(m.Chapter.AllyInCombatPose("Anh Thành"));Assert.AreEqual(hand,tr.parent,"Commander draws rifle before the fight");var grip=a.GetComponent<Map01EnemyGrip>();Assert.IsTrue(grip!=null&&grip.Calibrated,"Combat grip must be calibrated");Assert.Less(grip.PalmError,.08f,"Supporting hand must remain near the foregrip");}
   else {Assert.AreEqual(chest,tr.parent,"Commander carries rifle on his back outside combat");
    Assert.That(tr.localPosition.x,Is.EqualTo(.20f).Within(.001f));Assert.That(tr.localPosition.z,Is.EqualTo(-.12f).Within(.001f));}
   var sb=new StringBuilder();
   void P(string name,Vector3 v){sb.AppendLine(name+"="+v.ToString("F5")+" actorLocal="+a.transform.InverseTransformPoint(v).ToString("F5"));}
   sb.AppendLine("phase="+m.Chapter.State.phase+" gunParent="+tr.parent.name+" localPosition="+tr.localPosition.ToString("F5")+" localEuler="+tr.localEulerAngles.ToString("F4")+" localScale="+tr.localScale.ToString("F5"));
   sb.AppendLine("chest X="+a.transform.InverseTransformDirection(chest.right).ToString("F4")+" Y="+a.transform.InverseTransformDirection(chest.up).ToString("F4")+" Z="+a.transform.InverseTransformDirection(chest.forward).ToString("F4"));P("actor",a.transform.position);P("chest",chest.position);P("hand",hand.position);P("gunRoot",tr.position);P("muzzle",gun.weapon.Muzzle.position);
   foreach(var r in tr.GetComponentsInChildren<Renderer>().Where(x=>x.name.Contains("grip")||x.name.Contains("stock")||x.name.Contains("trigger")))P(r.name,r.bounds.center);
   File.WriteAllText("Logs/CommanderRifleAudit/stage"+stage+".txt",sb.ToString());
   foreach(var k in new[]{0,1,2}){
    var main=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.isActiveAndEnabled);var g=new GameObject("Rifle review camera");var c=g.AddComponent<Camera>();c.CopyFrom(main);c.enabled=false;c.useOcclusionCulling=false;c.nearClipPlane=.03f;c.fieldOfView=36;
    var focus=role.visual.position+Vector3.up*.98f;var d=k==0?role.visual.forward:k==1?role.visual.right:-role.visual.forward;var eye=focus+d*3.2f+Vector3.up*.18f;c.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));
    var old=RenderTexture.active;var rt=new RenderTexture(1920,1080,24);rt.Create();c.targetTexture=rt;
    try{c.Render();c.Render();RenderTexture.active=rt;var img=new Texture2D(1920,1080,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1920,1080),0,0);img.Apply();File.WriteAllBytes("Logs/CommanderRifleAudit/stage"+stage+"_"+k+".png",img.EncodeToPNG());Object.Destroy(img);}finally{RenderTexture.active=old;c.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(g);}
   }
  }
  // Restoring a quiet checkpoint after combat must put the rifle back on his back.
  preview.Invoke(null,new object[]{m,2});yield return WaitGameSeconds(1);
  var after=m.Chapter.Actor("Anh Thành").visual.GetComponentInChildren<Animator>();
  Assert.AreEqual(after.GetBoneTransform(HumanBodyBones.Chest),after.GetComponent<Map01Rifle>().weapon.transform.parent);
  yield return new ExitPlayMode();
 }
}
}
