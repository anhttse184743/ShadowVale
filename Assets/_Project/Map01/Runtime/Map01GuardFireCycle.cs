using System;
using UnityEngine;

namespace ShadowVale.Map01
{
    [Serializable] public sealed class Map01GuardFireTuning
    {
        public int burstRounds = 3, magazineRounds = 30, maxConcurrentBursts = 2;
        public float burstPause = 1.8f, reloadSeconds = 3.2f, staggerSeconds = .35f;
        public int BurstRounds => Mathf.Clamp(burstRounds, 1, 6);
        public int MagazineRounds => Mathf.Clamp(magazineRounds, BurstRounds, 60);
    }

    /// <summary>NPC trigger discipline. Weapon RPM applies inside a burst, never through its recovery or reload.</summary>
    public sealed class Map01GuardFireCycle
    {
        public int BurstShots { get; private set; }
        public int MagazineSpent { get; private set; }
        public float AfterShot(Map01GuardFireTuning tuning, float shotSeconds, float stagger)
        {
            BurstShots++; MagazineSpent++;
            if (MagazineSpent >= tuning.MagazineRounds)
            {
                Reset();
                return Mathf.Max(.5f, tuning.reloadSeconds);
            }
            if (BurstShots < tuning.BurstRounds) return Mathf.Max(.05f, shotSeconds);
            BurstShots = 0;
            return Mathf.Max(.5f, tuning.burstPause) + Mathf.Max(0f, stagger);
        }
        public void Reset() { BurstShots = 0; MagazineSpent = 0; }
        public void Restore(int burstShots, int magazineSpent, Map01GuardFireTuning tuning)
        {
            BurstShots = Mathf.Clamp(burstShots, 0, tuning.BurstRounds - 1);
            MagazineSpent = Mathf.Clamp(magazineSpent, 0, tuning.MagazineRounds - 1);
        }
    }
}
