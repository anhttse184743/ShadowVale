using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02EnemyAnimationTests:ForestSceneTestBase
    {
        [Test]public void EnemyControllerIsTheSameAssetUsedInMapOne()
        {
            var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Characters/Animations/Controllers/AC_Enemy.controller");
            Assert.NotNull(controller);Assert.AreSame(controller,Map01ActorAssets.Load().enemyController);
        }
        [UnityTest,Timeout(240000)]public IEnumerator ChapterSpawnsAndRestoredEnemiesHaveLiveAnimationAndRifleGrip()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);
            var preview=System.Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyToMission");
            preview.Invoke(null,new object[]{m,2});
            var day=m.Capture();day.chapter.Complete("D2_01");m.Restore(day);
            float spawnDeadline=Time.time+20;
            while(Time.time<spawnDeadline&&!m.Chapter.Enemies.Any(e=>e!=null&&!e.Dead&&e.gameObject.activeInHierarchy))yield return null;
            yield return WaitGameSeconds(.3f);
            var enemies=m.Chapter.Enemies.Where(e=>e!=null&&!e.Dead&&e.gameObject.activeInHierarchy).ToArray();Assert.IsNotEmpty(enemies);
            foreach(var enemy in enemies)AssertPresentation(enemy);
            var guard=enemies[0];var actor=guard.GetComponentInChildren<Animator>();var agent=guard.GetComponent<NavMeshAgent>();
            // A real non-hostile patrol must update Speed, not only the combat branch.
            float until=Time.time+12;while(Time.time<until&&actor.GetFloat("Speed")<.1f)yield return null;
            Assert.Greater(actor.GetFloat("Speed"),.1f,"Patrolling enemies must animate their NavMesh movement.");
            guard.enabled=false;agent.isStopped=true;
            var tick=typeof(Map02ChapterEnemy).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            guard.Hear(guard.transform.position+Vector3.forward*2,true);tick.Invoke(guard,null);Assert.IsTrue(actor.GetBool("Engaged"));
            yield return WaitGameSeconds(.35f);
            CapturePose(guard,"aim");
            actor.SetTrigger("Attack");yield return WaitGameSeconds(.15f);
            Assert.IsTrue(actor.GetCurrentAnimatorStateInfo(0).IsName("Fire")||actor.GetNextAnimatorStateInfo(0).IsName("Fire"));
            yield return WaitGameSeconds(1);
            guard.HitPoints.TakeDamage(1,guard.transform.position,m.Player.gameObject);yield return WaitGameSeconds(.1f);
            Assert.IsTrue(actor.GetCurrentAnimatorStateInfo(0).IsName("Hit")||actor.GetNextAnimatorStateInfo(0).IsName("Hit"));
            guard.HitPoints.TakeDamage(200,guard.transform.position,m.Player.gameObject);yield return WaitGameSeconds(2);
            Assert.IsTrue(guard.Dead);Assert.IsTrue(actor.GetCurrentAnimatorStateInfo(0).IsName("Die_0"));
            CapturePose(guard,"dead");
            string guardKey=guard.id;var saved=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));m.Restore(saved);yield return WaitGameSeconds(2);
            var restored=FindEnemy(m.Chapter,guardKey);AssertPresentation(restored);
            Assert.IsTrue(restored.Dead);Assert.IsTrue(restored.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Die_0"));
            foreach(var enemy in m.Chapter.Enemies.Where(e=>e!=null))AssertPresentation(enemy);
            yield return new ExitPlayMode();
        }
        static Map02ChapterEnemy FindEnemy(Map02ChapterDirector chapter,string key)=>chapter.Enemies.Single(e=>e.id==key);
        static void AssertPresentation(Map02ChapterEnemy enemy)
        {
            var actor=enemy.GetComponentInChildren<Animator>();Assert.NotNull(actor,enemy.id);
            Assert.AreSame(Map01ActorAssets.Load().enemyController,actor.runtimeAnimatorController,enemy.id);
            Assert.IsTrue(actor.isHuman,enemy.id);Assert.IsFalse(actor.applyRootMotion);
            Assert.IsTrue(actor.GetComponent<Map01EnemyGrip>()?.Calibrated==true,enemy.id+" rifle contact");
            Assert.NotNull(actor.GetComponent<Map01Rifle>()?.weapon);
        }
        static void CapturePose(Map02ChapterEnemy enemy,string label)
        {
            var host=new GameObject("Enemy animation review camera");var camera=host.AddComponent<Camera>();
            host.transform.position=enemy.transform.position+enemy.transform.forward*3+enemy.transform.right*2+Vector3.up*1.7f;
            host.transform.LookAt(enemy.transform.position+Vector3.up*.85f);camera.fieldOfView=40;camera.nearClipPlane=.1f;
            var target=new RenderTexture(768,768,24);var pixels=new Texture2D(768,768,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,768,768),0,0);pixels.Apply();System.IO.Directory.CreateDirectory("Logs/Map02Chapter/EnemyAnimation");System.IO.File.WriteAllBytes("Logs/Map02Chapter/EnemyAnimation/"+label+".png",pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(host);Object.DestroyImmediate(pixels);target.Release();Object.DestroyImmediate(target);}
        }
    }
}
