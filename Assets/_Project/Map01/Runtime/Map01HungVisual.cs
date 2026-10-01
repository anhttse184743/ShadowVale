using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>Visual-only replacement. Existing rescue, navigation and save identity stay on Hung.</summary>
    public sealed class Map01HungVisual : MonoBehaviour
    {
        public Animator actor;
        private Map01Mission mission;
        private Map01Rescue rescue;
        private NavMeshAgent agent;
        private Renderer[] placeholder;
        private bool down;
        private float talkUntil;
        public void Speak(float seconds = 6) { if (!down) talkUntil = Time.time + seconds; }
        private void ResetPose()
        {
            talkUntil = 0;
            actor.SetBool("Dead", false); actor.SetBool("Talking", false); actor.SetFloat("Speed", 0);
        }

        public static void Attach(Map01Mission owner)
        {
            if (owner.hung == null || owner.hung.GetComponentInChildren<Map01HungVisual>() != null) return;
            var prefab = Resources.Load<Map01HungVisual>("Characters/HungVisual");
            if (prefab == null) return;
            var old = owner.hung.GetComponentsInChildren<Renderer>();
            var visual = Instantiate(prefab, owner.hung, false);
            if (visual.actor == null || !visual.actor.isHuman || !visual.actor.avatar.isValid) {
                Debug.LogError("Hung requires a valid Humanoid avatar.", visual);
                Destroy(visual.gameObject); return;
            }
            // Match standing head height at scene initialization, excluding hats and weapon bounds.
            // Scale only the visual around its grounded origin; navigation/save transforms stay intact.
            var playerActor = owner.player.GetComponentInChildren<Animator>();
            float targetSize = SkeletonSize(playerActor, owner.player), sourceSize = SkeletonSize(visual.actor, owner.hung);
            if (targetSize > .1f && sourceSize > .1f)
                visual.transform.localScale *= targetSize / sourceSize;
            visual.mission = owner;
            visual.rescue = owner.GetComponent<Map01Rescue>();
            visual.agent = owner.hung.GetComponent<NavMeshAgent>();
            visual.placeholder = old;
            foreach (var renderer in old) if (renderer != null && renderer.enabled) renderer.forceRenderingOff = true;
            visual.actor.applyRootMotion = false;
            visual.ResetPose();
        }

        private static float SkeletonSize(Animator animator, Transform groundedRoot)
        {
            if (animator == null || !animator.isHuman) return 0;
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            return head != null ? head.position.y - groundedRoot.position.y : 0;
        }

        private void Update()
        {
            if (mission == null || actor == null) return;
            bool fallen = rescue != null && rescue.HungDown;
            if (fallen != down) {
                down = fallen;
                if (down) { talkUntil = 0; actor.SetBool("Dead", true); }
                else {
                    actor.Rebind(); actor.Update(0);
                    ResetPose();
                }
            }
            float speed = !down && agent != null && agent.isOnNavMesh && !agent.isStopped
                ? agent.velocity.magnitude : 0;
            actor.SetFloat("Speed", speed, .12f, Time.deltaTime);
            if (speed > .15f) talkUntil = 0;
            actor.SetBool("Talking", !down && Time.time < talkUntil);
        }

        private void OnDestroy()
        {
            if (placeholder == null) return;
            foreach (var renderer in placeholder) if (renderer != null) renderer.forceRenderingOff = false;
        }
    }
}
