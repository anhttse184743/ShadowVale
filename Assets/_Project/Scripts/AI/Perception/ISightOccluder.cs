using UnityEngine;

namespace ShadowVale.AI.Perception
{
    /// <summary>
    /// Map-specific sight blockers that have no solid collider (Map 1: bushes and tree crowns).
    /// Walls, trunks and terrain still block through the physics ray in <see cref="VisionSensor"/>.
    /// </summary>
    public interface ISightOccluder
    {
        bool Blocks(Vector3 eye, Vector3 target);
    }
}
