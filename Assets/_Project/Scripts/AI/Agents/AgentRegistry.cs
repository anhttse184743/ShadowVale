using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.AI.Agents
{
    /// <summary>
    /// Every enabled enemy in the loaded map. The squad coordinator gathers squads from it; the
    /// debug console lists it. Cleared when play mode starts so no stale agent survives in the editor.
    /// </summary>
    public static class AgentRegistry
    {
        private static readonly List<IEnemyAgent> Agents = new List<IEnemyAgent>();

        public static IReadOnlyList<IEnemyAgent> All => Agents;
        public static event Action<IEnemyAgent> Registered, Unregistered;

        public static void Register(IEnemyAgent agent)
        {
            if (agent == null || Agents.Contains(agent)) return;
            Agents.Add(agent);
            Registered?.Invoke(agent);
        }

        public static void Unregister(IEnemyAgent agent)
        {
            if (Agents.Remove(agent)) Unregistered?.Invoke(agent);
        }

        public static IEnemyAgent Find(string id)
        {
            foreach (var a in Agents) if (a.Id == id) return a;
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            Agents.Clear();
            Registered = null; Unregistered = null;
        }
    }
}
