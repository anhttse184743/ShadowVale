using System;
using NUnit.Framework;
using ShadowVale.Gameplay.Skills;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.Gameplay
{
    public sealed class PlayerSkillsTests
    {
        private GameObject owner;
        private PlayerSkills skills;
        [SetUp] public void Setup()
        {
            owner = new GameObject("Skill test"); skills = owner.AddComponent<PlayerSkills>();
            skills.Configure(JsonUtility.FromJson<SkillBalance>(Resources.Load<TextAsset>("Skills/SkillBalance").text));
        }
        [TearDown] public void Cleanup() => UnityEngine.Object.DestroyImmediate(owner);

        [Test] public void ThresholdsLevelUpOnceAndCapWithoutOverflow()
        {
            int changes = 0; skills.LevelChanged += (_, _) => changes++;
            skills.Award(SkillKind.Shooting);
            Assert.AreEqual(0, skills.Level(SkillKind.Shooting));
            skills.Award(SkillKind.Shooting);
            Assert.AreEqual(1, skills.Level(SkillKind.Shooting));
            Assert.AreEqual(.98f, skills.RecoilMultiplier, .00001f);
            Assert.AreEqual(.99f, skills.ReloadMultiplier, .00001f);
            skills.Award(SkillKind.Shooting, int.MaxValue);
            skills.Award(SkillKind.Shooting, int.MaxValue);
            Assert.AreEqual(4, skills.Level(SkillKind.Shooting));
            Assert.AreEqual(37, skills.Xp(SkillKind.Shooting));
            Assert.AreEqual(2, changes);
            Assert.AreEqual(0, skills.Xp(SkillKind.Stealth));
        }

        [Test] public void DerivedParametersUseTheSuppliedBaseWithoutCompounding()
        {
            skills.Award(SkillKind.Stealth, 3);
            Assert.AreEqual(1.7472f, skills.TakedownRange(1.6f), .00001f);
            Assert.AreEqual(2.184f, skills.TakedownRange(2f), .00001f);
            Assert.AreEqual(1.7472f, skills.TakedownRange(1.6f), .00001f);
            skills.Award(SkillKind.Engineering, 20);
            Assert.AreEqual(80f, skills.RepairGain(20, 100), .00001f);
            Assert.AreEqual(160f, skills.RepairGain(40, 200), .00001f);
            Assert.AreEqual(100f, skills.RepairGain(90, 100), .00001f);
        }

        [Test] public void JsonRoundTripAndRollbackRestoreXpWithoutAnnouncingLevels()
        {
            skills.Award(SkillKind.Shooting, 3); skills.Award(SkillKind.Stealth);
            string json = JsonUtility.ToJson(skills.Capture());
            int notifications = 0; skills.LevelChanged += (_, _) => notifications++;
            skills.Restore(JsonUtility.FromJson<SkillProgress>(json));
            Assert.AreEqual(3, skills.Xp(SkillKind.Shooting));
            Assert.AreEqual(1, skills.Level(SkillKind.Stealth));
            skills.Restore(null);
            Assert.AreEqual(0, skills.Xp(SkillKind.Shooting));
            Assert.AreEqual(1f, skills.RecoilMultiplier);
            Assert.AreEqual(0, notifications);
        }

        [Test] public void BadSaveAndNegativeAwardsCannotSubtractXp()
        {
            skills.Restore(new SkillProgress { shooting = -3, stealth = int.MaxValue });
            skills.Award(SkillKind.Shooting, -1); skills.Award(SkillKind.Stealth);
            Assert.AreEqual(0, skills.Xp(SkillKind.Shooting));
            Assert.AreEqual(int.MaxValue, skills.Xp(SkillKind.Stealth));
            Assert.AreEqual(4, skills.Level(SkillKind.Stealth));
        }

        [Test] public void EachLevelRequiresAdditionalActionsAndCompoundsFromPreviousLevel()
        {
            foreach (var entry in new[] { (SkillKind.Shooting, new[] { 2, 5, 10, 20 }),
                (SkillKind.Stealth, new[] { 1, 2, 4, 10 }), (SkillKind.Engineering, new[] { 2, 4, 6, 8 }) })
            {
                for (int level = 1; level <= 4; level++)
                {
                    skills.Award(entry.Item1, entry.Item2[level - 1] - 1);
                    Assert.AreEqual(level - 1, skills.Level(entry.Item1));
                    skills.Award(entry.Item1);
                    Assert.AreEqual(level, skills.Level(entry.Item1));
                }
            }
            Assert.AreEqual(12f * .98f * .985f * .985f * .975f, 12f * skills.RecoilMultiplier, .00001f);
            Assert.AreEqual(.99f * .985f * .98f * .975f, skills.ReloadMultiplier, .00001f);
            Assert.AreEqual(1.6f * 1.05f * 1.04f * 1.05f * 1.05f, skills.TakedownRange(1.6f), .00001f);
        }

        [Test] public void EngineeringOnlyCountsPaidRepairsThatReachFullDurability()
        {
            Assert.IsFalse(skills.RecordFullRepair(20, 40, 100, true));
            Assert.IsFalse(skills.RecordFullRepair(100, 100, 100, true));
            Assert.IsFalse(skills.RecordFullRepair(80, 100, 100, false));
            Assert.IsFalse(skills.RecordFullRepair(float.NaN, 100, 100, true));
            Assert.AreEqual(0, skills.Xp(SkillKind.Engineering));
            Assert.IsTrue(skills.RecordFullRepair(80, 100, 100, true));
            Assert.AreEqual(0, skills.Level(SkillKind.Engineering));
            Assert.IsTrue(skills.RecordFullRepair(70, 100, 100, true));
            Assert.AreEqual(1, skills.Level(SkillKind.Engineering));
            Assert.AreEqual(40f, skills.RepairGain(20, 100), .00001f);
            skills.Award(SkillKind.Engineering, 4);
            Assert.AreEqual(55f, skills.RepairGain(20, 100), .00001f);
            skills.Award(SkillKind.Engineering, 6);
            Assert.AreEqual(70f, skills.RepairGain(20, 100), .00001f);
        }

        [Test] public void ContentCanBeReplacedWithoutMutatingItOrPlayerProgress()
        {
            var config = JsonUtility.FromJson<SkillBalance>(Resources.Load<TextAsset>("Skills/SkillBalance").text);
            skills.Award(SkillKind.Shooting, 2);
            config.recoilMultipliers[1] = .975f; skills.Configure(config);
            config.recoilMultipliers[1] = .1f;
            Assert.AreEqual(.975f, skills.RecoilMultiplier);
            Assert.AreEqual(2, skills.Xp(SkillKind.Shooting));
            config.shootingThresholds[1] = 0;
            Assert.Throws<ArgumentException>(() => skills.Configure(config));
            Assert.AreEqual(.975f, skills.RecoilMultiplier);
        }
    }
}
