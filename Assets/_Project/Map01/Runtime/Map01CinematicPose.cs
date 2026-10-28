using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ShadowVale.Map01 {
    /// <summary>Single owner for an explicitly sampled cinematic pose. No gameplay root motion.</summary>
    public sealed class Map01CinematicPose : IDisposable {
        readonly Animator actor;
        readonly Avatar originalAvatar;
        public Avatar OriginalAvatar=>originalAvatar;
        readonly RuntimeAnimatorController controller;
        readonly bool rootMotion;
        readonly AnimatorCullingMode culling;
        readonly Vector3 localPosition;
        readonly Quaternion localRotation;
        readonly float[] layers;
        readonly Map01SkinContact contact;
        readonly SkinnedMeshRenderer[] skins;
        readonly Mesh baked=new Mesh();
        PlayableGraph graph;
        AnimationClipPlayable playable;
        AnimationClip selected,secondaryClip;
        AnimationClipPlayable secondary;
        AnimationMixerPlayable mixer;
        bool mixed;
        public Map01CinematicPose(Animator target,Avatar poseAvatar=null,bool skinGrounding=true) {
            actor=target;originalAvatar=target.avatar;controller=actor.runtimeAnimatorController;rootMotion=actor.applyRootMotion;culling=actor.cullingMode;
            localPosition=actor.transform.localPosition;localRotation=actor.transform.localRotation;
            layers=new float[controller!=null?actor.layerCount:0];for(int i=0;i<layers.Length;i++)layers[i]=actor.GetLayerWeight(i);
            contact=skinGrounding?Map01InteractionWarmup.Contact(actor):null;skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
            actor.runtimeAnimatorController=null;actor.applyRootMotion=false;actor.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            if(poseAvatar!=null&&poseAvatar!=actor.avatar)BindAvatar(poseAvatar);
            graph=PlayableGraph.Create("Map 1 rescue pose");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();
        }
        public void Sample(AnimationClip clip,float seconds,bool loop=false) {
            if(clip==null||!graph.IsValid())return;
            if(mixed)ResetGraph();
            if(selected!=clip) {
                if(playable.IsValid())graph.DestroyPlayable(playable);
                if(graph.GetOutputCount()>0)graph.DestroyOutput(graph.GetOutput(0));
                playable=AnimationClipPlayable.Create(graph,clip);playable.SetSpeed(0);playable.SetApplyFootIK(false);
                AnimationPlayableOutput.Create(graph,"Rescue actor",actor).SetSourcePlayable(playable);selected=clip;
            }
            playable.SetTime(loop&&clip.length>.001f?Mathf.Repeat(seconds,clip.length):Mathf.Clamp(seconds,0,clip.length));graph.Evaluate(0);
        }
        void ResetGraph(){
            if(graph.IsValid())graph.Destroy();
            graph=PlayableGraph.Create("Map 1 rescue pose");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Play();selected=secondaryClip=null;mixed=false;
        }
        public void RestoreGameplayAvatar(){
            if(actor.avatar==originalAvatar)return;
            ResetGraph();BindAvatar(originalAvatar);
        }
        void BindAvatar(Avatar avatar){
            // Rebinding a bent grapple hierarchy would capture its deformed
            // transforms as defaults. Restore the Avatar's neutral skeleton
            // before creating the new muscle bindings, then sample immediately.
            var nodes=actor.GetComponentsInChildren<Transform>(true);
            foreach(var bone in avatar.humanDescription.skeleton)
                foreach(var node in nodes)if(node!=actor.transform&&node.name==bone.name){
                    node.SetLocalPositionAndRotation(bone.position,bone.rotation);node.localScale=bone.scale;break;
                }
            actor.avatar=avatar;actor.Rebind();
        }
        public void SampleBlend(AnimationClip first,float firstTime,AnimationClip recovery,float recoveryTime,float weight,bool loopFirst=false){
            if(first==null||recovery==null){Sample(first,firstTime);return;}
            if(!mixed||selected!=first||secondaryClip!=recovery){
                ResetGraph();playable=AnimationClipPlayable.Create(graph,first);secondary=AnimationClipPlayable.Create(graph,recovery);
                playable.SetSpeed(0);secondary.SetSpeed(0);playable.SetApplyFootIK(false);secondary.SetApplyFootIK(false);
                mixer=AnimationMixerPlayable.Create(graph,2);graph.Connect(playable,0,mixer,0);graph.Connect(secondary,0,mixer,1);
                AnimationPlayableOutput.Create(graph,"Rescue recovery",actor).SetSourcePlayable(mixer);selected=first;secondaryClip=recovery;mixed=true;
            }
            playable.SetTime(loopFirst?Mathf.Repeat(Mathf.Max(0,firstTime),Mathf.Max(.001f,first.length)):Mathf.Clamp(firstTime,0,first.length));secondary.SetTime(Mathf.Repeat(Mathf.Max(0,recoveryTime),Mathf.Max(.001f,recovery.length)));
            weight=Mathf.Clamp01(weight);mixer.SetInputWeight(0,1-weight);mixer.SetInputWeight(1,weight);graph.Evaluate(0);
        }
        public void Dispose() {
            if(graph.IsValid())graph.Destroy();
            if (baked != null) {
                if (Application.isPlaying) UnityEngine.Object.Destroy(baked);
                else UnityEngine.Object.DestroyImmediate(baked);
            }
            if(actor==null)return;
            if(actor.avatar!=originalAvatar)BindAvatar(originalAvatar);
            actor.runtimeAnimatorController=controller;actor.applyRootMotion=rootMotion;actor.cullingMode=culling;
            actor.transform.SetLocalPositionAndRotation(localPosition,localRotation);actor.Rebind();if(actor.isActiveAndEnabled)actor.Update(0);
            if(controller!=null)for(int i=0;i<Mathf.Min(layers.Length,actor.layerCount);i++)actor.SetLayerWeight(i,layers[i]);
        }
        public void Ground(float floor) {
            if(contact==null)return;
            float sole=contact.LowestY();
            if(float.IsInfinity(sole))foreach(var skin in skins)if(skin!=null){skin.BakeMesh(baked);foreach(var p in baked.vertices)sole=Mathf.Min(sole,skin.transform.TransformPoint(p).y);}
            if(!float.IsInfinity(sole))actor.transform.position+=Vector3.up*Mathf.Clamp(floor-sole,-.4f,.4f);
        }
        public static AnimationClip Clip(string name)=>Map01InteractionWarmup.Asset<AnimationClip>("Rescue/Story/"+name);
    }
}
