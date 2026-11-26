using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Map 1 opens in the command bunker. Until Nam has taken his orders from the commander (rescue Hùng), he cannot leave
    /// it: stepping out from under the bunker's roof holds him at the last spot still under it, like an invisible wall in
    /// the doorway, with a reminder (26/11/2026). Once the order is accepted, or on any later stage, he goes freely. A
    /// save that starts him outside is never pulled in: the hold only begins once he has stood under the roof.
    /// </summary>
    public sealed class Map01BunkerHold : MonoBehaviour
    {
        private Map01Mission mission;
        private Map01Quest quest;
        private Vector3 lastInside;
        private bool inside;
        private float saidAt = -10f;

        /// <summary>True while Nam is being kept in the bunker (orders not yet taken and he has been inside).</summary>
        public bool Holding => inside && Waiting;
        private bool Waiting => quest != null && quest.Stage == Map01Quest.RescueStage && !quest.RescueOrderAccepted;

        public static void Attach(Map01Mission owner)
        {
            if (owner == null || owner.GetComponent<Map01BunkerHold>() != null) return;
            owner.gameObject.AddComponent<Map01BunkerHold>().mission = owner;
        }

        /// <summary>Under a roof: anything solid within a few metres straight above his head.</summary>
        private static bool Roofed(Vector3 feet) =>
            Physics.Raycast(feet + Vector3.up * 1.2f, Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore);

        private void LateUpdate()
        {
            if (mission == null || !mission.IsInitialized || mission.player == null) return;
            quest ??= mission.GetComponent<Map01Quest>();
            if (!Waiting) { inside = false; return; }
            if (mission.Cinematic) return;   // a cutscene places him where it needs him
            var feet = mission.player.position;
            if (Roofed(feet)) { inside = true; lastInside = feet; return; }
            if (!inside) return;
            var controller = mission.player.GetComponent<CharacterController>();
            if (controller != null && controller.enabled) controller.Move(lastInside - feet);
            else mission.player.position = lastInside;
            if (Time.time > saidAt + 3f) { saidAt = Time.time; mission.Say("Gặp chỉ huy trong hầm để nhận nhiệm vụ trước khi ra ngoài.", 3f); }
        }
    }
}
