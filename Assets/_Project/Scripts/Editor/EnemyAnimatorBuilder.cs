using System.Collections.Generic;
using System.IO;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Builds <c>AC_Enemy.controller</c>: the same clips as the player, minus everything an
    /// enemy has no use for. No knife, no fists, no jump, no sneak — they carry a rifle and
    /// that is all.
    /// <para>
    /// The one structural difference from the player's controller is that the upper-body layer
    /// starts at full weight with the rifle stance as its default state. The player's starts
    /// muted because <see cref="PlayerCombat"/> raises it on equip; an enemy has no such script,
    /// so a muted layer would leave it running the empty-handed arm swing with a rifle glued to
    /// one fist.
    /// </para>
    /// Menu <b>ShadowVale ▸ Build Enemy Animator</b>.
    /// </summary>
    public static class EnemyAnimatorBuilder
    {
        public const string ControllerPath =
            "Assets/_Project/Art/Characters/Animations/Controllers/AC_Enemy.controller";

        [MenuItem("ShadowVale/Build Enemy Animator")]
        public static void Build()
        {
            Dictionary<string, string> resolved = CharacterClipLibrary.ResolveRoleToPath();

            // Locomotion goes through the same tuner as the player's, so the enemies inherit the
            // straightened elbows and wrists rather than reading as a different rig.
            AnimationClip idle = LocomotionClipTuner.Tune(
                CharacterClipLibrary.LoadClip(resolved, "Idle"), "Idle");
            AnimationClip walk = LocomotionClipTuner.Tune(
                CharacterClipLibrary.LoadClip(resolved, "Walk"), "Walk");
            AnimationClip run = LocomotionClipTuner.Tune(
                CharacterClipLibrary.LoadClip(resolved, "Run"), "Run");

            if (idle == null || walk == null || run == null)
            {
                Debug.LogError("[EnemyAnimator] Missing Idle/Walk/Run clips — run " +
                               "'ShadowVale ▸ Setup Character Import' first.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath)!);

            // Rebuilt in place: deleting would orphan any prefab already pointing at it.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }
            PlayerAnimatorBuilder.ClearController(controller);

            controller.AddParameter(PlayerAnimatorBuilder.Params.Speed,
                AnimatorControllerParameterType.Float);
            controller.AddParameter(PlayerAnimatorBuilder.Params.Attack,
                AnimatorControllerParameterType.Trigger);
            controller.AddParameter(PlayerAnimatorBuilder.Params.Die,
                AnimatorControllerParameterType.Trigger);

            BuildBaseLayer(controller, resolved, idle, walk, run);
            BuildRifleLayer(controller, resolved);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[EnemyAnimator] Built {ControllerPath}");
        }

        /// <summary>Stand, walk, run, die. Named to match what <see cref="Health"/> revives into.</summary>
        private static void BuildBaseLayer(AnimatorController controller,
            Dictionary<string, string> resolved,
            AnimationClip idle, AnimationClip walk, AnimationClip run)
        {
            AnimatorStateMachine root = controller.layers[0].stateMachine;

            var blendTree = new BlendTree
            {
                name = PlayerCombat.LocomotionState,
                blendType = BlendTreeType.Simple1D,
                blendParameter = PlayerAnimatorBuilder.Params.Speed,
                useAutomaticThresholds = false,
            };
            blendTree.AddChild(idle, 0f);
            blendTree.AddChild(walk, PlayerAnimatorBuilder.WalkThreshold);
            blendTree.AddChild(run, PlayerAnimatorBuilder.RunThreshold);
            AssetDatabase.AddObjectToAsset(blendTree, controller);

            AnimatorState locomotion = root.AddState(PlayerCombat.LocomotionState);
            locomotion.motion = blendTree;
            locomotion.writeDefaultValues = false;
            root.defaultState = locomotion;

            AnimationClip dieClip = CharacterClipLibrary.LoadClip(resolved, "Die");
            if (dieClip == null)
            {
                Debug.LogWarning("[EnemyAnimator] No death clip — enemies will not fall over.");
                return;
            }

            AnimatorState die = root.AddState("Die");
            die.motion = dieClip;
            die.writeDefaultValues = false;
            die.speed = PlayerAnimatorBuilder.DiePlaybackSpeed;

            AnimatorStateTransition toDie = root.AddAnyStateTransition(die);
            toDie.hasExitTime = false;
            toDie.duration = 0.1f;
            toDie.canTransitionToSelf = false;
            toDie.AddCondition(AnimatorConditionMode.If, 0f, PlayerAnimatorBuilder.Params.Die);
        }

        /// <summary>
        /// Arms only: the rifle stance, and the shot that plays over it. Full weight from the
        /// start, because nothing at runtime raises it for an enemy.
        /// </summary>
        private static void BuildRifleLayer(AnimatorController controller,
            Dictionary<string, string> resolved)
        {
            AnimationClip shoot = LocomotionClipTuner.Tune(
                CharacterClipLibrary.LoadClip(resolved, "Shoot"), "Shoot");
            if (shoot == null)
            {
                Debug.LogWarning("[EnemyAnimator] No shooting clip — enemies will carry the rifle " +
                                 "with empty-handed arms.");
                return;
            }

            var stateMachine = new AnimatorStateMachine
            {
                name = PlayerAnimatorBuilder.UpperBodyLayer,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = PlayerAnimatorBuilder.UpperBodyLayer,
                stateMachine = stateMachine,
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = PlayerAnimatorBuilder.EnsureUpperBodyMask(),
            });

            AnimatorState hold = PlayerAnimatorBuilder.AddHold(
                stateMachine, "Hold_Rifle", shoot, PlayerAnimatorBuilder.RifleHoldFrame);
            stateMachine.defaultState = hold;

            AnimatorState fire = stateMachine.AddState("Attack_Rifle");
            fire.motion = shoot;
            fire.writeDefaultValues = false;
            fire.speed = PlayerAnimatorBuilder.RifleAttackSpeed;

            AnimatorStateTransition enter = stateMachine.AddAnyStateTransition(fire);
            enter.hasExitTime = false;
            enter.duration = PlayerAnimatorBuilder.AttackEnterBlend;
            enter.canTransitionToSelf = true; // full auto re-triggers the take
            enter.AddCondition(AnimatorConditionMode.If, 0f, PlayerAnimatorBuilder.Params.Attack);

            AnimatorStateTransition back = fire.AddTransition(hold);
            back.hasExitTime = true;
            back.exitTime = PlayerAnimatorBuilder.AttackExitTime;
            back.duration = PlayerAnimatorBuilder.AttackExitBlend;
        }
    }
}
