using UnityEngine;

namespace ShadowVale.AI.Perception
{
    /// <summary>Anything registered with a <see cref="NoiseSystem"/> that reacts to noises (enemy agents).</summary>
    public interface INoiseListener
    {
        /// <summary>Returns true if this listener heard the noise and will react to it.</summary>
        bool OnNoise(Vector3 position, float radius, NoiseKind kind);
    }
}
