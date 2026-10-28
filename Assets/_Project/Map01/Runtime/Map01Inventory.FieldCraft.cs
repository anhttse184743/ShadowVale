using System.Collections.Generic;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    public sealed partial class Map01Inventory
    {
        public const string RecoveredAmmo = "ammo_recovered", AkLoadingPack = "ak_loading_pack";
        public const string RecoverM16Recipe = "recover_ammo_556", AssembleAkRecipe = "assemble_ammo_ak";
        // The toolkit is permanent equipment, not an expendable bag item.
        public bool HasFieldToolkit => true;
        int fieldKitVersion = 1;
        sealed class FieldJob
        {
            public ForestRecipe recipe;
            public int times;
            public float elapsed, seconds;
            public Vector3 position;
            public WeaponKind weapon;
            public uint jump;
        }
        FieldJob fieldJob;
        Health fieldHealth;
        readonly Dictionary<Renderer, bool> fieldHiddenWeapons = new Dictionary<Renderer, bool>();
        public bool IsFieldCrafting => fieldJob != null;
        public string FieldCraftingId => fieldJob?.recipe.id;
        public int FieldCraftQuantity => fieldJob?.times ?? 1;
        public float FieldCraftRemaining => fieldJob == null ? 0 : Mathf.Max(0, fieldJob.seconds - fieldJob.elapsed);
        public float FieldCraftProgress => fieldJob == null ? 0 : Mathf.Clamp01(fieldJob.elapsed / fieldJob.seconds);

        public int FieldBatchLimit(ForestRecipe recipe) => Mathf.Max(1, recipe?.field_max_batch ?? 1);
        public string FieldCraftRefusal(string recipeId, int times = 1)
        {
            var recipe = Recipe(recipeId);
            if (recipe == null || recipe.field_seconds <= 0) return "Cần bàn chế tạo.";
            if (fieldJob != null) return "Đang chế tạo; hoàn tất hoặc hủy trước.";
            if (mission == null || !mission.IsInitialized || mission.Stopped || ForestMenu.Visible) return "Chưa thể chế tạo lúc này.";
            if (!mission.InventoryOpen) return "Mở Tab → Chế tạo dã chiến.";
            if (mission.MapTwo?.CharacterGearLocked == true) return "Hoàn tất việc đang mang trước khi chế tạo.";
            if (times < 1 || times > FieldBatchLimit(recipe)) return "Vượt số lượng một đợt dã chiến.";
            var controller = mission.player.GetComponent<PlayerController>();
            if (controller != null && (!controller.IsGrounded || controller.IsMoving)) return "Đứng yên trên mặt đất để chế tạo.";
            if (mission.ModernCombat != null && mission.ModernCombat.IsReloading) return "Hoàn tất nạp đạn trước khi chế tạo.";
            if (Map01NamActions.For(mission.MapOne)?.Busy == true) return "Hoàn tất thao tác hiện tại trước.";
            if (CanMake(recipe) < times) return "Thiếu vật liệu: " + Inputs(recipe, times) + ".";
            return null;
        }

        /// <summary>Inputs are held in escrow. Only reserve rounds are in the bag; magazines are never drawn from.</summary>
        public bool TryFieldCraft(string recipeId, int times = 1)
        {
            string why = FieldCraftRefusal(recipeId, times);
            if (why != null) { mission?.Say(why, 3); return false; }
            var recipe = Recipe(recipeId);
            var controller = mission.player.GetComponent<PlayerController>();
            GetComponent<Map01StoneThrow>()?.Cancel();
            Map01NamActions.For(mission.MapOne)?.CancelAction();
            ThrowableEquipped = false;
            fieldJob = new FieldJob { recipe = recipe, times = times, seconds = recipe.field_seconds,
                position = mission.player.position, weapon = mission.ModernCombat != null ? mission.ModernCombat.EquippedKind : WeaponKind.Knife,
                jump = controller != null ? controller.JumpSequence : 0 };
            fieldHealth = mission.ModernHealth;
            if (fieldHealth != null) fieldHealth.Damaged += FieldDamaged;
            foreach (var input in recipe.inputs) Spend(input.item_id, input.count * times);
            SuppressFire = true;
            mission.ModernCombat?.SetHandsOccupied(true);
            if (mission.MapTwo != null)
                foreach (var weapon in mission.player.GetComponentsInChildren<Weapon>(true))
                    foreach (var renderer in weapon.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!fieldHiddenWeapons.ContainsKey(renderer)) fieldHiddenWeapons.Add(renderer, renderer.forceRenderingOff);
                        renderer.forceRenderingOff = true;
                    }
            // The existing kneeling/handling action frees the hands; no new animator controller or rig changes.
            Map01NamActions.For(mission.MapOne)?.Play(Map01NamActions.OpenChest, recipe.field_seconds + .5f, false);
            return true;
        }

        void FieldDamaged(DamageNotice notice) => CancelFieldCraft();

        void ReleaseFieldHands()
        {
            if (fieldHealth != null) fieldHealth.Damaged -= FieldDamaged;
            fieldHealth = null;
            Map01NamActions.For(mission?.MapOne)?.CancelAction();
            mission?.ModernCombat?.SetHandsOccupied(ThrowableEquipped);
            foreach (var pair in fieldHiddenWeapons) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            fieldHiddenWeapons.Clear();
            SuppressFire = true;
        }

        /// <summary>Clear before receiving, so callbacks and repeated cancellation cannot refund twice.</summary>
        public void CancelFieldCraft(bool announce = true)
        {
            var held = fieldJob;
            if (held == null) return;
            fieldJob = null;
            ReleaseFieldHands();
            bool overflow = false;
            foreach (var input in held.recipe.inputs)
            {
                int before = Count(input.item_id), amount = input.count * held.times;
                Receive(input.item_id, amount);
                overflow |= Count(input.item_id) - before < amount;
            }
            if (announce) mission?.Say("Đã hủy chế tạo, hoàn lại vật liệu." + (overflow ? " Túi đầy: phần còn lại ở dưới chân." : ""), 4);
        }

        void TickFieldCraft()
        {
            if (fieldJob == null) return;
            // Pause freezes progress; a cinematic, death or closing the bag cancels the job.
            if (mission.MapOne?.Paused == true || mission.MapTwo?.Paused == true || Time.timeScale == 0) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            var controller = mission.player.GetComponent<PlayerController>();
            bool walking = (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed || kb.spaceKey.isPressed))
                || (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > .04f || pad.buttonSouth.isPressed));
            bool switching = kb != null && (kb.digit1Key.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame || kb.digit3Key.wasPressedThisFrame || kb.digit4Key.wasPressedThisFrame);
            if (!mission.InventoryOpen || mission.Stopped || ForestMenu.Visible || walking || switching ||
                (mission.player.position - fieldJob.position).sqrMagnitude > .04f ||
                (controller != null && (controller.JumpSequence != fieldJob.jump || !controller.IsGrounded)) ||
                (mission.ModernCombat != null && mission.ModernCombat.EquippedKind != fieldJob.weapon))
            { CancelFieldCraft(); return; }
            fieldJob.elapsed += Time.deltaTime;
            if (fieldJob.elapsed < fieldJob.seconds) return;
            var done = fieldJob;
            fieldJob = null;
            ReleaseFieldHands();
            int amount = done.recipe.output_count * done.times, before = Count(done.recipe.output_item_id);
            Receive(done.recipe.output_item_id, amount);
            bool overflow = Count(done.recipe.output_item_id) - before < amount;
            mission.Say("Chế tạo xong: " + amount + " " + ForestInventory.Name(done.recipe.output_item_id).ToLowerInvariant()
                + "." + (overflow ? " Túi đầy: phần còn lại ở dưới chân." : ""), 4);
        }

        // Restored after ground items, so an old full bag keeps any overflow as a pickup.
        public void RestoreFieldToolkit(int savedVersion)
        {
            fieldKitVersion = 1;
            if (savedVersion < 1) Receive(AkLoadingPack, 2);
        }

        void OnDisable() => CancelFieldCraft(false);
    }
}
