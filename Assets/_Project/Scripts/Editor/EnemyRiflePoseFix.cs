using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ShadowVale.Map01;

namespace ShadowVale.Editor
{
    [InitializeOnLoad]
    public static class EnemyRiflePoseFix
    {
        public const string Output = "Assets/_Project/Art/Characters/Animations/Generated/";
        static EnemyRiflePoseFix() { EditorApplication.update += Poll; }
        static void Poll()
        {
            const string rename = "Tools/EnemyAnimationNames.request";
            if (File.Exists(rename) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                File.Delete(rename);
                foreach (string name in new[] { "Enemy_Rifle_AimIdle", "Enemy_Rifle_Fire" })
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Output + name + "_WristNeutral.anim");
                    clip.name = name; EditorUtility.SetDirty(clip);
                }
                AssetDatabase.SaveAssets();
                File.WriteAllText("Tools/EnemyAnimationNames.result.txt", "PASS: Enemy_Rifle_AimIdle; Enemy_Rifle_Fire");
                File.WriteAllText("Tools/BossAnimationPreview.request", "fire");
            }
            const string request = "Tools/EnemyRiflePoseFix.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try
            {
                Build("Enemy_Rifle_AimIdle"); Build("Enemy_Rifle_Fire");
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyAnimatorBuilder.ControllerPath);
                foreach (var layer in controller.layers) Replace(layer.stateMachine);
                EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
                File.WriteAllText("Tools/EnemyRiflePoseFix.result.txt", "Wrist curves calibrated against boss forearm and palm directions. See *_WristCheck.txt for measured angles.");
                File.WriteAllText("Tools/BossAnimationPreview.request", "fire");
            }
            catch(Exception e) { File.WriteAllText("Tools/EnemyRiflePoseFix.result.txt", e.ToString()); Debug.LogException(e); }
        }
        static void Replace(AnimatorStateMachine machine)
        {
            foreach (var state in machine.states)
                if (state.state.name == "Aim" || state.state.name == "Fire")
                    state.state.motion = StoryAnimationSetup.Clip("", state.state.name == "Aim" ? "Enemy_Rifle_AimIdle" : "Enemy_Rifle_Fire");
            foreach (var child in machine.stateMachines) Replace(child.stateMachine);
        }
        static void Build(string name)
        {
            string nam = name.Replace("Enemy_", "Nam_");
            var source = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Animations/Nam/" + nam + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
            var clip = UnityEngine.Object.Instantiate(source);
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Map01/Resources/Characters/EnemyCommanderVisual.prefab"));
            try
            {
                var actor = model.GetComponent<Animator>();
                using var pose = new Map01CinematicPose(actor, skinGrounding:false);
                using var handler = new HumanPoseHandler(actor.avatar, actor.transform);
                foreach (bool left in new[] { false, true })
                {
                    string side = left ? "Left" : "Right";
                    var hand = actor.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    var tip = actor.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                    if(tip == null) tip = hand.Cast<Transform>().FirstOrDefault();
                    if(tip == null) throw new Exception(side + " hand has no palm endpoint");
                    int[] indices = new[] { "Down-Up", "In-Out" }.Select(axis => Array.IndexOf(HumanTrait.MuscleName, side + " Hand " + axis)).ToArray();
                    var curves = new[] { new AnimationCurve(), new AnimationCurve() };
                    int frames = Mathf.CeilToInt(source.length * 60);
                    for(int frame=0;frame<=frames;frame++)
                    {
                        float t = Mathf.Min(source.length, frame / 60f);
                        pose.Sample(source, t);
                        var human = new HumanPose(); handler.GetHumanPose(ref human);
                        foreach(int i in indices) human.muscles[i] = 0;
                        handler.SetHumanPose(ref human);
                        for(int sweep=0;sweep<70;sweep++)
                        {
                            float step = sweep < 20 ? .1f : sweep < 45 ? .025f : .005f;
                            foreach(int i in indices)
                            {
                                float value = human.muscles[i], best = value, score = WristAngle(actor, left, tip);
                                foreach(float delta in new[] { -step, step })
                                {
                                    human.muscles[i] = Mathf.Clamp(value + delta, -1, 1); handler.SetHumanPose(ref human);
                                    float cost = WristAngle(actor, left, tip);
                                    if(cost < score) { score = cost; best = human.muscles[i]; }
                                }
                                human.muscles[i] = best; handler.SetHumanPose(ref human);
                            }
                        }
                        for(int i=0;i<2;i++) curves[i].AddKey(t, human.muscles[indices[i]]);
                    }
                    for(int i=0;i<2;i++) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), HumanTrait.MuscleName[indices[i]]), curves[i]);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
            string path = Output + name + "_WristNeutral.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) AssetDatabase.CreateAsset(clip, path);
            else { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); }
            var named = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            named.name = name; EditorUtility.SetDirty(named);
            Validate(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), name);
        }
        static float WristAngle(Animator actor, bool left, Transform tip)
        {
            var hand = actor.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var lower = actor.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            return Vector3.Angle(hand.position - lower.position, tip.position - hand.position);
        }
        static void Validate(AnimationClip clip, string name)
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Map01/Resources/Characters/EnemyCommanderVisual.prefab"));
            try
            {
                var actor = model.GetComponent<Animator>();
                using var pose = new Map01CinematicPose(actor, skinGrounding:false);
                string report = "Clip=" + clip.name + "\n";
                foreach(bool left in new[] { false, true })
                {
                    var hand = actor.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    var tip = actor.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal) ?? hand.Cast<Transform>().First();
                    float worst = 0;
                    for(int frame=0;frame<=60;frame++) { pose.Sample(clip, clip.length * frame / 60f); worst=Mathf.Max(worst,WristAngle(actor,left,tip)); }
                    report += (left ? "Left" : "Right") + " worst wrist angle=" + worst.ToString("F2") + " degrees; endpoint=" + tip.name + "\n";
                }
                File.WriteAllText("Tools/BossPose/"+name+"_WristCheck.txt", report);
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }
    }
}
