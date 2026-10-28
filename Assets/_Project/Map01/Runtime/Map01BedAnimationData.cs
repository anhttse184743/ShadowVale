using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Paired authored motion in one bed coordinate system. Original gameplay Avatars stay unchanged.</summary>
    public sealed class Map01BedAnimationData : ScriptableObject
    {
        public GameObject pairPrefab, sleeperPrefab;
        public AnimationClip execution, sleep, wake;
        public string namPath, enemyPath, pistolPath, holsterPath;
        public string sourceHash;
        public bool executionIsKnife;
        public string executionPropPath;
        public int executionPropVisibleFrame=175;
        public int frames=400, fps=60, shotFrame=239;
        public int wakePickupFrame=92; // Source visibility hand-off, in the 30 fps wake take.
        public Vector3 approach, exit, wakeExit;
        public Quaternion approachRotation=Quaternion.identity, exitRotation=Quaternion.identity;
        public Bounds sleepingBounds;
        public float Duration => (frames-1f)/fps;
        public float ShotSeconds => (shotFrame-1f)/fps;
        public static Map01BedAnimationData Load() => Resources.Load<Map01BedAnimationData>("C3/BedAnimationKnife")
            ?? Resources.Load<Map01BedAnimationData>("C3/BedAnimationV11");
    }
}
