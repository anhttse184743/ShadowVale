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
        float frame, weight, smoothedSpeed;
        static readonly int BlendSpeed = Animator.StringToHash("T005fBlendSpeed");

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
            frame = data.Find("pistol_crouch").At(0,true);
        }

        bool Free => (combat.HandsOccupied || combat.EquippedKind == WeaponKind.Unarmed) && combat.EquippedKind != WeaponKind.Pistol;
        void Update()
        {
            if (!actor || !motor || !combat) return;
            var target = actor.GetFloat("Speed");
            smoothedSpeed = Free && motor.IsSneaking ? Mathf.MoveTowards(smoothedSpeed,target,Time.deltaTime*1.2f/.20f) : target;
            actor.SetFloat(BlendSpeed,smoothedSpeed);
        }

        void LateUpdate()
        {
            if (!actor || !motor || !combat || tracks == null) return;
            if (!motion) motion = motor.GetComponent<NamMotionRefresh>();
            float applied = motion ? motion.AppliedWeight : 0f;
            bool active = Free && motor.IsSneaking && motor.IsGrounded && motor.PlanarSpeed < .05f && applied < .01f && (!motion || !motion.JumpPlaying);
            weight = Mathf.MoveTowards(weight, active ? 1f : 0f, Time.deltaTime/.20f);
            if (!Free || applied >= .01f) weight = 0f;
            if (weight <= 0) return;
            for (int i=0; i<tracks.Length; i++) tracks[i].At(frame).Apply(targets[i],weight);
        }
    }
}
