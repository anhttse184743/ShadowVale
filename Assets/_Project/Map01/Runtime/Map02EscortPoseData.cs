using System;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Approved Blender motion sampled onto bones by name, independent of an Animator root's hierarchy.</summary>
    [Serializable] public sealed class Map02EscortPoseData
    {
        [Serializable] public sealed class Frame {public Map01PistolAnimationData.Pose[] bones;}
        public string[] names;
        public Frame idle;
        public Frame[] walk;
        public float duration;
        public static Map02EscortPoseData Load(string who)
        {
            var text=Resources.Load<TextAsset>("Story/TamEscort/"+who+"Poses");
            return text!=null?JsonUtility.FromJson<Map02EscortPoseData>(text.text):null;
        }
        public Transform[] Bind(Animator actor)
        {
            var nodes=actor.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name.Split(':').Last()).ToDictionary(g=>g.Key,g=>g.First());
            var bones=names.Select(n=>nodes.TryGetValue(n,out var bone)?bone:null).ToArray();
            if(bones.Any(b=>b==null))throw new InvalidOperationException("Escort bones missing on "+actor.name+": "+string.Join(", ",names.Where((n,i)=>bones[i]==null)));
            return bones;
        }
        public void Apply(Transform[] bones,float seconds,bool moving)
        {
            float frame=moving?Mathf.Repeat(seconds,Mathf.Max(.001f,duration))/Mathf.Max(.001f,duration)*(walk.Length-1):0;
            int first=Mathf.FloorToInt(frame),second=Mathf.Min(first+1,walk.Length-1);float weight=frame-first;
            for(int i=0;i<bones.Length;i++)
            {
                var pose=moving?Map01PistolAnimationData.Pose.Mix(walk[first].bones[i],walk[second].bones[i],weight):idle.bones[i];
                pose.Apply(bones[i]);
            }
        }
    }
}
