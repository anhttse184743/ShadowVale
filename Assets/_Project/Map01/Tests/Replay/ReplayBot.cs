using System.Collections.Generic;
using ShadowVale.AI.Perception;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01.Tests.Replay
{
    /// <summary>
    /// Plays Nam from a script instead of the keyboard: walk or sneak along the NavMesh, stand or
    /// crouch somewhere for a while, make a noise (a thrown stone). The player controller is
    /// switched off while it runs; footsteps go to the guards exactly as Nam's own would
    /// (<see cref="Map01Mission.NotifyFootstep"/>), crouching and hiding through the same flags.
    /// Driven one fixed step at a time by the harness, so a run depends only on its seed.
    /// </summary>
    public sealed class ReplayBot
    {
        public const float WalkSpeed = 4.2f, SneakSpeed = 2.3f, RunSpeed = 7f;

        public abstract class Step
        {
            /// <returns>True when done.</returns>
            public abstract bool Tick(ReplayBot bot, float dt);
        }

        public sealed class MoveTo : Step
        {
            private readonly Vector3 _goal;
            private readonly MoveStance _stance;
            private readonly Queue<Vector3> _corners = new Queue<Vector3>();
            private bool _planned;
            private float _left;

            /// <param name="maxSeconds">Gives up after this long (a sprint that runs out of breath); 0 = until there.</param>
            public MoveTo(Vector3 goal, MoveStance stance, float maxSeconds = 0) { _goal = goal; _stance = stance; _left = maxSeconds > 0 ? maxSeconds : float.PositiveInfinity; }

            public override bool Tick(ReplayBot bot, float dt)
            {
                if (!_planned)
                {
                    _planned = true;
                    var path = new NavMeshPath();
                    if (NavMesh.CalculatePath(bot.Position, _goal, NavMesh.AllAreas, path) && path.corners.Length > 1)
                        for (int i = 1; i < path.corners.Length; i++) _corners.Enqueue(path.corners[i]);
                    else _corners.Enqueue(_goal);
                }
                bot.SetCrouch(_stance == MoveStance.Sneak);
                float budget = dt * Speed(_stance) * bot.SurfaceMultiplier;
                while (budget > 0 && _corners.Count > 0)
                {
                    Vector3 next = _corners.Peek();
                    Vector3 delta = next - bot.Position;
                    float d = delta.magnitude;
                    if (d <= budget) { bot.Walk(next, _stance); budget -= d; _corners.Dequeue(); continue; }
                    bot.Walk(bot.Position + delta / d * budget, _stance);
                    budget = 0;
                }
                _left -= dt;
                return _corners.Count == 0 || _left <= 0;
            }

            private static float Speed(MoveStance s) => s == MoveStance.Sneak ? SneakSpeed : s == MoveStance.Sprint ? RunSpeed : WalkSpeed;
        }

        public sealed class Hold : Step
        {
            private readonly bool _crouch;
            private float _left;
            public Hold(float seconds, bool crouch) { _left = seconds; _crouch = crouch; }

            public override bool Tick(ReplayBot bot, float dt)
            {
                bot.SetCrouch(_crouch);
                _left -= dt;
                return _left <= 0;
            }
        }

        /// <summary>A stone landing at <paramref name="at"/>: the noise the real throw makes where it lands.</summary>
        public sealed class Noise : Step
        {
            private readonly Vector3 _at;
            private readonly float _radius;
            public Noise(Vector3 at, float radius) { _at = at; _radius = radius; }

            public override bool Tick(ReplayBot bot, float dt)
            {
                bot.Mission.EmitNoise(_at, _radius, NoiseKind.Stone);
                bot.Stones++;
                return true;
            }
        }

        public Map01Mission Mission { get; }
        public int Stones { get; private set; }
        public bool Finished => _steps.Count == 0;
        public float DistanceWalked { get; private set; }

        private readonly Queue<Step> _steps;
        private readonly Transform _player;
        private readonly PlayerController _controller;
        private readonly CharacterController _body;
        private float _strideLeft;
        private bool _leftFoot;

        public ReplayBot(Map01Mission mission, IEnumerable<Step> steps)
        {
            Mission = mission;
            _player = mission.player;
            _controller = _player.GetComponent<PlayerController>();
            _body = _player.GetComponent<CharacterController>();
            _steps = new Queue<Step>(steps);
            if (_controller != null) _controller.enabled = false;
        }

        public Vector3 Position => _player.position;
        private float SurfaceMultiplier => Mission.MovementSurfaceMultiplier;

        /// <summary>Puts Nam at <paramref name="at"/> (on the NavMesh) facing <paramref name="towards"/>.</summary>
        public void Place(Vector3 at, Vector3 towards)
        {
            if (NavMesh.SamplePosition(at, out var hit, 4f, NavMesh.AllAreas)) at = hit.position;
            Teleport(at);
            Face(towards - at);
        }

        /// <summary>Drops what is left of the script for <paramref name="steps"/> (once: the reaction to being spotted).</summary>
        public bool Interrupt(IEnumerable<Step> steps)
        {
            if (Interrupted || steps == null) return false;
            Interrupted = true;
            _steps.Clear();
            foreach (var s in steps) _steps.Enqueue(s);
            return true;
        }

        public bool Interrupted { get; private set; }

        /// <summary>One fixed step. False once the script has run out.</summary>
        public bool Tick(float dt)
        {
            if (_steps.Count == 0) return false;
            if (_steps.Peek().Tick(this, dt)) _steps.Dequeue();
            Mission.UpdateHiddenState();
            return _steps.Count > 0;
        }

        /// <summary>Gives the keyboard back.</summary>
        public void Release()
        {
            SetCrouch(false);
            if (_controller != null) _controller.enabled = true;
        }

        private void Walk(Vector3 to, MoveStance stance)
        {
            Vector3 from = _player.position;
            float d = Vector3.Distance(from, to);
            if (d < 1e-4f) return;
            Face(to - from);
            Teleport(to);
            DistanceWalked += d;
            // A footstep every stride, as PlayerFootsteps paces them by distance.
            float stride = stance == MoveStance.Sneak ? .55f : stance == MoveStance.Sprint ? 1f : .75f;
            _strideLeft -= d;
            while (_strideLeft <= 0)
            {
                _strideLeft += stride;
                _leftFoot = !_leftFoot;
                Mission.NotifyFootstep(new PlayerFootsteps.Footstep(_player.position, stance,
                    Mission.IsWading ? FootSurface.Water : FootSurface.Grass, _leftFoot, landing: false));
            }
        }

        private void SetCrouch(bool crouch)
        {
            Mission.Crouched = crouch;
            // The interaction component copies the controller's sneak flag into the mission every frame.
            if (_controller != null && _controller.IsSneaking != crouch) _controller.RestoreMotion(crouch);
        }

        private void Teleport(Vector3 to)
        {
            bool had = _body != null && _body.enabled;
            if (had) _body.enabled = false;
            _player.position = to;
            if (had) _body.enabled = true;
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > 1e-4f) _player.rotation = Quaternion.LookRotation(direction);
        }
    }
}
