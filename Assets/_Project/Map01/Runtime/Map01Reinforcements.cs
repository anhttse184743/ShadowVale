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
    /// Who answers Map 1's commander. By the time he shows up the three camps have fallen, so the
    /// idle guards still standing (the jetty, the gates) go first and fresh men make up the rest:
    /// clones of an outpost guard, like the commander himself, arriving from behind him — away
    /// from Nam — so the player sees them come.
    /// </summary>
    public sealed class Map01Reinforcements : IReinforcementProvider
    {
        /// <summary>How far behind the commander the fresh men appear: close enough to reach the fight in a
        /// few seconds at a run (was 24 m, from where they arrived only after he had fallen).</summary>
        public const float SpawnDistance = 12f;
        /// <summary>What Nam hears when the call goes out.</summary>
        public const string CallLine = "Chỉ huy địch: \"Tiếp viện! Tất cả về đây!\"";

        private readonly Map01Mission _mission;
        private readonly Map01EnemyController _template;

        public int Spawned { get; private set; }

        public Map01Reinforcements(Map01Mission mission, Map01EnemyController template)
        {
            _mission = mission; _template = template;
        }

        public IReadOnlyList<IEnemyAgent> Summon(IEnemyAgent boss, Vector3 playerPosition, int count, float radius, float now)
        {
            _mission.Say(CallLine, 4f); // The call is heard, so the player knows what is coming.
            var sent = NearbyAgentsReinforcements.Instance.Summon(boss, playerPosition, count, radius, now).ToList();
            if (_template == null) return sent;
            for (int i = sent.Count; i < count; i++)
            {
                var guard = Spawn(boss.Position, playerPosition, i);
                if (guard == null) break;
                NearbyAgentsReinforcements.Send(guard, playerPosition, now, boss, i);
                sent.Add(guard);
            }
            return sent;
        }

        private Map01EnemyController Spawn(Vector3 boss, Vector3 player, int index)
        {
            if (!TrySpawnPoint(boss, player, index, out var at)) return null;
            var go = Object.Instantiate(_template.gameObject, at, Quaternion.LookRotation(Flat(player - at)), _template.transform.parent);
            go.name = "Viện binh " + (++Spawned);
            // The template may be a corpse already looted: the clone must not carry its loot point along.
            var loot = go.GetComponent<ForestPoint>();
            if (loot != null) Object.DestroyImmediate(loot);
            var guard = go.GetComponent<Map01EnemyController>();
            guard.Configure(System.Array.Empty<Vector3>());
            guard.BindMission(_mission);
            var health = go.GetComponent<Health>();
            if (health != null && health.IsDead) health.Revive();
            guard.SquadId = "reinforce";
            _mission.AddEnemy(guard);
            return guard;
        }

        /// <summary>Behind the commander as seen from Nam, fanned out, on the NavMesh.</summary>
        private static bool TrySpawnPoint(Vector3 boss, Vector3 player, int index, out Vector3 point)
        {
            Vector3 away = Flat(boss - player);
            float spread = (index - 1) * 25f;
            foreach (float turn in new[] { spread, spread + 60f, spread - 60f, spread + 120f })
            {
                Vector3 candidate = boss + Quaternion.Euler(0, turn, 0) * away * SpawnDistance;
                if (NavMesh.SamplePosition(candidate, out var hit, 6f, NavMesh.AllAreas)) { point = hit.position; return true; }
            }
            point = default;
            return false;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }
    }
}
