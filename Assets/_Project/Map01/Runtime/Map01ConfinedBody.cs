using System;
using ShadowVale.AI.Contracts;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The body the shared FSM drives a Map 1 guard through: the guard's own NavMesh body, with every destination the
    /// brain picks passed through <see cref="Confine"/> first. The C3 commander's keeps him inside the command post's
    /// fence (a commander who finds no cover would otherwise close in on Nam, out into the forest). Null = anywhere.
    /// The controller's own moves (posts, cutscenes) go to the inner body and are not confined.
    /// </summary>
    public sealed class Map01ConfinedBody : IEnemyBody
    {
        private readonly IEnemyBody inner;
        public Func<Vector3, Vector3> Confine { get; set; }

        public Map01ConfinedBody(IEnemyBody inner) { this.inner = inner; }

        private Vector3 Allowed(Vector3 destination) => Confine != null ? Confine(destination) : destination;

        public Vector3 Position => inner.Position;
        public Vector3 Forward => inner.Forward;
        public Vector3 Velocity => inner.Velocity;
        public bool FacesManually { get => inner.FacesManually; set => inner.FacesManually = value; }
        public Quaternion Rotation { get => inner.Rotation; set => inner.Rotation = value; }
        public float HealthFraction => inner.HealthFraction;
        public bool IsDead => inner.IsDead;
        public void MoveTo(Vector3 destination) => inner.MoveTo(Allowed(destination));
        public void MoveToIfNew(Vector3 destination) => inner.MoveToIfNew(Allowed(destination));
        public void Stop() => inner.Stop();
        public void Resume() => inner.Resume();
        public bool HasArrived(float tolerance) => inner.HasArrived(tolerance);
        public void SetPace(float scale) => inner.SetPace(scale);
        public void Face(Vector3 point) => inner.Face(point);
        public void Turn(float degrees) => inner.Turn(degrees);
        public void Animate(bool moving) => inner.Animate(moving);
        public bool TrySampleReachable(Vector3 desired, float maxDistance, out Vector3 point)
        {
            if (!inner.TrySampleReachable(desired, maxDistance, out point)) return false;
            if (Confine == null) return true;
            // A point outside the bounds is not reachable for him: the peek and duck spots stay inside too.
            return (Confine(point) - point).sqrMagnitude < .01f;
        }
    }
}
