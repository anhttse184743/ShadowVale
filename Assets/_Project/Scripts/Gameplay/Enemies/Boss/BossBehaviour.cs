using System;
using System.Linq;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Boss;
using ShadowVale.AI.Contracts;
using ShadowVale.Core.Events;
using ShadowVale.Core.Services;
using ShadowVale.Data.Content;
using UnityEngine;

namespace ShadowVale.Gameplay.Enemies.Boss
{
    /// <summary>
    /// A boss's extra behaviour on top of the shared FSM (A04), for any map: every frame
    /// <see cref="BossDirector"/> decides, this component acts — a new cover position handed to the
    /// brain (TakeCover runs there, firing on the way), a grenade at a player who camps behind cover,
    /// one call for reinforcements. The boss is left out of squad plans so his positions stay his.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossBehaviour : MonoBehaviour
    {
        public BossDirector Director { get; private set; }
        public BossProfile Profile => Director.Profile;
        /// <summary>Who answers the call; nearby idle enemies unless the map says otherwise.</summary>
        public IReinforcementProvider Reinforcements { get; set; }
        /// <summary>True while the map is paused, in a cutscene or restoring: the boss does nothing then.</summary>
        public Func<bool> Suspended { get; set; }
        public int Relocations { get; private set; }
        public int GrenadesThrown { get; private set; }

        private IEnemyAgent _agent;
        private System.Random _rng;

        public static BossBehaviour Attach(GameObject enemy, IEnemyAgent agent, BossProfile profile,
            IReinforcementProvider reinforcements = null, int seed = 0)
        {
            var b = enemy.GetComponent<BossBehaviour>();
            if (b == null) b = enemy.AddComponent<BossBehaviour>();
            b._agent = agent;
            b.Director = new BossDirector(profile);
            b.Reinforcements = reinforcements;
            b._rng = new System.Random(seed);
            return b;
        }

        private void Update()
        {
            if (_agent == null || Director == null || !_agent.Alive || (Suspended?.Invoke() ?? false)) return;
            var memory = _agent.Brain.Memory;
            float now = Time.time;
            ServiceLocator.TryResolve<IPlayerObservable>(out var player);
            bool sheltered = !_agent.SeesPlayer || (player != null && (player.IsInCover || player.IsHidden));
            var d = Director.Tick(new BossSense(now, memory.Engaged(now), _agent.HealthFraction, _agent.Position,
                memory.LastKnownPlayerPosition, sheltered));
            if (d.CallReinforcements) CallReinforcements(memory.LastKnownPlayerPosition, now);
            if (d.Relocate) Relocate(memory.LastKnownPlayerPosition, d.RelocateReason);
            if (d.ThrowGrenade) ThrowGrenade(d.GrenadeTarget);
        }

        /// <summary>Moves to another cover node; false when the map has no graph or no node fits.</summary>
        public bool Relocate(Vector3 playerPosition, string reason = "interval")
        {
            if (!ServiceLocator.TryResolve<IWorldQuery>(out var graph)) return false;
            Vector3 from = _agent.Position;
            int here = graph.NearestNode(from);
            if (!BossRelocation.TryPick(graph, Profile, from, playerPosition, here, _rng, out int node, out var to)) return false;
            _agent.Brain.Assign(to, node, "relocate");
            Relocations++;
            EventBus.Publish(new BossRelocatedEvent(_agent.Id, node, from, to, reason ?? ""));
            return true;
        }

        public Grenade ThrowGrenade(Vector3 target)
        {
            Vector3 hand = _agent.Position + Vector3.up * 1.6f + _agent.Forward * .4f;
            var grenade = Grenade.Launch(hand, target, Profile, _agent.Id);
            GrenadesThrown++;
            EventBus.Publish(new GrenadeThrownEvent(_agent.Id, hand, target));
            return grenade;
        }

        public int CallReinforcements(Vector3 playerPosition, float now)
        {
            var provider = Reinforcements ?? NearbyAgentsReinforcements.Instance;
            var sent = provider.Summon(_agent, playerPosition, Profile.ReinforceCount, Profile.ReinforceRadius, now);
            EventBus.Publish(new BossReinforcementsEvent(_agent.Id, Profile.ReinforceCount, sent.Count, string.Join("+", sent.Select(a => a.Id))));
            return sent.Count;
        }
    }
}
