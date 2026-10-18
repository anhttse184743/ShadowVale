using UnityEngine;

namespace ShadowVale.AI.Fsm
{
    /// <summary>
    /// One enemy's memory. The FSM state is derived from it every tick (<see cref="EnemyTransitions"/>),
    /// so saving these fields is enough to restore behaviour. Times are in the brain's clock
    /// (Time.time in play).
    /// </summary>
    public sealed class EnemyBlackboard
    {
        /// <summary>0..1 — how sure he is that he has seen the player; 1 means spotted.</summary>
        public float Suspicion;
        public float EngagedUntil, AlertUntil, SearchUntil, CalmUntil;
        public Vector3 InvestigatePoint;
        public Vector3 LastKnownPlayerPosition;
        public float LastSeenTime = float.NegativeInfinity;

        /// <summary>Where to walk back to after an investigation: his post or the spot on his patrol.</summary>
        public Vector3 ReturnPoint;
        public Quaternion ReturnRotation = Quaternion.identity;
        public bool Returning;

        public Vector3[] PatrolPoints = System.Array.Empty<Vector3>();
        public int PatrolIndex;
        /// <summary>Standing at a patrol point until then (<see cref="EnemyTuning.PatrolPauseSeconds"/>).</summary>
        public float PatrolPauseUntil;

        /// <summary>Position to fight from (squad plan or nearby cover); valid while <see cref="HasAssignment"/>.</summary>
        public bool HasAssignment;
        public Vector3 AssignedPosition;
        /// <summary>Tactical-graph node of the assignment, −1 for local cover.</summary>
        public int AssignedNode = -1;
        public string AssignedRole = "";
        public bool ReachedAssignment;
        public float TakeCoverUntil;

        public float SuppressUntil;
        /// <summary>Ordered to give covering fire (his commander is moving): he stops and fires until then.</summary>
        public float CoverFireUntil;
        public bool Retreating, RetreatDone;
        public Vector3 RetreatPoint;
        public float RetreatHoldUntil, RetreatGiveUpAt;

        public bool Engaged(float now) => now < EngagedUntil;
        public bool Alerted(float now) => now < AlertUntil;
        public bool Searching(float now) => Alerted(now) && SearchUntil > 0;

        public void ClearAssignment()
        {
            HasAssignment = false; ReachedAssignment = false; AssignedNode = -1; AssignedRole = "";
        }
    }
}
