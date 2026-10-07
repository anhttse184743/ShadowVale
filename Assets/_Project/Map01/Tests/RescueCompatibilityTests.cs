using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class RescueCompatibilityTests:ForestSceneTestBase
    {
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static readonly FieldInfo Storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static Map01Rescue R=>M.GetComponent<Map01Rescue>();
        static Map01Quest Q=>M.GetComponent<Map01Quest>();
        static void Place(Vector3 p,Quaternion rotation){var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;M.player.SetPositionAndRotation(p,rotation);cc.enabled=true;Physics.SyncTransforms();}
        static void Isolate(){Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/compat-saves/"+Guid.NewGuid().ToString("N")));foreach(var g in M.Enemies)g.enabled=false;}
        static Vector3 Along(Vector3[] points,float fraction){float length=0;for(int i=1;i<points.Length;i++)length+=Vector3.Distance(points[i-1],points[i]);float d=length*fraction;for(int i=1;i<points.Length;i++){float seg=Vector3.Distance(points[i-1],points[i]);if(d<=seg)return Vector3.Lerp(points[i-1],points[i],d/seg);d-=seg;}return points.Last();}
        [UnityTest]public IEnumerator LegacyEscortSkipsCrossedThresholdsKeepsDeathsAndCanRetry()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;Isolate();
            Q.RestoreStage(Map01Quest.EscortStage);var path=Map01Rescue.Path(R.CaptivePost,R.Layout.shelterDoor.position);var p=Along(path,.6f);
            M.hung.GetComponent<NavMeshAgent>().Warp(p);Place(p+Vector3.left*.7f,Quaternion.identity);
            R.Squad[0].GetComponent<Health>().TakeDamage(9999,R.Squad[0].transform.position,null);
            // Serialize the real payload while omitting every newly introduced optional field.
            var data=typeof(Map01SaveSystem).GetMethod("Capture",Private).Invoke(M.GetComponent<Map01SaveSystem>(),new object[]{false});
            foreach(var key in new[]{"rescue","rescueStart","escortStart"})data.GetType().GetField(key).SetValue(data,null);
            // Unity serializes an inline null class as an empty/default object. A historical
            // checkpoint truly predates this field, so remove it from the serialized JSON.
            string historical=Regex.Replace(JsonUtility.ToJson(data),",?\"rescue\":\\{[^{}]*\\}","");
            StringAssert.DoesNotContain("\"rescue\":",historical);
            ForestSaveSlots.Write(3,new ForestSaveSlots.Entry{version=1,sceneName="Map 1",checkpoint=historical,savedAt=DateTime.UtcNow.ToString("o"),location="legacy escort"});
            Map01SaveSystem.BeginGame(3);yield return WaitGameSeconds(.8f);
            Assert.AreEqual(3,R.WaveMask,"Already crossed 80% and 55% thresholds must be skipped.");Assert.AreEqual(0,R.Pursuers.Count);Assert.IsFalse(R.Squad[0].Alive);
            Assert.AreEqual(Map01Quest.EscortStage,Q.Stage);R.HitHung(9999);Assert.IsTrue(R.Failed);R.Retry();yield return WaitGameSeconds(.8f);
            Assert.IsFalse(R.Failed);Assert.AreEqual(Map01Quest.EscortStage,Q.Stage);Assert.AreEqual(3,R.WaveMask);Assert.AreEqual(0,R.Pursuers.Count);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator KnifeRejectsRangeFrontAndPhysicalObstructionThenKillsOverseerLast()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;Isolate();
            var guard=R.Squad[0];M.ModernCombat.Equip(WeaponKind.Knife);
            Place(guard.transform.position-guard.transform.forward*1.9f,guard.transform.rotation);Assert.IsFalse(guard.CanSilentTakedown());
            Place(guard.transform.position+guard.transform.forward,guard.transform.rotation);Assert.IsFalse(guard.CanSilentTakedown());
            Place(guard.transform.position-guard.transform.forward*1.2f,guard.transform.rotation);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=Vector3.Lerp(M.player.position,guard.transform.position,.5f)+Vector3.up;wall.transform.localScale=Vector3.one*.35f;Physics.SyncTransforms();
            Assert.IsFalse(guard.CanSilentTakedown());Object.Destroy(wall);yield return null;
            foreach(var other in R.Squad.Skip(1)){
                other.GetComponent<Animator>();other.GetComponentInChildren<Animator>().speed=1;
                Place(other.transform.position-other.transform.forward*1.2f,other.transform.rotation);
                Assert.IsTrue(other.TrySilentTakedown());yield return WaitGameSeconds(5);Assert.IsFalse(R.Failed);Assert.IsTrue(R.OverseerAlive);
            }
            guard.GetComponentInChildren<Animator>().speed=1;Place(guard.transform.position-guard.transform.forward*1.2f,guard.transform.rotation);Assert.IsTrue(guard.TrySilentTakedown());yield return WaitGameSeconds(5);
            Assert.AreEqual(0,R.Remaining);Assert.IsTrue(R.CanFree);Assert.IsFalse(R.Failed);yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator PursuerCannotShootHungThroughSolidCover()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;Isolate();
            Q.RestoreStage(Map01Quest.EscortStage);var guard=R.Squad[0];
            var start=new Vector3(-19,3,65);NavMesh.SamplePosition(start,out var nav,4,NavMesh.AllAreas);start=nav.position;
            guard.GetComponent<NavMeshAgent>().Warp(start);M.hung.GetComponent<NavMeshAgent>().Warp(start+Vector3.forward*6);guard.FaceImmediate(M.hung.position);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=(guard.transform.position+M.hung.position)*.5f+Vector3.up;wall.transform.localScale=new Vector3(6,3,.6f);Physics.SyncTransforms();
            float health=R.HungHealth;Assert.IsFalse(guard.SafeShot(M.hung.position));Assert.AreEqual(health,R.HungHealth);
            Object.Destroy(wall);yield return null;yield return new ExitPlayMode();
        }
        [TearDown]public void Cleanup()=>Storage.SetValue(null,null);
    }
}
