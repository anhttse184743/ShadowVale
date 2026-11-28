using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    /// <summary>Before taking the rescue order, Nam cannot walk out of the command bunker; after it, he can (26/11/2026).</summary>
    public sealed class Map01BunkerHoldTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(240000)]public IEnumerator NoLeavingTheBunkerBeforeTheRescueOrder()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator InPlayMode()
        {
            Map01Mission m=null;float end=Time.realtimeSinceStartup+120;
            while(Time.realtimeSinceStartup<end){m=Object.FindFirstObjectByType<Map01Mission>();if(m!=null&&m.IsInitialized&&!m.Cinematic)break;yield return null;}
            Assert.NotNull(m);Assert.IsTrue(m.IsInitialized);Assert.IsFalse(m.Cinematic,"The opening must hand back control.");
            var quest=m.GetComponent<Map01Quest>();var hold=m.GetComponent<Map01BunkerHold>();Assert.NotNull(hold);
            Assert.AreEqual(Map01Quest.RescueStage,quest.Stage);Assert.IsFalse(quest.RescueOrderAccepted);
            void Place(Vector3 p){var cc=m.player.GetComponent<CharacterController>();cc.enabled=false;m.player.position=p;cc.enabled=true;Physics.SyncTransforms();}
            bool Roofed()=>Physics.Raycast(m.player.position+Vector3.up*1.2f,Vector3.up,6,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<5;i++)yield return null;
            Assert.IsTrue(Roofed(),"Map 1 starts Nam under the bunker roof.");Assert.IsTrue(hold.Holding);
            // Out through the doorway (measured: the roof ends at about (-65.4, -70.1), the ramp runs south from there).
            var outside=new Vector3(-65.2f,.4f,-72.5f);Place(outside);
            for(int i=0;i<5;i++)yield return null;
            Assert.IsTrue(Roofed(),"Before the order, he is held under the roof, not let out.");
            // With the order, the way out is open.
            m.GetComponent<Map01Story>().Progress.rescueAccepted=true;Assert.IsTrue(quest.RescueOrderAccepted);Assert.IsFalse(hold.Holding);
            Place(outside);for(int i=0;i<5;i++)yield return null;
            Assert.IsFalse(Roofed(),"After the order, he walks out.");
            Assert.Less(Vector3.Distance(new Vector3(m.player.position.x,0,m.player.position.z),new Vector3(outside.x,0,outside.z)),1f);
        }
    }
}
