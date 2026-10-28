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
    /// <summary>Plan step 5 / P8 (2026-10-25): Day 1 and Night 1 sound, Hùng's theme and laugh, the duck, ông Sáu's cough.</summary>
    public sealed class Map02DayOneAudioTests:ForestSceneTestBase
    {
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static readonly string[] OneShots={"duck","cough","d1_rooster","hung_laugh","crate_place","crate_wrong","child_giggle"};
        static readonly string[] Beds={"n1_frogs_loop","n1_fire_loop","d1_bamboo_loop","d1_children_loop","d1_oars_loop","d1_plane_far","hung_theme"};
        [Test]public void EverySoundIsACreditedRecording()
        {
            var sources=File.ReadAllText("SourceArt/Audio/Map02_DayOne_20261025/sources.json");
            foreach(var id in new[]{719107,418509,178615,530127,32655,532191,435508,406938,588308,335891,348595,720133,372072,648959,742356})
            {StringAssert.Contains("\"freesoundId\": "+id,sources);Assert.True(File.Exists($"SourceArt/Audio/Map02_DayOne_20261025/source/{id}_hq.mp3"),id+" source kept");}
            StringAssert.DoesNotContain("Attribution",sources,"CC0 only.");
            foreach(var name in OneShots.Concat(Beds)){var clip=Resources.Load<AudioClip>("Map02DayOne/"+name);Assert.NotNull(clip,name);Assert.Greater(clip.length,.15f,name);}
            foreach(var name in OneShots)
            {var clip=Resources.Load<AudioClip>("Map02DayOne/"+name);var data=new float[clip.samples*clip.channels];Assert.True(clip.GetData(data,0),name);Assert.Greater(data.Max(Mathf.Abs),.05f,name+" is audible");}
            Assert.Greater(Resources.Load<AudioClip>("Map02DayOne/n1_frogs_loop").length,45,"The night bed is long enough not to be heard looping.");
            var echo=Map02ChapterDefinition.Load().Lines("sacrifice")[0];Assert.True(Map02DialogueCatalog.HasApprovedRecording(echo),"The remembered promise has its own take for Night 3.");
            Assert.Less(DialogueVoice.Length(echo.RecordingId),DialogueVoice.Length("m2v3_promise_05"),"Only the first half of the promise, up to \"rau đay\".");
        }
        [UnityTest,Timeout(240000)]public IEnumerator AmbienceFollowsThePhaseAndSceneSoundStopsWithTheScene()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();yield return Check();yield return new ExitPlayMode();
        }
        static IEnumerator Check()
        {
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.Cancel();
            object Call(string n,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(n,Private).Invoke(c,args);
            object Get(string n)=>typeof(Map02ChapterDirector).GetField(n,Private).GetValue(c);
            void Set(string n,object v)=>typeof(Map02ChapterDirector).GetField(n,Private).SetValue(c,v);
            void LoadBefore(string id){var snap=m.Capture();var state=new Map02ChapterState();foreach(var task in Map02ChapterDefinition.Load().tasks){if(task.id==id)break;state.Complete(task.id);}state.dayOne.arrivalSeen=true;snap.chapter=state;m.Restore(snap);}
            try
            {
                LoadBefore("D1_03");Assert.AreEqual(0,c.State.phase);c.enabled=false;for(int i=0;i<120;i++)Call("TickAmbience",.05f,false);
                Assert.Greater(c.AmbienceVolume("d1_bamboo_loop"),.4f,"Day 1: bamboo in the wind.");Assert.Greater(c.AmbienceVolume("d1_children_loop"),.4f);Assert.Less(c.AmbienceVolume("n1_frogs_loop"),.01f,"No night frogs by day.");
                Set("dayAmbienceClock",Map02ChapterDirector.PlaneAt-.01f);Call("TickAmbience",.05f,false);Assert.True((bool)Get("planeFlown"),"One aeroplane, far off, eight minutes in.");
                Call("TickAmbience",.05f,true);var bed=((System.Collections.IDictionary)Get("ambience"))["d1_bamboo_loop"];var source=(AudioSource)bed.GetType().GetField("source").GetValue(bed);Assert.False(source.isPlaying,"Ambience follows the pause menu.");Call("TickAmbience",.05f,false);Assert.True(source.isPlaying);
                LoadBefore("N1_02");Assert.AreEqual(1,c.State.phase);for(int i=0;i<120;i++)Call("TickAmbience",.05f,false);
                Assert.Greater(c.AmbienceVolume("n1_frogs_loop"),.4f,"Night 1: frogs and crickets.");Assert.Greater(c.AmbienceVolume("n1_fire_loop"),.3f,"The stove fire in chị Lành's kitchen.");Assert.Less(c.AmbienceVolume("d1_bamboo_loop"),.01f);
                // "Lời hứa": Hùng's laugh after the punch, his theme from "Tôi hả..." to the end; a skip stops them.
                LoadBefore("N1_01");Call("BeginScene",Map02ChapterDefinition.Load().Find("N1_01"),0,0);float approach=(float)Get("dayApproachDuration");
                Set("sceneClock",approach+43.2f);Call("TickScene",.02f);Assert.True((bool)Get("supperLaughed"));
                Set("sceneClock",approach+50f);Call("TickScene",.02f);var music=(AudioSource)Get("supperMusic");Assert.True(music.isPlaying);Assert.AreEqual("hung_theme",music.clip.name);
                c.Cancel();Assert.False(music.isPlaying,"Skipping the supper stops the theme.");
                // Ông Sáu coughs in the inner room, then speaks.
                LoadBefore("D1_03");Call("BeginScene",Map02ChapterDefinition.Load().Find("D1_03"),0,0);var lines=(Map02DialogueCatalog.Entry[])Get("lines");int elder=System.Array.FindIndex(lines,l=>l.speaker=="Ông Sáu");Assert.GreaterOrEqual(elder,0);
                Set("sceneCoughed",false);Set("lineIndex",elder);Call("Speak");var voice=DialogueVoice.For(m.gameObject);Assert.NotNull(Get("pendingVoice"),"His line waits for the cough.");Assert.False(voice.HasSpeech);
                Call("TickScene",1.7f);Assert.IsNull(Get("pendingVoice"));Assert.True(voice.HasSpeech,"Then he speaks.");Assert.False(c.Actor("Ông Sáu").visual.gameObject.activeSelf,"He is in the inner room.");c.Cancel();
                Assert.True(c.Actor("Ông Sáu").visual.gameObject.activeSelf,"Back in his chair once the scene is over.");
                // Bé Tí peeks from behind a porch post at the radio set, giggles at "Nhẹ thì đưa Tí xách thử coi" and runs off.
                LoadBefore("D1_02");Call("BeginScene",Map02ChapterDefinition.Load().Find("D1_02"),0,0);var child=c.Actor("Bé Tí");var peek=child.transform.position;
                Assert.Less(Vector3.Distance(peek,(Vector3)Get("porchDoor")),6,"She hides at chị Lành's veranda.");
                Set("lineIndex",2);Call("TickScene",.02f);Assert.GreaterOrEqual((float)Get("childLaughAt"),0f,"She giggles at the line.");Set("sceneClock",(float)Get("sceneClock")+2f);Call("TickScene",.02f);Assert.True((bool)Get("childRan"));Assert.True(child.SceneDriven,"She runs off while the scene plays.");
                // Group B "thay băng" on mocap: she kneels at his right and wraps his offered right forearm; he sits on the stool.
                var hung=c.Actor("Hùng").Animator;var lanh=c.Actor("Chị Lành").Animator;var stool=(Transform)Get("sceneChair");
                Set("sceneClock",10f);Call("LateUpdate");
                var elbow=hung.GetBoneTransform(HumanBodyBones.RightLowerArm).position;var wrist=hung.GetBoneTransform(HumanBodyBones.RightHand).position;
                var hand=lanh.GetBoneTransform(HumanBodyBones.RightHand).position;var along=Vector3.Project(hand-elbow,(wrist-elbow).normalized);
                Assert.Less((hand-elbow-along).magnitude,.2f,"Her right hand circles his forearm.");
                Assert.That(hung.GetBoneTransform(HumanBodyBones.Hips).position.y-stool.position.y,Is.InRange(.45f,.75f),"He sits on the stool.");
                Assert.Less(lanh.GetBoneTransform(HumanBodyBones.Hips).position.y-stool.position.y,.75f,"She kneels.");
                Assert.True(((Transform)Get("bandageRoll")).gameObject.activeSelf,"The roll is in her hand.");Assert.True(((Transform)Get("bandageSleeve")).gameObject.activeSelf,"The turns show on his arm.");
                c.enabled=true;yield return WaitGameSeconds(.3f);ScreenCapture.CaptureScreenshot("Logs/Map02Presentation_20261022/bandage-mocap.png");yield return null;c.enabled=false;
                Set("sceneClock",23f);Call("LateUpdate");Assert.Greater(lanh.GetBoneTransform(HumanBodyBones.Hips).position.y-stool.position.y,.7f,"She is up again at the end.");c.Cancel();
            }
            finally{c.Cancel();c.enabled=true;}
        }
    }
}
