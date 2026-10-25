using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Adds a slowed walk to the player's unarmed locomotion tree, between idle and walk. Without it, any pace under a full
    /// walk (carrying a load, leading someone, wounded, wading) blends the walk with the idle, the legs slow far more than
    /// the body does and the feet slide. With the walk again at half the walking speed, and every clip played at the rate
    /// its own stride needs for its speed, the feet keep the ground's pace. Edits the existing controller in place (the builder would discard teammates' later work).
    /// </summary>
    public static class PlayerSlowWalkSetup
    {
        const string ControllerPath="Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller";
        const float SlowShare=.5f;   // of the walking speed, at the same share of the walk's rate

        /// <summary>Each locomotion clip's own ground speed (root motion), against the threshold it is played at.</summary>
        public static void Report()
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            foreach(var state in controller.layers[0].stateMachine.states.Select(s=>s.state))
                if(state.motion is BlendTree tree)foreach(var c in tree.children)
                    if(c.motion is AnimationClip clip)Debug.Log($"[SlowWalk] {state.name} | {clip.name} at {c.threshold} ({c.position}) x{c.timeScale}: clip {clip.length:0.000}s, root speed {clip.averageSpeed.magnitude:0.000} m/s, apparent {clip.apparentSpeed:0.000}, loop {clip.isLooping}");
        }
        /// <summary>
        /// Each moving clip played at the rate that makes its own ground speed (its root motion) the speed it stands for in
        /// the tree: the walk is authored for 1.45 m/s and the run for 4.1, but Nam walks at 2.5 and runs at 6.
        /// </summary>
        static void MatchStride(BlendTree tree)
        {
            var children=tree.children;
            for(int i=0;i<children.Length;i++)
                if(children[i].motion is AnimationClip clip&&children[i].threshold>0&&clip.averageSpeed.magnitude>.1f)
                    children[i].timeScale=children[i].threshold/clip.averageSpeed.magnitude;
            tree.children=children;
            foreach(var c in children)Debug.Log($"[SlowWalk] {c.motion.name} at {c.threshold} m/s plays x{c.timeScale:0.000}");
        }
        /// <summary>
        /// The unarmed run is an old clip with the legs thrown wide; Nam's own run (made for the knife, the upper body free) is
        /// what a run with a load should look like. The unarmed tree takes that clip at the knife tree's rate (25/10/2026).
        /// </summary>
        [MenuItem("ShadowVale/Player/Use Nam's run when unarmed")]
        public static void UseNamRun()
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);var states=controller.layers[0].stateMachine.states.Select(s=>s.state).ToArray();
            var tree=states.FirstOrDefault(s=>s.name==PlayerCombat.LocomotionState)?.motion as BlendTree;var knife=states.FirstOrDefault(s=>s.name=="Locomotion_Knife")?.motion as BlendTree;
            if(tree==null||knife==null){Debug.LogError("[SlowWalk] Locomotion or Locomotion_Knife tree missing in "+ControllerPath);return;}
            var run=knife.children.OrderBy(c=>c.threshold).Last();var children=tree.children;int i=System.Array.FindLastIndex(children,c=>c.threshold>=run.threshold-.01f);
            if(i<0){Debug.LogError("[SlowWalk] No run in the unarmed tree.");return;}
            children[i].motion=run.motion;children[i].timeScale=run.timeScale;tree.children=children;
            EditorUtility.SetDirty(tree);EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
            Debug.Log($"[SlowWalk] Unarmed run is now {run.motion.name} x{run.timeScale:0.000}");
        }
        [MenuItem("ShadowVale/Player/Add slowed walk to locomotion")]
        public static void Apply()
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var tree=controller.layers[0].stateMachine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==PlayerCombat.LocomotionState)?.motion as BlendTree;
            if(tree==null||tree.blendType!=BlendTreeType.Simple1D){Debug.LogError("[SlowWalk] No 1D locomotion tree in "+ControllerPath);return;}
            var children=tree.children.OrderBy(c=>c.threshold).ToList();
            if(children.Count<3){Debug.LogError("[SlowWalk] Expected idle, walk and run in the locomotion tree.");return;}
            var walk=children[children.Count-2];   // idle, (slow walk), walk, run
            float slow=walk.threshold*SlowShare;
            if(children.Any(c=>c.motion==walk.motion&&Mathf.Abs(c.threshold-slow)<.01f)){MatchStride(tree);EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();return;}
            children.Insert(children.Count-2,new ChildMotion{motion=walk.motion,threshold=slow,timeScale=walk.timeScale*SlowShare,directBlendParameter=walk.directBlendParameter});
            tree.useAutomaticThresholds=false;tree.children=children.ToArray();
            MatchStride(tree);
            EditorUtility.SetDirty(tree);EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
            Debug.Log($"[SlowWalk] Added {walk.motion.name} at {slow} m/s, rate x{walk.timeScale*SlowShare}.");
        }
    }
}
