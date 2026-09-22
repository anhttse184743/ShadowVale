using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    public sealed partial class ForestMission : MonoBehaviour
    {
        public Transform player, hung;
        public Camera gameCamera;
        public Transform encounterExit;
        public Vector2 mapMin = new Vector2(-45, -60), mapMax = new Vector2(45, 88);
        public TextAsset balanceJson, contentBundle;
        public ForestSettings Settings { get; private set; }
        public ForestWeapon Weapon { get; private set; }
        public ForestArchetype GuardData { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool Alarmed { get; set; }
        public bool Hidden { get; private set; }
        public bool Paused => paused;
        public float PlaySeconds { get; private set; }
        public void SetPaused(bool value) { paused = value; Time.timeScale = value ? 0 : 1; }
        public bool Stopped => (modernHealth != null ? modernHealth.IsDead : hp <= 0) || stage == CompleteStage || paused || pendingCheckpoint != null;
        public int ObstructionMask => LayerMask.GetMask("Default", "Obstacle", "Cover", "VisionBlocker");
        public int Stage => stage;
        private ForestBundle bundle;
        private CharacterController controller;
        private NavMeshAgent companion;
        private ForestGuard[] guards;
        private readonly List<ForestPoint> points = new List<ForestPoint>();
        private readonly Dictionary<string, int> inventory = new Dictionary<string, int>();
        private float hp, stamina, nextShot, nextNoise, dialogueUntil, craftUntil;
        private float verticalVelocity;
        public bool IsWading => player != null && player.position.y < .12f &&
            Mathf.Abs(player.position.x - (8 + 12 * Mathf.Sin(player.position.z * .041f) + 4 * Mathf.Sin(player.position.z * .105f))) < 5f;
        public float MovementSurfaceMultiplier => IsWading ? .68f : 1f;
        private int stage, stones;
        private bool crouched, inventoryOpen, mapOpen, paused, encounterLine;
        private string dialogue, crafting;
        private Vector3 aim;
        private ForestPoint nearby;
        private GUIStyle titleStyle, textStyle, smallStyle;
        // Stage numbers are the save format — see Checkpoint.cs — so append, never reorder.
        public const int BossStage = 4;
        public const int CompleteStage = 5;
        private static readonly string[] Objectives = {
            "Tìm Hùng đang bị thương và dùng thảo dược chữa trị cho anh ấy [E]",
            "Đưa Hùng về căn cứ, nhận hàng tiếp tế [E]",
            "Trinh sát địa hình — mở bản đồ [M]",
            "Chiếm đóng 3 doanh trại của địch — tiêu diệt toàn bộ lính",
            "Tiêu diệt chỉ huy địch",
            "Hoàn thành Map 1 — Những dấu chân trong rừng"
        };

        private void Awake()
        {
            if (player == null)
                player = FindFirstObjectByType<ShadowVale.Gameplay.Player.PlayerController>()?.transform;
            // Unity objects can retain a managed wrapper after the asset was deleted.
            // Use Unity's null check before accessing TextAsset.text.
            if (balanceJson == null || contentBundle == null)
            {
                FailInitialization("Assign valid balanceJson and contentBundle TextAssets in the Inspector.");
                return;
            }
            try
            {
                Settings = JsonUtility.FromJson<ForestSettings>(balanceJson.text);
                bundle = JsonUtility.FromJson<ForestBundle>(contentBundle.text);
            }
            catch (ArgumentException exception)
            {
                FailInitialization("Invalid mission JSON: " + exception.Message);
                return;
            }
            Weapon = bundle?.weapons?.FirstOrDefault(w => w != null && w.id == "rifle_standard");
            GuardData = bundle?.enemy_archetypes?.FirstOrDefault(e => e != null && e.id == "grunt");
            if (Settings == null || Weapon == null || GuardData == null)
            {
                FailInitialization("Mission JSON must contain settings, rifle_standard and grunt.");
                return;
            }
            if (player == null || hung == null || gameCamera == null)
            {
                FailInitialization("Assign player, hung and gameCamera in the Inspector.");
                return;
            }
            controller = player.GetComponent<CharacterController>();
            companion = hung.GetComponent<NavMeshAgent>();
            if (controller == null || companion == null)
            {
                FailInitialization("Player needs a CharacterController and Hung needs a NavMeshAgent.");
                return;
            }
            guards = FindObjectsByType<ForestGuard>(FindObjectsSortMode.None);
            points.AddRange(FindObjectsByType<ForestPoint>(FindObjectsSortMode.None));
            hp = Settings.playerHP; stamina = Settings.stamina; stones = Settings.startingStones;
            inventory["ammo_rifle"] = Settings.startingAmmo;
            IsInitialized = true;
            ConnectGameplay();
            PositionHungForRescue();
            Say("Nam: Hùng đâu rồi? ... Kia! Bị thương rồi. Phải tìm thảo dược cứu anh ấy.", 9);
        }

        private void FailInitialization(string reason)
        {
            Debug.LogError("ForestMission could not initialize. " + reason, this);
            enabled = false;
        }

        public void RegisterPoint(ForestPoint point) => points.Add(point);
        public int Count(string id) => id == "stone" ? stones : inventory.TryGetValue(id, out int count) ? count : 0;
        public void Say(string text, float seconds = 7) { dialogue = text; dialogueUntil = Time.time + seconds; }
        public void EmitNoise(Vector3 position, float radius) {
            foreach (var guard in guards) guard.Hear(position, radius);
            foreach (var enemy in modernEnemies) enemy.Hear(position, radius);
        }
        public void Damage(float amount) {
            if (Stopped) return;
            if (modernHealth != null) { modernHealth.TakeDamage(amount, player.position, gameObject); hp = modernHealth.Current; }
            else hp = Mathf.Max(0, hp - amount);
            if (hp <= 0) GetComponent<ForestSquadCoordinator>()?.Event("player_down", "", player.position);
        }
        public float PlayerHealth => hp;

        private void Update()
        {
            if (modernHealth != null) hp = modernHealth.Current;
            if (!Stopped) {
                PlaySeconds += Time.deltaTime;
                AdvanceMission();
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (ForestMenu.Visible) return;
            if (kb.f9Key.wasPressedThisFrame) Load();
            if (kb.enterKey.wasPressedThisFrame && (hp <= 0 || stage == CompleteStage)) Restart();
            if (Stopped) return;
            if (kb.tabKey.wasPressedThisFrame) { inventoryOpen = !inventoryOpen; mapOpen = false; CancelHudDrag(); suppressFireUntilRelease = true; }
            if (kb.mKey.wasPressedThisFrame) {
                mapOpen = !mapOpen; inventoryOpen = false; CancelHudDrag(); suppressFireUntilRelease = true;
                if (mapOpen) OnMapOpened();
            }
            if (kb.cKey.wasPressedThisFrame) crouched = !crouched;
            if (kb.f5Key.wasPressedThisFrame) SaveSlot(0, out _);
            if (kb.hKey.wasPressedThisFrame) UseItem("medkit_small");
            HandleQuickKeys(kb);
            if (inventoryOpen || mapOpen) { if (modernPlayer == null) MovePlayer(Vector3.zero); if (stage >= 1) UpdateCompanion(); return; }

            if (modernPlayer == null) {
                var motion = new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                    (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
                var right = gameCamera.transform.right; right.y = 0;
                var forward = gameCamera.transform.forward; forward.y = 0;
                var direction = Vector3.ClampMagnitude(right.normalized * motion.x + forward.normalized * motion.y, 1);
                bool sprint = !crouched && kb.leftShiftKey.isPressed && stamina > 2 && direction.sqrMagnitude > .01f;
                float speed = crouched ? Settings.crouchSpeed : sprint ? Settings.sprintSpeed : Settings.walkSpeed;
                if (crafting != null) speed = 0;
                if (kb.spaceKey.wasPressedThisFrame) TryJump();
                MovePlayer(direction * speed * MovementSurfaceMultiplier);
                stamina = Mathf.Clamp(stamina + (sprint ? -Settings.staminaDrain : Settings.staminaRecovery) * Time.deltaTime, 0, Settings.stamina);
                Hidden = crouched && points.Any(p => p.kind == ForestPointKind.Hide && Vector3.Distance(player.position, p.transform.position) < p.radius);
                if (sprint && Time.time > nextNoise) { nextNoise = Time.time + .6f; EmitNoise(player.position, 7); }
                Aim();
                if (Mouse.current == null || !Mouse.current.leftButton.isPressed) suppressFireUntilRelease = false;
                if (Mouse.current != null && Mouse.current.leftButton.isPressed && !suppressFireUntilRelease && !HudPointerBlocked() && Time.time > nextShot && crafting == null) Fire();
                } else {
                modernPlayer.SurfaceSpeedMultiplier = MovementSurfaceMultiplier;
                crouched = modernPlayer.IsSneaking;
                Hidden = crouched && points.Any(p => p.kind == ForestPointKind.Hide && Vector3.Distance(player.position, p.transform.position) < p.radius);
                stamina = Mathf.Clamp(stamina + (modernPlayer.IsSprinting ? -Settings.staminaDrain : Settings.staminaRecovery) * Time.deltaTime, 0, Settings.stamina);
                if (modernPlayer.IsSprinting && Time.time > nextNoise) { nextNoise = Time.time + .6f; EmitNoise(player.position, 7); }
            }
            if (kb.qKey.wasPressedThisFrame) UseItem("stone");
            nearby = points.Where(p => !p.used && p.kind != ForestPointKind.Hide && p.kind != ForestPointKind.Cover)
                .OrderBy(p => Vector3.Distance(player.position, p.transform.position))
                .FirstOrDefault(p => Vector3.Distance(player.position, p.transform.position) < Settings.interactRange);
            if (kb.eKey.wasPressedThisFrame) HandleInteractKey();
            if (kb.bKey.wasPressedThisFrame && nearWorkbench()) Craft();
            if (crafting != null && Time.time >= craftUntil)
            {
                var recipe = bundle.craft_recipes.First(r => r.id == crafting);
                inventory[recipe.output_item_id] = Count(recipe.output_item_id) + recipe.output_count;
                crafting = null; Say("Đã chế tạo băng cứu thương. Nhấn H để sử dụng.");
            }
            if (stage >= 1) UpdateCompanion();
        }

        private void AdvanceMission()
        {
            UpdateRescuePrompt();
            HandleCampObjective();
            if (!encounterLine && Alarmed && guards.All(g => !g.Alive) && modernEnemies.All(g => !g.Alive))
            { encounterLine = true; Say("Hùng: Bọn này hôm nay phản ứng nhanh hơn bình thường."); }
        }

        private bool nearWorkbench() => points.Any(p => p.kind == ForestPointKind.Workbench && Vector3.Distance(player.position, p.transform.position) < Settings.interactRange);

        public Material trailMaterial;

        public bool CameraInputEnabled => IsInitialized && !Stopped && !inventoryOpen && !mapOpen;

        public Vector3 GuardCover(Vector3 origin, Vector3 threat)
        {
            var cover = points.Where(p => p.kind == ForestPointKind.Cover && Vector3.Distance(origin, p.transform.position) < 12)
                .OrderBy(p => Vector3.Distance(origin, p.transform.position)).FirstOrDefault();
            return cover == null ? origin : cover.transform.position + (cover.transform.position - threat).normalized * 2;
        }

        public void Interact(ForestPoint point)
        {
            switch (point.kind)
            {
                case ForestPointKind.Supplies:
                    if (stage != 1) return;
                    point.used = true; stage = 2; inventory["supplies"] = 1;
                    Say("Hùng: Cảm ơn Nam. Anh sẽ ở lại căn cứ chỉ huy — cứ quay lại đây khi cần giao nhiệm vụ mới.");
                    break;
                case ForestPointKind.Loot:
                    if (point.used) return;
                    foreach (var item in point.items) inventory[item.item_id] = Count(item.item_id) + item.count;
                    point.used = true; Say("Đã nhặt vật tư. Tab mở túi đồ. Bàn chế tạo nằm ở điểm tiếp tế.");
                    break;
                case ForestPointKind.Workbench:
                    Say("Bàn chế tạo: B để làm băng cứu thương (2 vải + 1 thảo dược). Game tự lưu khi về menu hoặc thoát.");
                    break;
                case ForestPointKind.Documents:
                    // Side content: useful lore, not on the critical path the briefing laid out.
                    if (point.used) return;
                    point.used = true; inventory["river_documents"] = 1;
                    Say("Nam: Tài liệu cũ của đơn vị tuần tra — cất đi, có thể còn hữu ích.", 8);
                    break;
                case ForestPointKind.Exit:
                    Say("Bến sông vắng lặng. Đường rút lui vẫn còn đó, nhưng nhiệm vụ chưa xong.", 6);
                    break;
            }
        }

        private void Craft()
        {
            if (crafting != null) return;
            var recipe = bundle.craft_recipes.First(r => r.id == "craft_medkit_small");
            if (recipe.inputs.Any(i => Count(i.item_id) < i.count)) { Say("Chưa đủ vật liệu: cần 2 vải và 1 thảo dược."); return; }
            foreach (var input in recipe.inputs) inventory[input.item_id] -= input.count;
            crafting = recipe.id; craftUntil = Time.time + recipe.craft_seconds;
        }

    }
}
