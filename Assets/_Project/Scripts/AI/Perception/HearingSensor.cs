using UnityEngine;

namespace ShadowVale.AI.Perception
{
    /// <summary>
    /// A noise carries <c>radius</c> metres; a listener inside it hears it. <paramref name="hearingScale"/>
    /// is the archetype's sharpness (1 = the noise's own radius). No occlusion by walls yet.
    /// </summary>
    public static class HearingSensor
    {
        public static bool Hears(Vector3 listener, Vector3 noisePosition, float radius, float hearingScale = 1f)
        {
            float reach = radius * hearingScale;
            return reach > 0f && (noisePosition - listener).sqrMagnitude <= reach * reach;
        }
    }
}
