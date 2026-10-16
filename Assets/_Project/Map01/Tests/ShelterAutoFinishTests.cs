using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
    public sealed class ShelterAutoFinishTests:ForestSceneTestBase {
        static readonly FieldInfo Storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
        [UnityTest] public IEnumerator NamFirstCompletesInsideWhileHungIsStillFarOutside(){
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            yield return Review(false);yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator HungFirstStartsImmediatelyAndCompletesWhenNamEnters(){
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            yield return Review(true);yield return new ExitPlayMode();
        }
        static float Value(Map01RescueCinematic d,string name)=>(float)typeof(Map01RescueCinematic).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(d);
        static float Length(Vector3[] path){float total=0;for(int i=1;i<path.Length;i++)total+=Vector3.Distance(path[i-1],path[i]);return total;}
        static Vector3 BackFromDoor(Vector3[] path,float remaining){
            for(int i=path.Length-1;i>0;i--){float span=Vector3.Distance(path[i-1],path[i]);if(span>=remaining)return Vector3.Lerp(path[i],path[i-1],remaining/span);remaining-=span;}
            return path[0];
        }
        static IEnumerator Review(bool hungFirst){
            Storage.SetValue(null,Path.GetFullPath("Logs/ShelterAutoFinish/saves/"+Guid.NewGuid().ToString("N")));
            var mission=Object.FindFirstObjectByType<Map01Mission>();var rescue=mission.GetComponent<Map01Rescue>();
            rescue.MarkIntroSeen();foreach(var enemy in mission.Enemies)if(enemy!=null)enemy.enabled=false;
            var layout=rescue.Layout;var quest=mission.GetComponent<Map01Quest>();quest.RestoreStage(Map01Quest.EscortStage);
            var path=Map01Rescue.Path(rescue.CaptivePost,layout.shelterDoor.position);Assert.Greater(path.Length,1);
            var outside=BackFromDoor(path,hungFirst?9:30);Assert.IsTrue(NavMesh.SamplePosition(outside,out var point,1,NavMesh.AllAreas));
            var cc=mission.player.GetComponent<CharacterController>();cc.enabled=false;
            mission.player.position=hungFirst?point.position:layout.shelterDoor.position;cc.enabled=true;
            mission.hung.GetComponent<NavMeshAgent>().Warp(hungFirst?layout.shelterDoor.position:point.position);
            Physics.SyncTransforms();int supplies=mission.GetComponent<Map01Inventory>().Count("supplies"),ammo=mission.ModernCombat.RoundsInMagazine;
            yield return WaitGameSeconds(.35f);
            var director=mission.GetComponent<Map01RescueCinematic>();Assert.NotNull(director);Assert.IsTrue(director.IsPlaying);
            Assert.AreEqual(Map01Rescue.Phase.EnteringShelter,rescue.CurrentPhase);Assert.IsTrue(rescue.SafeReached);
            Assert.AreEqual(hungFirst,(bool)typeof(Map01RescueCinematic).GetField("hungFirst",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(director));
            Assert.Less(Vector3.Distance(hungFirst?mission.player.position:mission.hung.position,point.position),1,"Start from the follower's actual position.");
            float deadline=Time.realtimeSinceStartup+90;
            while(director.IsPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(director.IsPlaying);Assert.GreaterOrEqual(Value(director,"namDistance"),Value(director,"namInsideDistance")-.001f);
            if(!hungFirst){
                var hungPath=(Vector3[])typeof(Map01RescueCinematic).GetField("hungPath",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(director);
                Assert.Greater(Length(hungPath)-Value(director,"hungDistance"),10,"Nam entering must finish even while Hung has a long route left.");
            }
            Assert.AreEqual(Map01Quest.BriefingStage,quest.Stage);Assert.IsTrue(rescue.RewardDelivered);
            Assert.Less(Vector3.Distance(mission.player.position,layout.reportPoint.position),hungFirst?1.7f:.15f,"Keep the existing leader/follower endpoints when auto-skipping.");
            Assert.Greater(Vector3.Distance(mission.player.position,mission.hung.position),1.3f);
            Assert.AreEqual(supplies+1,mission.GetComponent<Map01Inventory>().Count("supplies"));
            director.Skip();Assert.IsFalse(rescue.CompleteDelivery());Assert.AreEqual(supplies+1,mission.GetComponent<Map01Inventory>().Count("supplies"));
            Assert.AreEqual(ammo,mission.ModernCombat.RoundsInMagazine);Assert.IsTrue(cc.enabled&&mission.ModernPlayer.enabled&&mission.ModernCombat.enabled);
            Assert.IsFalse(mission.Cinematic);Assert.IsFalse(mission.hung.GetComponentInChildren<Map01HungVisual>().RopeVisible);
            Directory.CreateDirectory("Logs/ShelterAutoFinish");File.WriteAllText("Logs/ShelterAutoFinish/"+(hungFirst?"HungFirst":"NamFirst")+".txt",$"Elapsed {director.Elapsed:F3}; Nam path {Value(director,"namDistance"):F3}; inside marker {Value(director,"namInsideDistance"):F3}; Hung path {Value(director,"hungDistance"):F3}; supplies +1; controls restored.");
        }
        [TearDown]public void ResetStorage()=>Storage.SetValue(null,null);
    }
}
