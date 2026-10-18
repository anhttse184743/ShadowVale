using System;
using ShadowVale.Core.Events;
using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Gameplay.Enemies
{
    /// <summary>
    /// When a wounded enemy starts recovering (debug console). Raised once per recovery, not every frame.
    /// </summary>
    public readonly struct EnemyRegenEvent : IEvent
    {
        public readonly string AgentId;
        /// <summary>Share of max health when the recovery began.</summary>
        public readonly float From;
        public EnemyRegenEvent(string agentId, float from) { AgentId = agentId; From = from; }
    }

    /// <summary>
    /// The rule, apart from the component so it is unit-tested: after <see cref="QuietSeconds"/> without
    /// losing health, health comes back at <see cref="SharePerSecond"/> of the maximum each second, up
    /// to full. Any loss stops it and the wait starts over.
    /// </summary>
    public sealed class HealthRegen
    {
        public const float DefaultQuietSeconds = 30f, DefaultSharePerSecond = .05f;

        public float QuietSeconds { get; set; } = DefaultQuietSeconds;
        public float SharePerSecond { get; set; } = DefaultSharePerSecond;
        /// <summary>Health is coming back right now.</summary>
        public bool Recovering { get; private set; }

        private float _lastHealth = float.NaN, _quietSince;

        /// <summary>The health he should have now, given <paramref name="current"/>; true in <paramref name="started"/> on the tick a recovery begins.</summary>
        public float Tick(float now, float deltaTime, float current, float max, out bool started)
        {
            started = false;
            if (float.IsNaN(_lastHealth) || current < _lastHealth - 1e-4f || current <= 0f)
            {
                // Hurt (or just started watching, or down): wait the full quiet time from here.
                _quietSince = now;
                Recovering = false;
            }
            else if (current < max && now - _quietSince >= QuietSeconds && deltaTime > 0f)
            {
                started = !Recovering;
                Recovering = true;
                current = Mathf.Min(max, current + max * SharePerSecond * deltaTime);
            }
            if (current >= max) Recovering = false;
            _lastHealth = current;
            return current;
        }

        /// <summary>Forget the last reading (a checkpoint restored his health): the wait starts over.</summary>
        public void Reset() { _lastHealth = float.NaN; Recovering = false; }
    }

    /// <summary>
    /// <see cref="HealthRegen"/> on an enemy's <see cref="Health"/>: guards left alone for half a minute
    /// patch themselves up, so wearing a guard down and then hiding does not leave him wounded for good.
    /// Not on a commander. Nothing happens while dead, while the map is paused or in a cutscene.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyRegen : MonoBehaviour
    {
        public HealthRegen Rule { get; } = new HealthRegen();
        /// <summary>True while the map is paused, in a cutscene or restoring.</summary>
        public Func<bool> Suspended { get; set; }
        public string AgentId { get; set; }

        private Health _health;

        public static EnemyRegen Attach(GameObject enemy, string agentId)
        {
            var r = enemy.GetComponent<EnemyRegen>();
            if (r == null) r = enemy.AddComponent<EnemyRegen>();
            r.AgentId = agentId;
            return r;
        }

        public static void Detach(GameObject enemy)
        {
            var r = enemy.GetComponent<EnemyRegen>();
            if (r == null) return;
            r.enabled = false; // At once: Destroy only takes effect at the end of the frame.
            if (Application.isPlaying) Destroy(r); else DestroyImmediate(r);
        }

        private void Awake() => _health = GetComponent<Health>();

        private void Update()
        {
            if (_health == null || _health.IsDead) { Rule.Reset(); return; }
            float dt = Time.deltaTime;
            if ((Suspended?.Invoke() ?? false) || _health.CinematicInvulnerable) dt = 0f;
            float before = _health.Current;
            float after = Rule.Tick(Time.time, dt, before, _health.Max, out bool started);
            if (started) EventBus.Publish(new EnemyRegenEvent(AgentId ?? name, before / Mathf.Max(1f, _health.Max)));
            if (after > before) _health.RestoreHealth(after);
        }
    }
}
