using System;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Skills;
using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed partial class Map02Mission
    {
        public PlayerSkills Skills => player != null ? player.GetComponent<PlayerSkills>() : null;
        SkillCampaignPersistence skillCareer;
        bool restoringSkills;
        float nextTakedown;

        void InitializeSkills()
        {
            skillCareer = player.GetComponent<SkillCampaignPersistence>();
            if (skillCareer == null) skillCareer = player.gameObject.AddComponent<SkillCampaignPersistence>();
            if (string.IsNullOrEmpty(skillCareer.RunId))
            {
                var inherited = Skills.Capture();
                skillCareer.Initialize(Guid.NewGuid().ToString("N"));
                skillCareer.Restore(inherited, null);
            }
            Skills.LevelChanged += SkillLevelChanged;
            combat.GunKilled += SkillGunKilled;
            combat.TrySpecialMelee = TrySkillTakedown;
            SyncSkills();
        }

        public void SyncSkills()
        {
            if (restoringSkills || skillCareer == null || Skills == null || Chapter == null) return;
            if (World.story.failed || health.IsDead) { Skills.FailMission(); return; }
            if (Chapter.State.finished) { if (Skills.MissionId != null) Skills.CompleteMission(); return; }
            string id = "map2_phase_" + Chapter.State.phase;
            if (Skills.MissionId == id) return;
            Skills.CompleteMission();
            Skills.BeginMission(id);
        }

        void SkillGunKilled(GameObject target)
        {
            if (restoringSkills || !Ready || World.story.failed || health.IsDead || Skills.MissionFailed || target == null) return;
            var enemy = target.GetComponentInParent<Map02ChapterEnemy>();
            if (enemy != null && Chapter.Enemies.Contains(enemy) && enemy.Dead) Skills.Award(SkillKind.Shooting);
        }

        void SkillLevelChanged(SkillKind kind, int level)
        {
            Say((kind == SkillKind.Shooting ? "Bắn súng" : kind == SkillKind.Stealth ? "Lén lút" : "Kỹ thuật") + " lên cấp " + level + ".");
        }

        public bool TrySkillTakedown()
        {
            if (InputBlocked || health.IsDead || Skills == null || Skills.MissionFailed || combat.EquippedKind != WeaponKind.Knife) return false;
            if (Time.time < nextTakedown) return true;
            var enemy = Chapter.Enemies.Where(e => e != null && e.CanSilentTakedown(this))
                .OrderBy(e => (e.transform.position - Player.position).sqrMagnitude).FirstOrDefault();
            if (enemy == null) return false;
            nextTakedown = Time.time + .9f;
            var actor = Player.GetComponentInChildren<Animator>();
            if (actor != null && actor.runtimeAnimatorController != null) actor.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            enemy.HitPoints.TakeDamage(enemy.HitPoints.Current, enemy.transform.position + Vector3.up, Player.gameObject);
            if (enemy.Dead) Skills.Award(SkillKind.Stealth);
            return true;
        }

        void RestoreSkills(Snapshot data)
        {
            restoringSkills = true;
            try
            {
                skillCareer.TryFlush(out _);
                string run = data.skillRunId;
                if (string.IsNullOrEmpty(run))
                {
                    using var hash = System.Security.Cryptography.SHA256.Create();
                    run = BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)))).Replace("-", "").ToLowerInvariant();
                }
                skillCareer.Initialize(run);
                var baseline = data.skillMission == null ? null : JsonUtility.FromJson<SkillMissionProgress>(JsonUtility.ToJson(data.skillMission));
                var progress = data.skills == null ? new SkillProgress() : JsonUtility.FromJson<SkillProgress>(JsonUtility.ToJson(data.skills));
                var journal = SkillCampaignPersistence.Read(run);
                if (baseline != null && (baseline.failed || journal.failedAttempts.Contains(baseline.attemptId)))
                {
                    progress.shooting = baseline.shooting; progress.stealth = baseline.stealth;
                    baseline.failed = false; baseline.attemptId = Guid.NewGuid().ToString("N");
                }
                skillCareer.Restore(progress, baseline);
                nextTakedown = 0;
            }
            finally { restoringSkills = false; }
        }

        void DestroySkills()
        {
            if (Skills != null) Skills.LevelChanged -= SkillLevelChanged;
            if (combat != null)
            {
                combat.GunKilled -= SkillGunKilled;
                if (combat.TrySpecialMelee == (Func<bool>)TrySkillTakedown) combat.TrySpecialMelee = null;
            }
            skillCareer?.TryFlush(out _);
        }
    }
}
