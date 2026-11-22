using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// T032 (giám đốc 22/11: "sửa lại như cách thằng Nam cầm súng"): the enemy commander keeps the guards' controller
    /// (states, triggers, deaths) but his rifle clips are Nam's — an override controller built by T032EnemyCommanderInstall.
    /// Ordinary guards are not touched.
    /// </summary>
    public static class Map01EnemyCommanderRig
    {
        const string OverridePath = "Characters/EnemyCommander_NamRifle";
        static AnimatorOverrideController cached; static bool loaded;

        /// <summary>The commander's controller for the given guards' controller (the guards' one when the override is missing or was built for another).</summary>
        public static RuntimeAnimatorController Controller(RuntimeAnimatorController guards)
        {
            if (!loaded) { cached = Resources.Load<AnimatorOverrideController>(OverridePath); loaded = true; }
            return cached != null && guards != null && cached.runtimeAnimatorController == guards ? cached : guards;
        }
    }
}
