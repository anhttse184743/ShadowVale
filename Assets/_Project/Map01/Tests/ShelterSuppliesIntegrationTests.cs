using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class ShelterSuppliesIntegrationTests:ForestSceneTestBase
    {
        [Test]public void ImportedReviewedModelsAndClothAreComplete()
        {
            var data=Map01SupplyPerformance.Data;Assert.NotNull(data);Assert.NotNull(data.namModel);Assert.NotNull(data.hungModel);
            Assert.That(data.collect.length,Is.InRange(15.95f,16.05f));Assert.That(data.handoff.length,Is.InRange(15.95f,16.05f));Assert.Greater(data.hungRest.length,12);
            foreach(var text in new[]{data.collectCloth,data.handoffCloth}){
                var cloth=JsonUtility.FromJson<Map01SupplyPerformance.ClothData>(text.text);Assert.AreEqual(481,cloth.frames);
                foreach(var strip in cloth.straps){Assert.AreEqual(481*strip.vertices*3,strip.xyz.Length);Assert.Less(strip.triangles.Max(),strip.vertices);}
            }
        }
        [Test]public void BackpackGeometryAndCargoMotionSurviveTheFbxImport()
        {
            var data=Map01SupplyPerformance.Data;var model=Object.Instantiate(data.namModel);var raw=model.transform.GetChild(0).gameObject;
            try{
                var nodes=model.GetComponentsInChildren<Transform>(true);
                foreach(var name in new[]{"Interaction_Backpack_Body","Interaction_Backpack_Flap","Backpack_TopHandle_v5_Baked"})Assert.NotNull(nodes.FirstOrDefault(t=>t.name==name),"Missing exported prop: "+name);
                var bag=nodes.First(t=>t.name=="SupplyBag_Interaction");data.handoff.SampleAnimation(raw,7);var first=bag.position;data.handoff.SampleAnimation(raw,8.3f);Assert.Greater(Vector3.Distance(first,bag.position),.25f,"Cargo must move with the new handoff clip.");
            }finally{Object.DestroyImmediate(model);}
        }
        [UnityTest]public IEnumerator HandoffRestoresGameplayAndHungSpeaksWhileLyingOnCot()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;
            var m=Object.FindFirstObjectByType<Map01Mission>();var warm=Map01InteractionWarmup.For(m);float deadline=Time.realtimeSinceStartup+90;
            while(!warm.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Assert.IsTrue(warm.Ready);
            foreach(var e in m.Enemies)if(e!=null)e.enabled=false;
            m.GetComponent<Map01Quest>().RestoreStoryStage(4,new Map01Story.Snapshot{rescueAccepted=true,supplies=true,supplyReported=true});yield return null;yield return null;
            var rest=m.hung.GetComponentInChildren<Map01HungRest>();Assert.NotNull(rest);Assert.IsTrue(rest.IsResting,"Hung should rest after the escort, including the supply return.");
            var story=m.GetComponent<Map01Story>();PlaceNam(m,story.Layout.handoff.position);SetPreviewResolution(1920,1080);
            var actor=m.player.GetComponentInChildren<Animator>();var skins=actor.GetComponentsInChildren<Renderer>(true);var before=skins.Select(r=>r.forceRenderingOff).ToArray();
            var method=typeof(Map01Story).GetMethod("AnimateSupply",BindingFlags.NonPublic|BindingFlags.Instance);Assert.NotNull(method);
            Directory.CreateDirectory("Logs/ShelterSupplies");m.StartCoroutine((IEnumerator)method.Invoke(story,new object[]{true}));
            yield return WaitGameSeconds(1.6f);Assert.IsTrue(m.Cinematic);Assert.IsTrue(rest.IsResting);ScreenCapture.CaptureScreenshot("Logs/ShelterSupplies/handoff-shoulder.png");
            yield return WaitGameSeconds(2);ScreenCapture.CaptureScreenshot("Logs/ShelterSupplies/handoff-front.png");
            yield return WaitGameSeconds(4.7f);ScreenCapture.CaptureScreenshot("Logs/ShelterSupplies/handoff-bag.png");
            var performance=GameObject.Find("Nam supply interaction");Assert.NotNull(performance);var nodes=performance.GetComponentsInChildren<Transform>(true);
            var grip=(nodes.First(t=>t.name=="RightHandMiddle1").position+nodes.First(t=>t.name=="RightHandMiddle3").position)*.5f;
            var cargo=nodes.First(t=>t.name=="SupplyBag_Interaction");Assert.Less(Vector3.Distance(grip,cargo.position+Vector3.up*.38f),.025f,"In-game hand and bag must share the same timing.");
            yield return WaitGameSeconds(8.2f);yield return null;Assert.IsFalse(m.Cinematic);Assert.IsTrue(m.ModernCombat.enabled&&m.ModernPlayer.enabled);
            for(int i=0;i<skins.Length;i++)Assert.AreEqual(before[i],skins[i].forceRenderingOff,"Original renderer state must restore: "+skins[i].name);
            var dialogue=m.GetComponent<Map01StoryDialogue>();var line=Map01OperationLines.Handoff.First(l=>l.speaker=="Hùng");Assert.IsTrue(dialogue.Begin(new[]{line},null));
            yield return WaitGameSeconds(1);Assert.IsTrue(rest.IsResting);Assert.Less(Vector3.Angle(m.gameCamera.transform.forward,rest.DialogueFocus-m.gameCamera.transform.position),8);
            ScreenCapture.CaptureScreenshot("Logs/ShelterSupplies/hung-rest-dialogue.png");yield return null;dialogue.Close(false);
            Assert.IsTrue(rest.IsResting);
            m.GetComponent<Map01Quest>().RestoreStoryStage(0,new Map01Story.Snapshot{rescueAccepted=true});yield return null;yield return null;Assert.IsFalse(rest.IsResting,"Retry must release the cot presentation.");
            yield return new ExitPlayMode();
        }
    }
}
