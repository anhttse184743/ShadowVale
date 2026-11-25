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
    /// <summary>Playing straight from one day into the next must leave the village as a load of that day would: the same
    /// shared Hùng, nobody still sitting or held in a clip from the day before (26/11/2026).</summary>
    public sealed class Map02DayChangeResetTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(240000)]public IEnumerator ANewDayPlayedStraightOnStartsAsCleanAsALoad()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator InPlayMode()
        {
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;var def=Map02ChapterDefinition.Load();c.enabled=false;
            void Load(string id){var snapshot=m.Capture();var s=new Map02ChapterState();foreach(var t in def.tasks){s.Complete(t.id);if(t.id==id)break;}snapshot.chapter=s;m.Restore(snapshot);}
            bool Sitting(string role){var a=c.Actor(role).Animator;return a!=null&&a.parameters.Any(p=>p.name=="Sitting")&&a.GetBool("Sitting");}
            object Pose(string role)=>typeof(Map02ChapterActor).GetField("pose",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c.Actor(role));
            try
            {
                Assert.NotNull(c.Actor("Hùng").GetComponentInChildren<Map01HungVisual>(true),"Map 2's Hùng is the shared Map 1 model (HungVisual).");
                bool tamSits=c.Actor("Tâm").Animator!=null&&c.Actor("Tâm").Animator.parameters.Any(p=>p.name=="Sitting");
                Load("D2_04");yield return null;
                if(tamSits)Assert.IsTrue(Sitting("Tâm"),"Day 2: the recovered scout rests seated.");
                // Played straight on into Day 3, with a clip still forced on Hùng from the night before.
                var clip=c.Actor("Hùng").Animator.runtimeAnimatorController.animationClips.First();c.Actor("Hùng").SamplePerformance(clip,0);var forced=Pose("Hùng");Assert.NotNull(forced);
                foreach(var t in def.tasks){if(c.State.phase>=4)break;if(!c.State.Done(t.id))Assert.IsTrue(c.State.Complete(t.id),t.id);}
                Assert.AreEqual(4,c.State.phase);
                typeof(Map02ChapterDirector).GetMethod("ApplyState",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,null);yield return null;
                Assert.IsFalse(Sitting("Tâm"),"Day 3: Tâm is up, not still sitting from Night 2.");
                Assert.AreNotSame(forced,Pose("Hùng"),"The clip forced the day before is gone (the new day may give him its own rest pose).");
                // A load of Day 3 after Night 2 in the same session: the same.
                Load("D2_04");yield return null;Load(def.tasks.First(t=>t.phase==4).id);yield return null;
                Assert.IsFalse(Sitting("Tâm"),"Loading Day 3 after Night 2 leaves Tâm standing too.");
            }
            finally{c.Cancel();}
        }
    }
}
