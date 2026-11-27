using System;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Map01
{
    // Runtime equivalent of the body/speed part of the approved V21 preview.
    [DefaultExecutionOrder(1200)]
    public sealed class T005fApprovedPose : MonoBehaviour
    {
        PlayerController motor;
        PlayerCombat combat;
        Animator actor;
        NamMotionRefresh motion;
        Map01PistolAnimationData.Track[] tracks;
        Transform[] targets;
        float frame, weight, smoothedSpeed, gait;
        NamMotionData.Clip pistolCrouch;
        static readonly int BlendSpeed = Animator.StringToHash("T005fBlendSpeed");

        public static AnimationClip CrouchIdle(AnimationClip[] clips) =>
            clips.FirstOrDefault(clip=>clip.name=="Nam_Crouch_Idle_V21") ?? clips.FirstOrDefault(clip=>clip.name=="Nam_Crouch_Idle");

        public static AnimationClip CrouchWalk(AnimationClip[] clips) =>
            clips.FirstOrDefault(clip=>clip.name=="Nam_Crouch_Walk_New") ?? clips.FirstOrDefault(clip=>clip.name=="Nam_Crouch_Walk_F");

        static bool IsArm(string n) => new[] { "Left", "Right" }.Any(s => n.StartsWith(s+"Shoulder") || n.StartsWith(s+"Arm") || n.StartsWith(s+"ForeArm") || n.StartsWith(s+"Hand"));

        void Awake()
        {
            // The tutorial uses Hùng's rig; V21 only applies to Nam.
            if (gameObject.scene.name == TutorialDirector.SceneName)
            {
                enabled = false;
                return;
            }
            actor = GetComponent<Animator>();
            motor = GetComponentInParent<PlayerController>();
            combat = GetComponentInParent<PlayerCombat>();
            var data = Resources.Load<NamMotionData>("Characters/NamMotionV3");
            if (!actor || !motor || !combat || !data || data.Find("pistol_crouch") == null) { enabled=false; Debug.LogError("T005f: missing actor, motor, combat or NamMotionV3", this); return; }
            var map = actor.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            tracks = data.bones.Where(t=>!IsArm(t.name)).ToArray();
            if (tracks.Any(t=>!map.ContainsKey(t.name))) { enabled=false; Debug.LogError("T005f: incompatible skeleton",this); return; }
            targets = tracks.Select(t=>map[t.name]).ToArray();
            pistolCrouch = data.Find("pistol_crouch");
            frame = pistolCrouch.At(0,true);
        }

        // 27/11: Map 2 has no NamMotionRefresh, so a crouch with the K-54 fell back on the plain crouch clip, which sits barely
        // lower than standing (hips 77-80 cm against 95). There the K-54 crouch takes the approved pistol_crouch body and legs,
        // still and walking, on the same step clock as Map 1; the arms stay with Map01Pistol (carry, aim, fire, reload).
        // Crouched still, the loop's first frame is mid-step (one sole 5.5 cm up): hold the frame of the loop where both soles
        // are lowest instead (measured once on this rig, before this frame's pose is written).
        float stillFrame = -1;
        float StillFrame()
        {
            if (stillFrame >= 0) return stillFrame;
            var feet = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightFoot, HumanBodyBones.RightToes }.Select(b => actor.GetBoneTransform(b)).ToArray();
            if (feet.Any(f => f == null)) return stillFrame = pistolCrouch.start;
            var saved = targets.Select(t => (t.localPosition, t.localRotation)).ToArray();
            float best = float.MaxValue; stillFrame = pistolCrouch.start;
            for (int f = pistolCrouch.start; f <= pistolCrouch.end; f++)
            {
                for (int i = 0; i < tracks.Length; i++) tracks[i].At(f).Apply(targets[i], 1);
                float baseY = actor.transform.position.y;
                float left = Mathf.Min(feet[0].position.y, feet[1].position.y) - baseY, right = Mathf.Min(feet[2].position.y, feet[3].position.y) - baseY;
                float worst = Mathf.Max(left, right) + .3f * Mathf.Abs(left - right);
                if (worst < best) { best = worst; stillFrame = f; }
            }
            for (int i = 0; i < targets.Length; i++) targets[i].SetLocalPositionAndRotation(saved[i].localPosition, saved[i].localRotation);
            return stillFrame;
        }
        bool PistolWithoutRefresh => combat.EquippedKind == WeaponKind.Pistol && !motion && !combat.HandsOccupied;

        bool Free => (combat.HandsOccupied || combat.EquippedKind == WeaponKind.Unarmed) && combat.EquippedKind != WeaponKind.Pistol;
        void Update()
        {
            if (!actor || !motor || !combat) return;
            if (actor.runtimeAnimatorController == null) { smoothedSpeed = 0f; return; }
            var target = actor.GetFloat("Speed");
            smoothedSpeed = Free && motor.IsSneaking ? Mathf.MoveTowards(smoothedSpeed,target,Time.deltaTime*1.2f/.20f) : target;
            actor.SetFloat(BlendSpeed,smoothedSpeed);
        }

        void LateUpdate()
        {
            if (!actor || !motor || !combat || tracks == null) return;
            if (actor.runtimeAnimatorController == null) { weight = 0f; return; }
            if (!motion) motion = motor.GetComponent<NamMotionRefresh>();
            float applied = motion ? motion.AppliedWeight : 0f;
            bool pistol = PistolWithoutRefresh;
            bool active = pistol ? motor.IsSneaking && motor.IsGrounded && !motor.IsProne
                : Free && motor.IsSneaking && motor.IsGrounded && motor.PlanarSpeed < .05f && applied < .01f && (!motion || !motion.JumpPlaying);
            weight = Mathf.MoveTowards(weight, active ? 1f : 0f, Time.deltaTime/.20f);
            if (!(Free || pistol) || applied >= .01f) weight = 0f;
            if (weight <= 0) { gait = 0f; return; }
            float at = frame;
            if (pistol)
            {
                // The step clock of NamMotionRefresh: a crouched stride covers 1.2 m per loop; standing still holds the first frame.
                gait += Time.deltaTime * Mathf.Max(0, motor.PlanarSpeed / 1.2f);
                at = motor.PlanarSpeed > .05f ? pistolCrouch.At(gait, true) : StillFrame();
            }
            for (int i=0; i<tracks.Length; i++) tracks[i].At(at).Apply(targets[i],weight);
        }
    }
}
