using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Perception;
using ShadowVale.AI.Tactical;
using ShadowVale.Core.Events;
using ShadowVale.Core.Services;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using ShadowVale.Telemetry;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Noises Nam makes: emitted to the guards and other listeners, and footsteps by how he moves (wading is louder).
    /// </summary>
    public sealed partial class Map01Mission
    {
        /// <summary>A noise every guard within <paramref name="radius"/> goes to check; returns how many heard it.</summary>
        /// <summary>Every noise made, for those who listen beyond the guards in <see cref="Enemies"/> (the C3 sleepers).
        /// The guards themselves hear it through <see cref="Noise"/>, the shared AI's noise system.</summary>
        public event Action<Vector3, float, NoiseKind> NoiseEmitted;
        public int EmitNoise(Vector3 position, float radius, NoiseKind kind = NoiseKind.Other)
        {
            NoiseEmitted?.Invoke(position, radius, kind);
            return Noise.Emit(position, radius, kind);
        }

        /// <summary>
        /// How far a footstep carries, by how the player is moving. Sneaking, walking and
        /// running have to be three distinct choices rather than "running is loud, everything
        /// else is free": before this, only a sprint made any sound at all, so walking through
        /// an outpost in plain view of nobody was silent and the crouch key bought nothing.
        /// </summary>
        private static float FootstepNoiseRadius(MoveStance stance) => stance switch
        {
            MoveStance.Sneak => 3f,
            MoveStance.Sprint => 16f,
            _ => 9f,
        };

        /// <summary>Wading splashes. Multiplies whichever stance the player is in.</summary>
        private const float WadingNoiseScale = 1.45f;

        /// <summary>Landing from a jump is a single heavy footfall, loud whatever the stance.</summary>
        private const float LandingNoiseRadius = 12f;

        /// <summary>
        /// Turns one footfall into a noise the guards can hear.
        /// <para>
        /// The rate limit is deliberately per-step rather than per-second: the step itself is
        /// already paced by distance travelled, so a sprint emits more often than a walk without
        /// a timer needing to know how fast anyone is going.
        /// </para>
        /// </summary>
        public void NotifyFootstep(PlayerFootsteps.Footstep step)
        {
            if (Stopped || Hidden) return;

            float radius = step.Landing ? LandingNoiseRadius : FootstepNoiseRadius(step.Stance);
            if (step.Surface == FootSurface.Water) radius *= WadingNoiseScale;
            if (Stealth != null) radius *= Stealth.NoiseScale;
            EmitNoise(step.Position, radius, step.Landing ? NoiseKind.Landing : NoiseKind.Footstep);
        }
    }
}
