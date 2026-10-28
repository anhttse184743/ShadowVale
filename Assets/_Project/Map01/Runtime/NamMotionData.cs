using System;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Approved v3 samples in the existing player's local bone space; no replacement Avatar.</summary>
    [PreferBinarySerialization]
    public sealed class NamMotionData : ScriptableObject
    {
        [Serializable] public sealed class Clip
        {
            public string id, label, prop;
            public int start, end;
            public int Frames => end-start+1;
            public float Seconds => Frames/60f;
            public float At(float seconds,bool loop=false) => start+(loop
                ? Mathf.Repeat(Mathf.Max(0,seconds)*60,Mathf.Max(1,end-start))
                : Mathf.Clamp(seconds*60,0,end-start));
            public float Normalized(float progress)=>Mathf.Lerp(start,end,Mathf.Clamp01(progress));
        }
        [Serializable] public sealed class Part
        {
            public Map01PistolAnimationData.Track track;
            public bool[] visible;
            public bool Visible(float frame)=>visible!=null&&visible.Length>0&&visible[Mathf.Clamp(Mathf.RoundToInt(frame)-1,0,visible.Length-1)];
        }
        [Serializable] public sealed class Prop
        {
            public string key;
            public GameObject prefab;
            public Map01PistolAnimationData.Track root;
            public Part[] parts;
        }
        public string sourceHash;
        public int frames=3208,fps=60;
        public bool rootFlightRemoved,rootXZRemoved;
        public Map01PistolAnimationData.Track[] bones;
        public Clip[] clips;
        public Prop[] props;
        public Clip Find(string id)=>clips.FirstOrDefault(c=>c.id==id);
        public Prop FindProp(string key)=>props.FirstOrDefault(p=>p.key==key);
        public static Matrix4x4 Matrix(Map01PistolAnimationData.Pose p)=>Matrix4x4.TRS(p.position,p.rotation,p.scale);
        public static bool FitsSkeleton(Transform rig,Map01PistolAnimationData.Track[] tracks)
        {
            if(rig==null||tracks==null)return false;
            var names=new System.Collections.Generic.HashSet<string>(rig.GetComponentsInChildren<Transform>(true).Select(t=>t.name));
            return tracks.All(t=>names.Contains(t.name));
        }
    }
}
