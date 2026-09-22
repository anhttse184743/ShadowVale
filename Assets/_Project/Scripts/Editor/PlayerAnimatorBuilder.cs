using System.Collections.Generic;
using System.IO;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowVale.Editor
{
    /// <summary>
    /// Builds <c>AC_Player.controller</c> from whatever clips are in the Clips folder:
    /// a base layer with a 1D locomotion blend tree (Idle → Walk → Run on <c>Speed</c>), plus
    /// sneak, jump and death; and an upper-body layer that plays the attack matching
    /// <c>Weapon</c> when <c>Attack</c> fires, and holds an aim pose while <c>Aiming</c>.
    /// Parameter names match <c>PlayerCombat.AnimatorParams</c>. States whose clip is missing are
    /// skipped and reported, so downloading the clip and re-running is all it takes to add it.
    /// Menu <b>ShadowVale ▸ Build Player Animator</b>.
    /// </summary>
    public static class PlayerAnimatorBuilder
    {
        public const string ControllerPath =
            "Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller";

        private const string UpperBodyMaskPath =
            "Assets/_Project/Art/Characters/Animations/Controllers/AM_UpperBody.mask";

        private const string UpperBodyLayer = "UpperBody";

        /// <summary>
        /// The Mixamo jump take includes a long landing settle that the controller does not wait
        /// for, so it is played faster and left early.
        /// </summary>
        private const float JumpPlaybackSpeed = 1.7f;
        private const float JumpExitTime = 0.45f;

        /// <summary>
        /// Attack takes are authored at their own pace, which is unrelated to how fast the weapon
        /// lets you swing. Each is sped up and left early so it finishes inside the weapon's
        /// cooldown, otherwise a second attack lands on top of the first.
        /// Keep in step with the cooldowns in WeaponPrefabBuilder and PlayerCombat.
        /// </summary>
        private const float AttackExitTime = 0.75f;

        /// <summary>Death take has a slow settle the testbed does not need to sit through.</summary>
        public const float DiePlaybackSpeed = 1.7f;

        /// <summary>
        /// Frame of each weapon's attack take, as a fraction, frozen to stand in for a proper
        /// "holding this weapon" idle. Without it, equipping a weapon changes nothing until you
        /// attack, and running armed looks identical to running empty-handed.
        /// </summary>
        private const float KnifeHoldFrame = 0.08f;
        private const float RifleHoldFrame = 0.12f;

        // Blend times. Long enough to read as a change of stance, short enough to stay responsive.
        private const float StanceBlend = 0.2f;
        private const float AttackEnterBlend = 0.06f;
        private const float AttackExitBlend = 0.18f;
        private const float UnarmedAttackSpeed = 1.8f;  // 0.85 s take vs 0.45 s cooldown
        private const float KnifeAttackSpeed = 2.4f;    // 2.12 s take vs 0.75 s cooldown
        private const float RifleAttackSpeed = 2.0f;    // 0.55 s take, full auto re-triggers it

        // Blend thresholds in m/s — must bracket PlayerController's walk/sprint speeds.
        private const float WalkThreshold = 2.5f;
        private const float RunThreshold = 6f;

        /// <summary>Parameter names, duplicated here so the Editor asmdef needs no runtime types.</summary>
        private static class Params
        {
            public const string Speed = "Speed";
            public const string Grounded = "Grounded";
            public const string Jump = "Jump";
            public const string Sneaking = "Sneaking";
            public const string Weapon = "Weapon";
            public const string Attack = "Attack";
            public const string Aiming = "Aiming";
            public const string Die = "Die";
            public const string SneakCycle = "SneakCycle";
        }

        [MenuItem("ShadowVale/Build Player Animator")]
        public static void Build()
        {
            Dictionary<string, string> resolved = CharacterClipLibrary.ResolveRoleToPath();

            // Locomotion clips go through the tuner; attack clips are used as authored.
            AnimationClip idle = LocomotionClipTuner.Tune(CharacterClipLibrary.LoadClip(resolved, "Idle"), "Idle");
            AnimationClip walk = LocomotionClipTuner.Tune(CharacterClipLibrary.LoadClip(resolved, "Walk"), "Walk");
            AnimationClip run = LocomotionClipTuner.Tune(CharacterClipLibrary.LoadClip(resolved, "Run"), "Run");

            if (idle == null || walk == null || run == null)
            {
                Debug.LogError("[PlayerAnimator] Missing Idle/Walk/Run clips — run " +
                               "'ShadowVale ▸ Setup Character Import' first.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath)!);

            // Rebuild in place: deleting would orphan any prefab that references the controller.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }
            ClearController(controller);

            controller.AddParameter(Params.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(Params.Grounded, AnimatorControllerParameterType.Bool);
            controller.AddParameter(Params.Jump, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(Params.Sneaking, AnimatorControllerParameterType.Bool);
            controller.AddParameter(Params.Weapon, AnimatorControllerParameterType.Int);
            controller.AddParameter(Params.Attack, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(Params.Aiming, AnimatorControllerParameterType.Bool);
            controller.AddParameter(Params.Die, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(Params.SneakCycle, AnimatorControllerParameterType.Float);

            BuildBaseLayer(controller, resolved, idle, walk, run);
            BuildUpperBodyLayer(controller, resolved);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);

            List<string> missing = CharacterClipLibrary.MissingRoles(resolved);
            Debug.Log($"[PlayerAnimator] Built {ControllerPath} from: {string.Join(", ", resolved.Keys)}");
            if (missing.Count > 0)
            {
                Debug.LogWarning($"[PlayerAnimator] No clip for: {string.Join(", ", missing)} — " +
                                 "those states were skipped.");
            }
        }

        // ---- Base layer: locomotion, sneak, jump, death ----------------------

        private static void BuildBaseLayer(AnimatorController controller,
            Dictionary<string, string> resolved, AnimationClip idle, AnimationClip walk, AnimationClip run)
        {
            AnimatorStateMachine root = controller.layers[0].stateMachine;

            var blendTree = new BlendTree
            {
                name = PlayerCombat.LocomotionState,
                blendType = BlendTreeType.Simple1D,
                blendParameter = Params.Speed,
                useAutomaticThresholds = false,
            };
            blendTree.AddChild(idle, 0f);
            blendTree.AddChild(walk, WalkThreshold);
            blendTree.AddChild(run, RunThreshold);
            // A BlendTree is a sub-asset of the controller, not a standalone file.
            AssetDatabase.AddObjectToAsset(blendTree, controller);

            AnimatorState locomotion = root.AddState(PlayerCombat.LocomotionState);
            locomotion.motion = blendTree;
            locomotion.writeDefaultValues = false;
            root.defaultState = locomotion;

            AnimationClip sneakClip = LocomotionClipTuner.Tune(
                CharacterClipLibrary.LoadClip(resolved, "Sneak"), "Sneak");
            if (sneakClip != null)
            {
                AnimatorState sneak = root.AddState("Sneak");
                sneak.motion = sneakClip;
                sneak.writeDefaultValues = false;
                // Playback speed follows movement, so a stationary crouch does not keep stepping.
                sneak.speedParameterActive = true;
                sneak.speedParameter = Params.SneakCycle;

                AnimatorStateTransition toSneak = locomotion.AddTransition(sneak);
                toSneak.hasExitTime = false;
                toSneak.duration = StanceBlend;
                toSneak.AddCondition(AnimatorConditionMode.If, 0f, Params.Sneaking);

                AnimatorStateTransition fromSneak = sneak.AddTransition(locomotion);
                fromSneak.hasExitTime = false;
                fromSneak.duration = StanceBlend;
                fromSneak.AddCondition(AnimatorConditionMode.IfNot, 0f, Params.Sneaking);
            }

            AnimationClip jumpClip = CharacterClipLibrary.LoadClip(resolved, "Jump");
            if (jumpClip != null)
            {
                AnimatorState jump = root.AddState("Jump");
                jump.motion = jumpClip;
                jump.writeDefaultValues = false;
                jump.speed = JumpPlaybackSpeed;

                AnimatorStateTransition toJump = locomotion.AddTransition(jump);
                toJump.hasExitTime = false;
                toJump.duration = 0.08f;
                toJump.AddCondition(AnimatorConditionMode.If, 0f, Params.Jump);

                AnimatorStateTransition fromJump = jump.AddTransition(locomotion);
                fromJump.hasExitTime = true;
                fromJump.exitTime = JumpExitTime;
                fromJump.duration = 0.1f;
            }

            AnimationClip dieClip = CharacterClipLibrary.LoadClip(resolved, "Die");
            if (dieClip != null)
            {
                AnimatorState die = root.AddState("Die");
                die.motion = dieClip;
                die.writeDefaultValues = false;
                die.speed = DiePlaybackSpeed;

                // From Any State: dying interrupts whatever is playing, and never loops back.
                AnimatorStateTransition toDie = root.AddAnyStateTransition(die);
                toDie.hasExitTime = false;
                toDie.duration = 0.1f;
                toDie.canTransitionToSelf = false;
                toDie.AddCondition(AnimatorConditionMode.If, 0f, Params.Die);
            }
        }

        // ---- Upper body layer: weapon stance and attacks ---------------------

        private static void BuildUpperBodyLayer(AnimatorController controller,
            Dictionary<string, string> resolved)
        {
            // Attacks keep their arm motion but have their torso bias removed, so firing does
            // not swing the upper body away from where the legs are pointing.
            AnimationClip punch = LocomotionClipTuner.Tune(CharacterClipLibrary.LoadClip(resolved, "Punch"), "Punch");
            AnimationClip stab = LocomotionClipTuner.Tune(CharacterClipLibrary.LoadClip(resolved, "Stab"), "Stab");
            AnimationClip shoot = LocomotionClipTuner.Tune(CharacterClipLibrary.LoadClip(resolved, "Shoot"), "Shoot");

            if (punch == null && stab == null && shoot == null)
            {
                Debug.LogWarning("[PlayerAnimator] No attack clips found — the upper-body layer " +
                                 "was skipped. Combat still works, it just has no animation.");
                return;
            }

            var stateMachine = new AnimatorStateMachine
            {
                name = UpperBodyLayer,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = UpperBodyLayer,
                stateMachine = stateMachine,
                // Starts muted. PlayerCombat raises it when a weapon is held; anything without
                // that script (the test bot) would otherwise have its arms frozen by this layer's
                // empty rest state, which writes nothing but still claims the masked bones.
                defaultWeight = 0f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = EnsureUpperBodyMask(),
            });

            // One resting state per weapon. Unarmed rests on an empty state so the base layer's
            // natural arm swing shows through; armed states freeze a frame of the attack take,
            // which is the closest thing to a "weapon held" pose the clip set offers.
            AnimatorState empty = stateMachine.AddState("Rest_Unarmed");
            empty.writeDefaultValues = false;
            stateMachine.defaultState = empty;

            AnimatorState holdKnife = AddHold(stateMachine, "Hold_Knife", stab, KnifeHoldFrame);
            AnimatorState holdRifle = AddHold(stateMachine, "Hold_Rifle", shoot, RifleHoldFrame);

            // Weapon values mirror WeaponKind: 0 unarmed, 1 knife, 2 rifle.
            var rest = new[] { empty, holdKnife ?? empty, holdRifle ?? empty };
            LinkStances(rest);

            AddAttack(stateMachine, rest, "Attack_Unarmed", punch, 0, UnarmedAttackSpeed, false);
            AddAttack(stateMachine, rest, "Attack_Knife", stab ?? punch, 1, KnifeAttackSpeed, false);
            // The rifle fires faster than its take runs, so a shot restarts the recoil mid-play.
            AddAttack(stateMachine, rest, "Attack_Rifle", shoot ?? punch, 2, RifleAttackSpeed, true);
        }

        /// <summary>A frozen frame of a clip, used as a weapon-in-hand idle.</summary>
        private static AnimatorState AddHold(AnimatorStateMachine machine, string name,
            AnimationClip clip, float frame)
        {
            if (clip == null)
            {
                return null;
            }

            AnimatorState state = machine.AddState(name);
            state.motion = clip;
            state.writeDefaultValues = false;
            state.speed = 0f;          // Hold, do not play.
            state.cycleOffset = frame; // Which frame to hold.
            return state;
        }

        /// <summary>
        /// Wires every resting state to every other one on the Weapon parameter, so pressing a
        /// number key changes the stance immediately instead of waiting for the next attack.
        /// </summary>
        private static void LinkStances(AnimatorState[] rest)
        {
            for (int from = 0; from < rest.Length; from++)
            {
                for (int to = 0; to < rest.Length; to++)
                {
                    if (from == to || rest[from] == rest[to])
                    {
                        continue;
                    }

                    AnimatorStateTransition t = rest[from].AddTransition(rest[to]);
                    t.hasExitTime = false;
                    t.duration = StanceBlend;
                    t.AddCondition(AnimatorConditionMode.Equals, to, Params.Weapon);
                }
            }
        }

        private static void AddAttack(AnimatorStateMachine machine, AnimatorState[] rest,
            string name, AnimationClip clip, int weaponValue, float speed, bool retriggerable)
        {
            if (clip == null)
            {
                return;
            }

            AnimatorState state = machine.AddState(name);
            state.motion = clip;
            state.writeDefaultValues = false;
            state.speed = speed;

            // From Any State: whatever stance is showing, the attack plays. One transition instead
            // of one per resting state, which also keeps it reachable mid-stance-change.
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = AttackEnterBlend;
            enter.canTransitionToSelf = retriggerable;
            enter.AddCondition(AnimatorConditionMode.If, 0f, Params.Attack);
            enter.AddCondition(AnimatorConditionMode.Equals, weaponValue, Params.Weapon);

            // Back to the stance for the weapon that is still held.
            AnimatorStateTransition back = state.AddTransition(rest[weaponValue]);
            back.hasExitTime = true;
            back.exitTime = AttackExitTime;
            back.duration = AttackExitBlend;

            // Swapping weapons mid-swing should not strand us in the wrong stance.
            for (int i = 0; i < rest.Length; i++)
            {
                if (i == weaponValue || rest[i] == rest[weaponValue])
                {
                    continue;
                }
                AnimatorStateTransition swap = state.AddTransition(rest[i]);
                swap.hasExitTime = false;
                swap.duration = StanceBlend;
                swap.AddCondition(AnimatorConditionMode.Equals, i, Params.Weapon);
            }
        }

        /// <summary>
        /// Upper-body-only mask, so an attack plays on the arms while the legs keep walking.
        /// </summary>
        private static AvatarMask EnsureUpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);

            EditorUtility.SetDirty(mask);
            return mask;
        }

        // ---- Teardown --------------------------------------------------------

        private static void ClearController(AnimatorController controller)
        {
            foreach (AnimatorControllerParameter p in controller.parameters)
            {
                controller.RemoveParameter(p);
            }

            // Drop every layer but the base one, back to front so indices stay valid.
            for (int i = controller.layers.Length - 1; i > 0; i--)
            {
                controller.RemoveLayer(i);
            }

            AnimatorStateMachine root = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in root.states)
            {
                root.RemoveState(child.state);
            }
            foreach (AnimatorStateTransition transition in root.anyStateTransitions)
            {
                root.RemoveAnyStateTransition(transition);
            }

            // Sub-assets from a previous build would otherwise pile up inside the file.
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (sub is BlendTree || (sub is AnimatorStateMachine machine && machine != root))
                {
                    Object.DestroyImmediate(sub, true);
                }
            }
        }
    }
}
