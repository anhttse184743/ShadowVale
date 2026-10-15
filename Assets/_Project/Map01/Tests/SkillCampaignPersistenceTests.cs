using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Skills;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class SkillCampaignPersistenceTests
    {
        private GameObject owner;
        private PlayerSkills skills;
        private SkillCampaignPersistence campaign;
        private string root, id;
        private object previousRoot;
        private static readonly FieldInfo StorageRoot = typeof(ForestSaveSlots).GetField("storageRoot", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp] public void Setup()
        {
            previousRoot = StorageRoot.GetValue(null);
            root = Path.Combine(Application.temporaryCachePath, "skill-career-" + Guid.NewGuid().ToString("N"));
            StorageRoot.SetValue(null, root);
            owner = new GameObject("Skill campaign test"); skills = owner.AddComponent<PlayerSkills>();
            skills.Configure(JsonUtility.FromJson<SkillBalance>(Resources.Load<TextAsset>("Skills/SkillBalance").text));
            campaign = owner.AddComponent<SkillCampaignPersistence>(); id = Guid.NewGuid().ToString("N"); campaign.Initialize(id);
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(owner); StorageRoot.SetValue(null, previousRoot);
            string path = Path.GetFullPath(root), boundary = Path.GetFullPath(Application.temporaryCachePath) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid test directory.");
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        [Test] public void EngineeringAndRepairTransactionsSurviveAnOlderCheckpointAndAppRestart()
        {
            Assert.IsTrue(skills.RecordFullRepair(80, 100, 100, true, "weapon-a-cycle-1"));
            Assert.IsTrue(skills.RecordFullRepair(60, 100, 100, true, "weapon-a-cycle-2"));
            campaign.Restore(new SkillProgress { shooting = 1 }, null);
            Assert.AreEqual(2, skills.Xp(SkillKind.Engineering));
            Object.DestroyImmediate(campaign); campaign = owner.AddComponent<SkillCampaignPersistence>();
            campaign.Initialize(id); campaign.Restore(new SkillProgress(), null);
            Assert.AreEqual(2, skills.Xp(SkillKind.Engineering));
            Assert.IsFalse(skills.RecordFullRepair(80, 100, 100, true, "weapon-a-cycle-1"));
            Assert.AreEqual(2, skills.Xp(SkillKind.Engineering));
        }
        [Test] public void NewRunDoesNotInheritAnotherRunsPermanentProgress()
        {
            skills.Award(SkillKind.Engineering, 6);
            campaign.Initialize(Guid.NewGuid().ToString("N"));
            Assert.AreEqual(0, skills.Xp(SkillKind.Engineering));
            campaign.Initialize(id);
            Assert.AreEqual(6, skills.Xp(SkillKind.Engineering));
        }
        [Test] public void MidMissionSaveRetainsProvisionalXpUntilActualFailure()
        {
            skills.Award(SkillKind.Shooting); skills.BeginMission("camps"); skills.Award(SkillKind.Shooting, 6);
            var xp = skills.Capture(); var state = skills.CaptureMission();
            campaign.Restore(xp, state);
            Assert.AreEqual(7, skills.Xp(SkillKind.Shooting));
            Assert.AreEqual(1, skills.CaptureMission().shooting);
            Assert.IsFalse(SkillCampaignPersistence.Read(id).failedAttempts.Contains(state.attemptId));
            skills.FailMission();
            Assert.AreEqual(1, skills.Xp(SkillKind.Shooting));
            Assert.Contains(state.attemptId, SkillCampaignPersistence.Read(id).failedAttempts);
        }
        [Test] public void LoadingAnAttemptThatAlreadyFailedReturnsToItsMissionStart()
        {
            skills.Award(SkillKind.Shooting); skills.BeginMission("camps"); skills.Award(SkillKind.Shooting, 6);
            var state = skills.CaptureMission();
            var type = typeof(Map01SaveSystem).GetNestedType("CheckpointData", BindingFlags.NonPublic);
            object start = Activator.CreateInstance(type, true), mid = Activator.CreateInstance(type, true);
            type.GetField("stage").SetValue(start, Map01Quest.CampsStage);
            type.GetField("player").SetValue(start, new Vector3(2, 3, 4));
            type.GetField("stage").SetValue(mid, Map01Quest.CampsStage);
            type.GetField("player").SetValue(mid, new Vector3(20, 3, 40));
            type.GetField("skillRunId").SetValue(mid, id);
            type.GetField("skillMission").SetValue(mid, state);
            type.GetField("skills").SetValue(mid, skills.Capture());
            type.GetField("missionStart").SetValue(mid, JsonUtility.ToJson(start));
            skills.Award(SkillKind.Engineering, 2); skills.FailMission();
            var normalize = typeof(Map01SaveSystem).GetMethod("NormalizeFailedAttempt", BindingFlags.Static | BindingFlags.NonPublic);
            object restored = normalize.Invoke(null, new[] { mid, JsonUtility.ToJson(mid) });
            var xp = (SkillProgress)type.GetField("skills").GetValue(restored);
            var retry = (SkillMissionProgress)type.GetField("skillMission").GetValue(restored);
            Assert.AreEqual(1, xp.shooting); Assert.AreEqual(2, xp.engineering);
            Assert.AreEqual(new Vector3(2, 3, 4), (Vector3)type.GetField("player").GetValue(restored));
            Assert.AreNotEqual(state.attemptId, retry.attemptId); Assert.IsFalse(retry.failed);
        }
        [Test] public void CareerPathsCannotEscapeTheSaveDirectory()
        {
            Assert.Throws<ArgumentException>(() => ForestSaveSlots.SkillCareerPath("../other"));
            Assert.Throws<ArgumentException>(() => ForestSaveSlots.SkillCareerPath(new string('z', 32)));
        }
        [Test] public void LegacyCheckpointKeepsXpAndGetsAStableRunIdentity()
        {
            var type = typeof(Map01SaveSystem).GetNestedType("CheckpointData", BindingFlags.NonPublic);
            var source = Activator.CreateInstance(type, true);
            type.GetField("skills").SetValue(source, new SkillProgress { shooting = 3, stealth = 1, engineering = 2 });
            string json = JsonUtility.ToJson(source);
            var normalize = typeof(Map01SaveSystem).GetMethod("NormalizeFailedAttempt", BindingFlags.Static | BindingFlags.NonPublic);
            var first = normalize.Invoke(null, new[] { JsonUtility.FromJson(json, type), json });
            var second = normalize.Invoke(null, new[] { JsonUtility.FromJson(json, type), json });
            string firstId = (string)type.GetField("skillRunId").GetValue(first);
            Assert.AreEqual(64, firstId.Length);
            Assert.AreEqual(firstId, type.GetField("skillRunId").GetValue(second));
            var xp = (SkillProgress)type.GetField("skills").GetValue(first);
            Assert.AreEqual(3, xp.shooting); Assert.AreEqual(1, xp.stealth); Assert.AreEqual(2, xp.engineering);
        }
        [Test] public void DamagedCareerIsRejectedWithoutReplacingItsFile()
        {
            string otherId = Guid.NewGuid().ToString("N"), path = ForestSaveSlots.SkillCareerPath(otherId);
            Directory.CreateDirectory(root);
            const string invalid = "{\"version\":999,\"career\":{\"engineering\":6}}";
            File.WriteAllText(path, invalid);
            Assert.Throws<InvalidDataException>(() => campaign.Initialize(otherId));
            Assert.AreEqual(id, campaign.RunId);
            Assert.AreEqual(invalid, File.ReadAllText(path));
        }
    }
}
