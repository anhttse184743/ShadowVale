using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Fits the playable Hùng's rifle to his own palms after humanoid retargeting.</summary>
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCombat))]
    public sealed class HungPlayerGrip : MonoBehaviour
    {
        private PlayerCombat combat;
        private Animator actor;
        private Weapon rifle;
        private Map01NamActions actions;
        private Health health;
        private int actionLayer;
        private float raised, firedUntil;

        private void Awake()
        {
            combat = GetComponent<PlayerCombat>();
            health = GetComponent<Health>();
            actor = GetComponentsInChildren<Animator>(true).First(a => a.name == "Model");
            actionLayer = actor.GetLayerIndex(Map01NamActions.UpperLayer);
            combat.Attacked += OnAttack;
        }
        private void OnAttack(WeaponKind kind, Vector3 origin) { if (kind == WeaponKind.Rifle) firedUntil = Time.time + .35f; }
        private void LateUpdate()
        {
            // Full-body actions, reloads and cinematic playables own the hands themselves.
            if (!combat.enabled || combat.EquippedKind != WeaponKind.Rifle || combat.IsReloading || actor.runtimeAnimatorController == null) { raised = 0; return; }
            if (actions == null) actions = GetComponent<Map01NamActions>();
            if ((actions != null && actions.Busy) || (actionLayer > 0 && actor.GetLayerWeight(actionLayer) > .01f)) return;
            if (health != null && health.IsDead) return;
            if (rifle == null) rifle = GetComponentsInChildren<Weapon>(true).FirstOrDefault(w => w.Kind == WeaponKind.Rifle);
            if (rifle == null || !rifle.gameObject.activeInHierarchy) return;
            raised = Mathf.MoveTowards(raised, combat.IsAiming || Time.time < firedUntil ? 1 : 0, Time.deltaTime / .16f);
            var camera = Camera.main;
            var direction = camera != null && raised > .01f ? camera.transform.forward : transform.forward - Vector3.up * .18f;
            var shoulder = actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
            TutorialCapturePoses.Rifle(actor, rifle, shoulder + direction * 100, raised: raised);
        }
        private void OnDestroy() { if (combat != null) combat.Attacked -= OnAttack; }
    }
}
