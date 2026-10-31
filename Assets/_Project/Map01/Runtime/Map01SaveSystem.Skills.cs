using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Keeping the skill campaign (SkillCampaignPersistence) in step with the mission being played.
    /// </summary>
    public sealed partial class Map01SaveSystem
    {
        private static string RunId(CheckpointData data, string json)
        {
            if (!string.IsNullOrEmpty(data?.skillRunId)) return data.skillRunId;
            // Stable migration for the same pre-feature save, without rewriting the user's old slot.
            using var hash = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant();
        }
        private void EnsureSkillCampaign()
        {
            if (skillCampaign != null && !string.IsNullOrEmpty(skillCampaign.RunId)) return;
            skillCampaign = mission.player.GetComponent<SkillCampaignPersistence>();
            if (skillCampaign == null) skillCampaign = mission.player.gameObject.AddComponent<SkillCampaignPersistence>();
            if (pendingCheckpoint == null) skillCampaign.Initialize(Guid.NewGuid().ToString("N"));
            else skillCampaign.Initialize(RunId(JsonUtility.FromJson<CheckpointData>(pendingCheckpoint), pendingCheckpoint));
        }
        private static string SkillMissionId(int stage) => stage switch
        {
            Map01Quest.RescueStage => "rescue", Map01Quest.EscortStage => "escort", Map01Quest.ScoutStage => "scouting",
            Map01Quest.CampsStage => "camps", Map01Quest.BossStage => "boss",
            Map01Quest.ExtractionStage or Map01Quest.BoardingStage => "extraction", _ => null
        };
        public void SyncSkillMission()
        {
            if (syncingSkills || IsRestoring || !mission.IsInitialized) return;
            EnsureSkillCampaign();
            syncingSkills = true;
            try
            {
                if (mission.PlayerHealth <= 0 || (mission.ModernHealth != null && mission.ModernHealth.IsDead)
                    || GetComponent<Map01Rescue>().HungDown || GetComponent<Map01Rescue>().CurrentPhase == Map01Rescue.Phase.Alarm
                    || GetComponent<Map01Scouting>().FailedRun)
                { mission.Skills.FailMission(); return; }
                string id = SkillMissionId(quest.Stage);
                if (mission.Skills.MissionId == id)
                {
                    if (missionStart == null) missionStart = JsonUtility.ToJson(Capture(false));
                    return;
                }
                mission.Skills.CompleteMission();
                missionStart = null;
                if (id != null)
                {
                    mission.Skills.BeginMission(id);
                }
                missionStart = JsonUtility.ToJson(Capture(false));
            }
            finally { syncingSkills = false; }
        }
    }
}
