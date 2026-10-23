using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// Patching the two boats with chú Bảy (D3_03): rings close on leaks over the hull photograph, a click as a ring meets its
    /// circle fills the boat's 0–100 bar, and the closer the click the more it fills; early clicks and missed rings add nothing.
    /// </summary>
    public sealed class Map02BoatPatchingTests:ForestSceneTestBase
    {
        const string MapPath="Assets/_Project/Scenes/Maps/Map 2.unity";

        [UnityTest,Timeout(300000)]public IEnumerator ClosingRingsPatchBothBoatsFasterTheCloserTheClicks()
        {
            EditorSceneManager.OpenScene(MapPath);yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator InPlayMode()
        {
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.enabled=false;
            var tick=typeof(Map02ChapterDirector).GetMethod("TickWork",BindingFlags.NonPublic|BindingFlags.Instance);
            void Tick(float dt)=>tick.Invoke(c,new object[]{dt});
            // Ripen the oldest leak to `age` (others keep ageing and may be missed meanwhile).
            Map02ChapterDirector.Leak Ripen(float age){for(int i=0;i<200;i++){var l=c.Leaks.OrderByDescending(x=>x.age).FirstOrDefault();if(l!=null&&l.age>=age-.001f)return l;Tick(l==null?.05f:Mathf.Min(.05f,age-l.age));}Assert.Fail("No leak appeared.");return null;}

            var snapshot=m.Capture();var s=new Map02ChapterState();
            foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id=="D3_03")break;s.Complete(t.id);}
            s.introduced.Add("D3_03");snapshot.chapter=s;m.Restore(snapshot);yield return null;
            var marker=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(o=>o.chapterId=="D3_03");
            var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=marker.transform.position;cc.enabled=true;Physics.SyncTransforms();
            Assert.IsTrue(m.Interact(marker));Assert.IsTrue(c.Patching,"The boats are patched with the ring game, not the rhythm bar.");
            Tick(.01f);Assert.AreEqual(CursorLockMode.None,Cursor.lockState,"The pointer is free to click on the hull.");
            StringAssert.Contains("Bấm chuột",c.Instruction);

            var first=Ripen(.05f);Assert.IsFalse(c.PatchAt(first.at+Vector2.right*300),"A click on bare planking is not a patch.");
            Assert.IsTrue(c.PatchAt(first.at));Assert.AreEqual(0,c.PatchProgress,"A click long before the ring arrives wastes the leak.");
            var perfect=Ripen(Map02ChapterDirector.PatchApproach);Assert.IsTrue(c.PatchAt(perfect.at));Assert.AreEqual(20,c.PatchProgress,"A click as the ring meets the circle fills a fifth of the bar.");
            var rough=Ripen(Map02ChapterDirector.PatchApproach-.25f);Assert.IsTrue(c.PatchAt(rough.at));Assert.AreEqual(27,c.PatchProgress,"A rough click fills far less.");
            float before=c.PatchProgress;var late=Ripen(.05f);for(int i=0;i<60&&c.Leaks.Contains(late);i++)Tick(.05f);
            Assert.IsFalse(c.Leaks.Contains(late),"A ring left alone runs out.");
            Assert.AreEqual(before,c.PatchProgress,"A ring that runs past its circle adds nothing.");

            int hits=0;while(c.State.Count("D3_03")==0&&hits<10){var l=Ripen(Map02ChapterDirector.PatchApproach);c.PatchAt(l.at);hits++;}
            Assert.AreEqual(1,c.State.Count("D3_03"),"The full bar seals the first boat.");Assert.AreEqual(4,hits,"Clean clicks finish it in a few patches.");
            Assert.IsTrue(c.Patching);for(int i=0;i<30;i++)Tick(.05f);
            Assert.IsTrue(c.Patching,"The second boat follows.");Assert.AreEqual(0,c.PatchProgress);
            hits=0;while(c.Patching&&hits<10){var l=Ripen(Map02ChapterDirector.PatchApproach);c.PatchAt(l.at);hits++;if(!c.Patching)break;for(int i=0;i<35&&c.Patching&&c.State.Count("D3_03")>=2;i++)Tick(.05f);}
            Assert.AreEqual(5,hits,"Five clean patches seal a boat from empty.");
            for(int i=0;i<40&&c.Patching;i++)Tick(.05f);
            Assert.IsFalse(c.Patching);Assert.IsTrue(c.State.Done("D3_03"),"Both boats patched completes the task.");
            Assert.IsFalse(Cursor.visible,"The pointer is hidden again for play (batch mode cannot lock it without a focused window).");
            c.enabled=true;
        }
    }
}
