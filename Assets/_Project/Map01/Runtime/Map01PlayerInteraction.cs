using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Routes keyboard input to whichever sibling component owns it, keeps Hùng following once
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
        private Map01Hud hud;
        private Map01SaveSystem saveSystem;
        private NavMeshAgent companion;

        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            quest = GetComponent<Map01Quest>();
            inventory = GetComponent<Map01Inventory>();
            hud = GetComponent<Map01Hud>();
            saveSystem = GetComponent<Map01SaveSystem>();
        }

        private void Start()
        {
            if (mission.IsInitialized) companion = mission.hung.GetComponent<NavMeshAgent>();
        }

        private void Update()
        {
            if (!mission.IsInitialized) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (ForestMenu.Visible) return;
            if (kb.f9Key.wasPressedThisFrame) saveSystem.Load();
            if (mission.Stopped) return;
            if (kb.tabKey.wasPressedThisFrame)
            { mission.SetInventoryOpen(!mission.InventoryOpen); hud.CancelDrag(); inventory.SuppressFire = true; }
            if (kb.mKey.wasPressedThisFrame)
            {
                bool open = !mission.MapOpen;
                mission.SetMapOpen(open); hud.CancelDrag(); inventory.SuppressFire = true;
                if (open) quest.OnMapOpened();
            }
            if (kb.cKey.wasPressedThisFrame) mission.Crouched = !mission.Crouched;
            if (kb.f5Key.wasPressedThisFrame) saveSystem.SaveSlot(0, out _);
            if (kb.hKey.wasPressedThisFrame) inventory.UseItem("medkit_small");
            inventory.HandleQuickKeys(kb);
            if (mission.InventoryOpen || mission.MapOpen) { UpdateCompanion(); return; }

            // The panel is shut again, so hand the weapon back — but not until the button that
            // shut it is released, which is the whole reason the flag exists: a click on the
            // HUD must not carry through into a shot. Nothing cleared it before, so the first
            // Tab or M of a run disabled firing, aiming and the 6/7/8 weapon keys for good.
            if (inventory.SuppressFire && (Mouse.current == null || !Mouse.current.leftButton.isPressed))
            {
                inventory.SuppressFire = false;
            }

            if (mission.ModernPlayer != null)
            {
                mission.ModernPlayer.SurfaceSpeedMultiplier = mission.MovementSurfaceMultiplier;
                mission.Crouched = mission.ModernPlayer.IsSneaking;
                mission.UpdateHiddenState();
                mission.SetStamina(mission.Stamina + (mission.ModernPlayer.IsSprinting ? -mission.Settings.staminaDrain : mission.Settings.staminaRecovery) * Time.deltaTime);
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
        /// [E]: treating Hùng takes priority when he's the one in range, otherwise it's the usual
        /// point interaction. Rescuing him must never require walking away from every loot crate
        /// first — the herb he needs at stage 0 comes from one.
        /// </summary>
        private void HandleInteractKey()
        {
            if (quest.HungInRange) quest.TryRescueHung();
            else if (Nearby != null) Interact(Nearby);
        }

        public void Interact(ForestPoint point)
        {
            switch (point.kind)
            {
                case ForestPointKind.Supplies:
                    if (!quest.TryDeliverSupplies()) return;
                    point.used = true; inventory.Add("supplies", 1);
                    break;
                case ForestPointKind.Loot:
                    if (point.used) return;
                    foreach (var item in point.items) inventory.Add(item.item_id, item.count);
                    point.used = true; mission.Say("Đã nhặt vật tư. Tab mở túi đồ. Bàn chế tạo nằm ở điểm tiếp tế.");
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
