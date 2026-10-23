using System;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Editor-baked approved K54 motion. Poses address Nam's existing bones, never a replacement Avatar.</summary>
    public sealed class Map01PistolAnimationData : ScriptableObject
    {
        [Serializable] public struct Pose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public static Pose Read(Transform t) => new() {position=t.localPosition, rotation=t.localRotation, scale=t.localScale};
            public static Pose Mix(Pose a, Pose b, float u) => new() {position=Vector3.LerpUnclamped(a.position,b.position,u), rotation=Quaternion.SlerpUnclamped(a.rotation,b.rotation,u), scale=Vector3.LerpUnclamped(a.scale,b.scale,u)};
            public void Apply(Transform t, float weight=1)
            {
                t.SetLocalPositionAndRotation(Vector3.Lerp(t.localPosition,position,weight),Quaternion.Slerp(t.localRotation,rotation,weight));
                t.localScale=Vector3.Lerp(t.localScale,scale,weight);
            }
        }
        [Serializable] public sealed class Track
        {
            public string name;
            public Pose[] poses;
            public Pose At(float frame)
            {
                float f=Mathf.Clamp(frame-1,0,poses.Length-1);int a=(int)f;
                return Pose.Mix(poses[a],poses[Mathf.Min(a+1,poses.Length-1)],f-a);
            }
        }
        [Serializable] public sealed class Hand
        {
            public string name, rootBone;
            public string[] bones;
            public Mesh mesh;
            public Bounds bounds;
        }
        public Track[] bones, parts;
        public Track weapon;
        public GameObject prefab;
        public Hand[] hands;
        public Material skin;
        public bool[] magazineVisible;
        public bool[] roundVisible;
        public string sourceHash;
        public int frames=652, fps=60;
    }
}
