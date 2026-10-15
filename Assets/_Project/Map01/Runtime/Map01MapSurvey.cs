using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Editor-baked top-down scene survey cropped to the physical playable boundaries.</summary>
    public sealed class Map01MapSurvey : ScriptableObject
    {
        public Texture2D chart;
        public Rect bounds;
        public Vector3 safeCamp;
    }
}
