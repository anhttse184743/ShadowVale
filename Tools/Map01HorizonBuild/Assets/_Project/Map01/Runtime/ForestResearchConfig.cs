using System;
namespace ShadowVale.Map01
{
    [Serializable] public sealed class ForestResearchConfig
    {
        public string solver="qiea";
        public int seed=1729, solverBudgetMs=4, population=8, iterations=80;
        public float replanSeconds=2, radioRange=42, memorySeconds=9;
        public float coverageWeight=6, flankWeight=3, travelWeight=.16f, overlapPenalty=12, assignmentPenalty=100;
        public int burstRounds=3, magazineRounds=18;
        public float shotSpacing=.19f, burstPause=1.05f, reloadSeconds=2.6f, aimSpreadDegrees=3.2f, guardDamageScale=.16f;
        public bool telemetry=true, captureSnapshots=true;
    }
}
