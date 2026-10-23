using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02ClarityTests:ForestSceneTestBase
    {
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        [Test]public void NorthUpMapAndCompassShareOneProjection()
        {
            var rect=new Rect(28,24,230,230);var north=Map02Navigation.Plot(Vector3.forward*40,rect);var east=Map02Navigation.Plot(Vector3.right*40,rect);
            Assert.Less(north.y,rect.center.y);Assert.Greater(east.x,rect.center.x);
            Assert.Less(Vector2.Distance(north,Map02Navigation.Disc(Vector3.forward*40,rect.center,115)),.01f);
            Assert.AreEqual(new Vector2(.5f,.5f),Map02Navigation.ChartUv(Vector3.zero));
            Assert.LessOrEqual(Vector2.Distance(Map02Navigation.Disc(Vector3.one*10000,rect.center,115),rect.center),107.01f);
        }
        [Test]public void FocusIsStableSelectableAndCannotAbandonCarriedCargo()
        {
            var s=Prefix("D1_02");Assert.AreEqual("D1_03",s.TrackedTask.id);Assert.IsTrue(s.Track("D1_05"));Assert.AreEqual("D1_05",s.TrackedTask.id);
            var save=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(s));save.Validate();Assert.AreEqual("D1_05",save.TrackedTask.id);
            s.activeTask="D1_04";s.carry="D1_04";Assert.IsFalse(s.Track("D1_03"));Assert.AreEqual("D1_04",s.TrackedTask.id);
            s.carry=null;Assert.IsTrue(s.Track("D1_03"));Assert.AreEqual("D1_03",s.TrackedTask.id);
        }
        [Test]public void IconsRotateAtTheirOwnPositionAtEveryHudScale()
        {
            var pivot=new Vector3(111,157,0);
            foreach(float scale in new[]{.8f,1f,1.2f,2.4f})foreach(float angle in new[]{0f,45f,90f,180f,270f})
            {
                var canvas=Matrix4x4.Scale(new Vector3(scale,scale,1));var rotated=Map02Navigation.RotateUi(canvas,pivot,angle);
                Assert.Less(Vector3.Distance(canvas.MultiplyPoint3x4(pivot),rotated.MultiplyPoint3x4(pivot)),.001f,$"pivot drift at {scale} / {angle}");
            }
        }
        [Test]public void PostLampBeamStartsAtTheLampAndPointsWhereTheShadeFacesAtEveryHudScale()
        {
            // 1920×1080 drew the beam above the panel's text (screenshot post-lamp.png, 23/10): the pivot was taken in screen pixels.
            var lamp=new Vector2(780,455);
            foreach(float scale in new[]{1f,1.2f,2.4f})foreach(float angle in new[]{0f,90f,180f,270f})
            {
                var canvas=Matrix4x4.Scale(new Vector3(scale,scale,1));var beam=Map02ChapterDirector.LampBeamMatrix(canvas,lamp,angle);
                var tip=new Vector3(lamp.x+Mathf.Sin(angle*Mathf.Deg2Rad)*130,lamp.y-Mathf.Cos(angle*Mathf.Deg2Rad)*130,0);
                Assert.Less(Vector3.Distance(canvas.MultiplyPoint3x4(lamp),beam.MultiplyPoint3x4(lamp)),.001f,$"beam root at {scale} / {angle}");
                Assert.Less(Vector3.Distance(canvas.MultiplyPoint3x4(tip),beam.MultiplyPoint3x4(new Vector3(lamp.x+130,lamp.y,0))),.01f,$"beam tip at {scale} / {angle}");
            }
        }
        [Test]public void EveryTaskExplainsItsPurposeWithoutRevealingFutureTasksInHud()
        {
            foreach(var task in Map02ChapterDefinition.Load().tasks)Assert.IsFalse(string.IsNullOrWhiteSpace(task.purpose),task.id);
            var s=new Map02ChapterState();Assert.IsFalse(s.Objective.Contains("hy sinh"));
            s=Prefix("D1_02");Assert.IsFalse(s.AvailableTasks.Any(t=>t.phase!=0));
        }
        [Test]public void DialogueFollowsWherePeopleActuallyAreAndWhyTheyMove()
        {
            var d=Map02ChapterDefinition.Load();Assert.AreEqual(43,d.tasks.Length);Assert.AreEqual(140,d.entries.Length);Assert.True(d.Lines("arrivalbank").All(e=>!e.radio));Assert.AreEqual(2,d.Lines("duckevent").Length);
            Assert.IsFalse(d.Lines("day1report").Any(e=>e.speaker=="Hùng"));
            Assert.IsTrue(d.Lines("promise").Any(e=>e.id=="m2v3_day1report_04"));
            Assert.IsTrue(d.Lines("boatsafe").All(e=>e.radio));
            Assert.IsTrue(d.Lines("child").Last().speaker=="Chú Bảy");
            Assert.AreEqual("patrolHung",d.Find("N1_03").kind);Assert.AreEqual("cinematic",d.Find("N1_04").kind);
            Assert.AreEqual("command",d.Find("D2_04").site);
        }
        [UnityTest,Timeout(360000)]public IEnumerator LiveFocusPromptsAndRealEscortPathKeepRescuedPeopleAtSafety()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();
            Map02Mission m=null;float limit=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<limit){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;
            void Load(string id){var snapshot=m.Capture();snapshot.chapter=Prefix(id);m.Restore(snapshot);}
            Map02ObjectiveObject Mark(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
            Load("D1_02");Assert.AreEqual("D1_03",m.TrackedObjective.chapterId);
            Place(m,c.Sites["dock"].position);yield return null;Assert.AreEqual("D1_03",m.TrackedObjective.chapterId,"Walking near another task must not switch the minimap pin.");
            Assert.IsTrue(c.Track("D1_05"));var snapshot=m.Capture();m.Restore(snapshot);Assert.AreEqual("D1_05",m.TrackedObjective.chapterId);
            var crate=Mark("D1_04");Place(m,crate.transform.position);Assert.IsTrue(m.Interact(crate));Assert.IsTrue(crate.prompt.StartsWith("Đặt thùng"));Assert.IsFalse(c.Track("D1_05"));
            var pickup=c.Sites["arrival"].position;Assert.Greater(Vector3.Distance(crate.transform.position,pickup),10);
            Assert.IsFalse(m.Interact(crate),"A second E at the pickup cannot deliver cargo remotely.");Place(m,crate.transform.position);Assert.IsTrue(m.Interact(crate));Assert.IsTrue(crate.prompt.StartsWith("Nhận thùng"));
            Directory.CreateDirectory("Logs/Map02Chapter/Clarity");ScreenCapture.CaptureScreenshot("Logs/Map02Chapter/Clarity/fixed-minimap.png");yield return null;
            Load("N3_01");var family=Mark("N3_02");Place(m,family.transform.position);Assert.IsTrue(m.Interact(family));Assert.AreEqual("N3_02",c.CurrentScene.id);Assert.IsFalse(c.State.Done("N3_02"));typeof(Map02ChapterDirector).GetMethod("EndScene",Private).Invoke(c,null);
            var follower=typeof(Map02ChapterDirector).GetField("secondaryEscort",Private).GetValue(c);Assert.AreSame(c.Actor("Chị Lành"),follower,"Both members of the family must be escorted.");
            var daughterStart=c.Actor("Chị Lành").transform.position;c.Actor("Chị Lành").transform.position=m.Player.position+Vector3.forward*12;
            Assert.AreSame(c.Actor("Chị Lành"),c.Companion);
            StringAssert.Contains("ông Sáu và chị Lành",c.Instruction);
            StringAssert.Contains("đang theo sau, cách 12 m",c.Instruction);
            c.Actor("Chị Lành").transform.position=daughterStart;
            var controller=m.Player.GetComponent<PlayerController>();bool wasEnabled=controller.enabled;controller.enabled=false;
            var body=m.Player.GetComponent<CharacterController>();var path=new NavMeshPath();var destination=c.Sites["southStore"].position;
            Assert.IsTrue(NavMesh.CalculatePath(m.Player.position,destination,NavMesh.AllAreas,path));Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status);
            int corner=1;float until=Time.time+180;float stuckAt=Time.time;var last=m.Player.position;
            try
            {
                while(!c.State.Done("N3_02")&&c.CurrentScene==null&&Time.time<until)
                {
                    m.Player.GetComponent<Health>().CinematicInvulnerable=true;
                    foreach(var enemy in c.Enemies)if(enemy!=null)enemy.gameObject.SetActive(false); // Isolate traversal, not a combat balance test.
                    if(corner<path.corners.Length)
                    {
                        var delta=Vector3.ProjectOnPlane(path.corners[corner]-m.Player.position,Vector3.up);
                        if(delta.magnitude<.45f)corner++;
                        else{m.Player.rotation=Quaternion.LookRotation(delta);body.Move((delta.normalized*.9f+Vector3.down*2)*Mathf.Min(.05f,Time.deltaTime));}
                    }
                    if(Vector3.Distance(last,m.Player.position)>.3f){last=m.Player.position;stuckAt=Time.time;}
                    Assert.Less(Time.time-stuckAt,15,"CharacterController got stuck on the real escort route at "+m.Player.position);
                    yield return null;
                }
                Assert.IsTrue(c.State.Done("N3_02"),"The family must reach the actual dock without teleporting.");
            }
            finally{controller.enabled=wasEnabled;}
            if(c.CurrentScene!=null)typeof(Map02ChapterDirector).GetMethod("EndScene",Private).Invoke(c,null);yield return null;
            var elder=c.Actor("Ông Sáu").transform.position;var daughter=c.Actor("Chị Lành").transform.position;
            Assert.Less(Vector3.Distance(elder,destination),5);Assert.Less(Vector3.Distance(daughter,destination),5);
            Assert.IsTrue(c.State.Complete("N3_03"));typeof(Map02ChapterDirector).GetMethod("ApplyState",Private).Invoke(c,null);
            Assert.AreEqual(elder,c.Actor("Ông Sáu").transform.position);Assert.AreEqual(daughter,c.Actor("Chị Lành").transform.position);
            snapshot=m.Capture();m.Restore(snapshot);Assert.AreEqual(elder,c.Actor("Ông Sáu").transform.position);Assert.AreEqual(daughter,c.Actor("Chị Lành").transform.position);
            snapshot.chapter.actors.Single(a=>a.role=="Ông Sáu").position=c.Sites["family"].position;m.Restore(snapshot);Assert.Less(Vector3.Distance(c.Actor("Ông Sáu").transform.position,destination),8,"Repair old saves where rescued residents were reset to their houses.");
            ScreenCapture.CaptureScreenshot("Logs/Map02Chapter/Clarity/family-at-safety.png");yield return null;
            yield return new ExitPlayMode();
        }
        [UnityTest,Timeout(240000)]public IEnumerator RifleActuallyHitsMapTwoEnemyAndARealWallBlocksTheBullet()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;var combat=m.Player.GetComponent<PlayerCombat>();combat.Equip(WeaponKind.Rifle);combat.RestoreMagazine(10);
            var host=new GameObject("Map 2 actual gunfire test");host.transform.position=c.Sites["bridge"].position;var enemy=host.AddComponent<Map02ChapterEnemy>();enemy.Initialize(c,"gun_test","gun_test",100,false);enemy.enabled=false;
            var rig=Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None).First(r=>r.GetComponent<Camera>().enabled);var eye=host.transform.position+Vector3.forward*2+Vector3.up*1.1f;var look=Quaternion.LookRotation(host.transform.position+Vector3.up*1.1f-eye);rig.SetCinematicView(eye,look,60,1);yield return null;
            void Fire(){typeof(PlayerCombat).GetField("_nextAttackTime",Private).SetValue(combat,0f);typeof(PlayerCombat).GetMethod("Attack",Private).Invoke(combat,null);typeof(PlayerCombat).GetMethod("ResolvePendingGunfire",Private).Invoke(combat,null);}
            Physics.SyncTransforms();Fire();Assert.Less(enemy.HitPoints.Current,100,"Actual rifle ray must hit the Map 2 enemy collider.");Assert.AreEqual(9,combat.RoundsInMagazine);
            enemy.HitPoints.RestoreHealth(100);var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=host.transform.position+Vector3.forward+Vector3.up;wall.transform.localScale=new Vector3(2,2,.3f);Physics.SyncTransforms();
            rig.SetCinematicView(eye,look,60,1);yield return null;Fire();Assert.AreEqual(100,enemy.HitPoints.Current,"The wall must stop damage.");Assert.AreEqual(8,combat.RoundsInMagazine);
            rig.ClearCinematicView();Object.Destroy(wall);Object.Destroy(host);yield return new ExitPlayMode();
        }
        static Map02ChapterState Prefix(string id){var s=new Map02ChapterState();foreach(var task in Map02ChapterDefinition.Load().tasks){Assert.IsTrue(s.Complete(task.id));if(task.id==id)break;}return s;}
        static void Place(Map02Mission m,Vector3 p){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=p+Vector3.up*.2f;cc.enabled=true;m.Player.GetComponent<PlayerController>().RestoreMotion(false);Physics.SyncTransforms();}
    }
}
