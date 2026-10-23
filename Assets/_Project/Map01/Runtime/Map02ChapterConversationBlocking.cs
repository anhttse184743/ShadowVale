using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        public const float ConversationDistance = 1.8f;
        Map02ChapterActor conversationPartner;
        Vector3 partnerPosition;
        Quaternion partnerRotation;

        void StageConversation(Map02ChapterTask task)
        {
            ReleaseConversation();
            // These scenes stage physical action rather than a face-to-face conversation.
            if (task.id == "N3_09" || task.id == "DAWN" || task.id == "N3_WOUND" || task.kind == "crawl") return;
            // Day 3 onwards is staged scene by scene (people walk into frame, the supper places, the ditch):
            // moving Nam beside the first speaker or pinning that speaker would undo it.
            if (task.phase >= 4) return;
            conversationPartner = lines.Where(l => !l.radio && l.speaker != "Nam")
                .Select(l => Actor(l.speaker)).FirstOrDefault(a => a != null && a.visual != null && a.visual.gameObject.activeInHierarchy);
            if (conversationPartner == null) return;
            partnerPosition = conversationPartner.transform.position;
            var outward = Vector3.ProjectOnPlane(Mission.Player.position - partnerPosition, Vector3.up);
            if (outward.sqrMagnitude < .001f) outward = conversationPartner.transform.forward;
            outward.Normalize();
            var chosen = partnerPosition + outward * ConversationDistance;
            var controller = Mission.Player.GetComponent<CharacterController>();
            float radius = controller != null ? controller.radius * Mathf.Max(Mission.Player.lossyScale.x, Mission.Player.lossyScale.z) : .3f;
            for (int i = 0; i < 24; i++)
            {
                int step = i == 0 ? 0 : (i + 1) / 2 * (i % 2 == 1 ? 1 : -1);
                var candidate = partnerPosition + Quaternion.AngleAxis(step * 15, Vector3.up) * outward * ConversationDistance;
                if (!NavMesh.SamplePosition(candidate, out var floor, .35f, NavMesh.AllAreas)) continue;
                // Preserve the exact horizontal spacing; use the NavMesh only for floor height.
                if (Vector3.ProjectOnPlane(floor.position - candidate, Vector3.up).sqrMagnitude > .0025f) continue;
                candidate.y = floor.position.y;
                bool blocked = Physics.OverlapCapsule(candidate + Vector3.up * (radius + .08f), candidate + Vector3.up * 1.45f, radius, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => !c.transform.IsChildOf(Mission.Player) && !c.transform.IsChildOf(conversationPartner.transform));
                if (blocked) continue;
                if (NavMesh.Raycast(partnerPosition, candidate, out _, NavMesh.AllAreas)) continue;
                chosen = candidate;
                break;
            }
            lockedPosition = chosen;
            var toward = Vector3.ProjectOnPlane(partnerPosition - chosen, Vector3.up).normalized;
            lockedRotation = Quaternion.LookRotation(toward);
            partnerRotation = Quaternion.LookRotation(-toward);
            conversationPartner.ConversationHeld = true;
            HoldConversation();
        }

        void HoldConversation()
        {
            if (conversationPartner == null) return;
            conversationPartner.transform.SetPositionAndRotation(partnerPosition, partnerRotation);
            conversationPartner.SetSitting(false);
            var animator = conversationPartner.Animator;
            if (animator != null && animator.runtimeAnimatorController != null && animator.parameters.Any(p => p.name == "Speed")) animator.SetFloat("Speed", 0);
        }

        void ReleaseConversation()
        {
            if (conversationPartner != null) conversationPartner.ConversationHeld = false;
            conversationPartner = null;
        }
    }
}
