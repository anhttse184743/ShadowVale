using System.IO;
using NUnit.Framework;
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
