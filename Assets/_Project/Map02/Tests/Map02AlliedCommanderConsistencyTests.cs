using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using ShadowVale.Gameplay.Combat;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
public sealed class Map02AlliedCommanderConsistencyTests:ForestSceneTestBase {
 [UnityTest,Timeout(240000)] public IEnumerator DirectMap2UsesNewCommanderModelWithControllerAndRifle() {
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
  Map02Mission mission=null;float deadline=Time.realtimeSinceStartup+100;
  while(Time.realtimeSinceStartup<deadline){mission=Object.FindFirstObjectByType<Map02Mission>();if(mission?.Ready==true&&mission.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
  Assert.NotNull(mission?.Chapter);var role=mission.Chapter.Actor("Anh Thành");Assert.NotNull(role);Assert.NotNull(role.visual);
  var animator=role.visual.GetComponentInChildren<Animator>();Assert.NotNull(animator);Assert.IsTrue(animator.isHuman&&animator.avatar.isValid); // Cinematic pose may temporarily clear its controller.
  var skin=role.visual.GetComponentsInChildren<SkinnedMeshRenderer>().First(x=>x.sharedMesh!=null);
  Assert.IsTrue(AssetDatabase.GetAssetPath(skin.sharedMesh).Contains("CommanderBodyTransferReview/Commander.fbx"),"Direct Map 2 must use the new allied commander mesh");
  var rifle=animator.GetComponent<Map01Rifle>();Assert.NotNull(rifle);Assert.NotNull(rifle.weapon);Assert.IsTrue(rifle.weapon.gameObject.activeInHierarchy);
  // Move to the authored day-two briefing rather than showing the actor behind tall grass at spawn.
  var preview=System.Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyToMission");
  preview.Invoke(null,new object[]{mission,2});yield return WaitGameSeconds(2);
  Assert.IsTrue(role.visual.gameObject.activeInHierarchy);
  var main=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x=>x.isActiveAndEnabled);
  var go=new GameObject("Temporary full HD commander review camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(main);camera.enabled=false;camera.orthographic=false;camera.useOcclusionCulling=false;camera.nearClipPlane=.03f;camera.fieldOfView=35;
  var focus=role.visual.position+Vector3.up*.95f;var eye=focus+role.visual.forward*3.6f+role.visual.right*1.1f+Vector3.up*.35f;camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));
  var old=RenderTexture.active;var rt=new RenderTexture(1920,1080,24);rt.Create();camera.targetTexture=rt;
  try{camera.Render();camera.Render();RenderTexture.active=rt;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();Directory.CreateDirectory("Logs/Map02CommanderReview");File.WriteAllBytes("Logs/Map02CommanderReview/chi_huy_map2_fullhd.png",image.EncodeToPNG());Object.Destroy(image);}
  finally{RenderTexture.active=old;camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(go);}
  yield return new ExitPlayMode();
 }
}
}
