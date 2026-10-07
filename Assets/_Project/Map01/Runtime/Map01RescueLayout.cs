using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>Editable world markers; no terrain, collision or navigation edits.</summary>
    public sealed class Map01RescueLayout : MonoBehaviour
    {
        public Transform[] guardPosts, patrolRoutes, pursuitSites, shelterRun;
        public Transform safeEntry, shelterDoor, reportPoint;
        public AnimationClip namRun, hungRun, namIdle, hungIdle;
        public AudioClip gunshot;
        [Tooltip("Authored run-cycle speed in metres per second; playback follows actual path speed.")]
        public float namRunSpeed=4.34f, hungRunSpeed=6f;
        public Vector3 shelterCameraOffset=new Vector3(3,2.1f,-4);
        public Vector3 interiorCameraOffset=new Vector3(-2,1.8f,-4);
        public float shelterCameraFov=54, cameraDamping=.22f;
        [Range(10,30)] public float patrolVision = 20;
        [Range(45,130)] public float patrolAngle = 95;
        public float patrolSpeed = 1.94f, patrolPause = 2.5f;
        [Tooltip("Rescue guards only: quiet crouched footsteps, followed by a short hearing reaction.")]
        [Range(.3f,1f)] public float crouchedFootstepScale=.55f;
        [Range(.2f,1.5f)] public float footstepReactionSeconds=1f;
        [Range(1f,3f)] public float closeDetectionSeconds=1.6f;
        public float safeRadius = 8, doorRadius = 3.5f;
        public float backstabRange = 1.6f, backstabAngle = 130;
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
