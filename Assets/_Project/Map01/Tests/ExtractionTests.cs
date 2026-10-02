using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ShadowVale.Map01.Tests
{
    public sealed class ExtractionTests : ForestSceneTestBase
    {
        [UnityTest]
        public IEnumerator NaturalBoardingDepartsBeforeCompletion()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;
            IsolateSaves();
            var mission=Object.FindFirstObjectByType<Map01Mission>();
            var quest=mission.GetComponent<Map01Quest>();
            quest.RestoreStage(Map01Quest.ExtractionStage);
            var extraction=mission.GetComponentInChildren<Map01Extraction>();
            Assert.IsTrue(extraction.Prepared);
            var takes=new[]{extraction.radio,extraction.seatedReady,extraction.seatedFire,extraction.seatedReload,
                extraction.lowerWeapon,extraction.board,extraction.sit,extraction.travel}.Concat(extraction.hungClips);
            foreach(var take in takes) {Assert.IsTrue(take.humanMotion,take.name);Assert.AreEqual(30,take.frameRate,take.name);}
            Assert.That(extraction.radio.length,Is.EqualTo(7).Within(.04f));
            Assert.That(extraction.board.length,Is.EqualTo(6).Within(.04f));
            Assert.AreEqual(4,mission.Enemies.Count(e=>e.name.StartsWith("Extraction guard ")));
            Assert.AreEqual(1,Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Count(a=>a.name=="Commander (shared player mesh)"));
            yield return WaitGameSeconds(.2f);
            var hung=mission.hung.GetComponentInChildren<Animator>();
            Debug.Log("Extraction scale Nam="+mission.player.GetComponentInChildren<Animator>().transform.lossyScale+" Hung="+hung.transform.lossyScale);
            mission.gameCamera.transform.SetPositionAndRotation(new Vector3(6,3,83),Quaternion.LookRotation(new Vector3(1,.6f,87)-new Vector3(6,3,83)));
            Capture(mission,"seated-allies");
            Assert.That(hung.GetBoneTransform(HumanBodyBones.Hips).position.y,Is.InRange(.28f,.43f),"Hung must actually sit at bench height.");
            foreach(var enemy in mission.Enemies) enemy.GetComponent<Health>().TakeDamage(99999,enemy.transform.position,null);
            var cc=mission.player.GetComponent<CharacterController>();cc.enabled=false;
            mission.StartCoroutine(RecordVideo(mission,extraction));
            mission.player.position=extraction.approach+Vector3.left*.25f;cc.enabled=true;
            yield return WaitGameSeconds(.3f);
            Assert.IsTrue(mission.Cinematic);
            Assert.AreEqual(Map01Quest.BoardingStage,quest.Stage);
            Assert.IsFalse(mission.GetComponent<Map01SaveSystem>().SaveSlot(0,out _,true));
            yield return WaitGameSeconds(2);
            Capture(mission,"boarding-start");
            yield return WaitGameSeconds(3);
            Capture(mission,"boarding-shelf");
            yield return WaitGameSeconds(4);
            Capture(mission,"seating");
            Assert.AreNotEqual(Map01Quest.CompleteStage,quest.Stage);
            yield return WaitGameSeconds(4);
            Capture(mission,"departure");
            Assert.AreEqual(Map01Extraction.Phase.Departing,extraction.CurrentPhase);
            yield return WaitGameSeconds(19);
            Assert.IsTrue(extraction.HasDeparted);
            Assert.AreEqual(Map01Quest.CompleteStage,quest.Stage);
            Assert.IsNotNull(ForestSaveSlots.Read(ForestSaveSlots.AutoSlot));
            Assert.AreEqual(mission.player.parent,mission.hung.parent);
            Assert.Greater(Vector3.Distance(mission.player.position,extraction.approach),8);
            Assert.AreEqual("Map 1",UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            Capture(mission,"complete");
            ScreenCapture.CaptureScreenshot("Logs/Extraction/completion-ui.png");yield return WaitGameSeconds(1);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RadioSkipAndExtractionCheckpointAreIdempotent()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;IsolateSaves();
            var mission=Object.FindFirstObjectByType<Map01Mission>();
            var quest=mission.GetComponent<Map01Quest>();
            quest.RestoreStage(Map01Quest.BossStage);
            var boss=mission.Enemies.First(e=>e.IsBoss);
            boss.GetComponent<Health>().TakeDamage(99999,boss.transform.position,null);
            mission.GetComponent<Map01EndingCutscene>().Skip();
            var extraction=mission.GetComponentInChildren<Map01Extraction>();
            extraction.Skip();extraction.Skip();
            Assert.AreEqual(Map01Quest.ExtractionStage,quest.Stage);
            Assert.IsFalse(mission.Cinematic);
            var guard=mission.Enemies.First(e=>e.name=="Extraction guard 0");
            guard.GetComponent<Health>().TakeDamage(99999,guard.transform.position,null);
            yield return null;
            Assert.IsTrue(mission.GetComponent<Map01SaveSystem>().SaveSlot(0,out _,true));
            ForestSaveSlots.Entry saved=ForestSaveSlots.Read(0);
            Assert.IsTrue(saved.checkpoint.Contains("Extraction guard 0"));
            Map01SaveSystem.BeginGame(0);
            yield return WaitGameSeconds(.7f);
            mission=Object.FindFirstObjectByType<Map01Mission>();quest=mission.GetComponent<Map01Quest>();
            Assert.AreEqual(Map01Quest.ExtractionStage,quest.Stage);
            Assert.AreEqual(4,mission.Enemies.Count(e=>e.name.StartsWith("Extraction guard ")));
            Assert.IsFalse(mission.Enemies.First(e=>e.name=="Extraction guard 0").Alive);
            extraction=mission.GetComponentInChildren<Map01Extraction>();
            Assert.IsTrue(mission.Enemies.Any(e=>e.Alive),"This scenario boards while blockers remain alive.");
            var cc=mission.player.GetComponent<CharacterController>();cc.enabled=false;mission.player.position=extraction.approach;cc.enabled=true;
            yield return WaitGameSeconds(.3f);
            yield return HoldEscape();
            Assert.IsTrue(extraction.HasDeparted,"Holding Escape must complete boarding without a direct Skip call.");
            extraction.Skip();
            Assert.AreEqual(Map01Quest.CompleteStage,quest.Stage);
            Assert.IsTrue(extraction.HasDeparted);
            quest.RestoreStage(Map01Quest.CompleteStage);
            Assert.AreEqual(Map01Quest.CompleteStage,quest.Stage);
            yield return new ExitPlayMode();
        }
        private static IEnumerator RecordVideo(Map01Mission mission,Map01Extraction sequence)
        {
            string folder="Logs/Extraction/RevisionFrames";Directory.CreateDirectory(folder);
            int frame=0;float next=Time.unscaledTime;
            while(!sequence.HasDeparted) {
                if(Time.unscaledTime>=next) {
                    next+=.1f;
                    var camera=mission.gameCamera;var previous=camera.targetTexture;var active=RenderTexture.active;
                    var target=RenderTexture.GetTemporary(960,540,24);var texture=new Texture2D(960,540,TextureFormat.RGB24,false);
                    try {
                        camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                        texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();
                        File.WriteAllBytes(folder+"/frame-"+(frame++).ToString("D05")+".jpg",texture.EncodeToJPG(85));
                    } finally {camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Object.Destroy(texture);}
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RiflesAndAimSurviveRadioHandoff()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;IsolateSaves();
            var mission=Object.FindFirstObjectByType<Map01Mission>();
            foreach(var enemy in mission.Enemies) {
                var rifles=enemy.GetComponentsInChildren<Weapon>(true).Where(w=>w.IsGun).ToArray();
                Assert.AreEqual(1,rifles.Length,enemy.name);Assert.IsTrue(rifles[0].gameObject.activeInHierarchy,enemy.name);
            }
            var actor=mission.player.GetComponentInChildren<Animator>();
            int ammo=mission.ModernCombat.RoundsInMagazine;
            var routing=RouteInputToGame();var mouse=InputSystem.AddDevice<Mouse>();
            var state=new MouseState();state=state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse,state);InputSystem.Update();
            var sequence=Map01Extraction.Get(mission);sequence.Begin(true);
            yield return WaitGameSeconds(.4f);sequence.Skip();yield return WaitGameSeconds(.2f);
            Assert.IsTrue(mission.ModernCombat.IsAiming);
            Assert.IsTrue(actor.GetBool(PlayerCombat.AnimatorParams.Aiming));
            Assert.AreEqual((int)mission.ModernCombat.EquippedKind,actor.GetInteger(PlayerCombat.AnimatorParams.Weapon));
            Assert.AreEqual(ammo,mission.ModernCombat.RoundsInMagazine);
            InputSystem.RemoveDevice(mouse);RestoreInputRouting(routing);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator GroundedMatchingActorsAndBoardingWithAllEnemiesAlive()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;IsolateSaves();
            var mission=Object.FindFirstObjectByType<Map01Mission>();var quest=mission.GetComponent<Map01Quest>();
            quest.RestoreStage(Map01Quest.BriefingStage);
            mission.ModernPlayer.enabled=false;mission.ModernCombat.enabled=false;
            mission.player.GetComponent<CharacterController>().enabled=false;
            mission.hung.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled=false;
            var points=new[]{mission.player.position,mission.Interactables.First(p=>p.kind==ForestPointKind.Supplies).transform.position};
            int index=0;
            foreach(var point in points) {
                var namPosition=point+Vector3.right*2;
                if(Physics.Raycast(namPosition+Vector3.up*3,Vector3.down,out var nHit,8,mission.ObstructionMask,QueryTriggerInteraction.Ignore))namPosition.y=nHit.point.y;
                var hungPosition=namPosition+Vector3.right*1.5f;
                if(Physics.Raycast(hungPosition+Vector3.up*3,Vector3.down,out var hHit,8,mission.ObstructionMask,QueryTriggerInteraction.Ignore))hungPosition.y=hHit.point.y;
                mission.player.SetPositionAndRotation(namPosition,Quaternion.identity);mission.hung.SetPositionAndRotation(hungPosition,Quaternion.identity);
                yield return WaitGameSeconds(.5f);
                var nam=mission.player.GetComponentInChildren<Animator>();var hung=mission.hung.GetComponentInChildren<Animator>();
                float namSole=Sole(nam),hungSole=Sole(hung);
                float namHeight=nam.GetBoneTransform(HumanBodyBones.Head).position.y-namSole;
                float hungHeight=hung.GetBoneTransform(HumanBodyBones.Head).position.y-hungSole;
                var focus=(namPosition+hungPosition)/2+Vector3.up;
                mission.gameCamera.transform.SetPositionAndRotation(focus+new Vector3(1,1,4),Quaternion.LookRotation(-new Vector3(1,1,4)));
                Capture(mission,"standing-"+index);
                File.WriteAllText("Logs/Extraction/standing-"+index+"-metrics.txt","Nam="+namHeight+" Hung="+hungHeight+" hung sole="+hungSole+" surface="+hungPosition.y);
                Assert.That(Mathf.Abs(hungHeight/namHeight-1),Is.LessThanOrEqualTo(.02f),"Matching standing stature");
                Assert.That(Mathf.Abs(hungSole-hungPosition.y),Is.LessThanOrEqualTo(.02f),"Boot sole contact");
                index++;
            }
            quest.RestoreStage(Map01Quest.ExtractionStage);
            var extraction=mission.GetComponentInChildren<Map01Extraction>();
            Assert.AreEqual(4,mission.Enemies.Count(e=>e.name.StartsWith("Extraction guard ")&&e.Alive));
            mission.player.position=extraction.approach;yield return WaitGameSeconds(.3f);
            Assert.IsTrue(mission.Cinematic);extraction.Skip();Assert.IsTrue(extraction.HasDeparted);
            yield return new ExitPlayMode();
        }
        private static float Sole(Animator actor)
        {
            float y=float.PositiveInfinity;
            foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                var mesh=new Mesh();renderer.BakeMesh(mesh);
                foreach(var vertex in mesh.vertices)y=Mathf.Min(y,renderer.transform.TransformPoint(vertex).y);
                Object.Destroy(mesh);
            }
            return y;
        }

        private static void IsolateSaves()
        {
            UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            Directory.CreateDirectory("Logs/Extraction/TestSaves");
            typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,Path.GetFullPath("Logs/Extraction/TestSaves"));
        }
        private static IEnumerator HoldEscape()
        {
            var routing=RouteInputToGame();var keyboard=InputSystem.AddDevice<Keyboard>();
            try {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                yield return WaitGameSeconds(.1f);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));InputSystem.Update();
                yield return WaitGameSeconds(1.2f);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            } finally {InputSystem.RemoveDevice(keyboard);RestoreInputRouting(routing);}
        }
        private static void Capture(Map01Mission mission,string name)
        {
            Directory.CreateDirectory("Logs/Extraction");
            var camera=mission.gameCamera;var previous=camera.targetTexture;var active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                File.WriteAllBytes("Logs/Extraction/"+name+".png",image.EncodeToPNG());
                var actors=mission.player.GetComponentsInChildren<Animator>().Concat(mission.hung.GetComponentsInChildren<Animator>());
                File.WriteAllLines("Logs/Extraction/"+name+"-bones.txt",actors.SelectMany(a=>new[]{HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot}.Select(b=>a.name+" "+b+" "+a.GetBoneTransform(b).position)));
            } finally {camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);}
        }
    }
}
