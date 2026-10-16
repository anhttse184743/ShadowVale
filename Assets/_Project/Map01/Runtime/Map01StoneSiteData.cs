using System;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Where throwing stones lie on Map 1, worked out from the map by the editor tool
    /// (ShadowVale > Map 1 > Rải đá nhặt được) and spawned at run time by <see cref="Map01StoneSites"/>,
    /// so Map 1.unity itself is not touched. Run the tool again after moving camps, boulders or the river.
    /// </summary>
    public sealed class Map01StoneSiteData : ScriptableObject
    {
        public const string ResourcePath = "Stones/Map01StoneSites";

        public enum Source
        {
            /// <summary>A gravel bar at the water's edge: the river keeps bringing more.</summary>
            Riverbank,
            /// <summary>Loose stones at the foot of a boulder.</summary>
            Boulder,
            /// <summary>A few stones beside a path, where neither river nor boulder is near.</summary>
            Trail
        }

        [Serializable]
        public struct Site
        {
            public Vector3 position;
            public Source source;
            [Tooltip("Stones lying here.")]
            public int stones;
        }

        public Site[] sites = Array.Empty<Site>();

        [Tooltip("Seconds until a riverbank bar has its stones again. The others are picked up once.")]
        public float riverbankRestockSeconds = 90f;

        [Tooltip("The pebble models the stones and the gravel around them are made of.")]
        public GameObject[] pebbles = Array.Empty<GameObject>();
    }
}
