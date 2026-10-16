using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Editable world markers; no terrain, collision or navigation edits.</summary>
    public sealed class Map01RescueLayout : MonoBehaviour
    {
        public Transform[] guardPosts, patrolRoutes, pursuitSites, shelterRun;
        public Transform safeEntry, shelterDoor, reportPoint;
        public AnimationClip namRun, hungRun, namIdle, hungIdle;
        public AnimationClip namWalk,hungWalk,namKnifeWalk;
        [Tooltip("Shelter cutscene only: walking speed and minimum separation between passengers.")]
        public float shelterWalkSpeed=1.35f,shelterFollowGap=1.4f;
        public float namWalkCycleSpeed=1.8f,hungWalkCycleSpeed=1.94f,namKnifeWalkCycleSpeed=1.94f;
        public AudioClip gunshot;
        [Header("Rescue story pacing")]
        public float introRadius=40, introSeconds=12, executionCameraSeconds=3, executionAimSeconds=.8f, executionHoldSeconds=2;

        [Header("Distraction stone pickups")]
        [Min(1)] public int stonesPerPile=3;
        [Tooltip("World X/Z offsets from Hung's captive post; sampled onto safe ground. Stable index = save ID.")]
        public Vector3[] stoneOffsets={
            new(-9,0,-44),new(9,0,-44),new(-18,0,-32),new(16,0,-32),
            new(-22,0,-20),new(20,0,-20),new(-16,0,-9),new(12,0,-12),
            new(-28,0,5),new(24,0,7),new(-12,0,18),new(12,0,20)
        };

        [Tooltip("Authored run-cycle speed in metres per second; playback follows actual path speed.")]
        public float namRunSpeed=4.34f, hungRunSpeed=6f;
        public Vector3 shelterCameraOffset=new Vector3(3,2.1f,-4);
        public Vector3 interiorCameraOffset=new Vector3(-4,1.8f,-2);
        public float shelterCameraFov=54, cameraDamping=.22f;
        [Range(10,30)] public float patrolVision = 20;
        [Range(45,130)] public float patrolAngle = 95;
        public float patrolSpeed = 1.94f, patrolPause = 2.5f;
        [Tooltip("Rescue guards only: quiet crouched footsteps, followed by a short hearing reaction.")]
        [Range(.3f,1f)] public float crouchedFootstepScale=.55f;
        [Range(.2f,1.5f)] public float footstepReactionSeconds=1f;
        [Range(1f,3f)] public float closeDetectionSeconds=1.6f;
        [Tooltip("Overseer only: walk out toward the stone, then keep a narrow forward search instead of sweeping behind.")]
        public float overseerStoneRadius=12f, overseerStoneFocusSeconds=11f, overseerStoneSweepDegrees=8f;
        [Tooltip("Only an overseer physically within this distance of his guard post can execute the captive.")]
        public float overseerHostageRadius=1.25f;
        [Tooltip("Slightly more forgiving rear knife approach for the overseer only.")]
        public float overseerBackstabRange=2f, overseerBackstabAngle=120f;
        public float safeRadius = 8, doorRadius = 3.5f;
        public float backstabRange = 1.6f, backstabAngle = 130;
        public float takedownHintRange=3.2f;
        public float killCameraSeconds = .9f, shelterSeconds = 8, spawnClearance = 18;
        public float[] waveThresholds = { .8f, .55f, .3f };
        private void OnDrawGizmosSelected()
        {
            if(safeEntry!=null){Gizmos.color=Color.green;Gizmos.DrawWireSphere(safeEntry.position,safeRadius);}
            if(shelterDoor!=null){Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(shelterDoor.position,doorRadius);}
            foreach(var route in patrolRoutes??System.Array.Empty<Transform>())
                for(int i=0;route!=null&&i<route.childCount;i++){
                    Gizmos.color=Color.yellow;Gizmos.DrawLine(route.GetChild(i).position,route.GetChild((i+1)%route.childCount).position);
                }
        }
    }
    public enum Map01NoiseKind { Other, Gunshot, Footstep, Landing, Stone }
    public enum Map01DetectionCause { Sight, Gunshot }
    public readonly struct Map01Detection
    {
        public readonly Map01EnemyController observer;
        public readonly Map01DetectionCause cause;
        public readonly Vector3 position;
        public Map01Detection(Map01EnemyController observer, Map01DetectionCause cause, Vector3 position)
        {this.observer=observer;this.cause=cause;this.position=position;}
    }
}
