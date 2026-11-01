using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Registers Map 2's entry points with <see cref="MapTwoBridge"/> before any scene loads (and on every editor
    /// domain reload, for edit-mode tools and tests), so Map 1's menu, HUD and boat crossing can reach Map 2.
    /// </summary>
    public static class Map02Bridge
    {
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Register()
        {
            MapTwoBridge.FindMission = () => Object.FindFirstObjectByType<Map02Mission>();
            MapTwoBridge.ExplorationOpen = () => Object.FindFirstObjectByType<Map02Exploration>() != null;
            MapTwoBridge.BeginGame = Map02Mission.BeginGame;
            MapTwoBridge.AddArrival = boat => boat.AddComponent<Map02Arrival>();
        }
    }
}
