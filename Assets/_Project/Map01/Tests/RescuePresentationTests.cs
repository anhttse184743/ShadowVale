using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
public sealed class RescuePresentationTests:ForestSceneTestBase {
    static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
    static IEnumerator Warm(){
        float end=Time.realtimeSinceStartup+90;
        while(Time.realtimeSinceStartup<end&&(M==null||!M.IsInitialized||M.ModernPlayer==null||M.GetComponent<Map01Rescue>().Squad.Count!=4))yield return null;
        Assert.NotNull(M);Assert.IsTrue(M.IsInitialized);Assert.AreEqual(4,M.GetComponent<Map01Rescue>().Squad.Count);
        foreach(var g in M.Enemies)g.enabled=false;
    }
    [UnityTest,Timeout(240000)] public IEnumerator CaptiveShotIsVisibleAndReturnsNamToStealth(){
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return Warm();yield return IntroReview();yield return new ExitPlayMode();
    }
    static IEnumerator IntroReview(){
        var m=M;var r=m.GetComponent<Map01Rescue>();m.GetComponent<Map01Quest>().RestoreStoryStage(Map01Quest.RescueStage,RescueOrdered());
        var cc=m.player.GetComponent<CharacterController>();cc.enabled=false;m.player.position=r.RetryPoint;cc.enabled=true;yield return null;
        var keyboard=InputSystem.AddDevice<Keyboard>();var routing=RouteInputToGame();
        var d=m.GetComponent<Map01RescueCinematic>()??m.gameObject.AddComponent<Map01RescueCinematic>();
        var pos=m.player.position;float hp=m.ModernHealth.Current;int ammo=m.ModernCombat.RoundsInMagazine;
        var cameraPos=m.gameCamera.transform.position;
        try {
            m.Crouched=false;m.ModernPlayer.RestoreMotion(false);Assert.IsTrue(d.ShowIntro(r,r.Squad[0]));
            while(d.IsPlaying&&d.Elapsed<1.5f)yield return null;
            Assert.IsTrue(d.IntroShowsCaptive);Assert.Greater(Vector3.Distance(cameraPos,m.gameCamera.transform.position),5);
            var h=m.hung.GetComponentInChildren<Map01HungVisual>();Assert.IsTrue(h.RopeVisible);
            Capture(m.gameCamera,"captive-shot");
            Directory.CreateDirectory("Logs/RescueClarity");
            File.WriteAllLines("Logs/RescueClarity/intro.txt",new[]{"camera="+m.gameCamera.transform.position+" rotation="+m.gameCamera.transform.eulerAngles}.Concat(new[]{h.actor,r.Squad[0].GetComponentInChildren<Animator>()}.Select(a=>a.name+" root="+a.transform.position+" chest="+a.GetBoneTransform(HumanBodyBones.Chest).position+" view="+m.gameCamera.WorldToViewportPoint(a.GetBoneTransform(HumanBodyBones.Chest).position))));
            foreach(var actor in new[]{h.actor,r.Squad[0].GetComponentInChildren<Animator>()}){
                var p=m.gameCamera.WorldToViewportPoint(actor.GetBoneTransform(HumanBodyBones.Chest).position);
                Assert.Greater(p.z,0);Assert.That(p.x,Is.InRange(.05f,.95f),actor.name);Assert.That(p.y,Is.InRange(.1f,.9f),actor.name);
            }
            Assert.Less(Vector3.Distance(pos,m.player.position),.02f);Capture(m.gameCamera,"captive-shot");
            Time.timeScale=0;float at=d.Elapsed;yield return new WaitForSecondsRealtime(.2f);Assert.That(d.Elapsed,Is.EqualTo(at).Within(.001f));Time.timeScale=1;
            d.Skip();yield return null;Assert.IsTrue(m.Crouched&&m.ModernPlayer.IsSneaking);Assert.IsFalse(m.ModernPlayer.IsSprinting||r.Failed||m.Cinematic);
            Assert.AreEqual(hp,m.ModernHealth.Current);Assert.AreEqual(ammo,m.ModernCombat.RoundsInMagazine);
            m.Crouched=false;m.ModernPlayer.RestoreMotion(false);Assert.IsTrue(d.ShowIntro(r,r.Squad[0]));yield return null;d.CancelForRestore();
            Assert.IsFalse(m.Crouched||m.ModernPlayer.IsSneaking,"Loading a save cancels, rather than commits, automatic stealth.");
            var old=r.Capture();old.revision=4;old.introSeen=true;r.Restore(old,Array.Empty<Map01EnemyController.Snapshot>());Assert.IsFalse(r.IntroSeen,"The old radio-only save gets the new captive shot once.");
            old.revision=Map01Rescue.SnapshotRevision;r.Restore(old,Array.Empty<Map01EnemyController.Snapshot>());Assert.IsTrue(r.IntroSeen);
        }finally{Time.timeScale=1;if(d.IsPlaying)d.CancelForRestore();InputSystem.RemoveDevice(keyboard);RestoreInputRouting(routing);}
    }
    [UnityTest,Timeout(240000)] public IEnumerator CommanderRemainsSeatedOnBoatAfterShelterHandoff(){
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return Warm();yield return SeatReview();yield return new ExitPlayMode();
    }
    static IEnumerator SeatReview(){
        var m=M;m.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ExtractionStage);yield return null;
        var e=m.GetComponentInChildren<Map01Extraction>();Assert.NotNull(e);Assert.IsTrue(e.Prepared);e.autoContinueToMap2=false;
        foreach(var g in m.Enemies)g.enabled=false;
        yield return WaitGameSeconds(.3f);var root=e.CommanderRoot;var local=root.localPosition;
        var actor=e.CommanderAnimator;var hips=actor.GetBoneTransform(HumanBodyBones.Hips);
        var benches=e.BoatRoot.GetComponentsInChildren<MeshRenderer>().Where(x=>x.name=="Boat bench").OrderBy(x=>Vector3.ProjectOnPlane(x.bounds.center-hips.position,Vector3.up).sqrMagnitude).ToArray();
        Assert.IsNotEmpty(benches);float surface=benches[0].bounds.max.y;
        Directory.CreateDirectory("Logs/RescueClarity");
        File.WriteAllText("Logs/RescueClarity/seat.txt",$"root local={local:F4}\nhips={hips.position:F4}\nbench={benches[0].bounds}\nleft foot={actor.GetBoneTransform(HumanBodyBones.LeftFoot).position:F4}\nright foot={actor.GetBoneTransform(HumanBodyBones.RightFoot).position:F4}");
        var previous=m.gameCamera.transform.position;var rotation=m.gameCamera.transform.rotation;
        var lens=root.position+actor.transform.forward*2.7f+actor.transform.right*2.4f+Vector3.up*1.4f;
        m.gameCamera.transform.SetPositionAndRotation(lens,Quaternion.LookRotation(hips.position+Vector3.up*.15f-lens));
        Capture(m.gameCamera,"commander-seat");m.gameCamera.transform.SetPositionAndRotation(previous,rotation);
        Assert.That(hips.position.y-surface,Is.InRange(.04f,.34f),"Pelvis sits over the bench instead of floating at the shelter-floor height.");
        foreach(var bone in new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot}){
            Assert.Greater(hips.position.y-actor.GetBoneTransform(bone).position.y,.28f,"Feet point down into the hull.");
            Assert.GreaterOrEqual(e.BoatRoot.InverseTransformPoint(actor.GetBoneTransform(bone).position).x,-.34f,"Boots remain inside the port side.");
        }
        yield return WaitGameSeconds(1);Assert.Less(Vector3.Distance(local,root.localPosition),.02f,"The briefing ground pass must release its actor to the boat.");
    }
    static void Capture(Camera camera,string name){
        Directory.CreateDirectory("Logs/RescueClarity");var rt=RenderTexture.GetTemporary(1280,720,24);var before=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/RescueClarity/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=before;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
    }
}
}
