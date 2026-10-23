using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02DayOneTests
    {
        Map02DayOneState New(int seed=73){var s=new Map02DayOneState();s.Initialize(seed);return s;}
        bool Solve(Map02DayOneState s,int index=0)
        {
            if(index==3)return s.Packed;
            for(int r=0;r<2;r++)for(int x=2;x<6;x++)for(int y=0;y<4;y++)
            {if(!s.PlaceCrate(index,x,y,r==1,out _))continue;if(Solve(s,index+1))return true;s.crates[index]=new Map02DayOneState.Placement();}
            return false;
        }
        [TestCase(0)][TestCase(1)][TestCase(2)]public void EveryWetTemplateHasSolution(int pattern){var s=New();s.wetPattern=pattern;Assert.True(Solve(s));s.Validate();}
        [Test]public void InvalidCrateMoveLeavesPreviousPosition(){var s=New();s.wetPattern=0;Assert.True(s.PlaceCrate(0,2,0,false,out _));Assert.False(s.PlaceCrate(0,0,0,false,out var reason));StringAssert.Contains("lối",reason);Assert.AreEqual(2,s.crates[0].x);Assert.False(s.PlaceCrate(1,2,0,false,out reason));StringAssert.Contains("chồng",reason);Assert.False(s.PlaceCrate(2,5,2,false,out reason));}
        [Test]public void SaveAndResetKeepPuzzleIdentity(){var s=New();Solve(s);s.stakeHits=4;s.posts[1].bundleStowed=true;s.postsReached=1;var restored=JsonUtility.FromJson<Map02DayOneState>(JsonUtility.ToJson(s));restored.Initialize(999);restored.Validate();Assert.AreEqual(s.seed,restored.seed);Assert.True(restored.Packed);Assert.AreEqual(4,restored.stakeHits);CollectionAssert.AreEqual(s.boardOrder,restored.boardOrder);restored.ResetCrates();Assert.False(restored.Packed);Assert.AreEqual(s.wetPattern,restored.wetPattern);}
        [Test]public void StakeHitsAndRepairUnitsAreDifferent(){var s=New();Assert.False(s.Strike(0));for(int i=0;i<6;i++)Assert.True(s.Strike(.5f));Assert.False(s.DockReady);Assert.False(s.Strike(.5f));Assert.False(s.LayBoard(3,0,false,out _));Assert.False(s.LayBoard(4,0,false,out _));Assert.False(s.LayBoard(0,0,true,out _));Assert.False(s.LayBoard(0,0,false,.2f,out _));for(int i=0;i<3;i++)Assert.True(s.LayBoard(i,i,false,out _));Assert.True(s.DockReady);s.Validate();}
        [Test]public void PostsContainOnlySpecifiedFaultCounts(){for(int i=0;i<100;i++){var s=New(i);Assert.AreEqual(1,s.posts[0].faults);Assert.False(s.posts[0].LampSafe);Assert.True(s.posts[0].bundleStowed&&s.posts[0].latchSeated);Assert.Contains(s.posts[1].faults,new[]{3,5,6});}}
        [Test]public void LampNeedsShieldAndGateNeedsBothSteps(){var s=New();var p=s.posts[0];p.lampAngle=180;Assert.True(p.Safe);p.gateClosed=false;Assert.False(p.Safe);p.gateClosed=true;p.latchSeated=false;Assert.False(p.Safe);p.latchSeated=true;p.bundleStowed=false;Assert.False(p.Safe);}
        [Test]public void TwoQuestPacketsAreIdempotent(){var s=New();Assert.True(s.GiveMedicine(0));Assert.False(s.GiveMedicine(0));Assert.True(s.GiveMedicine(1));Assert.AreEqual(3,s.medicineMask);}
        [Test]public void CorruptSavedGeometryIsRejected(){var s=New();s.crates[0].x=0;s.crates[0].y=0;Assert.Throws<System.IO.InvalidDataException>(()=>s.Validate());}
        [TestCase("D1_03","D1_04","D1_05")][TestCase("D1_03","D1_05","D1_04")][TestCase("D1_04","D1_03","D1_05")][TestCase("D1_04","D1_05","D1_03")][TestCase("D1_05","D1_03","D1_04")][TestCase("D1_05","D1_04","D1_03")]
        public void EveryChoreOrderWaitsForReport(string first,string second,string third){var state=new Map02ChapterState();state.Complete("D1_01");state.Complete("D1_02");Assert.True(state.Complete(first));Assert.False(state.Available(Map02ChapterDefinition.Load().Find("D1_06")));Assert.True(state.Complete(second));Assert.True(state.Complete(third));Assert.AreEqual(0,state.phase);Assert.True(state.Complete("D1_06"));Assert.AreEqual(1,state.phase);state.Validate();}
        [Test]public void ApprovedAcknowledgementsReuseExactCaptionOnly(){var d=Map02ChapterDefinition.Load();foreach(var e in d.entries.Where(e=>e.speaker=="Nam"&&e.text=="Rõ."&&new[]{"m2v3_contact","m2v3_arrivalbank","m2v3_day1report","m2v3_postbamboo"}.Contains(e.conversation)))Assert.True(Map02DialogueCatalog.HasApprovedRecording(e),e.id);Assert.True(Map02DialogueCatalog.HasApprovedRecording(d.entries.First(e=>e.id=="m2v3_bandage_01")));StringAssert.StartsWith("Dialogue/Map02DayOneFree/",DialogueVoice.Find("m2v3_bandage_01").resource,"New lines use their own approved free recordings.");}
        [Test]public void NamAcknowledgesOnceAfterTheCompleteInstructionBlock(){
            var d=Map02ChapterDefinition.Load();foreach(var group in d.entries.GroupBy(e=>e.conversation))Assert.LessOrEqual(group.Count(e=>e.speaker=="Nam"&&e.text.Trim().StartsWith("Rõ")),1,group.Key);
            var arrival=d.Lines("arrivalbank");Assert.AreEqual("Nam",arrival.Last().speaker);Assert.AreEqual("Rõ.",arrival.Last().text);Assert.AreEqual("m2v3_contact_09",arrival.Last().id);
            var last=d.Lines("supper");   // Day 3's supper scene carries Thành's last orderAssert.Greater(System.Array.FindIndex(last,e=>e.id=="m2v3_lastorder_04"),System.Array.FindLastIndex(last,e=>e.speaker=="Anh Thành"));
        }
        [Test]public void DayOneTasksKeepThreeIndependentBranches(){var d=Map02ChapterDefinition.Load();CollectionAssert.AreEquivalent(new[]{"D1_03","D1_04","D1_05"},d.Find("D1_06").requires);foreach(var id in new[]{"D1_03","D1_04","D1_05"})CollectionAssert.AreEqual(new[]{"D1_02"},d.Find(id).requires);Assert.AreEqual(5,d.Find("D1_05").count);}
    }
}
