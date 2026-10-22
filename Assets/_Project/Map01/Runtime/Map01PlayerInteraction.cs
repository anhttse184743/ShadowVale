using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Routes keyboard input to whichever sibling component owns it, guides Hùng home during
    /// the escort, and dispatches world-point interactions (loot, workbench,
    /// documents and the base store). One
    /// place for "what does this key do right now" instead of it being spread across a dozen
    /// conditions in a mission god-class.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class Map01PlayerInteraction : MonoBehaviour
    {
        public ForestPoint Nearby { get; private set; }

        private Map01Mission mission;
        private Map01Quest quest;
        private Map01Inventory inventory;
        private Map01SaveSystem saveSystem;
        private IBinoculars scouting;
        private Map01StoneThrow stoneThrow;
        private Map01StoneSites stoneSites;
        private Map01CampChests chests;
        private Map01Hud hud;
        private NavMeshAgent companion;

        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            quest = GetComponent<Map01Quest>();
            inventory = GetComponent<Map01Inventory>();
            saveSystem = GetComponent<Map01SaveSystem>();
            hud = GetComponent<Map01Hud>();
            // Added at runtime rather than baked into Map 1.unity, like the scouting order.
            // Deliberately not "GetComponent() ?? AddComponent()" — see WeaponHotbar.
            stoneThrow = GetComponent<Map01StoneThrow>();
            if (stoneThrow == null) stoneThrow = gameObject.AddComponent<Map01StoneThrow>();
        }

        private void Start()
        {
            if (mission.IsInitialized && mission.hung != null) companion = mission.hung.GetComponent<NavMeshAgent>();
            scouting = GetComponents<MonoBehaviour>().OfType<IBinoculars>().FirstOrDefault(); // Added by Map01Quest.Awake, so only certain from Start.
            stoneSites = GetComponent<Map01StoneSites>(); // Added by Map01Mission.Awake; none on a map without stone data.
            chests = GetComponent<Map01CampChests>();     // Likewise.
        }

        private void Update()
        {
            if (!mission.IsInitialized || mission.Cinematic) return;
            // Hùng's journey is world simulation; it must not depend on a keyboard being available.
            if(!mission.Stopped && quest != null && quest.ShouldEscortHungHome()) UpdateCompanion();
            var kb = Keyboard.current;
            if (kb == null) return;
            scouting?.HoldBinoculars(kb.fKey.isPressed); // Map01Scouting decides when they actually work.
            if (ForestMenu.Visible) return;
            if (kb.backquoteKey.wasPressedThisFrame && hud != null) hud.ToggleObjective();
            if (kb.f9Key.wasPressedThisFrame) saveSystem?.Load();
            if (mission.Stopped) return;
            if (kb.tabKey.wasPressedThisFrame)
            {
                // From the bench screen, Tab goes over to the bag; otherwise it opens or shuts the bag.
                mission.SetInventoryOpen(mission.BenchOpen || !mission.InventoryOpen); inventory.SuppressFire = true;
            }
            if (kb.mKey.wasPressedThisFrame)
            {
                mission.SetMapOpen(!mission.MapOpen); inventory.SuppressFire = true;
            }
            if (kb.cKey.wasPressedThisFrame) mission.Crouched = !mission.Crouched;
            if (kb.f5Key.wasPressedThisFrame) saveSystem?.SaveSlot(0, out _);
            if (kb.hKey.wasPressedThisFrame) inventory.UseItem("medkit_small");
            if (mission.InventoryOpen || mission.MapOpen)
            {
                // [E] shuts the bench screen it opened.
                if (mission.BenchOpen && kb.eKey.wasPressedThisFrame) mission.CloseGameplayPanel();
                // Same gate as below: this used to call UpdateCompanion unconditionally, so opening
                // the bag or map pulled Hùng to Nam at any stage — wounded, or at his post at base.
                return;
            }

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
                // Sprint is a toggle: only running on it costs breath, not standing still with it on.
                bool running = mission.ModernPlayer.IsSprinting && mission.ModernPlayer.IsMoving;
                mission.SetStamina(mission.Stamina + (running ? -mission.Settings.staminaDrain : mission.Settings.staminaRecovery) * Time.deltaTime);
            }
            // Slot 4 owns the mouse before PlayerCombat reads it later in the frame.
            if (ShadowVale.Core.Services.InputFocus.OverlayOpen) { stoneThrow.Cancel(); return; }
            if (kb.digit4Key.wasPressedThisFrame) inventory.OnSlotKey(3);
            else if (inventory.ThrowableEquipped)
            {
                if (kb.digit1Key.wasPressedThisFrame) inventory.OnSlotKey(0);
                else if (kb.digit2Key.wasPressedThisFrame) inventory.OnSlotKey(1);
                else if (kb.digit3Key.wasPressedThisFrame) inventory.OnSlotKey(2);
            }
            var mouse = Mouse.current;
            if (inventory.ThrowableEquipped && !inventory.SuppressFire && mouse != null)
            {
                if (mouse.rightButton.wasPressedThisFrame) stoneThrow.Cancel();
                else if (mouse.leftButton.wasPressedThisFrame && (Map01NamActions.For(mission)?.Busy != true)) stoneThrow.BeginAim();
                else if (stoneThrow.Aiming && mouse.leftButton.wasReleasedThisFrame) stoneThrow.Release();
            }
            Nearby = NearestUsable();
            // A stone site's offer depends on the room left in the bag, so its prompt is worded as he stands there.
            if (Nearby != null && Nearby.kind == ForestPointKind.Stones && stoneSites != null) Nearby.label = stoneSites.Prompt(Nearby);
            if (kb.eKey.wasPressedThisFrame) HandleInteractKey();
            if (kb.bKey.wasPressedThisFrame) inventory.TryCraft();
        }

        /// <summary>The closest unused point within reach, or null. Runs every frame, so a plain loop
        /// over the few usable points rather than LINQ over every bush.</summary>
        private ForestPoint NearestUsable()
        {
            ForestPoint nearest = null;
            float best = mission.Settings.interactRange;
            Vector3 at = mission.player.position;
            foreach (var point in mission.Interactables)
            {
                if (point.used || !point.isActiveAndEnabled) continue;   // a hidden point (the C3 Mosin cache before its order)
                float distance = Vector3.Distance(at, point.transform.position);
                if (distance < best) { best = distance; nearest = point; }
            }
            return nearest;
        }

        private void UpdateCompanion()
        {
            if (companion == null || !companion.isOnNavMesh) return;
            // Hùng heads for the shelter himself; Nam can flank or stay behind to cover him.
            GetComponent<Map01Rescue>()?.GuideHungHome(companion);
        }

        /// <summary>
        /// [E]: talking to Hùng (treating him, or reporting in) takes priority while he is in range
        /// and has something to say; otherwise it's the usual point interaction. HungInRange is only
        /// true at those moments, so the loot crate with the rescue's herb is never blocked.
        /// </summary>
        private void HandleInteractKey()
        {
            var story=GetComponent<Map01Story>();
            // The displayed report prompt owns E, even beside an outpost/base workbench.
            // Otherwise a cleared point 3 can open crafting instead of the required retreat call.
            if (story != null && story.RadioReport && story.TryInteract()) return;
            if (GetComponent<Map01C3BedTakedown>()?.TryBeginNearest() == true) return;
            if (Nearby != null && Nearby.kind == ForestPointKind.Workbench) { Interact(Nearby); return; }
            if (story?.TryInteract() == true) return;
            if (quest != null && quest.HungInRange) quest.TalkToHung();
            else if (Nearby != null) Interact(Nearby);
        }

        /// <summary>
        /// Grants the point's items and starts its restock timer; returns only capacity warnings. Stones go in only
        /// while his pockets have room (Map01Inventory.StoneCapacity): any that do not fit stay at the point,
        /// which then stays open with just those. <paramref name="any"/> is false when nothing could be taken.
        /// </summary>
        private string TakeItems(ForestPoint point, out bool any)
        {
            any = false;
            int leftStones = 0;
            foreach (var item in point.items)
            {
                int count = item.item_id == "stone" ? Mathf.Min(item.count, inventory.StoneRoom) : item.count;
                if (item.item_id == "stone") leftStones += item.count - count;
                if (count <= 0) continue;
                inventory.Receive(item.item_id, count, item.condition);
                any = true;
            }
            if (leftStones > 0)
            {
                // Everything else is in the bag now: only the stones that did not fit are left here.
                foreach (var item in point.items) item.count = item.item_id == "stone" ? Mathf.Min(item.count, leftStones) : 0;
                if (point.id != null && point.id.StartsWith(Map01RescueStones.IdPrefix)) point.label = $"Đá ném — {leftStones} viên";
                string full = $"Túi đá đã đầy ({inventory.Stones}/{inventory.StoneCapacity}), còn {leftStones} viên ở đây.";
                return full;
            }
            point.MarkLooted();
            return null;
        }
        /// <summary>Nam crouches to the pack or opens the crate (Blender clips); walking off cuts it short.</summary>
        private void Rummage(int action) =>
            Map01NamActions.For(mission)?.Play(action, action == Map01NamActions.PickUp ? 288f / 30f / 2.4f : 269f / 30f / 2.6f, true);

        public void Interact(ForestPoint point)
        {
            switch (point.kind)
            {
                case ForestPointKind.Supplies:
                    // The base store (Map01Logistics): opens once Hùng is home; what it issues is paid in logistics points.
                    point.used = false;
                    if (quest != null && quest.BaseResupplyOpen) { hud?.OpenStore(); inventory.SuppressFire = true; }
                    else mission.Say("Kho căn cứ A chưa cấp phát — đưa Hùng về căn cứ an toàn trước.", 4);
                    break;
                case ForestPointKind.Loot:
                    if (point.used) return;
                    if (point.used) return;
                    string took = TakeItems(point, out bool any);
                    if (!string.IsNullOrEmpty(took)) mission.Say(took, 6);
                    if (!any) break; // A full bag at a pile of stones: no stooping for nothing.
                    // a fallen guard's pack or gun is picked up off the ground; a crate is opened
                    Rummage(point.GetComponent<Map01EnemyController>() != null || (point.id!=null&&(point.id.StartsWith(Map01RescueStones.IdPrefix)||point.id.StartsWith(Map01Drops.IdPrefix)||point.id.StartsWith(Map01Logistics.GunPointPrefix)||point.id.StartsWith(Map01FieldSupplies.IdPrefix))) ? Map01NamActions.PickUp : Map01NamActions.OpenChest);
                    break;
                case ForestPointKind.Workbench:
                    // [E] opens the bench screen (Map01Hud.Bench) and shuts it again.
                    mission.SetBenchOpen(!mission.BenchOpen); inventory.SuppressFire = true;
                    break;
                case ForestPointKind.Documents:
                    // Side content: useful lore, not on the critical path the briefing laid out.
                    if (point.used) return;
                    point.used = true; inventory.Receive("river_documents", 1);
                    Rummage(Map01NamActions.PickUp);
                    mission.Say("Nam: Tài liệu cũ của đơn vị tuần tra — cất đi, có thể còn hữu ích.", 8);
                    break;
                case ForestPointKind.Stones:
                    // Straight into the pocket, crouched or standing: no rummaging take to sit through
                    // a few metres from a guard.
                    if (stoneSites == null) stoneSites = GetComponent<Map01StoneSites>();
                    stoneSites?.PickUp(point);
                    break;
                case ForestPointKind.Chest:
                    if (chests == null) chests = GetComponent<Map01CampChests>();
                    chests?.Use(point);
                    break;
                case ForestPointKind.Sabotage:
                    // C3: the kẻng's striker, the field telephone line.
                    GetComponent<Map01C3Mission>()?.Use(point);
                    break;
            }
        }
    }
}
