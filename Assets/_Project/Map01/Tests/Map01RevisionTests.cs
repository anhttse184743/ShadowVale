using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map01RevisionTests:ForestSceneTestBase
    {
        [Serializable]sealed class Manifest{public Row[] entries;}
        [Serializable]sealed class Row{public string id,caption,resource,cleanResource;public bool radio;public float duration;}
        static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        [Test]public void RevisedAudioHasExactCaptionsAndRetiresObsoleteOrders()
        {
            var rows=JsonUtility.FromJson<Manifest>(File.ReadAllText("SourceArt/Dialogue/Map01Revision20261015/voice-manifest.json")).entries;
            Assert.AreEqual(17,rows.Length);
            foreach(var row in rows){
                var voice=DialogueVoice.Find(row.id);Assert.NotNull(voice,row.id);Assert.NotNull(voice.clip,row.id);
                Assert.AreEqual(row.caption,voice.caption);Assert.AreEqual(row.radio,voice.radio);
                Assert.That(voice.clip.length,Is.EqualTo(row.duration).Within(.04f));
                Assert.NotNull(Resources.Load<AudioClip>(row.cleanResource));
                Assert.AreEqual(row.radio,row.resource.EndsWith("_radio"));
            }
            foreach(var id in new[]{"m1_radio_order","m1_radio_reply","m1_story_01","m1_story_supply_report_02","m1_story_radio_report_04","m1_rescue_radio_instruction"})Assert.IsNull(DialogueVoice.Find(id),id);
            StringAssert.Contains("Chuyển tới điểm ba.",DialogueVoice.Caption("m1_v2_radio_report_02"));
        }
        [UnityTest]public IEnumerator EarlyOfficerDeathCannotSkipPointThree()
        { Open();yield return new EnterPlayMode();yield return null;yield return null;yield return CheckOrder();yield return new ExitPlayMode(); }
        [UnityTest]public IEnumerator GunContactsAndEnemyShotHaveVisibleAndAudibleFeedback()
        { Open();yield return new EnterPlayMode();yield return null;yield return null;yield return CheckContacts();yield return new ExitPlayMode(); }
        [UnityTest]public IEnumerator LocalConversationFramesHungAndRadioKeepsNamStill()
        { Open();yield return new EnterPlayMode();yield return null;yield return null;yield return CheckCamera();yield return new ExitPlayMode(); }
        static void Open(){Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/Maps/Map 1.unity");}
        static IEnumerator Ready()
        {
            float end=Time.realtimeSinceStartup+25;while((M==null||!M.IsInitialized||M.ModernCombat==null||M.GetComponent<Map01Story>().Commander==null)&&Time.realtimeSinceStartup<end)yield return null;
            Assert.NotNull(M);Assert.IsTrue(M.IsInitialized);
            typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,Path.GetFullPath("Logs/Map01Revision/saves/"+Guid.NewGuid().ToString("N")));
            foreach(var e in M.Enemies)e.enabled=false;
            M.ModernHealth.CinematicInvulnerable=true;
        }
        static void At(Vector3 p){var cc=M.player.GetComponent<CharacterController>();bool on=cc.enabled;cc.enabled=false;M.player.position=p;cc.enabled=on;Physics.SyncTransforms();}
        static IEnumerator CheckOrder()
        {
            yield return Ready();var q=M.GetComponent<Map01Quest>();var story=M.GetComponent<Map01Story>();
            q.RestoreStoryStage(6,new Map01Story.Snapshot{revision=2,rescueAccepted=true,supplies=true,radioDisabled=true});q.BeginCommanderOrder();
            var officer=q.CommanderEnemy;officer.GetComponent<Health>().TakeDamage(999999,officer.transform.position,null);yield return null;
            Assert.AreEqual(7,q.Stage);Assert.IsTrue(story.Progress.commanderKilled);Assert.IsNull(M.GetComponentInChildren<Map01Extraction>());
            At(story.Layout.observationSites[0].position);M.ModernPlayer.RestoreMotion(false);M.Crouched=false;
            var input=RouteInputToGame();var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));InputSystem.Update();
            var target=story.Layout.commanderPost.position+Vector3.up*1.3f;float until=Time.time+8;
            while(!M.GetComponent<Map01StoryDialogue>().IsPlaying&&Time.time<until){
                M.GetComponent<Map01Scouting>().HoldBinoculars(true);Map01OperationsTests.LookAtOfficer(M,target);yield return null;
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();RestoreInputRouting(input);
            var d=M.GetComponent<Map01StoryDialogue>();Assert.IsTrue(d.IsPlaying,"Early report missing: safe="+story.SafeToReport+" state="+JsonUtility.ToJson(story.Capture()));yield return WaitGameSeconds(1.1f);d.Close(true);yield return null;
            Assert.IsTrue(story.CommanderObjectiveComplete,JsonUtility.ToJson(story.Capture()));Assert.AreEqual(8,q.Stage,"An early kill still requires the final retreat report.");
            Assert.IsTrue(story.TryInteract());yield return WaitGameSeconds(1.1f);d.Close(true);yield return null;
            Assert.AreEqual(10,q.Stage);var e=M.GetComponentInChildren<Map01Extraction>();Assert.NotNull(e);e.autoContinueToMap2=false;
            Assert.AreEqual(Map01Extraction.Phase.Extraction,e.CurrentPhase);Assert.IsTrue(M.ModernCombat.enabled&&M.ModernPlayer.enabled);
            story.Restore(new Map01Story.Snapshot{revision=1},10);Assert.IsTrue(story.CommanderObjectiveComplete);
        }
        static IEnumerator CheckContacts()
        {
            yield return Ready();M.GetComponent<Map01Quest>().RestoreStage(Map01Quest.BriefingStage);
            var guard=M.GetComponent<Map01Rescue>().Squad[0];var actor=guard.GetComponentInChildren<Animator>();
            var grip=actor.GetComponent<Map01EnemyGrip>();Assert.NotNull(grip);Assert.IsTrue(grip.Calibrated);
            var clips=actor.runtimeAnimatorController.animationClips;
            Directory.CreateDirectory("Logs/Map01Revision");File.WriteAllText("Logs/Map01Revision/hand-contact-measurements.txt","");
            using(var pose=new Map01CinematicPose(actor)){
                foreach(var name in new[]{"Enemy_Guard_Idle","Enemy_Patrol_Walk","Enemy_Rifle_AimIdle","Enemy_Rifle_Fire"}){
                    // The mount the game holds each clip in (Map01EnemyGrip.LateUpdate): idle and walk play in AC_Enemy's Locomotion blend tree,
                    // so they use the patrol carry; only the aim and fire states raise the rifle to the shoulder.
                    bool patrol=name=="Enemy_Guard_Idle"||name=="Enemy_Patrol_Walk";
                    var clip=clips.First(c=>c.name==name);pose.Sample(clip,clip.length*.3f);grip.Apply(patrol);yield return null;yield return null;grip.Apply(patrol);
                    Capture(name,actor.transform.position+actor.transform.forward*1.9f+actor.transform.right*1.3f+Vector3.up*1.5f,actor.transform.position+Vector3.up*1.3f);
                    File.AppendAllText("Logs/Map01Revision/hand-contact-measurements.txt",name+" palm="+grip.PalmError+"m wrist="+grip.WristBend+"deg\n");
                    Assert.Less(grip.PalmError,.035f,name);Assert.Less(grip.WristBend,1,name);
                }
            }
            // Shots are heard within 65 m of Nam (EnemyFeedback): stand within earshot of the jetty guard.
            At(guard.transform.position+guard.transform.forward*8);yield return null;
            int before=guard.AudibleShots;
            typeof(Map01EnemyController).GetField("_nextShot",Hidden).SetValue(guard,0f);
            typeof(Map01EnemyController).GetMethod("Shoot",Hidden).Invoke(guard,null);
            Assert.AreEqual(before+1,guard.AudibleShots);
            // A guard's round sounds through the shared enemy feedback (EnemyFeedback), with the guards' own rifle report
            // (the AK of the extraction film, not Nam's gunshot) and a flash at his muzzle.
            var report=Resources.Load<Map01Extraction>("Cutscenes/Map01Extraction").rifleAudio;
            Assert.IsNotNull(report);
            Assert.IsTrue(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a=>a.isPlaying&&a.clip==report),"The guard's shot is heard, his rifle's report.");
            Assert.IsTrue(guard.GetComponentsInChildren<Light>().Any(l=>l.name=="Rifle muzzle flash"&&l.enabled),"Muzzle flash at his rifle.");
            yield return null;
        }
        static IEnumerator CheckCamera()
        {
            yield return Ready();var q=M.GetComponent<Map01Quest>();var story=M.GetComponent<Map01Story>();q.RestoreStage(Map01Quest.BriefingStage);
            PlaceNamAtCommander(M);
            var d=M.GetComponent<Map01StoryDialogue>();
            Assert.IsTrue(story.TryInteract());
            for(int line=0;line<d.Count&&d.Current.speaker!="Hùng";line++){yield return WaitGameSeconds(1.1f);d.Advance();}
            yield return WaitGameSeconds(.8f);
            Assert.AreEqual("Hùng",d.Current.speaker);
            var resting=M.hung.GetComponentInChildren<Map01HungRest>();
            var screen=M.gameCamera.WorldToViewportPoint(resting!=null&&resting.IsResting?resting.DialogueFocus:M.hung.position+Vector3.up*1.42f);
            Assert.Greater(screen.z,0);Assert.That(screen.x,Is.InRange(.15f,.85f));Assert.That(screen.y,Is.InRange(.15f,.85f));
            Capture("Hung-speaks");d.Close(false);yield return null;
            var at=M.player.position;var ammo=M.ModernCombat.RoundsInMagazine;
            Assert.IsTrue(d.Begin(new[]{new Map01StoryDialogue.Line("Nam","Địch đông quá, tôi không thể tấn công trực diện.",false,"m1_v2_rescue_approach_01"),new Map01StoryDialogue.Line("Chỉ huy","Vậy hãy xem xét địa hình và tiếp cận chúng một cách lén lút.",true,"m1_v2_rescue_approach_02"),new Map01StoryDialogue.Line("Nam","Rõ.",false,"m1_v2_rescue_approach_03")},null));
            yield return WaitGameSeconds(1);
            var actor=M.player.GetComponentInChildren<Animator>();var head=actor.GetBoneTransform(HumanBodyBones.Head);var hand=actor.GetBoneTransform(HumanBodyBones.LeftHand);
            Assert.Less(Vector3.Distance(at,M.player.position),.001f);Assert.Less(Vector3.Distance(head.position,hand.position),.28f);
            Assert.IsFalse(M.player.GetComponentsInChildren<Weapon>(true).Any(w=>w.gameObject.activeInHierarchy));
            Capture("Nam-radio-front",M.player.position+M.player.forward*1.4f+M.player.right*1.5f+Vector3.up*1.6f,M.player.position+Vector3.up*1.4f);
            Capture("Nam-radio-side",M.player.position-M.player.right*2.1f+Vector3.up*1.6f,M.player.position+Vector3.up*1.4f);
            d.Close(false);yield return null;Assert.AreEqual(ammo,M.ModernCombat.RoundsInMagazine);Assert.IsTrue(M.ModernPlayer.enabled&&M.ModernCombat.enabled);
        }
        static void Capture(string name,Vector3? eye=null,Vector3? target=null)
        {
            Directory.CreateDirectory("Logs/Map01Revision");var camera=M.gameCamera;
            var pos=camera.transform.position;var rot=camera.transform.rotation;
            if(eye.HasValue){camera.transform.position=eye.Value;camera.transform.LookAt(target.Value);}
            var rt=RenderTexture.GetTemporary(1280,720,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/Map01Revision/"+name+".png",image.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);camera.transform.SetPositionAndRotation(pos,rot);}
        }
        [TearDown]public void ResetStorage(){typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);}
    }
}

