using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace ShadowVale.Map01 {
    public sealed class Map02UpperBodyPose:IDisposable {
        readonly Animator actor; readonly PlayableGraph graph; readonly AnimationLayerMixerPlayable mixer;
        AnimationClipPlayable legs; readonly AnimationClipPlayable arms; AnimationClip selected; readonly AvatarMask mask;
        public Map02UpperBodyPose(Animator target,AnimationClip upper) {
            actor=target;graph=PlayableGraph.Create("Map 2 cargo upper body");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer=AnimationLayerMixerPlayable.Create(graph,2);arms=AnimationClipPlayable.Create(graph,upper);arms.SetSpeed(0);arms.SetApplyFootIK(false);
            graph.Connect(arms,0,mixer,1);mixer.SetInputWeight(0,1);mixer.SetInputWeight(1,1);
            mask=new AvatarMask();for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            foreach(var p in new[]{AvatarMaskBodyPart.Body,AvatarMaskBodyPart.LeftArm,AvatarMaskBodyPart.RightArm,AvatarMaskBodyPart.LeftFingers,AvatarMaskBodyPart.RightFingers})mask.SetHumanoidBodyPartActive(p,true);
            mixer.SetLayerMaskFromAvatarMask(1,mask);AnimationPlayableOutput.Create(graph,"Cargo",actor).SetSourcePlayable(mixer);graph.Play();
        }
        public void Sample(AnimationClip lower,float time) {
            if(lower==null)return;
            if(selected!=lower){if(legs.IsValid()){mixer.DisconnectInput(0);graph.DestroyPlayable(legs);}legs=AnimationClipPlayable.Create(graph,lower);legs.SetSpeed(0);legs.SetApplyFootIK(false);graph.Connect(legs,0,mixer,0);mixer.SetInputWeight(0,1);selected=lower;}
            legs.SetTime(Mathf.Repeat(time,Mathf.Max(.001f,lower.length)));arms.SetTime(0);graph.Evaluate(0);
        }
        public void Dispose(){if(graph.IsValid())graph.Destroy();if(Application.isPlaying)UnityEngine.Object.Destroy(mask);else UnityEngine.Object.DestroyImmediate(mask);if(actor!=null&&actor.isActiveAndEnabled)actor.Update(0);}
    }
}
