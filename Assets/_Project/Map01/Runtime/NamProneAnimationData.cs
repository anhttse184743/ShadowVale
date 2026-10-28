using System;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Approved Blender motion baked onto the existing Nam hierarchy.</summary>
    public sealed class NamProneAnimationData : ScriptableObject
    {
        [Serializable] public sealed class Take
        {
            public string name;
            public Map01PistolAnimationData.Track[] bones;
            public Map01PistolAnimationData.Track grip;
            public Map01PistolAnimationData.Track oldMagazine, newMagazine;
            public bool[] oldVisible, newVisible;
        }
        public int fps = 30, frames = 73;
        public Take[] takes;
        public string sourceHash;
        public Take Find(string name) => Array.Find(takes, t => t.name == name);
    }
}
