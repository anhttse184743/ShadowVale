using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Routes keyboard input to whichever sibling component owns it, keeps Hùng following while
    /// the quest says he should, and dispatches world-point interactions (loot, workbench,
    /// documents, exit — the supplies drop-off defers to Map01Quest for the stage gate). One
    /// place for "what does this key do right now" instead of it being spread across a dozen
    /// conditions in a mission god-class.
    /// </summary>
    public sealed class Map01PlayerInteraction : MonoBehaviour
    {
        public ForestPoint Nearby { get; private set; }

        private Map01Mission mission;
        private Map01Quest quest;
        private Map01Inventory inventory;
        private Map01SaveSystem saveSystem;
        private Map01Scouting scouting;
        private NavMeshAgent companion;

        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            quest = GetComponent<Map01Quest>();
            inventory = GetComponent<Map01Inventory>();
            saveSystem = GetComponent<Map01SaveSystem>();
        }

        private void Start()
        {
            if (mission.IsInitialized) companion = mission.hung.GetComponent<NavMeshAgent>();
            scouting = GetComponent<Map01Scouting>(); // Added by Map01Quest.Awake, so only certain from Start.
        }

        private void Update()
        {
            if (!mission.IsInitialized) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            scouting.HoldBinoculars(kb.fKey.isPressed); // Map01Scouting decides when they actually work.
            if (ForestMenu.Visible) return;
            if (kb.f9Key.wasPressedThisFrame) saveSystem.Load();
            if (mission.Stopped) return;
            if (kb.tabKey.wasPressedThisFrame)
            { mission.SetInventoryOpen(!mission.InventoryOpen); inventory.SuppressFire = true; }
            if (kb.mKey.wasPressedThisFrame)
            {
                mission.SetMapOpen(!mission.MapOpen); inventory.SuppressFire = true;
            }
            if (kb.cKey.wasPressedThisFrame) mission.Crouched = !mission.Crouched;
            if (kb.f5Key.wasPressedThisFrame) saveSystem.SaveSlot(0, out _);
            if (kb.hKey.wasPressedThisFrame) inventory.UseItem("medkit_small");
            if (mission.InventoryOpen || mission.MapOpen)
            {
                // Same gate as below: this used to call UpdateCompanion unconditionally, so opening
                // the bag or map pulled Hùng to Nam at any stage — wounded, or at his post at base.
                if (quest.ShouldFollowPlayer()) UpdateCompanion();
                return;
            }

            if (mission.ModernPlayer != null)
            {
                mission.ModernPlayer.SurfaceSpeedMultiplier = mission.MovementSurfaceMultiplier;
                mission.Crouched = mission.ModernPlayer.IsSneaking;
                mission.UpdateHiddenState();
                mission.SetStamina(mission.Stamina + (mission.ModernPlayer.IsSprinting ? -mission.Settings.staminaDrain : mission.Settings.staminaRecovery) * Time.deltaTime);
                if (mission.ModernPlayer.IsSprinting) mission.NotifySprintNoise();
            }
            if (kb.qKey.wasPressedThisFrame) ThrowStone();
            Nearby = mission.Points.Where(p => !p.used && p.kind != ForestPointKind.Hide && p.kind != ForestPointKind.Cover)
                .OrderBy(p => Vector3.Distance(mission.player.position, p.transform.position))
                .FirstOrDefault(p => Vector3.Distance(mission.player.position, p.transform.position) < mission.Settings.interactRange);
            if (kb.eKey.wasPressedThisFrame) HandleInteractKey();
            if (kb.bKey.wasPressedThisFrame) inventory.TryCraft();
            if (quest.ShouldFollowPlayer()) UpdateCompanion();
        }

        private void UpdateCompanion()
        {
            if (companion == null || !companion.isOnNavMesh) return;
            companion.speed = mission.Settings.sprintSpeed;
            companion.stoppingDistance = mission.Settings.followDistance;
            if (NavMesh.SamplePosition(mission.player.position, out var hit, 3, NavMesh.AllAreas))
                companion.SetDestination(hit.position);
        }

        private Vector3 AimPoint()
        {
            var ray = mission.gameCamera.ViewportPointToRay(new Vector3(.5f, .5f));
            return Physics.Raycast(ray, out var hit, mission.Weapon.range, mission.ObstructionMask | LayerMask.GetMask("Enemy"), QueryTriggerInteraction.Ignore)
                ? hit.point : ray.GetPoint(mission.Weapon.range);
        }

        private void ThrowStone()
        {
            var target = mission.player.position + Vector3.ClampMagnitude(AimPoint() - mission.player.position, mission.Settings.stoneRange);
            if (inventory.UseItem("stone", target))
                mission.Trace(mission.player.position + Vector3.up, target + Vector3.up * .2f, Color.yellow);
        }

        /// <summary>
        /// [E]: talking to Hùng (treating him, or reporting in) takes priority while he is in range
        /// and has something to say; otherwise it's the usual point interaction. HungInRange is only
        /// true at those moments, so the loot crate with the rescue's herb is never blocked.
        /// </summary>
        private void HandleInteractKey()
        {
            if (quest.HungInRange) quest.TalkToHung();
            else if (Nearby != null) Interact(Nearby);
        }

        /// <summary>Grants the point's items and starts its restock timer; returns what was taken.</summary>
        private string TakeItems(ForestPoint point)
        {
            foreach (var item in point.items) inventory.Add(item.item_id, item.count);
            point.MarkLooted();
            string taken = "Nhận " + string.Join(", ", point.items.Select(i => i.count + " " + ForestInventory.Name(i.item_id).ToLowerInvariant())) + ".";
            return point.restockSeconds > 0 ? taken + $" Tiếp tế lại sau {point.restockSeconds:0} giây." : taken;
        }

        public void Interact(ForestPoint point)
        {
            switch (point.kind)
            {
                case ForestPointKind.Supplies:
                    if (point.used) return;
                    if (quest.TryDeliverSupplies())
                    {
                        // Hùng's hand-off line stays on screen; the first resupply comes with it.
                        inventory.Add("supplies", 1);
                        TakeItems(point);
                    }
                    // Once Hùng is home, the base keeps resupplying on its restock timer.
                    else if (quest.BaseResupplyOpen) mission.Say("Căn cứ tiếp tế — " + TakeItems(point), 6);
                    break;
                case ForestPointKind.Loot:
                    if (point.used) return;
                    mission.Say(TakeItems(point) + " Tab mở túi đồ.", 6);
                    break;
                case ForestPointKind.Workbench:
                    mission.Say("Bàn chế tạo: B để làm băng cứu thương (2 vải + 1 thảo dược). Game tự lưu khi về menu hoặc thoát.");
                    break;
                case ForestPointKind.Documents:
                    // Side content: useful lore, not on the critical path the briefing laid out.
                    if (point.used) return;
                    point.used = true; inventory.Add("river_documents", 1);
                    mission.Say("Nam: Tài liệu cũ của đơn vị tuần tra — cất đi, có thể còn hữu ích.", 8);
                    break;
                case ForestPointKind.Exit:
                    mission.Say("Bến sông vắng lặng. Đường rút lui vẫn còn đó, nhưng nhiệm vụ chưa xong.", 6);
                    break;
            }
        }
    }
}
