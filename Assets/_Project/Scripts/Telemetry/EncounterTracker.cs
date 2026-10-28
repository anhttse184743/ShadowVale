using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Fsm;
using ShadowVale.Data.Telemetry;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// Cuts the session into encounters and measures each one (RQ4): an encounter opens on a guard's
    /// contact with the player and closes when the player dies (<c>captured</c>), when the last guard
    /// fighting him dies (<c>cleared</c>) or when no guard has been fighting for
    /// <see cref="EscapeGraceSeconds"/> (<c>escaped</c>; the escape time runs to the moment the last one
    /// gave up). "Fighting" is being in TakeCover, Engage or Retreat — SpotPlayer is suspicion that has
    /// not become contact yet, Investigate is searching for someone already lost.
    /// Pure logic with an explicit clock, so tests and the replay harness drive it directly.
    /// </summary>
    public sealed class EncounterTracker
    {
        /// <summary>A guard back in the fight this soon after the last one gave up keeps the encounter going.</summary>
        public const double EscapeGraceSeconds = 2.0;

        private sealed class Open
        {
            public int Index;
            public double Start;
            public readonly HashSet<string> Agents = new HashSet<string>();
            public int Kills, Replans, Fallbacks;
            public double ScoreSum;
            public readonly List<string> Variants = new List<string>();
        }

        private readonly HashSet<string> _fighting = new HashSet<string>();
        private Open _open;
        private double _calmSince = double.NaN;

        public List<EncounterDto> Finished { get; } = new List<EncounterDto>();
        public bool InEncounter => _open != null;
        public int FightingCount => _fighting.Count;

        public static bool IsFighting(EnemyStateId s) =>
            s == EnemyStateId.TakeCover || s == EnemyStateId.Engage || s == EnemyStateId.Retreat;

        public void OnContact(string agentId, double now)
        {
            _open ??= new Open { Index = Finished.Count, Start = now };
            _open.Agents.Add(agentId);
            _fighting.Add(agentId);
            _calmSince = double.NaN;
        }

        public void OnStateChanged(string agentId, EnemyStateId to, double now)
        {
            if (IsFighting(to))
            {
                _fighting.Add(agentId);
                if (_open != null) { _open.Agents.Add(agentId); _calmSince = double.NaN; }
                return;
            }
            if (_fighting.Remove(agentId) && _open != null && _fighting.Count == 0) _calmSince = now;
        }

        public void OnEnemyKilled(string agentId, double now)
        {
            bool wasFighting = _fighting.Remove(agentId);
            if (_open == null) return;
            if (_open.Agents.Contains(agentId)) _open.Kills++;
            if (wasFighting && _fighting.Count == 0) Close("cleared", now);
        }

        public void OnPlayerDied(double now)
        {
            if (_open != null) Close("captured", now);
            // Whoever was hunting him stops: their state events will follow, or never come (reload).
            _fighting.Clear();
        }

        public void OnPlan(string variant, double coordinationScore, bool fellBack)
        {
            if (_open == null) return;
            _open.Replans++;
            _open.ScoreSum += coordinationScore;
            if (fellBack) _open.Fallbacks++;
            if (!string.IsNullOrEmpty(variant) && !_open.Variants.Contains(variant)) _open.Variants.Add(variant);
        }

        /// <summary>Closes an encounter whose guards all gave up long enough ago.</summary>
        public void Tick(double now)
        {
            if (_open != null && !double.IsNaN(_calmSince) && now - _calmSince >= EscapeGraceSeconds)
                Close("escaped", _calmSince);
        }

        /// <summary>Session over: a fight still going is recorded as unresolved.</summary>
        public void Finish(double now)
        {
            Tick(now);
            if (_open != null) Close("unresolved", now);
        }

        private void Close(string result, double end)
        {
            var o = _open;
            _open = null;
            _calmSince = double.NaN;
            Finished.Add(new EncounterDto
            {
                Index = o.Index,
                StartT = EventRecorder.Round(o.Start),
                EndT = EventRecorder.Round(end),
                Result = result,
                DurationSeconds = EventRecorder.Round(Math.Max(0, end - o.Start)),
                Agents = o.Agents.Count,
                Kills = o.Kills,
                Replans = o.Replans,
                Fallbacks = o.Fallbacks,
                CoordinationScore = o.Replans > 0 ? Math.Round(o.ScoreSum / o.Replans, 4) : 0,
                Variants = string.Join("+", o.Variants),
            });
        }

        /// <summary>Fills the encounter part of the summary.</summary>
        public void Summarise(TelemetrySummaryDto s)
        {
            s.Encounters = Finished.Count;
            s.Captures = Finished.Count(e => e.Result == "captured");
            s.Escapes = Finished.Count(e => e.Result == "escaped");
            s.Cleared = Finished.Count(e => e.Result == "cleared");
            int resolved = s.Captures + s.Escapes + s.Cleared;
            s.CaptureRate = resolved > 0 ? Math.Round((double)s.Captures / resolved, 4) : 0;
            var escapes = Finished.Where(e => e.Result == "escaped").Select(e => e.DurationSeconds).OrderBy(d => d).ToList();
            s.EscapeTimeMeanSeconds = escapes.Count > 0 ? EventRecorder.Round(escapes.Average()) : 0;
            s.EscapeTimeMedianSeconds = escapes.Count > 0 ? EventRecorder.Round(SolverStats.Percentile(escapes, 50)) : 0;
        }
    }
}
