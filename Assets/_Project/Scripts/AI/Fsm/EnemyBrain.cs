using System;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Fsm.States;
using ShadowVale.Core.Events;
using ShadowVale.Core.Fsm;
using UnityEngine;

namespace ShadowVale.AI.Fsm
{
    /// <summary>
    /// One enemy's decision-making, shared by every map (A01). Each tick: update memory from the
    /// senses (suspicion, engagement, retreat trigger), pick the state (<see cref="EnemyTransitions"/>),
    /// run it. Plain C# — the MonoBehaviour that owns it supplies body, weapon, tactics, senses and
    /// clock, which is what makes the FSM testable without a scene.
    /// </summary>
    public sealed class EnemyBrain
    {
        private static readonly IState<EnemyContext>[] States =
        {
            new PatrolState(), new InvestigateState(), new SpotPlayerState(),
            new TakeCoverState(), new EngageState(), new RetreatState(),
        };

        private readonly EnemyContext _ctx;
        private readonly StateMachine<EnemyContext> _machine;
        private bool _sawLastTick;

        public EnemyStateId State { get; private set; } = EnemyStateId.Patrol;
        public EnemyBlackboard Memory => _ctx.Memory;
        public EnemyTuning Tuning => _ctx.Tuning;
        public EnemyContext Context => _ctx;
        public event Action<EnemyStateId, EnemyStateId> StateChanged;

        public EnemyBrain(string agentId, IEnemyBody body, IEnemyWeapon weapon, EnemyTuning tuning,
            IEnemyTactics tactics = null, EnemyBlackboard memory = null)
        {
            _ctx = new EnemyContext
            {
                AgentId = agentId, Body = body, Weapon = weapon, Tuning = tuning ?? new EnemyTuning(),
                Tactics = tactics ?? DefaultEnemyTactics.Instance, Memory = memory ?? new EnemyBlackboard(),
            };
            _machine = new StateMachine<EnemyContext>(_ctx);
            _machine.ChangeState(States[(int)EnemyStateId.Patrol]);
        }

        public void Tick(in PerceptionSample sense, float now, float deltaTime)
        {
            _ctx.Sense = sense; _ctx.Now = now; _ctx.DeltaTime = deltaTime;
            if (_ctx.Body.IsDead) return;
            UpdateMemory();
            var next = EnemyTransitions.Next(_ctx);
            if (next != State)
            {
                var from = State;
                State = next;
                _machine.ChangeState(States[(int)next]);
                StateChanged?.Invoke(from, next);
                EventBus.Publish(new EnemyStateChangedEvent(_ctx.AgentId, from, next));
            }
            _machine.Tick(deltaTime);
        }

        private void UpdateMemory()
        {
            var m = _ctx.Memory; var t = _ctx.Tuning; var s = _ctx.Sense;
            float now = _ctx.Now;
            bool wasEngaged = m.Engaged(now);
            if (s.SeesPlayer && !wasEngaged)
            {
                // Not an instant spot: suspicion builds while the player stays in view, faster up close.
                float closeness = 1f - Mathf.Clamp01(s.Distance / t.VisionRange);
                m.Suspicion = Mathf.Min(1f, m.Suspicion + _ctx.DeltaTime / Mathf.Max(.1f, t.DetectionSeconds) * Mathf.Lerp(.6f, 3f, closeness));
            }
            if (s.SeesPlayer && (wasEngaged || m.Suspicion >= 1f))
            {
                m.EngagedUntil = now + t.EngagedMemorySeconds;
                // Once out of sight again: to where the player was last seen, look around, go back.
                BeginInvestigation(s.PlayerPosition, now);
                m.LastKnownPlayerPosition = s.PlayerPosition;
                m.LastSeenTime = now;
                m.SuppressUntil = 0;
                if (!wasEngaged) OnContact(s.PlayerPosition, seen: true);
            }
            else if (!s.SeesPlayer && !wasEngaged)
            {
                m.Suspicion = Mathf.Max(0f, m.Suspicion - _ctx.DeltaTime * t.SuspicionDecayPerSecond);
            }

            bool engaged = m.Engaged(now);
            if (!s.SeesPlayer && engaged && _sawLastTick && t.SuppressSeconds > 0) m.SuppressUntil = now + t.SuppressSeconds;
            _sawLastTick = s.SeesPlayer;
            if (!engaged && m.HasAssignment) m.ClearAssignment();

            if (engaged && !m.Retreating && !m.RetreatDone && !t.IsBoss && t.RetreatHealthFraction > 0
                && _ctx.Body.HealthFraction < t.RetreatHealthFraction)
            {
                if (_ctx.Tactics.TryFindRetreatPoint(_ctx, m.LastKnownPlayerPosition, out var point))
                {
                    m.Retreating = true; m.RetreatPoint = point; m.RetreatHoldUntil = 0;
                    m.RetreatGiveUpAt = now + t.RetreatHoldSeconds * 2f;
                }
                else m.RetreatDone = true;
            }
        }

        private void OnContact(Vector3 playerPosition, bool seen)
        {
            var m = _ctx.Memory;
            if (!m.HasAssignment && _ctx.Tactics.TryFindCover(_ctx, playerPosition, out var cover))
                Assign(cover, -1, "cover");
            else if (m.HasAssignment) m.TakeCoverUntil = _ctx.Now + _ctx.Tuning.TakeCoverTimeoutSeconds;
            EventBus.Publish(new PlayerContactEvent(_ctx.AgentId, playerPosition, seen));
        }

        /// <summary>A noise or a sighting to check. The way back is kept from the first thing he
        /// noticed, not from wherever the next noise catches him on the way.</summary>
        public void BeginInvestigation(Vector3 position, float now)
        {
            var m = _ctx.Memory;
            if (!m.Alerted(now) && !m.Returning) { m.ReturnPoint = _ctx.Body.Position; m.ReturnRotation = _ctx.Body.Rotation; }
            m.Returning = false;
            m.InvestigatePoint = position; m.SearchUntil = 0;
            m.AlertUntil = now + _ctx.Tuning.InvestigateTimeoutSeconds;
        }

        /// <summary>Hurt by the player (not a silent takedown): he knows where it came from.</summary>
        public void OnHurtByPlayer(Vector3 playerPosition, float now)
        {
            var m = _ctx.Memory;
            _ctx.Now = now;
            bool wasEngaged = m.Engaged(now);
            m.Suspicion = 1f; m.EngagedUntil = now + _ctx.Tuning.EngagedMemorySeconds;
            BeginInvestigation(playerPosition, now);
            m.LastKnownPlayerPosition = playerPosition;
            _ctx.Body.Face(playerPosition);
            if (!wasEngaged) OnContact(playerPosition, seen: false);
        }

        /// <summary>Position to fight from, given by the squad plan (node ≥ 0) or local cover (−1).</summary>
        public void Assign(Vector3 position, int node, string role)
        {
            var m = _ctx.Memory;
            m.HasAssignment = true; m.ReachedAssignment = false;
            m.AssignedPosition = position; m.AssignedNode = node; m.AssignedRole = role ?? "";
            m.TakeCoverUntil = _ctx.Now + _ctx.Tuning.TakeCoverTimeoutSeconds;
        }

        /// <summary>Back to calm at his post: forget everything, ignore the player until <paramref name="calmUntil"/>.</summary>
        public void ResetToPost(float calmUntil)
        {
            var m = _ctx.Memory;
            m.Suspicion = 0; m.AlertUntil = 0; m.EngagedUntil = 0; m.SearchUntil = 0; m.SuppressUntil = 0;
            m.Returning = false; m.CalmUntil = calmUntil; m.PatrolIndex = 0;
            m.ClearAssignment();
            m.Retreating = false; m.RetreatDone = false; m.RetreatHoldUntil = 0;
            _sawLastTick = false;
        }
    }
}
