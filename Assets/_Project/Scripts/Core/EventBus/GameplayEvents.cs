using UnityEngine;

namespace ShadowVale.Core.Events
{
    // Map-agnostic gameplay facts. Every map publishes them the same way; telemetry and the debug
    // tools listen, so no map needs to know either exists. AI-specific events (state changes,
    // contacts, plans) live next to the AI that raises them.

    /// <summary>The player attacked (telemetry <c>shot_fired</c>, weapon usage).</summary>
    public readonly struct PlayerAttackEvent : IEvent
    {
        /// <summary>rifle | knife | … — lower-case weapon kind.</summary>
        public readonly string Weapon;
        public readonly Vector3 Position;

        public PlayerAttackEvent(string weapon, Vector3 position) { Weapon = weapon; Position = position; }
    }

    /// <summary>The player died (telemetry <c>player_death</c>: difficulty heatmap; capture if a fight was on).</summary>
    public readonly struct PlayerDiedEvent : IEvent
    {
        public readonly Vector3 Position;
        public PlayerDiedEvent(Vector3 position) { Position = position; }
    }

    /// <summary>An enemy died (telemetry <c>enemy_killed</c>: stealth vs combat, weapon usage).</summary>
    public readonly struct EnemyKilledEvent : IEvent
    {
        public readonly string VictimId;
        /// <summary>takedown (silent, from behind) | weapon.</summary>
        public readonly string Method;
        public readonly string WeaponId;
        public readonly Vector3 Position;

        public EnemyKilledEvent(string victimId, string method, string weaponId, Vector3 position)
        {
            VictimId = victimId; Method = method; WeaponId = weaponId; Position = position;
        }
    }

    /// <summary>A mission objective was completed (telemetry <c>objective_completed</c>: progression funnel).</summary>
    public readonly struct ObjectiveCompletedEvent : IEvent
    {
        public readonly string MapId, ObjectiveId;
        /// <summary>Position of the objective in the map's sequence, from 0.</summary>
        public readonly int Index;

        public ObjectiveCompletedEvent(string mapId, string objectiveId, int index)
        {
            MapId = mapId; ObjectiveId = objectiveId; Index = index;
        }
    }

    /// <summary>The map's mission is over (telemetry <c>mission_result</c>).</summary>
    public readonly struct MissionEndedEvent : IEvent
    {
        public readonly string MapId;
        /// <summary>completed | failed.</summary>
        public readonly string Result;

        public MissionEndedEvent(string mapId, string result) { MapId = mapId; Result = result; }
    }
}
