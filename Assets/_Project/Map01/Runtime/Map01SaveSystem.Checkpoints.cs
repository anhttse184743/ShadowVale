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
    /// The retry points inside a run: the operation, scouting, rescue, escort and interaction checkpoints, and restarting from them.
    /// </summary>
    public sealed partial class Map01SaveSystem
    {
        public void MarkOperationCheckpoint()
        {
            if(IsRestoring||mission.Cinematic||mission.PlayerHealth<=0||GetComponent<Map01Story>().Operating||Map01NamActions.For(mission).Busy)return;
            operationStart=JsonUtility.ToJson(Capture(false));
            SaveSlot(ForestSaveSlots.AutoSlot,out _,true);
        }
        public bool RestartOperation()
        {
            if(!HasOperationCheckpoint)return false;
            var data=PrepareRetry(JsonUtility.FromJson<CheckpointData>(operationStart));data.operationStart=operationStart;
            RestartInteractionCheckpoint(JsonUtility.ToJson(data));return true;
        }

        /// <summary>Hùng has just given the scouting order: remember this moment, for a failed run to
        /// start over from (<see cref="RestartScouting"/>).</summary>
        public void MarkScoutingStart()
        {
            scoutStart = null; // Not nested into itself.
            scoutStart = JsonUtility.ToJson(Capture());
        }

        public bool HasScoutStart => !string.IsNullOrEmpty(scoutStart);
        public void EnsureRescueCheckpoint()
        {
            if(string.IsNullOrEmpty(rescueStart)&&!mission.Cinematic&&!IsRestoring)MarkRescueCheckpoint(false);
        }
        public void EnsureEscortCheckpoint()
        {
            if(string.IsNullOrEmpty(escortStart)&&!mission.Cinematic&&!IsRestoring)MarkRescueCheckpoint(true);
        }
        public void MarkRescueCheckpoint(bool escort)
        {
            var data=Capture(false);
            if(!escort){data.player=GetComponent<Map01Rescue>().RetryPoint;data.alarmed=false;}
            string checkpoint=JsonUtility.ToJson(data);
            if(escort)escortStart=checkpoint;else rescueStart=checkpoint;
        }
        public bool RestartRescue(bool escort)
        {
            string checkpoint=escort?escortStart:rescueStart;
            if(string.IsNullOrEmpty(checkpoint)){mission.Say("Chưa có checkpoint nhiệm vụ. Hãy tải bản lưu hoặc chơi lại.",5);return false;}
            var data=PrepareRetry(JsonUtility.FromJson<CheckpointData>(checkpoint));
            if(!escort&&GetComponent<Map01Rescue>().IntroSeen&&data.rescue!=null){
                data.rescue.introSeen=true;
                data.rescue.revision=Map01Rescue.SnapshotRevision;
            }
            // Keep the non-recursive retry snapshots across repeated retries.
            data.rescueStart=rescueStart;data.escortStart=escortStart;
            pendingCheckpoint=JsonUtility.ToJson(data);pendingSeconds=mission.PlaySeconds;
            pendingMessage=escort?"Làm lại đoạn hộ tống Hùng về căn cứ.":"Làm lại: lén tiếp cận, hạ lính giám sát bằng dao rồi cứu Hùng.";
            Time.timeScale=1;SceneManager.LoadScene("Map 1");return true;
        }
        // Kept in memory before an interaction, never written while it is running.
        public string CaptureInteractionCheckpoint()=>JsonUtility.ToJson(Capture());
        public void RestartInteractionCheckpoint(string checkpoint){
            if(string.IsNullOrEmpty(checkpoint))return;
            pendingCheckpoint=checkpoint;pendingSeconds=mission.PlaySeconds;pendingMessage="Đã trở về trước lúc phục kích.";
            Time.timeScale=1;SceneManager.LoadScene("Map 1");
        }

        /// <summary>
        /// No recorded moment of the order (a save from before it was kept): make one as if Hùng
        /// had just given it — Nam beside him, no camp logged, every one of
        /// <paramref name="campGuards"/> fresh at his post; everything else as it is now.
        /// </summary>
        public void MarkFreshScoutingStart(IEnumerable<Map01EnemyController> campGuards)
        {
            scoutStart = null;
            var data = Capture();
            Vector3 beside = mission.hung.position + mission.hung.forward * 2f;
            if (NavMesh.SamplePosition(beside, out var hit, 3f, NavMesh.AllAreas)) beside = hit.position;
            data.player = beside;
            var toHung = mission.hung.position - beside;
            data.playerYaw = Mathf.Atan2(toHung.x, toHung.z) * Mathf.Rad2Deg;
            data.scoutedCamps = 0;
            data.alarmed = false;
            var fresh = campGuards.ToDictionary(g => g.SaveId, g => g.CaptureAtPost());
            for (int i = 0; i < data.enemies.Length; i++)
                if (fresh.TryGetValue(data.enemies[i].id, out var atPost)) data.enemies[i] = atPost;
            scoutStart = JsonUtility.ToJson(data);
        }

        /// <summary>Back to the moment Hùng gave the scouting order — Map 1 reloads there, with
        /// <paramref name="message"/> on screen. False if that moment was never recorded.</summary>
        public bool RestartScouting(string message)
        {
            if (string.IsNullOrEmpty(scoutStart)) return false;
            pendingCheckpoint = JsonUtility.ToJson(PrepareRetry(JsonUtility.FromJson<CheckpointData>(scoutStart))); pendingSeconds = mission.PlaySeconds; pendingMessage = message;
            Time.timeScale = 1;
            Map01Mission.ReloadScene(gameObject.scene); // Map 1, or the sandbox copy it is running in.
            return true;
        }
    }
}
