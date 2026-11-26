using System.IO;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map02EvacuationChoiceTests
    {
        static Map02ChapterState AfterStore()
        {
            var state=new Map02ChapterState();
            foreach(var task in Map02ChapterDefinition.Load().tasks){Assert.IsTrue(state.Complete(task.id));if(task.id=="N2_03")break;}
            state.night2EvacuationStarted=true;return state;
        }
        [TestCase(1,false)]
        [TestCase(1,true)]
        [TestCase(2,false)]
        [TestCase(3,false)]
        public void EvacuationReportStartsForTheSelectedRoute(int choice,bool alreadyObserved)
        {
            const BindingFlags privateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
            var session=new GameObject("Map 2 evacuation interaction test");
            session.SetActive(false);
            var pin=new GameObject("Evacuation pin");
            try
            {
                var state=AfterStore();
                Assert.IsTrue(state.SelectEvacuationRoute(choice));
                Assert.IsEmpty(state.night2RouteVisited);
                session.transform.position=pin.transform.position=new Vector3(1000,0,1000);
                var marker=pin.AddComponent<Map02ObjectiveObject>();
                marker.chapterId="N2_04";
                var player=session.AddComponent<PlayerController>();
                var mission=session.AddComponent<Map02Mission>();
                var chapter=session.AddComponent<Map02ChapterDirector>();
                var chapterType=typeof(Map02ChapterDirector);
                chapterType.GetProperty(nameof(Map02ChapterDirector.State)).SetValue(chapter,state);
                chapterType.GetProperty(nameof(Map02ChapterDirector.Mission)).SetValue(chapter,mission);
                chapterType.GetField("player",privateInstance).SetValue(chapter,player);
                chapterType.GetField("ready",privateInstance).SetValue(chapter,true);
                chapterType.GetField("evacuationConfigured",privateInstance).SetValue(chapter,true);
                var missionType=typeof(Map02Mission);
                missionType.GetProperty(nameof(Map02Mission.Chapter)).SetValue(mission,chapter);
                missionType.GetProperty(nameof(Map02Mission.Ready)).SetValue(mission,choice!=1);
                missionType.GetField("targets",privateInstance).SetValue(mission,new[]{marker});
                missionType.GetField("player",privateInstance).SetValue(mission,player);
                mission.World.story.stage=Map02Stage.QuietPreparation;

                var nearestObjective=missionType.GetMethod("NearestObjective",privateInstance);
                var target=(Map02ObjectiveObject)nearestObjective.Invoke(mission,null);
                if(choice==1)
                {
                    Assert.IsNull(target,"The field pin has no E interaction.");
                    var cover=(Vector3[])chapterType.GetField("evacuationPoints",privateInstance).GetValue(chapter);
                    cover[1]=pin.transform.position;
                    var tickReport=chapterType.GetMethod("TickEvacuationFieldReport",privateInstance);
                    if(alreadyObserved)
                    {
                        state.night2FieldObserved=true;
                        session.transform.position=cover[1]+Vector3.right*3;
                        tickReport.Invoke(chapter,new object[]{0f});
                        Assert.IsFalse(chapter.Busy,"A saved observation waits for Nam to return to cover.");
                        session.transform.position=cover[1];
                        tickReport.Invoke(chapter,new object[]{0f});
                    }
                    else
                    {
                        var view=new GameObject("Binocular view");
                        view.transform.SetParent(session.transform,false);
                        view.transform.localPosition=Vector3.up*1.3f;
                        view.AddComponent<Camera>();
                        chapterType.GetField("rig",privateInstance).SetValue(chapter,view.AddComponent<ThirdPersonCamera>());
                        chapterType.GetField("binocular",privateInstance).SetValue(chapter,true);

                        var guard=new GameObject("Visible patrol");
                        guard.transform.SetParent(pin.transform,false);
                        guard.transform.localPosition=Vector3.forward*8;
                        guard.AddComponent<BoxCollider>().center=Vector3.up;
                        var health=guard.AddComponent<Health>();
                        health.SetMaxHealth(100);
                        var unit=guard.AddComponent<Map02ChapterEnemy>();
                        unit.group=Map02ChapterDirector.EvacuationPatrolGroup;
                        typeof(Map02ChapterEnemy).GetProperty(nameof(Map02ChapterEnemy.HitPoints)).SetValue(unit,health);
                        chapter.Enemies.Add(unit);
                        Physics.SyncTransforms();

                        tickReport.Invoke(chapter,new object[]{.9f});
                        Assert.IsFalse(chapter.Busy,"Observation must reach a full second.");
                        tickReport.Invoke(chapter,new object[]{.11f});
                        Assert.IsTrue(state.night2FieldObserved);
                    }
                }
                else
                {
                    Assert.AreSame(marker,target);
                    Assert.IsTrue(mission.Interact(target),"Interact must start the radio report.");
                }
                Assert.IsTrue(chapter.Busy);
                Assert.IsFalse(state.Done("N2_04"),"The route can resolve after the radio response.");
            }
            finally
            {
                Object.DestroyImmediate(pin);
                Object.DestroyImmediate(session);
            }
        }
        [Test] public void VillageFirstCompletesWithoutCheckingOtherRoutes()
        {
            var state=AfterStore();Assert.IsTrue(state.SelectEvacuationRoute(3));
            Assert.IsTrue(state.ResolveEvacuationReport());Assert.IsTrue(state.Done("N2_04"));
            Assert.AreEqual(0,state.night2RejectedRoutes);Assert.IsFalse(state.night2FieldObserved);
            state.Validate();
        }
        [Test] public void FieldRequiresObservationAndRejectedRoutesDoNotCompleteTask()
        {
            var state=AfterStore();Assert.IsTrue(state.SelectEvacuationRoute(1));
            Assert.IsFalse(state.CanReportEvacuationRoute);Assert.IsFalse(state.ResolveEvacuationReport());
            state.night2FieldObserved=true;Assert.IsTrue(state.ResolveEvacuationReport());
            Assert.IsFalse(state.Done("N2_04"));Assert.IsFalse(state.SelectEvacuationRoute(1));
            Assert.IsTrue(state.SelectEvacuationRoute(2));Assert.IsTrue(state.ResolveEvacuationReport());
            Assert.AreEqual(3,state.night2RejectedRoutes);Assert.IsFalse(state.Done("N2_04"));
            Assert.IsTrue(state.SelectEvacuationRoute(3));Assert.IsTrue(state.ResolveEvacuationReport());state.Validate();
        }
        [Test] public void RiverFirstCanBeRejectedWithoutFieldObservation()
        {
            var state=AfterStore();Assert.IsTrue(state.SelectEvacuationRoute(2));Assert.IsTrue(state.ResolveEvacuationReport());
            Assert.AreEqual(2,state.night2RejectedRoutes);Assert.IsFalse(state.night2FieldObserved);Assert.IsFalse(state.Done("N2_04"));
            Assert.IsTrue(state.SelectEvacuationRoute(3));Assert.IsTrue(state.ResolveEvacuationReport());
        }
        [Test] public void SaveRoundTripPreservesChoiceObservationAndPatrolPhase()
        {
            var state=AfterStore();state.SelectEvacuationRoute(1);state.night2FieldObserved=true;
            state.night2EvacuationPatrolVersion=1;state.night2EvacuationPatrolTriggered=true;state.night2EvacuationPatrolDirection=-1;
            state.night2EvacuationPatrolAtEnd=true;state.night2EvacuationPatrolClock=1.5f;
            var saved=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(state));saved.Validate();
            Assert.IsTrue(saved.CanReportEvacuationRoute);Assert.IsTrue(saved.night2EvacuationPatrolAtEnd);
            Assert.AreEqual(1.5f,saved.night2EvacuationPatrolClock);
            Assert.IsTrue(saved.night2EvacuationPatrolTriggered);Assert.AreEqual(-1,saved.night2EvacuationPatrolDirection);
        }
        [Test] public void OldRouteProgressDoesNotSilentlyAcceptVillageAndCompletedSavesStayDone()
        {
            var state=AfterStore();state.night2EvacuationStarted=false;
            state.night2RouteVisited.AddRange(new[]{"ditchApproach","villageLane","southBank"});state.Validate();
            Assert.IsFalse(state.Done("N2_04"));Assert.IsFalse(state.SelectEvacuationRoute(3));
            Assert.IsTrue(state.Complete("N2_04"));state.Validate();Assert.IsTrue(state.Done("N2_04"));
        }
        [Test] public void InvalidChoiceAndForgedRejectedChoiceAreRejected()
        {
            var state=AfterStore();Assert.IsFalse(state.SelectEvacuationRoute(0));Assert.IsFalse(state.SelectEvacuationRoute(4));
            state.night2RejectedRoutes=1;state.night2EvacuationChoice=1;
            Assert.Throws<InvalidDataException>(()=>state.Validate());
        }
        [Test] public void LightConeUsesFullSixtyDegreesAndTenMetres()
        {
            Assert.IsTrue(Map02ChapterDirector.InEvacuationLightCone(Vector3.zero,Vector3.forward,Vector3.forward*9.9f));
            Assert.IsFalse(Map02ChapterDirector.InEvacuationLightCone(Vector3.zero,Vector3.forward,Vector3.forward*10.1f));
            Assert.IsTrue(Map02ChapterDirector.InEvacuationLightCone(Vector3.zero,Vector3.forward,Quaternion.Euler(0,29,0)*Vector3.forward*9));
            Assert.IsFalse(Map02ChapterDirector.InEvacuationLightCone(Vector3.zero,Vector3.forward,Quaternion.Euler(0,31,0)*Vector3.forward*9));
            Assert.IsFalse(Map02ChapterDirector.InEvacuationLightCone(Vector3.zero,Vector3.forward,Vector3.back));
        }
    }
}
