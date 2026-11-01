using ShadowVale.AI.Perception;
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Boss;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// What a checkpoint keeps of the C3 operation, and the HUD's status line and optional objectives.
    /// </summary>
    public sealed partial class Map01C3Mission
    {
        // ---- the save ------------------------------------------------------------------------------------------------

        public State Capture()
        {
            var s = JsonUtility.FromJson<State>(JsonUtility.ToJson(Current));
            s.sleepersAwake = (bool[])Current.sleepersAwake?.Clone();
            return s;
        }

        /// <summary>Back to a saved state, before the guards' own snapshots are restored: a woken sleeper is back on the
        /// field so his snapshot finds him.</summary>
        public void Restore(State saved)
        {
            if (!ready) return;
            var s = saved ?? new State { checkInRemaining = Data.checkInSeconds };
            if (s.checkInRemaining < 0) s.checkInRemaining = Data.checkInSeconds;
            s.bodyRunner ??= ""; bodyRunner = null;
            var awake = new bool[sleepers.Count];
            for (int i = 0; i < Mathf.Min(awake.Length, s.sleepersAwake?.Length ?? 0); i++) awake[i] = s.sleepersAwake[i];
            bool runnersOut = s.runnersSent;
            var killed = new bool[sleepers.Count]; var executed = new bool[sleepers.Count];
            for(int i=0;i<sleepers.Count;i++) { killed[i]=s.sleepersKilled!=null&&i<s.sleepersKilled.Length&&s.sleepersKilled[i]; executed[i]=s.sleepersExecuted!=null&&i<s.sleepersExecuted.Length&&s.sleepersExecuted[i]; }
            s.sleepersAwake = new bool[sleepers.Count]; s.sleepersKilled=killed; s.sleepersExecuted=executed;
            s.runnersSent = false;
            Current = s;
            kiengAt = -1f;
            for (int i = 0; i < sleepers.Count; i++)
            {
                var bed=sleepers[i].GetComponent<Map01C3Sleeper>();
                if(bed!=null)
                {
                    bed.ResetToSleep();
                    if(killed[i]) { sleepers[i].GetComponent<Health>().RestoreHealth(0); bed.HoldCorpse(executed[i]); }
                    else if(awake[i])Wake(i,false,true);
                }
                else if (awake[i]) Wake(i, false, true);
                else if (sleepers[i].gameObject.activeSelf) sleepers[i].gameObject.SetActive(false);
            }
            if (runnersOut) { Current.runnersSent = true; foreach (var r in runners) { r.gameObject.SetActive(true); mission.AddEnemy(r); } }
            if (striker != null) striker.SetActive(!s.kiengRemoved);
            if (s.phoneCut) DropWire(); else if (hqWire != null) Sag(hqWire, poleTop, new Vector3(Data.phone.hqEnd[0], Data.phone.hqEnd[1], Data.phone.hqEnd[2]));
            if (s.wary) ApplyWary();
        }

        // ---- the HUD ---------------------------------------------------------------------------------------------------

        /// <summary>A line for the HUD while Nam is near the post, or null.</summary>
        public string StatusLine()
        {
            if (!ready || !Near) return null;
            var bedHint=GetComponent<Map01C3BedTakedown>()?.Hint(); if(bedHint!=null)return bedHint;
            if (quest.Stage < Map01Quest.BossStage || quest.Stage > Map01Quest.ReportBossStage) return null;
            if (CanAmbush()) return "Sau lưng chỉ huy — CHUỘT TRÁI để đánh úp";
            if (Alarmed) return Current.kiengRung ? "Trại C3 báo động — kẻng đã gõ" : "Trại C3 báo động";
            if (Current.wary) return "Trại C3 đang cảnh giác — tuần tra mất liên lạc";
            var caller = Guard("gate");
            return caller != null && caller.Alive ? $"Gác cổng điểm danh tuần tra sau {Mathf.CeilToInt(Current.checkInRemaining)} giây" : null;
        }
        /// <summary>The optional objectives, as the HUD lists them.</summary>
        public IEnumerable<(string text, bool done)> OptionalObjectives()
        {
            var tower = Guard("tower");
            yield return ("Hạ lính tháp canh", tower != null && !tower.Alive);
            yield return ("Tháo dùi kẻng ở cổng", Current.kiengRemoved);
            yield return ("Cắt dây điện thoại sau trại", Current.phoneCut);
        }
    }
}
